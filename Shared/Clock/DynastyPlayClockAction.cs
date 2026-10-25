using Frosty.Core;
using FrostySdk.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace DynastyPlayClock
{
    public sealed partial class DynastyPlayClockAction : ExecutionAction
    {
        private const long ListRva = 0x79a7a9c, WriterRva = 0x79b5d53, RuntimeRva = 0x35e7592;
        private const int Key = 0x1000;
        private static readonly byte[] ListOriginal = Hex("488b8f18040000ffc3488b01ff9010050000");
        private static readonly byte[] WriterOriginal = Hex("83c0fe3ddb0000000f87ac270000");
        private static readonly byte[] RuntimeOriginal = Hex("488b0d47eeba0aba01000000e8ada77102");
        private static readonly byte[] EnumOriginal = Hex("33c083fa7577084863c20fb7444160c3");
        private static readonly Region[] Regions = {
            new Region("Clock list", 0x79a76d0, 0x15c0, "2f6aac8eff5254f387928bf0f6564ea0ccee5ffa897d3098f1e44f37ebd4912d"),
            new Region("Dynasty writer", 0x79b5cc0, 0x2d4d, "b7a20920e551b71b81584fdda0a5c8b33150ae55d56d26eaefb5719c9428a502"),
            new Region("Choice serializer", 0x79afaa0, 0x2a2, "a7eb30e14eaf61f68b71c847686e440a73c9ddf4c3d1e216e7b878ac1ad246af"),
            new Region("Profile accessor", 0x27ac630, 0x42, "7e5570dca9c9b1fcb3223c8483df638fc0af14cb273c56168a18a6dd7ffa5584"),
            new Region("Main-menu enum getter", 0x7f433a0, 0x4f, "bd655d22ccbda0073fdcc135b061fe716410ffb7e088c5a28eb18fc0f1c655f7"),
            new Region("Main-menu enum setter", 0x7f53b60, 0x54, "66dd4f9d591c010784a38296a13d9325de6966d22d104bb55ee372c1a5ddd9bf"),
            new Region("Profile getter", 0x27ae830, 0x36, "0a4d32a9fe54f16a89b8a9bf4c9e1d3718903d6250d4bb743e1c1a4eef140677"),
            new Region("Profile setter", 0x27b7410, 0x49, "16d57707fb89805f0713cfea45c357c6cb27a828b8f0a643e05e1a24cc80d344"),
            new Region("Enum metadata", 0x5d01900, 0x1d, "438037eda88548d6e9dfa43331dbc214a65312a90faa5005223905d3bac2a508"),
            new Region("Native Play Clock decision", 0x35e754e, 0x12f, "60fcd427eb3d3904ad176e8f8aaddf2ec99297304632ab7ad594f83214bd17a3"),
            new Region("Remaining-clock setter", 0x5d0e630, 0x99, "c31c83aa5404d6efa7d310720f3608ab87b7c9718dd520a778e51d5a87cde5aa"),
            new Region("Clock transition dispatcher", 0x6461420, 0x3c, "3d63926963ea64ee1ff059bb65db9a77aa434a8789a78f8862c75af028d34876"),
            new Region("Clock transition reset", 0x64751c0, 0x81, "a6531242282583ece301d958c10fcf864b680109bb274b23db0ffee3328640e5"),
            new Region("CPU snap cadence", 0x360d070, 0xcc, "56fa757a366f5e454c51cb55d67a09b87d019dda39fb8a3e3beb5b8906634f88"),
            new Region("Play-clock upper limit", 0x5d8fc30, 0x31, "1f3ba0a4d52362a81dc70d758c4c38aa6b3cd88dc96c8419884281841b907645"),
            new Region("Clock duration reset", 0x5d0e2e0, 0x9e, "20c6d67ccb1809470a550f5dfb971b4b71b499ca91644497276a13e923c7cbeb"),
            new Region("Match enum getter", 0x5d01d50, 0x10, "4a94c23be822b90127b5ce6c14f9a13ddcda763bd3d4442ae3382f410e3d6792"),
            new Region("Match type predicate", 0x300e880, 0x10, "c98372b27bf4994df56518210111b6e254a3e6611e637ac119e81bf30bfe6eeb"),
            new Region("Legacy league mode predicate", 0x7ef6110, 0x45, "0670f638897f4d939389e1c358e196973557c0e75a762a2447f343825e2dbea3")
        };
        private static readonly byte[] DurationOriginal = Hex("48895c240848896c24104889742418");
        private static int running;
        private static readonly object Sync = new object();
        private static int installedPid;
        private static long installedStart, installedBase, codeAddress, dataAddress;
        private static byte[] installedList, installedWriter, installedRuntime, installedEnum, installedDuration, installedCap, installedCpu, installedTransition;
        private static Layout installedLayout;

        public override Action<ILogger, PluginManagerType, CancellationToken> PreLaunchAction
        { get { return (logger, type, token) => { }; } }
        public override Action<ILogger, PluginManagerType, CancellationToken> PostLaunchAction
        {
            get { return (logger, type, token) => {
                if (Interlocked.CompareExchange(ref running, 1, 0) != 0) return;
                new Thread(() => Worker(logger, token)) { IsBackground = true, Name = "Dynasty Play Clock" }.Start();
            }; }
        }
        private static void Worker(ILogger logger, CancellationToken token)
        {
            try {
                Log(logger, "0.2.0 loaded; waiting for decrypted game code and a stable game window.");
                DateTime deadline = DateTime.UtcNow.AddMinutes(15), readySince = DateTime.MinValue;
                int lastPid = 0;
                while (!token.IsCancellationRequested && DateTime.UtcNow < deadline) {
                    foreach (Process game in Process.GetProcessesByName("CollegeFB27")) using (game) {
                        if (lastPid != game.Id) { lastPid = game.Id; readySince = DateTime.MinValue; }
                        if (!IsInteractiveGameWindow(game)) { readySince = DateTime.MinValue; continue; }
                        if (readySince == DateTime.MinValue) readySince = DateTime.UtcNow;
                        if (DateTime.UtcNow - readySince >= TimeSpan.FromSeconds(15)
                            && DateTime.UtcNow - game.StartTime.ToUniversalTime() >= TimeSpan.FromSeconds(45)) {
                            // Offline Playcall 0.4.0 fingerprints the whole list.
                            // Let its native gate run first before extending it.
                            string result = Apply(game.Id);
                            if (result != "Waiting for decrypted code") { Log(logger, result); if (result.StartsWith("Installed") || result == "Already installed") MonitorPreference(game.Id, logger, token); return; }
                        }
                    }
                    Thread.Sleep(250);
                }
                Log(logger, "Launch check ended without installing hooks.");
            } catch (Exception ex) { Log(logger, ex.ToString()); }
            finally { Interlocked.Exchange(ref running, 0); }
        }

        // Also used by the local test harness, so tests exercise the actual DLL.
        public static string Apply(int pid)
        {
            lock (Sync) using (Process game = Process.GetProcessById(pid)) {
                if (!String.Equals(game.ProcessName, "CollegeFB27", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Unexpected process");
                long start = game.StartTime.ToUniversalTime().Ticks, b = game.MainModule.BaseAddress.ToInt64();
                if (installedPid == pid && installedStart == start) return "Already installed";
                IntPtr h = OpenProcess(0x1038, false, pid);
                if (h == IntPtr.Zero) throw new InvalidOperationException("OpenProcess: " + Marshal.GetLastWin32Error());
                try {
                    Layout layout = null;
                    foreach (Layout candidate in GetLayouts()) {
                        if (Equal(Read(h, b + candidate.Map(ListRva), ListOriginal.Length), ListOriginal)
                            && Equal(Read(h, b + candidate.Map(WriterRva), WriterOriginal.Length), WriterOriginal)
                            && Equal(Read(h, b + candidate.Map(RuntimeRva), candidate.RuntimeOriginal.Length), candidate.RuntimeOriginal)) {
                            layout = candidate; break;
                        }
                    }
                    if (layout == null || WaitForPlaycallGate(game, layout)) return "Waiting for decrypted code";
#if COMBINED_DYNASTY_SETTINGS
                    if (layout.Map(ListRva) != 0x7993e6c)
                        return "The combined plugin requires the inspected October 13 game layout; no hooks installed";
                    ValidateCombinedDefensiveReadSite(h,b);
#endif
                    foreach (Region r in layout.Regions) {
                        byte[] bytes = Read(h, b + r.Rva, r.Length);
                        if (bytes == null) return "Waiting for decrypted code";
                        // Compatible with Offline Playcall 0.4.0's two-byte gate.
                        if (r.Rva == layout.Map(0x79a76d0)) {
                            int at = (int)(layout.Map(0x79a79ce) - r.Rva);
                            if (!((bytes[at] == 0x74 && bytes[at + 1] == 0x58)
                                || (bytes[at] == 0x90 && bytes[at + 1] == 0x90))) return "Unsupported Playcall gate";
                            bytes[at] = 0x74; bytes[at + 1] = 0x58;
                        }
                        if (Hash(bytes) != r.Hash) {
#if COMBINED_DYNASTY_SETTINGS
                            return OfflineDynastySettings.DiagnosticLog.Mismatch(r.Name,r.Rva,r.Hash,bytes);
#else
                            return "Unsupported code: " + r.Name + "; no hooks installed";
#endif
                        }
                    }
                    // Internal enum 1 must still be the same profile field.
                    if (!Equal(Read(h, b + layout.Map(0xb67e30c), 12), Hex("00236cbe0100000020000000")))
                        return "Unsupported profile metadata; no hooks installed";
                    bool extended = LoadExtended();
                    IntPtr data = VirtualAllocEx(h, IntPtr.Zero, new UIntPtr(4096), 0x3000, 4);
                    IntPtr code = VirtualAllocEx(h, IntPtr.Zero, new UIntPtr(8192), 0x3000, 4);
                    if (data == IntPtr.Zero || code == IntPtr.Zero) throw new InvalidOperationException("Remote allocation failed");
                    long d = data.ToInt64(), c = code.ToInt64();
                    byte[] descriptor = BuildData(d); Put(descriptor, 0x828, extended ? 1 : 0);
                    Write(h, d, descriptor);
                    byte[] list = BuildListFor(b, d, layout), writer = BuildWriterFor(b, d, layout), runtime = BuildRuntimeFor(b, d, layout);
                    byte[] enumBridge = BuildEnumFor(b, d, layout), duration = BuildDurationFor(b, d, layout), cap = BuildCapFor(b, d, layout), cpu = BuildCpuFor(b, d, layout), transition = BuildTransitionFor(b, d, layout);
                    if (list.Length >= 1024 || writer.Length >= 1024 || runtime.Length >= 1024 || enumBridge.Length >= 1024 || duration.Length >= 1024 || cap.Length >= 1024 || cpu.Length >= 1024 || transition.Length >= 1024) throw new InvalidOperationException("Hook code too large");
                    Write(h, c, list); Write(h, c + 1024, writer); Write(h, c + 2048, runtime); Write(h, c + 3072, enumBridge); Write(h, c + 4096, duration); Write(h, c + 5120, cap); Write(h, c + 6144, cpu); Write(h, c + 7168, transition);
                    uint previous;
                    if (!VirtualProtectEx(h, code, new UIntPtr(8192), 0x20, out previous))
                        throw new InvalidOperationException("Code protection failed");
                    FlushInstructionCache(h, code, new UIntPtr(8192));
                    byte[] lp = Pad(Jump(c), ListOriginal.Length), wp = Jump(c + 1024);
                    byte[] rp = Pad(Jump(c + 2048), layout.RuntimeOriginal.Length);
                    byte[] ep = Pad(Jump(c + 3072), EnumOriginal.Length), dp = Pad(Jump(c + 4096), DurationOriginal.Length), cp = Jump(c + 5120), ap = Pad(Jump(c + 6144), CpuOriginal.Length), tp = Pad(Jump(c + 7168), DurationOriginal.Length);
                    try {
                        // Install the value route before exposing its row.
                        Patch(h, b + layout.Map(0x5d0e630), tp);
                        Patch(h, b + layout.Map(0x360d0b0), ap);
                        Patch(h, b + layout.Map(0x5d8fc30), cp);
                        Patch(h, b + layout.Map(0x5d0e2e0), dp);
                        Patch(h, b + layout.Map(0x5d01d50), ep);
                        Patch(h, b + layout.Map(RuntimeRva), rp); Patch(h, b + layout.Map(WriterRva), wp); Patch(h, b + layout.Map(ListRva), lp);
                    } catch {
                        Patch(h, b + layout.Map(ListRva), ListOriginal); Patch(h, b + layout.Map(WriterRva), WriterOriginal); Patch(h, b + layout.Map(RuntimeRva), layout.RuntimeOriginal);
                        Patch(h, b + layout.Map(0x5d01d50), EnumOriginal);
                        Patch(h, b + layout.Map(0x5d0e2e0), DurationOriginal);
                        Patch(h, b + layout.Map(0x5d8fc30), CapOriginalFor(layout));
                        Patch(h, b + layout.Map(0x360d0b0), CpuOriginal);
                        Patch(h, b + layout.Map(0x5d0e630), DurationOriginal);
                        // Retain allocations: a thread may have entered a hook.
                        throw;
                    }
                    installedPid = pid; installedStart = start; installedBase = b;
                    codeAddress = c; dataAddress = d; installedList = lp; installedWriter = wp; installedRuntime = rp; installedEnum = ep; installedDuration = dp; installedCap = cp; installedCpu = ap; installedTransition = tp;
                    installedLayout = layout;
                    string result = "Installed offline Dynasty Play Clock profile bridge in PID " + pid
                        + "; code=0x" + c.ToString("x") + "; data=0x" + d.ToString("x") + "; build=" + layout.Name;
                    Log(null, result); return result;
                } finally { CloseHandle(h); }
            }
        }
        public static string Revert(int pid)
        {
            lock (Sync) using (Process game = Process.GetProcessById(pid)) {
                if (installedPid != pid || installedStart != game.StartTime.ToUniversalTime().Ticks)
                    return "No hooks owned by this DLL instance";
                IntPtr h = OpenProcess(0x1038, false, pid);
                if (h == IntPtr.Zero) throw new InvalidOperationException("OpenProcess failed");
                try {
                    if (!Equal(Read(h, installedBase + installedLayout.Map(ListRva), installedList.Length), installedList)
                        || !Equal(Read(h, installedBase + installedLayout.Map(WriterRva), installedWriter.Length), installedWriter)
                        || !Equal(Read(h, installedBase + installedLayout.Map(RuntimeRva), installedRuntime.Length), installedRuntime)
                        || !Equal(Read(h, installedBase + installedLayout.Map(0x5d01d50), installedEnum.Length), installedEnum)
                        || !Equal(Read(h, installedBase + installedLayout.Map(0x5d0e2e0), installedDuration.Length), installedDuration)
                        || !Equal(Read(h, installedBase + installedLayout.Map(0x5d8fc30), installedCap.Length), installedCap)
                        || !Equal(Read(h, installedBase + installedLayout.Map(0x360d0b0), installedCpu.Length), installedCpu)
                        || !Equal(Read(h, installedBase + installedLayout.Map(0x5d0e630), installedTransition.Length), installedTransition))
                        throw new InvalidOperationException("Hook ownership changed; refusing restoration");
                    Patch(h, installedBase + installedLayout.Map(ListRva), ListOriginal);
                    Patch(h, installedBase + installedLayout.Map(WriterRva), WriterOriginal);
                    Patch(h, installedBase + installedLayout.Map(RuntimeRva), installedLayout.RuntimeOriginal);
                    Patch(h, installedBase + installedLayout.Map(0x5d01d50), EnumOriginal);
                    Patch(h, installedBase + installedLayout.Map(0x5d0e2e0), DurationOriginal);
                    Patch(h, installedBase + installedLayout.Map(0x5d8fc30), CapOriginalFor(installedLayout));
                    Patch(h, installedBase + installedLayout.Map(0x360d0b0), CpuOriginal);
                    Patch(h, installedBase + installedLayout.Map(0x5d0e630), DurationOriginal);
                    installedPid = 0;
                    return "Original code restored; allocations retained until game exit";
                } finally { CloseHandle(h); }
            }
        }
        public static long[] DebugAddresses() { return new long[] { codeAddress, dataAddress }; }
        public static long[] DebugSites() { return new long[] { installedLayout.Map(ListRva), installedLayout.Map(WriterRva), installedLayout.Map(RuntimeRva), installedLayout.Map(0xe1963e0), installedLayout.Map(0x5d01d50), installedLayout.Map(0x5d0e2e0), installedLayout.Map(0x5d8fc30), installedLayout.Map(0x360d0b0), installedLayout.Map(0x5d0e630) }; }

        private static bool WaitForPlaycallGate(Process game, Layout layout)
        {
            bool loaded = false;
            foreach (System.Reflection.Assembly a in AppDomain.CurrentDomain.GetAssemblies()) {
                System.Reflection.AssemblyName name = a.GetName();
                if (name.Name == "OfflinePlaycall" && name.Version.Major == 0 && name.Version.Minor == 4)
                    loaded = true;
            }
            if (!loaded) return false;
            IntPtr h = OpenProcess(0x1010, false, game.Id);
            if (h == IntPtr.Zero) return true;
            try { return !Equal(Read(h, game.MainModule.BaseAddress.ToInt64() + layout.Map(0x79a79ce), 2), Hex("9090")); }
            finally { CloseHandle(h); }
        }

        private static byte[] BuildData(long d)
        {
            byte[] bytes = new byte[4096];
            Put(bytes, 0x83c, 0x42700000);
            bytes[0xa8] = 1; // Serializer property is isInteractive (not disabled).
            Put(bytes, 0x10, d + 0x400); Put(bytes, 0x28, d + 0x420);
            Put(bytes, 0x78, d + 0x100); Put(bytes, 0x80, d + 0x120);
            string[] labels = { "ON", "OFF at playcall", "OFF", "60 seconds" }; int[] values = { 1, 2, 0, 3 };
#if COMBINED_DYNASTY_SETTINGS
            labels=new string[]{"On","Off At Playcall","Off","60 Seconds"};
#endif
            for (int i = 0; i < 4; i++) {
                int obj = 0x180 + i * 0x80, label = 0x600 + i * 0x40;
                Put(bytes, 0x100 + i * 8, d + obj); Put(bytes, obj + 0x28, values[i]);
                Put(bytes, obj + 0x30, d + label); PutString(bytes, label, labels[i]);
            }
            PutString(bytes, 0x400, "Play Clock");
            PutString(bytes, 0x420, "Enable or disable the play clock. OFF at playcall disables it while you are choosing a play. 60 seconds gives a finite countdown with delay-of-game penalties. Applies across offline dynasties.");
#if COMBINED_DYNASTY_SETTINGS
            PutString(bytes,0x400,"Offensive Play Clock");
            PutString(bytes,0x420,"On: normal countdown. Off At Playcall: pauses during play selection. Off: disables the countdown and delay-of-game penalties. 60 Seconds: extended countdown. Applies across offline Dynasties.");
            BuildCombinedDefensiveClockData(bytes,d);
#endif
            return bytes;
        }
        private static byte[] BuildList(long b, long d) { return BuildListFor(b, d, GetLayouts()[0]); }
        private static byte[] BuildListUpdated(long b, long d) { return BuildListFor(b, d, GetLayouts()[1]); }
        private static byte[] BuildListFor(long b, long d, Layout layout)
        {
            Code c = new Code();
#if COMBINED_DYNASTY_SETTINGS
            // The combined menu emits Play Clock in GAME SETTINGS. Retain this
            // owned detour and its descriptor marker for chain verification.
            c.MovR10(d);
#else
            c.Hex("488b8f18040000488b01ff90100a000084c0"); // Native online-mode getter.
            c.Branch("0f85", "replay");
            c.Call(b + layout.Map(0x27ac630)); c.Hex("4885c0"); c.Branch("0f84", "replay");
            c.Hex("488bc8488b00ba01000000ff5010");
            c.Hex("83f802"); c.Branch("0f87", "replay");
            c.MovR10(d); c.Hex("83f801"); c.Branch("0f85", "listvalue");
            c.Hex("4183ba2808000001"); c.Branch("0f85", "listvalue"); c.Hex("b803000000");
            c.Label("listvalue"); c.Hex("4189425c41ff8200080000"); // Current value and list telemetry.
            c.Hex("ffc3895c242041b8"); c.Int(Key); c.Hex("49b9"); c.Long(d);
            c.Hex("488bd6488bcf"); c.Call(b + layout.Map(0x79afaa0));
#endif
            c.Label("replay"); c.Bytes(ListOriginal); c.Bytes(Jump(b + layout.Map(ListRva) + ListOriginal.Length));
            return c.Finish();
        }
        private static byte[] BuildWriter(long b, long d) { return BuildWriterFor(b, d, GetLayouts()[0]); }
        private static byte[] BuildWriterUpdated(long b, long d) { return BuildWriterFor(b, d, GetLayouts()[1]); }
        private static byte[] BuildWriterFor(long b, long d, Layout layout)
        {
            Code c = new Code();
#if COMBINED_DYNASTY_SETTINGS
            c.Hex("3d02100000");c.Branch("0f84","defensiveClock");
#endif
            c.Hex("3d"); c.Int(Key); c.Branch("0f84", "custom");
            c.Hex("83c0fe3ddb000000"); c.Branch("0f87", "end");
            c.Bytes(Jump(b + layout.Map(WriterRva) + WriterOriginal.Length));
            c.Label("custom");
            c.Hex("498b8e18040000488b01ff90100a000084c0"); c.Branch("0f85", "end");
            c.Hex("488b06418bd7"); c.Hex("49b8"); c.Long(b + layout.Map(0xac9aed0)); // 'value'
            c.Hex("488bceff501883f803"); c.Branch("0f87", "end");
            c.Hex("4883ec3089442420");
            c.Call(b + layout.Map(0x27ac630)); c.Hex("4885c0"); c.Branch("0f84", "cleanup");
            c.Hex("448b4424204183f803"); c.Branch("0f85", "nativevalue"); c.Hex("41b801000000");
            c.Label("nativevalue"); c.Hex("488bc8488b00ba01000000ff5028"); // Native profile enum writer; marks profile dirty.
            c.MovR10(d); c.Hex("41ff82040800008b4424204189820808000083f8030f94c00fb6c04189822808000041ff822c080000");
            c.Label("cleanup"); c.Hex("4883c430");
            c.Label("end"); c.Bytes(Jump(b + layout.Map(0x79b850d)));
#if COMBINED_DYNASTY_SETTINGS
            c.Label("defensiveClock");
            c.Hex("498b8e18040000488b01ff90100a000084c0");c.Branch("0f85","end");
            c.Hex("488b06418bd749b8");c.Long(b+layout.Map(0xac9aed0));c.Hex("488bceff5018");
            foreach(int seconds in new int[]{15,20,30,45,60,0}){c.Hex("83f8");c.Bytes(new byte[]{(byte)seconds});c.Branch("0f84","saveDefensiveClock");}
            c.Branch("e9","end");
            c.Label("saveDefensiveClock");c.MovR10(d);c.Hex("418982800800004189825c09000041ff8284080000");c.Branch("e9","end");
#endif
            return c.Finish();
        }
        private static byte[] BuildRuntime(long b, long d) { return BuildRuntimeFor(b, d, GetLayouts()[0]); }
        private static byte[] BuildRuntimeUpdated(long b, long d) { return BuildRuntimeFor(b, d, GetLayouts()[1]); }
        private static byte[] BuildRuntimeFor(long b, long d, Layout layout)
        {
            // Only the clock manager's enum-1 read is extended. The match cache,
            // timers, pause handling, and all other game modes retain native behavior.
            Code c = new Code();
            c.Hex("48b8"); c.Long(b + layout.Map(0xe1963e0)); c.Hex("488b08ba01000000");
            c.Call(b + layout.Map(0x5d01d50));
            // The original getter is a leaf. Preserve its otherwise untouched
            // volatile integer/vector registers around the profile accessor.
            c.Hex("4881ecc00000008944242048894c242848895424304c894424384c894c24404c895424484c895c2450");
            for (int i = 0; i < 6; i++) { c.Hex("f30f7f"); c.Bytes(new byte[] { (byte)(0x84 + i * 8), 0x24 }); c.Int(0x60 + i * 16); }
            // Live offline Dynasty uses legacy mode 14 and local match type 3.
            // Require both; unknown or online contexts fall back to the match enum.
            c.Hex("48b8"); c.Long(b + layout.Map(0xd8b4808)); c.Hex("83380e"); c.Branch("0f85", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xf2c4d48)); c.Hex("833803"); c.Branch("0f85", "restore");
            // The main-menu provider may be rebuilt with user index -1 while
            // in a match. Use the game's current-user profile accessor here.
            c.Hex("48b8"); c.Long(b + layout.Map(0xe1963d0)); c.Hex("48833800"); c.Branch("0f84", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xe1963d8)); c.Hex("48833800"); c.Branch("0f84", "restore");
            c.Call(b + layout.Map(0x27ac630)); c.Hex("4885c0"); c.Branch("0f84", "restore");
            c.MovR10(d); c.Hex("49898218080000488bc8488b00ba01000000ff5010");
            c.Hex("83f802"); c.Branch("0f87", "restore");
            c.Hex("89442420"); c.MovR10(d); c.Hex("41ff821008000041898214080000");
            c.Label("restore");
            for (int i = 0; i < 6; i++) { c.Hex("f30f6f"); c.Bytes(new byte[] { (byte)(0x84 + i * 8), 0x24 }); c.Int(0x60 + i * 16); }
            c.Hex("488b4c2428488b5424304c8b4424384c8b4c24404c8b5424484c8b5c24508b4424204881c4c0000000");
            c.Bytes(Jump(b + layout.Map(RuntimeRva) + layout.RuntimeOriginal.Length));
            return c.Finish();
        }
        private static byte[] BuildEnum(long b, long d) { return BuildEnumFor(b, d, GetLayouts()[0]); }
        private static byte[] BuildEnumUpdated(long b, long d) { return BuildEnumFor(b, d, GetLayouts()[1]); }
        private static byte[] BuildEnumFor(long b, long d, Layout layout)
        {
            // The display, actual countdown and expiry paths all read enum 1.
            // Extend that getter for the active offline Dynasty cache only.
            Code c = new Code();
            c.Hex("83fa01"); c.Branch("0f85", "native");
            c.Hex("4863c20fb7444160");
            // Function entry has an 8-byte return address: C8 aligns native calls.
            c.Hex("4881ecc80000008944242048894c242848895424304c894424384c894c24404c895424484c895c2450");
            for (int i = 0; i < 6; i++) { c.Hex("f30f7f"); c.Bytes(new byte[] { (byte)(0x84 + i * 8), 0x24 }); c.Int(0x60 + i * 16); }
            c.Hex("48b8"); c.Long(b + layout.Map(0xe1963e0)); c.Hex("483908"); c.Branch("0f85", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xd8b4808)); c.Hex("83380e"); c.Branch("0f85", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xf2c4d48)); c.Hex("833803"); c.Branch("0f85", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xe1963d0)); c.Hex("48833800"); c.Branch("0f84", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xe1963d8)); c.Hex("48833800"); c.Branch("0f84", "restore");
            c.Call(b + layout.Map(0x27ac630)); c.Hex("4885c0"); c.Branch("0f84", "restore");
            c.Hex("488bc8488b00ba01000000ff501083f802"); c.Branch("0f87", "restore");
            c.Hex("89442420"); c.MovR10(d); c.Hex("41ff822008000041898224080000");
#if COMBINED_DYNASTY_SETTINGS
            AppendCombinedDefensiveEnumOverride(c,b,d);
#endif
            c.Label("restore");
            for (int i = 0; i < 6; i++) { c.Hex("f30f6f"); c.Bytes(new byte[] { (byte)(0x84 + i * 8), 0x24 }); c.Int(0x60 + i * 16); }
            c.Hex("488b4c2428488b5424304c8b4424384c8b4c24404c8b5424484c8b5c24508b4424204881c4c8000000c3");
            c.Label("native"); c.Bytes(EnumOriginal);
            return c.Finish();
        }
        private sealed class Code
        {
            private readonly List<byte> bytes = new List<byte>();
            private readonly Dictionary<string, int> labels = new Dictionary<string, int>();
            private readonly List<KeyValuePair<int, string>> branches = new List<KeyValuePair<int, string>>();
            public void Hex(string s) { Bytes(DynastyPlayClockAction.Hex(s)); }
            public void Bytes(byte[] b) { bytes.AddRange(b); }
            public void Int(int n) { Bytes(BitConverter.GetBytes(n)); }
            public void Long(long n) { Bytes(BitConverter.GetBytes(n)); }
            public void Call(long a) { Hex("48b8"); Long(a); Hex("ffd0"); }
            public void MovR10(long a) { Hex("49ba"); Long(a); }
            public void Label(string s) { labels.Add(s, bytes.Count); }
            public void Branch(string op, string label) { Hex(op); branches.Add(new KeyValuePair<int, string>(bytes.Count, label)); Int(0); }
            public byte[] Finish() {
                byte[] b = bytes.ToArray();
                foreach (KeyValuePair<int, string> fix in branches)
                    Array.Copy(BitConverter.GetBytes(labels[fix.Value] - fix.Key - 4), 0, b, fix.Key, 4);
                return b;
            }
        }
        private static void Put(byte[] b, int offset, long n) { Array.Copy(BitConverter.GetBytes(n), 0, b, offset, 8); }
        private static void Put(byte[] b, int offset, int n) { Array.Copy(BitConverter.GetBytes(n), 0, b, offset, 4); }
        private static void PutString(byte[] b, int offset, string s) { byte[] str = Encoding.ASCII.GetBytes(s + "\0"); Array.Copy(str, 0, b, offset, str.Length); }
        private static byte[] Jump(long a) { byte[] b = new byte[14]; b[0] = 0xff; b[1] = 0x25; Put(b, 6, a); return b; }
        private static byte[] Pad(byte[] b, int n) { byte[] r = new byte[n]; for (int i = 0; i < n; i++) r[i] = 0x90; Array.Copy(b, r, b.Length); return r; }
        private static byte[] Hex(string s) { byte[] b = new byte[s.Length / 2]; for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16); return b; }
        private static bool Equal(byte[] a, byte[] b) { if (a == null || b == null || a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
        private static string Hash(byte[] b) { using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(b)).Replace("-", "").ToLowerInvariant(); }
        private static byte[] Read(IntPtr h, long a, int n) { byte[] b = new byte[n]; IntPtr got; return ReadProcessMemory(h, new IntPtr(a), b, new IntPtr(n), out got) && got.ToInt64() == n ? b : null; }
        private static void Write(IntPtr h, long a, byte[] b) { IntPtr got; if (!WriteProcessMemory(h, new IntPtr(a), b, new IntPtr(b.Length), out got) || got.ToInt64() != b.Length) throw new InvalidOperationException("Memory write failed"); }
        private static void Patch(IntPtr h, long a, byte[] b) {
            uint previous;
            if (!VirtualProtectEx(h, new IntPtr(a), new UIntPtr((uint)b.Length), 0x40, out previous)) throw new InvalidOperationException("Protection change failed");
            try { Write(h, a, b); if (!FlushInstructionCache(h, new IntPtr(a), new UIntPtr((uint)b.Length))) throw new InvalidOperationException("Instruction cache flush failed"); if (!Equal(Read(h, a, b.Length), b)) throw new InvalidOperationException("Hook readback failed"); }
            finally { uint ignored; VirtualProtectEx(h, new IntPtr(a), new UIntPtr((uint)b.Length), previous, out ignored); }
        }
        private static bool IsInteractiveGameWindow(Process game) {
            IntPtr window = GetForegroundWindow(); uint owner;
            if (window == IntPtr.Zero || !IsWindowVisible(window) || IsIconic(window)) return false;
            GetWindowThreadProcessId(window, out owner); if (owner != game.Id) return false;
            Rectangle r; return GetClientRect(window, out r) && r.Right - r.Left >= 1024 && r.Bottom - r.Top >= 600;
        }
        private static void Log(ILogger logger, string message) {
            try { if (logger != null) logger.Log("[Dynasty Play Clock] " + message, new object[0]); } catch { }
#if COMBINED_DYNASTY_SETTINGS
            OfflineDynastySettings.DiagnosticLog.Write(OfflineDynastySettings.DiagnosticLog.PluginFolder,"clock",message);
            try { File.AppendAllText(OfflineDynastySettings.SettingsFiles.PathFor(Path.GetDirectoryName(typeof(DynastyPlayClockAction).Assembly.Location), "DynastyPlayClock.log"), DateTime.Now.ToString("O", CultureInfo.InvariantCulture) + " " + message + Environment.NewLine); } catch { }
#else
            try { File.AppendAllText(Path.Combine(Path.GetDirectoryName(typeof(DynastyPlayClockAction).Assembly.Location), "DynastyPlayClock.log"), DateTime.Now.ToString("O", CultureInfo.InvariantCulture) + " " + message + Environment.NewLine); } catch { }
#endif
        }
        private sealed class Region { public readonly string Name, Hash; public readonly long Rva; public readonly int Length; public Region(string name, long rva, int length, string hash) { Name = name; Rva = rva; Length = length; Hash = hash; } }
        [StructLayout(LayoutKind.Sequential)] private struct Rectangle { public int Left, Top, Right, Bottom; }
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadProcessMemory(IntPtr h, IntPtr a, [Out] byte[] b, IntPtr n, out IntPtr got);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool WriteProcessMemory(IntPtr h, IntPtr a, byte[] b, IntPtr n, out IntPtr got);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr VirtualAllocEx(IntPtr h, IntPtr a, UIntPtr n, uint type, uint protection);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool VirtualProtectEx(IntPtr h, IntPtr a, UIntPtr n, uint protection, out uint old);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool FlushInstructionCache(IntPtr h, IntPtr a, UIntPtr n);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr w, out uint pid);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr w);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr w);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr w, out Rectangle r);
    }
}
