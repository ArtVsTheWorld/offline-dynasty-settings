using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using FrostySdk.Interfaces;

namespace DynastyPlayClock
{
    public sealed partial class DynastyPlayClockAction
    {
        private static string PreferencePath { get {
#if COMBINED_DYNASTY_SETTINGS
            return OfflineDynastySettings.SettingsFiles.PathFor(Path.GetDirectoryName(typeof(DynastyPlayClockAction).Assembly.Location), "DynastyPlayClock.ini");
#else
            return Path.Combine(Path.GetDirectoryName(typeof(DynastyPlayClockAction).Assembly.Location), "DynastyPlayClock.ini");
#endif
        } }
        private static bool LoadExtended()
        {
            try { return File.Exists(PreferencePath) && File.ReadAllText(PreferencePath).Trim() == "ExtendedSeconds=60"; }
            catch {
#if COMBINED_DYNASTY_SETTINGS
                throw new IOException("Could not read DynastyPlayClock.ini; will retry without resetting the saved choice.");
#else
                return false;
#endif
            }
        }
        private static void SaveExtended(bool enabled)
        {
            string path = PreferencePath, temporary = path + ".tmp";
            File.WriteAllText(temporary, enabled ? "ExtendedSeconds=60\r\n" : "ExtendedSeconds=0\r\n");
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
        private static void MonitorPreference(int pid, ILogger logger, CancellationToken token)
        {
            IntPtr h = OpenProcess(0x1010, false, pid);
            if (h == IntPtr.Zero) return;
            bool saved = LoadExtended(), warned = false;
            try {
                using (Process game = Process.GetProcessById(pid)) {
                    while (!game.HasExited && !token.IsCancellationRequested) {
                        byte[] flag = Read(h, dataAddress + 0x828, 4);
                        if (flag == null) return;
                        bool enabled = BitConverter.ToInt32(flag, 0) == 1;
                        if (enabled != saved) {
                            try { SaveExtended(enabled); saved = enabled; warned = false; }
                            catch (Exception ex) { if (!warned) Log(logger, "Could not save extended-clock choice: " + ex.Message); warned = true; }
                        }
                        Thread.Sleep(100);
                    }
                }
            } finally { CloseHandle(h); }
        }
        private static byte[] CapOriginalFor(Layout layout)
        {
            return Hex(layout.Map(0x5d8fc30) == 0x5d8fc30 ? "c5fa10766ce8d6def7ffc5f82fc6" : "c5fa10766ce8ecdcf7ffc5f82fc6");
        }
        private static byte[] BuildCap(long b, long d) { return BuildCapFor(b, d, GetLayouts()[0]); }
        private static byte[] BuildCapUpdated(long b, long d) { return BuildCapFor(b, d, GetLayouts()[1]); }
        private static byte[] BuildCapFor(long b, long d, Layout layout)
        {
            Code c = new Code();
            c.Hex("c5fa10766c"); // Native normal-duration limit in XMM6.
            c.Hex("4881ecc0000000488944242048894c242848895424304c894424384c894c24404c895424484c895c2450c744245800000000");
            for (int i = 0; i < 6; i++) { c.Hex("f30f7f"); c.Bytes(new byte[] { (byte)(0x84 + i * 8), 0x24 }); c.Int(0x60 + i * 16); }
            c.MovR10(d); c.Hex("4183ba2808000001"); c.Branch("0f85", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xd8b4808)); c.Hex("83380e"); c.Branch("0f85", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xf2c4d48)); c.Hex("833803"); c.Branch("0f85", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xf7261f8)); c.Hex("483908"); c.Branch("0f85", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xe1963d0)); c.Hex("48833800"); c.Branch("0f84", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xe1963d8)); c.Hex("48833800"); c.Branch("0f84", "restore");
            c.Call(b + layout.Map(0x27ac630)); c.Hex("4885c0"); c.Branch("0f84", "restore");
            c.Hex("488bc8488b00ba01000000ff501083f801"); c.Branch("0f85", "restore");
            c.Hex("c744245801000000"); c.MovR10(d); c.Hex("41ff8240080000");
            c.Label("restore");
            for (int i = 0; i < 6; i++) { c.Hex("f30f6f"); c.Bytes(new byte[] { (byte)(0x84 + i * 8), 0x24 }); c.Int(0x60 + i * 16); }
            c.Hex("488b442420488b4c2428488b5424304c8b4424384c8b4c24404c8b5424484c8b5c2450");
            c.Hex("837c245800"); c.Branch("0f84", "native");
            c.Hex("48b8"); c.Long(d); c.Hex("c5fa10b03c080000");
            c.Label("native"); c.Hex("4881c4c0000000");
            c.Call(b + layout.Map(0x5d0db10)); c.Hex("c5f82fc6");
            c.Bytes(Jump(b + layout.Map(0x5d8fc30) + 14));
            return c.Finish();
        }
        private static readonly byte[] CpuOriginal = Hex("c5fa10486cc5f82fc17608c5fa5cc7c5f25cf8");
        private static byte[] BuildCpu(long b, long d) { return BuildCpuFor(b, d, GetLayouts()[0]); }
        private static byte[] BuildCpuUpdated(long b, long d) { return BuildCpuFor(b, d, GetLayouts()[1]); }
        private static byte[] BuildCpuFor(long b, long d, Layout layout)
        {
            // The CPU delay calculation clamps its starting clock to the rules'
            // normal 40 seconds. Use 60 here so its existing cadence and other
            // readiness checks can run against the actual extended countdown.
            Code c = new Code();
            c.Hex("c5fa10486c");
            c.Hex("4881ecc0000000488944242048894c242848895424304c894424384c894c24404c895424484c895c2450");
            for (int i = 0; i < 6; i++) { c.Hex("f30f7f"); c.Bytes(new byte[] { (byte)(0x84 + i * 8), 0x24 }); c.Int(0x60 + i * 16); }
            c.Hex("817c247000002042"); c.Branch("0f85", "restore"); // Only the native 40-second cap.
            c.MovR10(d); c.Hex("4183ba2808000001"); c.Branch("0f85", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xd8b4808)); c.Hex("83380e"); c.Branch("0f85", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xf2c4d48)); c.Hex("833803"); c.Branch("0f85", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xf7261f8)); c.Hex("488b004885c0"); c.Branch("0f84", "restore");
            c.Hex("48394820"); c.Branch("0f85", "restore");
            c.Hex("81792c00007042"); c.Branch("0f85", "restore"); // Current play clock really has a 60-second maximum.
            c.Hex("48b8"); c.Long(b + layout.Map(0xe1963d0)); c.Hex("48833800"); c.Branch("0f84", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xe1963d8)); c.Hex("48833800"); c.Branch("0f84", "restore");
            c.Call(b + layout.Map(0x27ac630)); c.Hex("4885c0"); c.Branch("0f84", "restore");
            c.Hex("488bc8488b00ba01000000ff501083f801"); c.Branch("0f85", "restore");
            c.Hex("c744247000007042"); c.MovR10(d); c.Hex("41ff8244080000");
            c.Label("restore");
            for (int i = 0; i < 6; i++) { c.Hex("f30f6f"); c.Bytes(new byte[] { (byte)(0x84 + i * 8), 0x24 }); c.Int(0x60 + i * 16); }
            c.Hex("488b442420488b4c2428488b5424304c8b4424384c8b4c24404c8b5424484c8b5c24504881c4c0000000");
            c.Hex("c5f82fc1"); c.Branch("0f86", "continue");
            c.Hex("c5fa5cc7c5f25cf8");
            c.Label("continue"); c.Bytes(Jump(b + layout.Map(0x360d0b0) + CpuOriginal.Length));
            return c.Finish();
        }
        private static byte[] BuildDuration(long b, long d) { return BuildDurationFor(b, d, GetLayouts()[0]); }
        private static byte[] BuildDurationUpdated(long b, long d) { return BuildDurationFor(b, d, GetLayouts()[1]); }
        private static byte[] BuildTransition(long b, long d) { return BuildTransitionFor(b, d, GetLayouts()[0]); }
        private static byte[] BuildTransitionUpdated(long b, long d) { return BuildTransitionFor(b, d, GetLayouts()[1]); }
        private static byte[] BuildTransitionFor(long b, long d, Layout layout)
        {
            // Only the verified transition dispatcher can extend this remaining-time reset.
            // Only the current manager's play-clock object is eligible; quarter
            // clocks, other modes and every existing Play Clock choice are native.
            Code c = new Code();
            c.Hex("4881ecc8000000488944242048894c242848895424304c894424384c894c24404c895424484c895c2450");
            for (int i = 0; i < 6; i++) { c.Hex("f30f7f"); c.Bytes(new byte[] { (byte)(0x84 + i * 8), 0x24 }); c.Int(0x60 + i * 16); }
            c.Hex("48b8"); c.Long(b + layout.Map(0x6461440)); c.Hex("48398424c8000000"); c.Branch("0f85", "restore");
            c.MovR10(d);
            c.Hex("0f57c00f2ec8"); c.Branch("0f86", "restore"); // Zero/negative/NaN resets are control-flow signals, not durations.
            c.Hex("4183ba2808000001"); c.Branch("0f85", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xd8b4808)); c.Hex("83380e"); c.Branch("0f85", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xf2c4d48)); c.Hex("833803"); c.Branch("0f85", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xf7261f8)); c.Hex("488b004885c0"); c.Branch("0f84", "restore");
            c.Hex("48394820"); c.Branch("0f85", "restore");
            c.Hex("81792c00007042"); c.Branch("0f85", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xe1963d0)); c.Hex("48833800"); c.Branch("0f84", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xe1963d8)); c.Hex("48833800"); c.Branch("0f84", "restore");
            c.Call(b + layout.Map(0x27ac630)); c.Hex("4885c0"); c.Branch("0f84", "restore");
            c.Hex("488bc8488b00ba01000000ff501083f801"); c.Branch("0f85", "restore");
            // Other positive durations include presentation/control-flow timers.
            c.Hex("8b4424703d00002042"); c.Branch("0f84", "extend"); // 40 seconds.
            c.Hex("3d0000c841"); c.Branch("0f84", "extend"); // 25 seconds.
            c.Hex("3d0000f041"); c.Branch("0f85", "restore"); // 30-second timeout reset.
            c.Label("extend"); c.Hex("c744247000007042"); // Saved XMM1 low float = 60.0.
            c.MovR10(d); c.Hex("41ff8248080000");
            c.Label("restore");
            for (int i = 0; i < 6; i++) { c.Hex("f30f6f"); c.Bytes(new byte[] { (byte)(0x84 + i * 8), 0x24 }); c.Int(0x60 + i * 16); }
            c.Hex("488b442420488b4c2428488b5424304c8b4424384c8b4c24404c8b5424484c8b5c24504881c4c8000000");
            c.Bytes(DurationOriginal); c.Bytes(Jump(b + layout.Map(0x5d0e630) + DurationOriginal.Length));
            return c.Finish();
        }
        private static byte[] BuildDurationFor(long b, long d, Layout layout)
        {
            // Override a reset argument, never refresh a running countdown.
            // Only the current manager's play-clock object is eligible; quarter
            // clocks, other modes and every existing Play Clock choice are native.
            Code c = new Code();
            c.Hex("4881ecc8000000488944242048894c242848895424304c894424384c894c24404c895424484c895c2450");
            for (int i = 0; i < 6; i++) { c.Hex("f30f7f"); c.Bytes(new byte[] { (byte)(0x84 + i * 8), 0x24 }); c.Int(0x60 + i * 16); }
            c.MovR10(d); c.Hex("660f7eca41899234080000"); // Record the original float reset argument.
            c.Hex("0f57c00f2ec8"); c.Branch("0f86", "restore"); // Zero/negative/NaN resets are control-flow signals, not durations.
            c.Hex("4183ba2808000001"); c.Branch("0f85", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xd8b4808)); c.Hex("83380e"); c.Branch("0f85", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xf2c4d48)); c.Hex("833803"); c.Branch("0f85", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xf7261f8)); c.Hex("488b004885c0"); c.Branch("0f84", "restore");
            c.Hex("48394820"); c.Branch("0f85", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xe1963d0)); c.Hex("48833800"); c.Branch("0f84", "restore");
            c.Hex("48b8"); c.Long(b + layout.Map(0xe1963d8)); c.Hex("48833800"); c.Branch("0f84", "restore");
            c.Call(b + layout.Map(0x27ac630)); c.Hex("4885c0"); c.Branch("0f84", "restore");
            c.Hex("488bc8488b00ba01000000ff501083f801"); c.Branch("0f85", "restore");
            // Record eligible native reset arguments and return sites in a diagnostic ring.
#if COMBINED_DYNASTY_SETTINGS
            // 0x900 onward holds the defensive-clock descriptor and options.
            // The old standalone ring must never overwrite that UI data.
            c.MovR10(d); c.Hex("41ff8238080000");
#else
            c.MovR10(d); c.Hex("418b823808000041ff823808000083e03fc1e0044d8d9a000900004903c34c8bd88b442470418903488b8424c800000049894308");
#endif
            // Other positive durations include presentation/control-flow timers.
            c.Hex("8b4424703d00002042"); c.Branch("0f84", "extend"); // 40 seconds.
            c.Hex("3d0000c841"); c.Branch("0f84", "extend"); // 25 seconds.
            c.Hex("3d0000f041"); c.Branch("0f85", "restore"); // 30-second timeout reset.
            c.Label("extend"); c.Hex("c744247000007042"); // Saved XMM1 low float = 60.0.
            c.MovR10(d); c.Hex("41ff8230080000");
            c.Label("restore");
            for (int i = 0; i < 6; i++) { c.Hex("f30f6f"); c.Bytes(new byte[] { (byte)(0x84 + i * 8), 0x24 }); c.Int(0x60 + i * 16); }
            c.Hex("488b442420488b4c2428488b5424304c8b4424384c8b4c24404c8b5424484c8b5c24504881c4c8000000");
            c.Bytes(DurationOriginal); c.Bytes(Jump(b + layout.Map(0x5d0e2e0) + DurationOriginal.Length));
            return c.Finish();
        }
    }
}
