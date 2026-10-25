// Standalone read-only settings monitor. No MMC/Frosty dependency, no game patches.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;

namespace OfflineDynastySettings
{
    internal static class PersistenceHelper
    {
        private static PersistenceContext context;
        private static string lastLog;
        private static DateTime loggedAt;
        private static readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private static void Log(string message)
        {
            try {
                if (message == lastLog && DateTime.UtcNow - loggedAt < TimeSpan.FromMinutes(1)) return;
                lastLog = message; loggedAt = DateTime.UtcNow;
                DiagnosticLog.Write(context.PluginFolder,"helper",message);
                string path=Path.Combine(context.PluginFolder,"OfflineDynastySettings.Persistence.log");
                if(File.Exists(path) && new FileInfo(path).Length>2*1024*1024)File.WriteAllText(path,"");
                File.AppendAllText(path,DateTime.Now.ToString("O")+" "+message+Environment.NewLine);
            } catch { }
        }
        private static int Main(string[] args)
        {
            IntPtr handle = IntPtr.Zero;
            try {
                if (args.Length != 1) return 2;
                context = serializer.Deserialize<PersistenceContext>(File.ReadAllText(args[0]));
                if (context == null || context.Format != 1 || context.Pid <= 0 || context.Started <= 0
                    || context.ClockData <= 0 || context.SplitData <= 0 || context.Checks == null
                    || context.Checks.Count == 0 || context.Checks.Count > 32 || !Directory.Exists(context.PluginFolder)
                    || String.IsNullOrEmpty(context.ReadyEvent)) return 2;
                using (Process game = Process.GetProcessById(context.Pid)) {
                    if (game.HasExited || game.ProcessName != "CollegeFB27" || game.StartTime.ToUniversalTime().Ticks != context.Started) return 3;
                    using (Mutex mutex = new Mutex(false, "Local\\OfflineDynastySettingsPersistence_" + context.Pid + "_" + context.Started)) {
                        bool owned;
                        try { owned = mutex.WaitOne(0); } catch (AbandonedMutexException) { owned = true; }
                        if (!owned) return 4;
                        try {
                            handle = OpenProcess(0x1010, false, context.Pid);
                            if (handle == IntPtr.Zero) throw new IOException("Could not open game for read-only settings monitoring.");
                            List<byte[]> expected = new List<byte[]>();
                            foreach (MemoryCheck check in context.Checks) {
                                byte[] bytes = Convert.FromBase64String(check.Bytes);
                                if (check.Address <= 0 || bytes.Length == 0 || bytes.Length > 8192) throw new InvalidDataException("Invalid ownership check.");
                                expected.Add(bytes);
                            }
                            DateTime nextVerify = DateTime.MinValue;
                            bool ready = false, preferenceKnown = false, savedPreference = false;
                            uint savedSequence = 0;int savedDefensiveClock=-1;
                            Log("Read-only settings helper started for PID " + context.Pid + ". It will exit with this game.");
                            while (!game.HasExited) {
                                try {
                                    if (DateTime.UtcNow >= nextVerify) {
                                        for (int i=0;i<expected.Count;i++) {
                                            byte[] current = Read(handle, context.Checks[i].Address, expected[i].Length);
                                            if (!Equal(current, expected[i])) {
                                                Log("STOPPED: "+DiagnosticLog.Mismatch("Installed hook "+i,context.Checks[i].Address-context.ImageBase,Hash(expected[i]),current)+" Saved settings preserved. Restart the game."); return 5;
                                            }
                                        }
                                        DiagnosticLog.Runtime(context,(address,size)=>Read(handle,address,size));
                                        nextVerify = DateTime.UtcNow.AddSeconds(5);
                                    }
                                    // Do not silently replace an unreadable existing preference.
                                    if (!preferenceKnown) {
                                        string ini = Path.Combine(context.PluginFolder, "DynastyPlayClock.ini");
                                        savedPreference = File.Exists(ini) && File.ReadAllText(ini).Trim() == "ExtendedSeconds=60";
                                        preferenceKnown = true;
                                    }
                                    Read(handle, context.ClockData + 0x828, 4);
                                    Read(handle, context.SplitData + 0x1800, 4);
                                    if (!ready) {
                                        try { using (EventWaitHandle signal = EventWaitHandle.OpenExisting(context.ReadyEvent)) signal.Set(); }
                                        catch (WaitHandleCannotBeOpenedException) { Log("MMC already closed before acknowledgement; settings saving will continue."); }
                                        ready = true;
                                        Log("READY: MMC may close; the helper will keep saving settings until the game exits.");
                                    }
                                    Exception error = null;
                                    try { SavePreference(handle, ref savedPreference); } catch (Exception ex) { error = ex; }
                                    try { SaveDefensiveClock(handle, ref savedDefensiveClock); } catch (Exception ex) { if (error == null) error = ex; }
                                    try { SaveDefense(handle, ref savedSequence); } catch (Exception ex) { if (error == null) error = ex; }
                                    if (error != null) throw error;
                                    Thread.Sleep(100);
                                } catch (Exception ex) {
                                    if (game.HasExited) break;
                                    Log("Will retry saving: " + ex.ToString()); Thread.Sleep(1000);
                                }
                                game.Refresh();
                            }
                            int? exitCode=null;try { exitCode=game.ExitCode; } catch { }
                            DiagnosticLog.Ended(context.PluginFolder,context.Pid,exitCode);
                            Log("Game exited; settings helper is shutting down. Exit="+(exitCode.HasValue?"0x"+unchecked((uint)exitCode.Value).ToString("x8"):"unavailable"));
                            return 0;
                        } finally { mutex.ReleaseMutex(); }
                    }
                }
            } catch (Exception ex) { if (context != null) Log("Helper failed: " + ex.ToString()); return 1; }
            finally {
                if(context!=null && !String.IsNullOrEmpty(context.PluginFolder))DiagnosticLog.Bundle(context.PluginFolder);
                if (handle != IntPtr.Zero) CloseHandle(handle);
                if (args.Length == 1) try { File.Delete(args[0]); } catch { }
            }
        }
        private static void SavePreference(IntPtr handle, ref bool saved)
        {
            bool enabled = BitConverter.ToInt32(Read(handle, context.ClockData + 0x828, 4),0) == 1;
            if (enabled == saved) return;
            AtomicSave(Path.Combine(context.PluginFolder,"DynastyPlayClock.ini"),enabled ? "ExtendedSeconds=60\r\n" : "ExtendedSeconds=0\r\n",false);
            saved = enabled;Log("Saved Offensive Play Clock extended choice: "+(enabled?"60 Seconds":"native clock choice")+".");
        }
        private static void SaveDefensiveClock(IntPtr handle,ref int saved)
        {
            int seconds=BitConverter.ToInt32(Read(handle,context.ClockData+0x880,4),0);
            if(!ClockPreferences.Valid(seconds))throw new InvalidDataException("Invalid remote defensive clock; saved preference preserved.");
            if(saved<0)saved=ClockPreferences.Load(context.PluginFolder);
            if(saved==seconds)return;
            ClockPreferences.Save(context.PluginFolder,seconds);saved=seconds;
            Log("Saved Defensive Play Clock: "+(seconds==0?"Off":seconds+" seconds")+".");
        }
        private sealed class DefenseDocument
        {
            public int FormatVersion { get; set; }
            public Dictionary<string,int> DefenseCooldowns { get; set; }
            public Dictionary<string,int> DefenseLimits { get; set; }
            internal DefenseDocument() { FormatVersion = 2; DefenseCooldowns=new Dictionary<string,int>(); DefenseLimits=new Dictionary<string,int>(); }
        }
        private static void SaveDefense(IntPtr handle, ref uint saved)
        {
            uint current = BitConverter.ToUInt32(Read(handle,context.SplitData+0x1800,4),0);
            if (current == saved) return;
            byte[] table = Read(handle,context.SplitData+0x2000,1024*12);
            if (BitConverter.ToUInt32(Read(handle,context.SplitData+0x1800,4),0) != current) return;
            DefenseDocument updated = new DefenseDocument(); HashSet<uint> seen=new HashSet<uint>();
            for(int i=0;i<1024;i++) {
                uint id=BitConverter.ToUInt32(table,i*12); if(id==0)continue;
                int cooldown=BitConverter.ToInt32(table,i*12+4), limit=BitConverter.ToInt32(table,i*12+8);
                if(cooldown < -1 || cooldown > 10 || limit < -1 || limit > 20 || (cooldown == -1 && limit == -1) || !seen.Add(id))
                    throw new InvalidDataException("Invalid remote defense configuration; previous saved settings preserved.");
                string key=id.ToString(CultureInfo.InvariantCulture);
                if(cooldown>=0)updated.DefenseCooldowns.Add(key,cooldown);
                if(limit>=0)updated.DefenseLimits.Add(key,limit);
            }
            AtomicSave(Path.Combine(context.PluginFolder,"OfflinePlaycallSettings","dynasties.json"),serializer.Serialize(updated),true);
            saved=current; Log("Saved per-dynasty defensive settings; revision " + current + ", dynasties="+seen.Count+".");
        }
        private static void AtomicSave(string path, string contents, bool backup)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)); string temporary=path+".helper.tmp";
            using(FileStream file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
            using(StreamWriter writer=new StreamWriter(file)) { writer.Write(contents); writer.Flush(); file.Flush(true); }
            if(File.Exists(path))File.Replace(temporary,path,backup?path+".bak":null); else File.Move(temporary,path);
        }
        private static byte[] Read(IntPtr handle,long address,int size)
        {
            byte[] bytes=new byte[size]; IntPtr count;
            if(!ReadProcessMemory(handle,new IntPtr(address),bytes,new IntPtr(size),out count) || count.ToInt64()!=size)
                throw new IOException("Game settings memory is temporarily unreadable.");
            return bytes;
        }
        private static string Hash(byte[] bytes) { using(System.Security.Cryptography.SHA256 sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(); }
        private static bool Equal(byte[] a,byte[] b) { if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true; }
        [DllImport("kernel32.dll",SetLastError=true)] private static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
        [DllImport("kernel32.dll",SetLastError=true)] private static extern bool ReadProcessMemory(IntPtr handle,IntPtr address,byte[] bytes,IntPtr size,out IntPtr count);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    }
}
