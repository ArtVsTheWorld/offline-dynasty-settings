using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using Frosty.Core;
using FrostySdk.Interfaces;

namespace OfflineDynastySettings
{
    public sealed class CombinedSettingsAction : ExecutionAction
    {
        private static int running;
        private readonly ILaunchEnvironment launchEnvironment;
        public CombinedSettingsAction() { }
        internal CombinedSettingsAction(ILaunchEnvironment environment) { launchEnvironment = environment; }
        public override Action<ILogger, PluginManagerType, CancellationToken> PreLaunchAction
        { get { return (logger, type, token) => Start(logger, launchEnvironment); } }
        public override Action<ILogger, PluginManagerType, CancellationToken> PostLaunchAction
        { get { return (logger, type, token) => Start(logger, launchEnvironment); } }
        private static void Start(ILogger logger, ILaunchEnvironment suppliedEnvironment)
        {
            if (Interlocked.CompareExchange(ref running, 1, 0) != 0) return;
            try {
                new Thread(() => {
                    ILaunchEnvironment environment = suppliedEnvironment ?? new RuntimeEnvironment(logger);
                    environment.Log("Offline Dynasty Settings " + typeof(CombinedSettingsAction).Assembly.GetName().Version
                        + " launch watcher started from " + typeof(CombinedSettingsAction).Assembly.Location + ".");
                    using (LaunchCoordinator coordinator = new LaunchCoordinator(environment)) {
                        while (true) {
                            try { coordinator.Step(); }
                            catch (Exception ex) { environment.Log("Watcher will retry after unexpected error: " + ex.ToString()); }
                            Thread.Sleep(100);
                        }
                    }
                }) { IsBackground = true, Name = "Offline Dynasty Settings launch watcher" }.Start();
            } catch (Exception ex) {
                Interlocked.Exchange(ref running, 0);
                new RuntimeEnvironment(logger).Log("Could not start launch watcher: " + ex.ToString());
            }
        }
        private sealed class RuntimeEnvironment : ILaunchEnvironment
        {
            private readonly ILogger logger;
            internal RuntimeEnvironment(ILogger logger) { this.logger = logger; }
            public DateTime Now { get { return DateTime.UtcNow; } }
            public GameLaunch FindGame()
            {
                GameLaunch newest = null;
                foreach (Process process in Process.GetProcessesByName("CollegeFB27")) using (process) {
                    try {
                        if (process.HasExited) continue;
                        DateTime start = process.StartTime.ToUniversalTime();
                        if (newest == null || start.Ticks > newest.Started)
                            newest = new GameLaunch { Pid = process.Id, Started = start.Ticks, StartedUtc = start };
                    } catch { } // A process can exit while Windows enumerates it.
                }
                return newest;
            }
            public ILaunchSession Begin(GameLaunch game)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                    string name = assembly.GetName().Name;
                    if (name == "DynastyPlayClock" || name == "OfflinePlaycall")
                        throw new InvalidOperationException("Remove the separate DynastyPlayClock.dll and OfflinePlaycall.dll from Plugins and restart MMC.");
                }
                return new RuntimeSession(game, this);
            }
            public bool IsAlive(GameLaunch launch)
            {
                Process process;
                try { process = Process.GetProcessById(launch.Pid); }
                catch (ArgumentException) { return false; }
                using (process) return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == launch.Started;
            }
            public void Log(string message)
            {
                try { if (logger != null) logger.Log("[Offline Dynasty Settings] " + message, new object[0]); } catch { }
                DiagnosticLog.Write(DiagnosticLog.PluginFolder,"MMC",message);
            }
        }
        private sealed class RuntimeSession : ILaunchSession
        {
            private readonly Process game;
            private readonly RuntimeEnvironment environment;
            private DynastyPlayClock.DynastyPlayClockAction.CombinedPersistence clock;
            private OfflinePlaycall.SplitSettingsAction.Session playcall;
            private PersistenceController persistence;
            internal RuntimeSession(GameLaunch launch, RuntimeEnvironment environment)
            {
                this.environment = environment;
                game = Process.GetProcessById(launch.Pid);
                if (game.HasExited || game.StartTime.ToUniversalTime().Ticks != launch.Started) {
                    game.Dispose(); throw new InvalidOperationException("Game exited or restarted during inspection.");
                }
                DiagnosticLog.Started(game);
            }
            public string InstallClock()
            {
                string result = DynastyPlayClock.DynastyPlayClockAction.Apply(game.Id);
                if (result.StartsWith("Installed", StringComparison.Ordinal) || result == "Already installed")
                    if (clock == null) clock = DynastyPlayClock.DynastyPlayClockAction.OpenCombinedPersistence(game);
                return result;
            }
            public bool InstallPlaycall()
            {
                DynastyPlayClock.DynastyPlayClockAction.InstallCombinedDefensiveClock(game.Id);
                if (playcall == null) playcall = OfflinePlaycall.SplitSettingsAction.TryInstallCombined(game, environment.Log);
                if (playcall != null) DynastyPlayClock.DynastyPlayClockAction.InstallControllerSelect(game.Id);
                return playcall != null;
            }
            public void Poll()
            {
                if (persistence != null) { persistence.EnsureReady(); return; }
                Exception error = null;
                try { if (clock != null) clock.Poll(); } catch (Exception ex) { error = ex; }
                try { if (playcall != null) playcall.PollCombined(environment.Log); } catch (Exception ex) { if (error == null) error = ex; }
                if (error != null) throw error;
            }
            public void Verify()
            {
                if (game.HasExited) return;
                DynastyPlayClock.DynastyPlayClockAction.VerifyCombinedHooks(game, playcall != null);
                if (playcall != null) playcall.VerifyCombined();
                if (playcall != null) DynastyPlayClock.DynastyPlayClockAction.VerifyControllerSelect(game.Id);
                if (playcall != null) DynastyPlayClock.DynastyPlayClockAction.VerifyCombinedDefensiveClock(game.Id);
                if (playcall != null) {
                    if (persistence == null) {
                        PersistenceContext context = new PersistenceContext {
                            Format=1, Pid=game.Id, Started=game.StartTime.ToUniversalTime().Ticks,
                            ImageBase=game.MainModule.BaseAddress.ToInt64(),
                            ClockData=DynastyPlayClock.DynastyPlayClockAction.CombinedData, SplitData=playcall.CombinedData,
                            PluginFolder=Path.GetDirectoryName(typeof(CombinedSettingsAction).Assembly.Location),
                            Checks=DynastyPlayClock.DynastyPlayClockAction.CombinedChecks(true)
                        };
                        context.Checks.AddRange(DynastyPlayClock.DynastyPlayClockAction.ControllerChecks());
                        context.Checks.AddRange(playcall.CombinedChecks(DynastyPlayClock.DynastyPlayClockAction.CombinedWriter));
                        DiagnosticLog.Hooks(context);
                        persistence=new PersistenceController(context,environment.Log);
                    }
                    persistence.EnsureReady();
                }
            }
            public void Dispose()
            {
                if (persistence != null) persistence.Dispose();
                if (clock != null) clock.Dispose();
                if (playcall != null) playcall.Dispose();
                game.Dispose();
            }
        }
    }
}
