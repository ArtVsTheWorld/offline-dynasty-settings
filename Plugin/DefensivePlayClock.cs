using System;
using System.Diagnostics;
using System.Collections.Generic;
namespace DynastyPlayClock
{
    public sealed partial class DynastyPlayClockAction
    {
        private const long DefensiveTimerRva=0x35f7d31;
        private static readonly byte[] DefensiveTimerOriginal=Hex("e86aa99f02c4c17a114538c4c17a114534");
        private const long DefensiveGateRva=0x35fd7d3;
        private static readonly byte[] DefensiveGateOriginal=Hex("e888856f02c5c857f6c4c1782f7638440fb7e0");
        private static long defensiveCode,defensiveStart;
        private static void ValidateCombinedDefensiveReadSite(IntPtr handle,long image)
        {
            byte[] current=Read(handle,image+DefensiveGateRva,DefensiveGateOriginal.Length);
            if(!Equal(current,DefensiveGateOriginal))throw new InvalidOperationException(OfflineDynastySettings.DiagnosticLog.Mismatch("Defensive countdown enable check",DefensiveGateRva,Hash(DefensiveGateOriginal),current));
        }
        private static void AppendCombinedDefensiveEnumOverride(Code c,long b,long d)
        {
            // Called only after the normal enum hook has validated the current
            // offline Dynasty cache and obtained a valid offensive clock choice.
            // The vanilla defensive UI/tick gate shares that offensive choice.
            // Return the defensive enable choice for this ONE caller; all other reads retain the offensive choice.
            c.MovR10(b+0x35fd7d8);c.Hex("4c399424c8000000");c.Branch("0f85","defensiveGateDone");
            c.MovR10(b+0xf8abb50);c.Hex("4d3932");c.Branch("0f85","defensiveGateDone");
            c.MovR10(d);c.Hex("8b442420418982c408000041ff82c0080000");
            c.Hex("4183ba8008000000");c.Branch("0f84","defensiveGateOff");
            foreach(int seconds in new int[]{5,15,20,30,45,60}) {
                c.Hex("4183ba80080000");c.Bytes(new byte[]{(byte)seconds});c.Branch("0f84","defensiveGateOn");
            }
            c.Branch("e9","defensiveGateDone"); // Invalid configuration: preserve native behavior.
            c.Label("defensiveGateOff");c.Hex("85c0");c.Branch("0f84","defensiveGateDone");
            c.Hex("33c0");c.Branch("e9","defensiveGateChanged");
            c.Label("defensiveGateOn");c.Hex("83f801");c.Branch("0f84","defensiveGateDone");c.Hex("b801000000");
            c.Label("defensiveGateChanged");c.Hex("41ff82c808000089442420");
            c.Label("defensiveGateDone");
        }
        private static byte[] defensiveDetour,defensiveBody;
        internal static void InstallCombinedDefensiveClock(int pid)
        {
            lock(Sync) {
                if(installedPid!=pid || dataAddress==0)throw new InvalidOperationException("Waiting for the offensive clock bridge.");
                if(defensiveCode!=0 && defensiveStart==installedStart)return;
                IntPtr h=OpenProcess(0x1038,false,pid);if(h==IntPtr.Zero)throw new InvalidOperationException("Could not open defensive timer installation.");
                try {
                    ValidateCombinedDefensiveReadSite(h,installedBase);
                    byte[] native=Read(h,installedBase+DefensiveTimerRva,DefensiveTimerOriginal.Length);
                    if(native==null)throw new InvalidOperationException("Defensive timer code is not ready.");
                    if(!Equal(native,DefensiveTimerOriginal))
                        throw new InvalidOperationException(OfflineDynastySettings.DiagnosticLog.Mismatch("Defensive selection initializer",DefensiveTimerRva,Hash(DefensiveTimerOriginal),native));
                    if(!Equal(Read(h,installedBase+0x5ff26a0,9),Hex("c5fa10052435db04c3")))
                        throw new InvalidOperationException("Defensive clock getter changed at RVA 0x5ff26a0; no timer hook installed.");
                    byte[] body=BuildCombinedDefensiveTimer(installedBase,dataAddress);
                    IntPtr allocation=VirtualAllocEx(h,IntPtr.Zero,new UIntPtr(4096),0x3000,4);
                    if(allocation==IntPtr.Zero)throw new InvalidOperationException("Defensive timer allocation failed.");
                    long address=allocation.ToInt64();Write(h,address,body);
                    if(!Equal(Read(h,address,body.Length),body))throw new InvalidOperationException("Defensive timer body verification failed.");
                    uint previous;if(!VirtualProtectEx(h,allocation,new UIntPtr(4096),0x20,out previous))throw new InvalidOperationException("Defensive timer protection failed.");
                    FlushInstructionCache(h,allocation,new UIntPtr(4096));
                    byte[] detour=Pad(Jump(address),DefensiveTimerOriginal.Length);
                    Patch(h,installedBase+DefensiveTimerRva,detour);
                    defensiveCode=address;defensiveBody=body;defensiveDetour=detour;defensiveStart=installedStart;
                    Log(null,"Defensive Play Clock active: active selection initializer at RVA 0x35f7d31; native default 15, selectable 5/15/20/30/45/60 seconds or Off; independent defensive enable gate.");
                } finally { CloseHandle(h); }
            }
        }
        internal static void VerifyCombinedDefensiveClock(int pid)
        {
            lock(Sync) {
                if(installedPid!=pid || defensiveCode==0 || defensiveStart!=installedStart)throw new InvalidOperationException("Defensive timer is not installed.");
                IntPtr h=OpenProcess(0x1010,false,pid);if(h==IntPtr.Zero)throw new InvalidOperationException("Could not verify defensive timer.");
                try {
                    ValidateCombinedDefensiveReadSite(h,installedBase);
                    if(!Equal(Read(h,installedBase+DefensiveTimerRva,defensiveDetour.Length),defensiveDetour) || !Equal(Read(h,defensiveCode,defensiveBody.Length),defensiveBody) || !Equal(Read(h,installedBase+0x5ff26a0,9),Hex("c5fa10052435db04c3")))
                        throw new OfflineDynastySettings.HookOwnershipException("Defensive timer hook changed; restart the game.");
                } finally { CloseHandle(h); }
            }
        }
        internal static byte[] BuildCombinedDefensiveTimer(long b,long d)
        {
            Code c=new Code();
            // This call site initializes BOTH the displayed duration and remaining time.
            // Keep the separate native 10-second special-case path unchanged.
            c.Hex("9c5041524153"); // Preserve flags and every temporary register.
            c.MovR10(b+0xada5bcc);c.Hex("c4c17a1002"); // Replay native getter: XMM0 = 15.
            c.MovR10(d);c.Hex("41ff82a0080000");
            c.MovR10(b+0xf8abb50);c.Hex("4d392a");c.Branch("0f85","done");
            c.MovR10(b+0xd9e3818);c.Hex("41833a0e");c.Branch("0f85","done");
            c.MovR10(b+0xf3f5d48);c.Hex("41833a03");c.Branch("0f85","done");
            c.MovR10(d);c.Hex("418b828008000085c0");c.Branch("0f84","recordChoice");
            foreach(int seconds in new int[]{5,15,20,30,45,60}) { c.Hex("83f8");c.Bytes(new byte[]{(byte)seconds});c.Branch("0f84","valid"); }
            c.Branch("e9","done");
            c.Label("valid");c.Hex("c5fa2ac0");
            // Off keeps a positive native duration. Its separate enable gate
            // skips countdown and hides the UI; a zero duration would expire immediately.
            c.Label("recordChoice");c.Hex("41ff82a4080000418982a80800004d89aab0080000");
            c.Label("done");c.Hex("415b415a589d");
            c.Hex("c4c17a114538c4c17a114534"); // The native stores for remaining and maximum.
            c.Bytes(Jump(b+DefensiveTimerRva+DefensiveTimerOriginal.Length));
            return c.Finish();
        }
    }
}
