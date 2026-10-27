using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace DynastyPlayClock
{
    public sealed partial class DynastyPlayClockAction
    {
        internal static long CombinedData { get { lock(Sync)return dataAddress; } }
        internal static long CombinedWriter { get { lock(Sync)return codeAddress+1024; } }
        internal static byte[] BuildCombinedSectionClockRow(long b, long d)
        {
            // Called between the offensive and defensive playcall rows.
            // Native caller is already scoped to offline Dynasty settings.
            Layout layout=GetLayouts()[1];
            Code c=new Code();
            c.Hex("4883ec28");
            c.Call(b+layout.Map(0x27ac630));c.Hex("4885c0");c.Branch("0f84","done");
            c.Hex("488bc8488b00ba01000000ff501083f802");c.Branch("0f87","done");
            c.MovR10(d);c.Hex("83f801");c.Branch("0f85","value");
            c.Hex("4183ba2808000001");c.Branch("0f85","value");c.Hex("b803000000");
            c.Label("value");c.Hex("4189425c41ff8200080000");
            c.Hex("895c242041b8");c.Int(Key);c.Hex("49b9");c.Long(d);
            c.Hex("488bd6488bcf");c.Call(b+layout.Map(0x79afaa0));c.Hex("ffc3");
            c.Label("done");c.Hex("4883c428c3");
            return c.Finish();
        }
        private static void BuildCombinedDefensiveClockData(byte[] bytes,long d)
        {
            int seconds=OfflineDynastySettings.ClockPreferences.Load(System.IO.Path.GetDirectoryName(typeof(DynastyPlayClockAction).Assembly.Location));
            Put(bytes,0x880,seconds);
            Array.Copy(bytes,0,bytes,0x900,0xb0);
            Put(bytes,0x910,d+0xf00);Put(bytes,0x928,d+0xf30);Put(bytes,0x95c,seconds);
            Put(bytes,0x978,d+0xa00);Put(bytes,0x980,d+0xa40);
            // Labels use the free 0x700..0x7ff range; the final two option
            // objects occupy 0xe00..0xeff, before the heading at 0xf00.
            int[] choices={3,5,15,20,30,45,60,0};
            for(int i=0;i<choices.Length;i++) {
                int obj=0xb00+i*0x80,label=0x700+i*0x20;
                Put(bytes,0xa00+i*8,d+obj);Put(bytes,obj+0x28,choices[i]);Put(bytes,obj+0x30,d+label);
                PutString(bytes,label,choices[i]==0?"Off":choices[i]+" Seconds");
            }
            PutString(bytes,0xf00,"Defensive Play Clock");
            PutString(bytes,0xf30,"Time to choose a defensive play after the offense selects. Off removes the limit. Default: 15 seconds. Independent of Offensive Play Clock. Applies across offline Dynasties.");
        }
        internal static byte[] BuildCombinedDefensiveClockRow(long b,long d)
        {
            Code c=new Code();c.Hex("4883ec28");c.MovR10(d);c.Hex("418b82800800004189825c090000");
            c.Hex("895c242041b80210000049b9");c.Long(d+0x900);c.Hex("488bd6488bcf");c.Call(b+0x799be70);c.Hex("ffc34883c428c3");
            return c.Finish();
        }
        internal static List<OfflineDynastySettings.MemoryCheck> CombinedChecks(bool writerChained)
        {
            lock(Sync) {
                List<OfflineDynastySettings.MemoryCheck> checks=new List<OfflineDynastySettings.MemoryCheck>();
                long[] rvas={ListRva,WriterRva,RuntimeRva,0x5d01d50,0x5d0e2e0,0x5d8fc30,0x360d0b0,0x5d0e630};
                byte[][] entries={installedList,installedWriter,installedRuntime,installedEnum,installedDuration,installedCap,installedCpu,installedTransition};
                for(int i=0;i<rvas.Length;i++)if(i!=1 || !writerChained)checks.Add(new OfflineDynastySettings.MemoryCheck(installedBase+installedLayout.Map(rvas[i]),entries[i]));
                byte[][] bodies={BuildListFor(installedBase,dataAddress,installedLayout),BuildWriterFor(installedBase,dataAddress,installedLayout),BuildRuntimeFor(installedBase,dataAddress,installedLayout),BuildEnumFor(installedBase,dataAddress,installedLayout),BuildDurationFor(installedBase,dataAddress,installedLayout),BuildCapFor(installedBase,dataAddress,installedLayout),BuildCpuFor(installedBase,dataAddress,installedLayout),BuildTransitionFor(installedBase,dataAddress,installedLayout)};
                for(int i=0;i<bodies.Length;i++)checks.Add(new OfflineDynastySettings.MemoryCheck(codeAddress+1024*i,bodies[i]));
                if(defensiveCode!=0 && defensiveStart==installedStart) {
                    checks.Add(new OfflineDynastySettings.MemoryCheck(installedBase+DefensiveTimerRva,defensiveDetour));
                    checks.Add(new OfflineDynastySettings.MemoryCheck(defensiveCode,defensiveBody));
                    checks.Add(new OfflineDynastySettings.MemoryCheck(installedBase+0x5ff26a0,Hex("c5fa10052435db04c3")));
                    checks.Add(new OfflineDynastySettings.MemoryCheck(installedBase+DefensiveGateRva,DefensiveGateOriginal));
                }
                return checks;
            }
        }
        internal sealed class CombinedPersistence : IDisposable
        {
            private IntPtr handle;
            private readonly long data;
            private bool saved;
            internal CombinedPersistence(IntPtr handle, long data, bool saved)
            { this.handle = handle; this.data = data; this.saved = saved; }
            internal void Poll()
            {
                byte[] flag = Read(handle, data + 0x828, 4);
                if (flag == null) throw new InvalidOperationException("Could not read the 60-second preference; will retry.");
                bool enabled = BitConverter.ToInt32(flag, 0) == 1;
                if (enabled == saved) return;
                SaveExtended(enabled); // Only acknowledge a change after it was saved.
                saved = enabled;
            }
            public void Dispose() { if (handle != IntPtr.Zero) { CloseHandle(handle); handle = IntPtr.Zero; } }
        }
        internal static CombinedPersistence OpenCombinedPersistence(Process game)
        {
            lock (Sync) {
                if (installedPid != game.Id || installedStart != game.StartTime.ToUniversalTime().Ticks)
                    throw new InvalidOperationException("Game changed before preference monitoring started.");
                bool saved = LoadExtended();
                IntPtr handle = OpenProcess(0x1010, false, game.Id);
                if (handle == IntPtr.Zero) throw new InvalidOperationException("Could not open preference monitor; will retry.");
                return new CombinedPersistence(handle, dataAddress, saved);
            }
        }
        internal static void VerifyCombinedHooks(Process game, bool writerChained)
        {
            lock (Sync) {
                if (installedPid != game.Id || installedStart != game.StartTime.ToUniversalTime().Ticks)
                    throw new InvalidOperationException("Game changed during hook verification.");
                IntPtr handle = OpenProcess(0x1010, false, game.Id);
                if (handle == IntPtr.Zero) throw new InvalidOperationException("Could not open hook verification; will retry.");
                try {
                    long[] rvas = { ListRva, WriterRva, RuntimeRva, 0x5d01d50, 0x5d0e2e0, 0x5d8fc30, 0x360d0b0, 0x5d0e630 };
                    byte[][] expected = { installedList, installedWriter, installedRuntime, installedEnum, installedDuration, installedCap, installedCpu, installedTransition };
                    for (int i = 0; i < rvas.Length; i++) {
                        if (i == 1 && writerChained) continue; // Split verifies the shared writer.
                        VerifyBytes(handle, installedBase + installedLayout.Map(rvas[i]), expected[i], "Play Clock entry " + i);
                    }
                    byte[][] bodies = {
                        BuildListFor(installedBase, dataAddress, installedLayout), BuildWriterFor(installedBase, dataAddress, installedLayout),
                        BuildRuntimeFor(installedBase, dataAddress, installedLayout), BuildEnumFor(installedBase, dataAddress, installedLayout),
                        BuildDurationFor(installedBase, dataAddress, installedLayout), BuildCapFor(installedBase, dataAddress, installedLayout),
                        BuildCpuFor(installedBase, dataAddress, installedLayout), BuildTransitionFor(installedBase, dataAddress, installedLayout)
                    };
                    for (int i = 0; i < bodies.Length; i++) VerifyBytes(handle, codeAddress + 1024 * i, bodies[i], "Play Clock body " + i);
                } finally { CloseHandle(handle); }
            }
        }
        private static void VerifyBytes(IntPtr handle, long address, byte[] expected, string name)
        {
            byte[] actual = Read(handle, address, expected.Length);
            if (actual == null) throw new InvalidOperationException("Could not read " + name + "; will retry verification.");
            if (!Equal(actual, expected)) throw new OfflineDynastySettings.HookOwnershipException(OfflineDynastySettings.DiagnosticLog.Mismatch(name,address-installedBase,Hash(expected),actual));
        }
    }
}
