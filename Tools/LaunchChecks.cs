// Fault-injection checks against the coordinator and callbacks in the release DLL.
// Native memory checks use only this disposable test process, never the game.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using OfflineDynastySettings;
using DynastyPlayClock;
using OfflinePlaycall;
using Frosty.Core;
class LaunchChecks
{
    static int checks;
    static string isolated;
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    static int Main(string[] args)
    {
        try {
            string host = Path.GetFullPath(args[0]);
            isolated = Path.GetFullPath(args[2]); Directory.CreateDirectory(isolated);
            File.Copy(Path.GetFullPath(args[1]), Path.Combine(isolated, "OfflineDynastySettings.dll"), true);
            AppDomain.CurrentDomain.AssemblyResolve += (sender, request) => {
                string name = new AssemblyName(request.Name).Name + ".dll";
                foreach (string folder in new string[] { isolated, host, Path.Combine(host, "ThirdParty") }) {
                    string path = Path.Combine(folder, name); if (File.Exists(path)) return Assembly.LoadFrom(path);
                }
                return null;
            };
            Run(); Console.WriteLine("PASS " + checks + " launch lifecycle, callback and persistence checks against release DLL"); return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    class Session : ILaunchSession
    {
        internal int ClockCalls, SplitCalls, PollCalls, VerifyCalls, Disposals;
        internal bool ClockAvailable = true, SplitAvailable = true;
        internal Exception ClockError, SplitError, PollError, VerifyError;
        public string InstallClock() { ClockCalls++; if (ClockError != null) { Exception ex = ClockError; ClockError = null; throw ex; } return ClockAvailable ? "Installed fake verified hooks" : "Unsupported code: not fully decrypted; no hooks installed"; }
        public bool InstallPlaycall() { SplitCalls++; if (SplitError != null) { Exception ex = SplitError; SplitError = null; throw ex; } return SplitAvailable; }
        public void Poll() { PollCalls++; if (PollError != null) { Exception ex = PollError; PollError = null; throw ex; } }
        public void Verify() { VerifyCalls++; if (VerifyError != null) { Exception ex = VerifyError; VerifyError = null; throw ex; } }
        public void Dispose() { Disposals++; }
    }
    class Env : ILaunchEnvironment
    {
        internal DateTime Time = new DateTime(2026,10,6,12,0,0,DateTimeKind.Utc);
        internal GameLaunch Current;
        internal bool Alive = true, ThrowLog;
        internal Exception FindError, BeginError;
        internal Session Next = new Session();
        internal int Begins, Finds;
        internal List<string> Logs = new List<string>();
        public DateTime Now { get { return Time; } }
        public GameLaunch FindGame() { Finds++; if (FindError != null) { Exception ex = FindError; FindError = null; throw ex; } return Current; }
        public bool IsAlive(GameLaunch launch) { return Alive; }
        public ILaunchSession Begin(GameLaunch launch) { Begins++; if (BeginError != null) { Exception ex = BeginError; BeginError = null; throw ex; } return Next; }
        public void Log(string message) { if (ThrowLog) throw new IOException("log is locked"); Logs.Add(message); }
        internal void Launch(int pid, int age) { DateTime start = Time.AddSeconds(-age); Current = new GameLaunch { Pid = pid, Started = start.Ticks, StartedUtc = start }; Alive = true; }
        internal void Tick(LaunchCoordinator coordinator, double seconds) { Time = Time.AddSeconds(seconds); coordinator.Step(); }
        internal bool Active { get { return Logs.Exists(s => s.StartsWith("ACTIVE:")); } }
    }
    [MethodImpl(MethodImplOptions.NoInlining)] static void Run()
    {
        Env e = new Env(); e.Launch(1,0);
        using (LaunchCoordinator c = new LaunchCoordinator(e)) {
            c.Step(); Check(e.Begins == 0, "Startup grace ignored");
            e.Tick(c,44); Check(e.Begins == 0, "Installed during initial startup");
            e.Tick(c,1); Check(e.Active && e.Next.VerifyCalls == 1, "No activation without foreground/window metadata");
            Check(e.Next.ClockCalls == 1 && e.Next.SplitCalls == 1, "Ordering or duplicate installation");
            e.Tick(c,0.1); Check(e.Next.PollCalls == 2, "Persistence not polled at watcher rate");
            e.Tick(c,5); Check(e.Next.VerifyCalls == 2, "Hook health not checked periodically");
        }
        Check(e.Next.Disposals == 1, "Leaked session on disposal");
        e = new Env(); e.Launch(2,46); e.Next.ClockAvailable = false;
        using (LaunchCoordinator c = new LaunchCoordinator(e)) {
            c.Step(); Check(e.Next.SplitCalls == 0, "Split installed before Clock");
            for (int i=0;i<20;i++) e.Tick(c,60);
            Check(!e.Active && e.Next.ClockCalls == 21, "Slow launch expired or stopped retrying fingerprints");
            e.Next.ClockAvailable = true; e.Tick(c,1); Check(e.Active, "Late decryption never activated");
        }
        e = new Env(); e.Launch(3,46); e.FindError = new IOException("process snapshot temporarily failed");
        using (LaunchCoordinator c = new LaunchCoordinator(e)) {
            c.Step(); Check(e.Begins == 0, "Failed snapshot unexpectedly installed");
            e.BeginError = new InvalidOperationException("game module loading"); e.Tick(c,1);
            Check(e.Begins == 1 && e.Next.ClockCalls == 0, "Begin failure consumed game launch");
            e.Next.ClockError = new IOException("temporary memory read failure"); e.Tick(c,1);
            Check(e.Next.ClockCalls == 1 && !e.Active, "Clock failure status wrong");
            e.Next.SplitError = new IOException("settings file is temporarily locked"); e.Tick(c,1);
            Check(e.Next.ClockCalls == 2 && e.Next.SplitCalls == 1 && !e.Active, "Split error lost Clock state");
            e.Tick(c,1); Check(e.Active && e.Next.ClockCalls == 2, "Transient install errors killed worker");
            e.Next.PollError = new IOException("disk locked"); e.Tick(c,0.1); e.Tick(c,1);
            Check(e.Next.ClockCalls == 2 && e.Next.SplitCalls == 2 && e.Next.PollCalls >= 3, "Save error caused reinstallation or killed watcher");
            GameLaunch previous = e.Current; e.Current = null; e.Tick(c,1);
            Check(e.Next.Disposals == 0 && e.Begins == 2, "Incomplete snapshot abandoned live hook ownership");
            e.Current = previous; e.Tick(c,1); Check(e.Next.SplitCalls == 2, "Duplicate hooks after transient missing process");
            e.Alive = false; e.Current = null; e.Tick(c,1); Check(e.Next.Disposals == 1, "Exited game not released");
            e.Next = new Session(); e.Launch(4,46); e.Tick(c,1); Check(e.Next.ClockCalls == 1 && e.Next.SplitCalls == 1, "Second game not activated in same watcher");
            Session old = e.Next; e.Next = new Session(); e.Time = e.Time.AddSeconds(1); e.Launch(4,46); c.Step();
            Check(old.Disposals == 1 && e.Next.ClockCalls == 1, "Reused PID confused with previous game instance");
        }
        e = new Env(); e.Launch(5,46); e.Next.SplitAvailable = false;
        using (LaunchCoordinator c = new LaunchCoordinator(e)) {
            c.Step(); e.Tick(c,20); Check(e.Next.PollCalls == 2 && e.Next.ClockCalls == 1 && !e.Active, "Partial activation lost preference saving");
            e.Next.SplitAvailable = true; e.Next.VerifyError = new IOException("readback temporarily failed"); e.Tick(c,1);
            Check(!e.Active, "Claimed active before verification succeeded");
            e.Tick(c,1); Check(e.Active && e.Next.VerifyCalls == 2, "Verification error prevented recovery");
            e.Next.VerifyError = new HookOwnershipException("foreign patch replaced hook"); e.Tick(c,5);
            int polls = e.Next.PollCalls, installs = e.Next.ClockCalls; e.Tick(c,10);
            Check(e.Next.PollCalls == polls && e.Next.ClockCalls == installs, "Foreign hooks were overwritten or kept saving");
            Session old = e.Next; e.Next = new Session(); e.Launch(6,46); e.Tick(c,1);
            Check(old.Disposals == 1 && e.Next.VerifyCalls == 1, "Conflict on old game blocked later launch");
        }
        e = new Env { ThrowLog = true }; e.Launch(7,46);
        using (LaunchCoordinator c = new LaunchCoordinator(e)) { c.Step(); Check(e.Next.VerifyCalls == 1, "Logging exception disabled activation"); }
        PersistenceChecks(); CallbackChecks();
    }
    static void PersistenceChecks()
    {
        IntPtr memory = Marshal.AllocHGlobal(0x5000);
        try {
            byte[] zero = new byte[0x5000]; Marshal.Copy(zero,0,memory,zero.Length);
            long remote = memory.ToInt64(); string ini = Path.Combine(isolated,"DynastyPlayClock.ini");
            File.WriteAllText(ini,"ExtendedSeconds=0\r\n");
            IntPtr handle = OpenProcess(0x1010,false,Process.GetCurrentProcess().Id);
            using (DynastyPlayClockAction.CombinedPersistence persistence = new DynastyPlayClockAction.CombinedPersistence(handle,remote,false)) {
                Marshal.WriteInt32(new IntPtr(remote+0x828),1);
                bool failed = false;
                using (FileStream locked = new FileStream(ini,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                    try { persistence.Poll(); } catch (IOException) { failed = true; }
                Check(failed,"Locked preference silently acknowledged"); persistence.Poll();
                Check(File.ReadAllText(ini).Trim()=="ExtendedSeconds=60","Preference save did not retry after lock released");
                Marshal.WriteInt32(new IntPtr(remote+0x828),0); persistence.Poll();
                Check(File.ReadAllText(ini).Trim()=="ExtendedSeconds=0","Preference changes stopped being monitored");
            }
            using (DynastyPlayClockAction.CombinedPersistence persistence = new DynastyPlayClockAction.CombinedPersistence(IntPtr.Zero,remote,false)) {
                bool failed = false; try { persistence.Poll(); } catch (InvalidOperationException) { failed = true; }
                Check(failed,"Failed remote read did not reach retry logic");
            }
            MethodInfo load = typeof(DynastyPlayClockAction).GetMethod("LoadExtended",BindingFlags.NonPublic|BindingFlags.Static);
            using (FileStream locked = new FileStream(ini,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) {
                bool failed=false; try { load.Invoke(null,null); } catch (TargetInvocationException ex) { failed=ex.InnerException is IOException; }
                Check(failed,"Locked startup preference reset saved choice");
            }
            string folder = Path.Combine(isolated,"OfflinePlaycallSettings"); Directory.CreateDirectory(folder);
            string storage = Path.Combine(folder,"dynasties.json"), control=Path.Combine(folder,"restore-request.txt");
            if (File.Exists(storage)) File.Delete(storage);
            File.WriteAllText(control,"stale diagnostic command");
            Marshal.WriteInt32(new IntPtr(remote+SplitHookCode.SequenceOffset),1);
            Marshal.WriteInt32(new IntPtr(remote+SplitHookCode.TableOffset),100);
            Marshal.WriteInt32(new IntPtr(remote+SplitHookCode.TableOffset+4),3);
            Marshal.WriteInt32(new IntPtr(remote+SplitHookCode.TableOffset+8),5);
            handle = OpenProcess(0x1010,false,Process.GetCurrentProcess().Id);
            using (SplitSettingsAction.Session session = new SplitSettingsAction.Session(handle,remote,new SplitSettingsAction.Site[0],storage,new SplitSettingsAction.SettingsDocument())) {
                session.PollCombined(s=>{}); Check(File.ReadAllText(storage).Contains("100") && File.Exists(control),"Stale restore command disabled combined persistence");
                Marshal.WriteInt32(new IntPtr(remote+SplitHookCode.SequenceOffset),2);
                Marshal.WriteInt32(new IntPtr(remote+SplitHookCode.TableOffset+4),4);
                bool failed=false;
                using (FileStream locked=new FileStream(storage,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                    try { session.PollCombined(s=>{}); } catch(IOException) { failed=true; }
                Check(failed,"Locked defense settings acknowledged revision"); session.PollCombined(s=>{});
                Check(File.ReadAllText(storage).Contains("\"100\":4"),"Defense persistence did not recover from file lock");
                // Exercise the real combined Poll, including independent saves
                // when the Clock monitor experiences a transient failure.
                Marshal.WriteInt32(new IntPtr(remote+SplitHookCode.SequenceOffset),3);
                Marshal.WriteInt32(new IntPtr(remote+SplitHookCode.TableOffset+4),5);
                Type runtime=typeof(CombinedSettingsAction).GetNestedType("RuntimeSession",BindingFlags.NonPublic);
                Type envType=typeof(CombinedSettingsAction).GetNestedType("RuntimeEnvironment",BindingFlags.NonPublic);
                object env=Activator.CreateInstance(envType,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{null},null);
                object combined=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(runtime);
                BindingFlags fields=BindingFlags.NonPublic|BindingFlags.Instance;
                using (DynastyPlayClockAction.CombinedPersistence badClock=new DynastyPlayClockAction.CombinedPersistence(IntPtr.Zero,remote,false)) {
                    runtime.GetField("environment",fields).SetValue(combined,env);
                    runtime.GetField("clock",fields).SetValue(combined,badClock);
                    runtime.GetField("playcall",fields).SetValue(combined,session);
                    bool retried=false; try { runtime.GetMethod("Poll").Invoke(combined,null); } catch(TargetInvocationException ex) { retried=ex.InnerException is InvalidOperationException; }
                    Check(retried && File.ReadAllText(storage).Contains("\"100\":5"),"Clock save failure prevented defense saving");
                }
            }
            MethodInfo verify=typeof(DynastyPlayClockAction).GetMethod("VerifyBytes",BindingFlags.Static|BindingFlags.NonPublic);
            handle=OpenProcess(0x1010,false,Process.GetCurrentProcess().Id);
            byte[] expected=new byte[]{0x90,0x90}; Marshal.Copy(expected,0,memory,expected.Length);
            try {
                verify.Invoke(null,new object[]{handle,remote,expected,"test entry"}); Check(true,"Exact owned code rejected");
                Marshal.WriteByte(memory,0xcc); bool rejected=false;
                try { verify.Invoke(null,new object[]{handle,remote,expected,"test entry"}); } catch(TargetInvocationException ex) { rejected=ex.InnerException is HookOwnershipException; }
                Check(rejected && Marshal.ReadByte(memory)==0xcc,"Changed hook overwritten or accepted");
                rejected=false; try { verify.Invoke(null,new object[]{handle,0L,expected,"test entry"}); } catch(TargetInvocationException ex) { rejected=ex.InnerException is InvalidOperationException; }
                Check(rejected,"Read failure mistaken for conflict");
            } finally { CloseHandle(handle); }
            Marshal.WriteByte(new IntPtr(remote+0xe),0x90); Marshal.WriteByte(new IntPtr(remote+0xf),0x90);
            SplitSettingsAction.Site site=new SplitSettingsAction.Site(remote+0x10,0,new byte[]{0xcc}); site.Replacement=new byte[]{0x90};
            Marshal.WriteByte(new IntPtr(site.Address),0x90); handle=OpenProcess(0x1010,false,Process.GetCurrentProcess().Id);
            using (SplitSettingsAction.Session session=new SplitSettingsAction.Session(handle,remote,new SplitSettingsAction.Site[]{site},storage,new SplitSettingsAction.SettingsDocument())) {
                session.VerifyCombined(); Check(true,"Owned split site/gate rejected");
                Marshal.WriteByte(new IntPtr(site.Address),0xcc); bool rejected=false;
                try { session.VerifyCombined(); } catch(HookOwnershipException) { rejected=true; }
                Check(rejected && Marshal.ReadByte(new IntPtr(site.Address))==0xcc,"Foreign split hook overwritten or accepted");
                Marshal.WriteByte(new IntPtr(site.Address),0x90); Marshal.WriteByte(new IntPtr(remote+0xe),0x74); rejected=false;
                try { session.VerifyCombined(); } catch(HookOwnershipException) { rejected=true; }
                Check(rejected,"Changed visibility gate accepted");
            }
        } finally { Marshal.FreeHGlobal(memory); }
    }
    class CallbackEnv : ILaunchEnvironment
    {
        internal int Finds;
        public DateTime Now { get { return DateTime.UtcNow; } }
        public GameLaunch FindGame() { Interlocked.Increment(ref Finds); return null; }
        public bool IsAlive(GameLaunch game) { return false; }
        public ILaunchSession Begin(GameLaunch game) { throw new Exception("No test game"); }
        public void Log(string message) { }
    }
    static void CallbackChecks()
    {
        CallbackEnv e = new CallbackEnv(); CombinedSettingsAction action=new CombinedSettingsAction(e);
        CancellationTokenSource canceled = new CancellationTokenSource(); canceled.Cancel();
        Thread[] calls=new Thread[20];
        for(int i=0;i<calls.Length;i++) { int index=i; calls[i]=new Thread(()=> {
            if(index%2==0)action.PreLaunchAction(null,PluginManagerType.ModManager,canceled.Token);
            else action.PostLaunchAction(null,PluginManagerType.ModManager,canceled.Token);
        }); calls[i].Start(); }
        foreach(Thread call in calls)call.Join(); Thread.Sleep(350);
        Check(e.Finds>=3 && e.Finds<10,"Canceled/concurrent callbacks created no watcher or multiple watchers");
        int before=e.Finds; Thread.Sleep(250); Check(e.Finds>before,"Per-launch cancellation stopped persistent watcher");
        action.PostLaunchAction(null,PluginManagerType.ModManager,CancellationToken.None); before=e.Finds; Thread.Sleep(250);
        Check(e.Finds-before<=4,"Later callback spawned duplicate watcher");
    }
}
