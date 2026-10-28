using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DynastyPlayClock
{
    public sealed partial class DynastyPlayClockAction
    {
        private const long ControllerEligibilityRva=0x7faaa90, ControllerPhaseRva=0x5d0e950;
        private const long ControllerCommitSlotRva=0xbac85d8, ControllerCommitRva=0x7a97050;
        private static readonly byte[] ControllerEligibilityOriginal=Hex("488bc455574156488bec4881ec80000000");
        private static readonly byte[] ControllerPhaseOriginal=Hex("48895c241048896c24184889742420");
        private static readonly Region[] ControllerRegions={
            new Region("Controller eligibility", 0x7faaa90, 0x20c, "b49703bf276f0e7eff535d8a79b2f1d5eefbad2381924e1f3e818ba772ef67f1"),
            new Region("Controller native commit", 0x7a97050, 0x180, "9bf79ecd17da5df1ee940b41990b4f398cd452b474cf0bd038e774ae67a5702d"),
            new Region("Controller input setter", 0x2eb3e40, 0xa1, "10b310a8c332eb59077c3cfb10f293f7e5174db7b4080ed0a31f6670559ea8fc"),
            new Region("Controller Bool result", 0x9130550, 0xd, "07fa584adb72c5b00661c65a636c474ab356ea437a9ba959f898f2419318e09c"),
            new Region("Controller play phase", 0x5d0e950, 0x40, "d717623619014ffac45778406f7b16bda60c9325bf6151390fe014fa4baa99e1"),
        };
        private static long controllerStart,controllerDisabledStart;
        private static List<OfflineDynastySettings.MemoryCheck> controllerChecks;

        // The three sites are separate from shared expression/string writers,
        // Drive-Based Practice and Dynamic Gameday Atmosphere's Resume caption.
        internal static void InstallControllerSelect(int pid)
        {
            lock(Sync) using(Process game=Process.GetProcessById(pid)) {
                long start=game.StartTime.ToUniversalTime().Ticks;
                if(installedPid!=pid || installedStart!=start)throw new InvalidOperationException("Clock installation not ready.");
                if(controllerStart==start || controllerDisabledStart==start)return;
                IntPtr h=OpenProcess(0x1038,false,pid);
                if(h==IntPtr.Zero)throw new InvalidOperationException("Could not open Controller Select installation.");
                try {
                    long b=installedBase;
                    List<OfflineDynastySettings.MemoryCheck> nativeChecks=new List<OfflineDynastySettings.MemoryCheck>();
                    foreach(Region region in ControllerRegions) {
                        byte[] bytes=Read(h,b+region.Rva,region.Length);
                        if(bytes==null)throw new InvalidOperationException("Controller Select code not readable yet.");
                        if(Hash(bytes)!=region.Hash) {
                            controllerDisabledStart=start;
                            Log(null,"Controller Select unavailable; clock/settings remain active. "+OfflineDynastySettings.DiagnosticLog.Mismatch(region.Name,region.Rva,region.Hash,bytes));
                            return;
                        }
                        nativeChecks.Add(new OfflineDynastySettings.MemoryCheck(b+region.Rva,bytes));
                    }
                    byte[] originalPointer=BitConverter.GetBytes(b+ControllerCommitRva);
                    if(!Equal(Read(h,b+ControllerCommitSlotRva,8),originalPointer)) {
                        controllerDisabledStart=start;
                        Log(null,"Controller Select unavailable: GameSetup commit slot is owned by another hook. Clock/settings remain active; no controller hooks installed.");
                        return;
                    }
                    IntPtr allocation=VirtualAllocEx(h,IntPtr.Zero,new UIntPtr(8192),0x3000,4);
                    if(allocation==IntPtr.Zero)throw new InvalidOperationException("Controller Select allocation failed.");
                    long remote=allocation.ToInt64();
                    byte[][] blocks={BuildControllerQueue(b,remote,b+ControllerCommitRva),BuildControllerBoundary(b,remote,0),BuildControllerApply(b,remote,b+ControllerCommitRva),BuildControllerEligibility(b,remote,0)};
                    List<OfflineDynastySettings.MemoryCheck> checks=new List<OfflineDynastySettings.MemoryCheck>();
                    for(int i=0;i<blocks.Length;i++) {
                        if(blocks[i].Length>1024)throw new InvalidOperationException("Controller bridge exceeds reserved region.");
                        Write(h,remote+i*1024,blocks[i]);checks.Add(new OfflineDynastySettings.MemoryCheck(remote+i*1024,blocks[i]));
                    }
                    byte[] trampoline=new byte[ControllerEligibilityOriginal.Length+14];
                    Array.Copy(ControllerEligibilityOriginal,trampoline,ControllerEligibilityOriginal.Length);
                    Array.Copy(Jump(b+ControllerEligibilityRva+ControllerEligibilityOriginal.Length),0,trampoline,ControllerEligibilityOriginal.Length,14);
                    Write(h,remote+0xe00,trampoline);checks.Add(new OfflineDynastySettings.MemoryCheck(remote+0xe00,trampoline));
                    Write(h,remote+0x1900,BitConverter.GetBytes(-1));
                    foreach(OfflineDynastySettings.MemoryCheck check in checks)VerifyBytes(h,check.Address,Convert.FromBase64String(check.Bytes),"Controller bridge body");
                    uint previous;
                    if(!VirtualProtectEx(h,allocation,new UIntPtr(4096),0x20,out previous) || !FlushInstructionCache(h,allocation,new UIntPtr(4096)))
                        throw new InvalidOperationException("Controller bridge code protection failed.");
                    List<ControllerChange> changes=new List<ControllerChange>{
                        new ControllerChange(b+ControllerCommitSlotRva,originalPointer,BitConverter.GetBytes(remote)),
                        new ControllerChange(b+ControllerPhaseRva,ControllerPhaseOriginal,Pad(Jump(remote+0x400),ControllerPhaseOriginal.Length)),
                        new ControllerChange(b+ControllerEligibilityRva,ControllerEligibilityOriginal,Pad(Jump(remote+0xc00),ControllerEligibilityOriginal.Length))
                    };
                    using(new ControllerFrozen(game,changes)) {
                        // Validate every target before the first mutation. No unknown hook is chained or overwritten.
                        foreach(OfflineDynastySettings.MemoryCheck check in nativeChecks)
                            if(!Equal(Read(h,check.Address,Convert.FromBase64String(check.Bytes).Length),Convert.FromBase64String(check.Bytes)))
                                throw new InvalidOperationException("Controller contract changed before installation; retry.");
                        foreach(ControllerChange change in changes)
                            if(!Equal(Read(h,change.Address,change.Before.Length),change.Before))throw new InvalidOperationException("Controller target changed before installation; retry.");
                        int applied=0;
                        try {
                            foreach(ControllerChange change in changes){applied++;Patch(h,change.Address,change.After);}
                        } catch {
                            for(int i=applied-1;i>=0;i--)Patch(h,changes[i].Address,changes[i].Before);
                            throw;
                        }
                    }
                    foreach(ControllerChange change in changes)checks.Add(new OfflineDynastySettings.MemoryCheck(change.Address,change.After));
                    // Verify untouched contract tails too, including the native fallback.
                    for(int i=0;i<ControllerRegions.Length;i++) {
                        Region region=ControllerRegions[i];
                        int skip=region.Rva==ControllerEligibilityRva?ControllerEligibilityOriginal.Length:region.Rva==ControllerPhaseRva?ControllerPhaseOriginal.Length:0;
                        byte[] tail=new byte[region.Length-skip];
                        Array.Copy(Convert.FromBase64String(nativeChecks[i].Bytes),skip,tail,0,tail.Length);
                        checks.Add(new OfflineDynastySettings.MemoryCheck(b+region.Rva+skip,tail));
                    }
                    controllerChecks=checks;controllerStart=start;
                    Log(null,"Controller Select active: Pause > Options > Controller Select. Single-controller offline Dynasty; queued team changes apply after the current play, before normal next-play selection. No forced refresh. Code=0x"+remote.ToString("x"));
                } finally { CloseHandle(h); }
            }
        }
        internal static List<OfflineDynastySettings.MemoryCheck> ControllerChecks()
        {
            lock(Sync) return controllerStart==installedStart && controllerChecks!=null
                ?new List<OfflineDynastySettings.MemoryCheck>(controllerChecks):new List<OfflineDynastySettings.MemoryCheck>();
        }
        internal static void VerifyControllerSelect(int pid)
        {
            lock(Sync) {
                if(controllerStart!=installedStart || controllerChecks==null)return;
                IntPtr h=OpenProcess(0x1010,false,pid);if(h==IntPtr.Zero)throw new InvalidOperationException("Could not verify Controller Select.");
                try{foreach(OfflineDynastySettings.MemoryCheck check in controllerChecks)VerifyBytes(h,check.Address,Convert.FromBase64String(check.Bytes),"Controller Select ownership");}
                finally{CloseHandle(h);}
            }
        }
        private sealed class ControllerChange
        {
            internal readonly long Address;internal readonly byte[] Before,After;
            internal ControllerChange(long address,byte[] before,byte[] after){Address=address;Before=before;After=after;}
        }
        private sealed class ControllerFrozen:IDisposable
        {
            private readonly List<IntPtr> threads=new List<IntPtr>();
            internal ControllerFrozen(Process game,List<ControllerChange> changes)
            {
                try {
                    game.Refresh();
                    foreach(ProcessThread thread in game.Threads) {
                        IntPtr h=ControllerOpenThread(0x4a,false,thread.Id);
                        if(h==IntPtr.Zero){if(Marshal.GetLastWin32Error()==87)continue;throw new InvalidOperationException("Cannot hold game thread for controller installation.");}
                        if(ControllerSuspendThread(h)==UInt32.MaxValue){CloseHandle(h);throw new InvalidOperationException("Controller thread suspension failed.");}
                        threads.Add(h);IntPtr raw=Marshal.AllocHGlobal(1248);
                        try {
                            long aligned=(raw.ToInt64()+15)&~15L;
                            for(int i=0;i<1232;i++)Marshal.WriteByte(new IntPtr(aligned),i,0);
                            Marshal.WriteInt32(new IntPtr(aligned+48),0x100001);
                            if(!ControllerGetThreadContext(h,new IntPtr(aligned)))throw new InvalidOperationException("Controller thread context unavailable.");
                            long rip=Marshal.ReadInt64(new IntPtr(aligned+248));
                            foreach(ControllerChange change in changes)
                                if(change.Address<rip && rip<change.Address+change.Before.Length)throw new InvalidOperationException("Thread inside controller patch instructions; retry while idle.");
                        } finally{Marshal.FreeHGlobal(raw);}
                    }
                } catch{Dispose();throw;}
            }
            public void Dispose(){for(int i=threads.Count-1;i>=0;i--){ControllerResumeThread(threads[i]);CloseHandle(threads[i]);}threads.Clear();}
        }
        [DllImport("kernel32.dll",EntryPoint="OpenThread",SetLastError=true)] private static extern IntPtr ControllerOpenThread(uint access,bool inherit,int id);
        [DllImport("kernel32.dll",EntryPoint="SuspendThread")] private static extern uint ControllerSuspendThread(IntPtr thread);
        [DllImport("kernel32.dll",EntryPoint="ResumeThread")] private static extern uint ControllerResumeThread(IntPtr thread);
        [DllImport("kernel32.dll",EntryPoint="GetThreadContext")] private static extern bool ControllerGetThreadContext(IntPtr thread,IntPtr context);
    }
}
