using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Web.Script.Serialization;

namespace OfflineDynastySettings
{
    // Diagnostics are best effort. A locked report must never disable gameplay or saving.
    internal static class DiagnosticLog
    {
        internal const string Version = "0.2.4";
        private static readonly object sync = new object();
        internal static string PluginFolder { get { return Path.GetDirectoryName(typeof(DiagnosticLog).Assembly.Location); } }
        private static string Reports(string folder) { return Path.Combine(folder,"OfflineDynastySettings","Diagnostics"); }
        internal static void Write(string folder,string component,string message)
        {
            try { lock(sync) {
                string path=SettingsFiles.PathFor(folder,"OfflineDynastySettings.log");
                if(File.Exists(path) && new FileInfo(path).Length>2*1024*1024) {
                    string old=path+".1";if(File.Exists(old))File.Delete(old);File.Move(path,old);
                }
                File.AppendAllText(path,DateTime.Now.ToString("O",CultureInfo.InvariantCulture)+" ["+Version+"]["+component+"][host "+Process.GetCurrentProcess().Id+"/thread "+Thread.CurrentThread.ManagedThreadId+"] "+message+Environment.NewLine);
            } } catch { }
        }
        private static string Digest(byte[] bytes)
        { if(bytes==null)return "unreadable";using(SHA256 sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(); }
        internal static string Mismatch(string component,long rva,string expected,byte[] actual)
        {
            return component+" compatibility mismatch at RVA 0x"+rva.ToString("x")+"; length="+(actual==null?0:actual.Length)
                +" expected region SHA256="+expected+" actual="+Digest(actual)+" first bytes="
                +(actual==null?"unreadable":BitConverter.ToString(actual,0,Math.Min(actual.Length,32)))
                +". No unknown code will be overwritten. This is a code-region check, not a whole-EXE hash requirement.";
        }
        private static void Report(string folder,string name,object value)
        {
            try { lock(sync) {
                string directory=Reports(folder);Directory.CreateDirectory(directory);
                string path=Path.Combine(directory,name),temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                try {
                    File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(value));
                    if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
                } finally { if(File.Exists(temporary))File.Delete(temporary); }
            } } catch { }
        }
        internal static void Started(Process game)
        {
            string folder=PluginFolder;
            SettingsFiles.Migrate(folder);
            Write(folder,"startup","Game PID="+game.Id+" started="+game.StartTime.ToUniversalTime().ToString("O")+". Whole-EXE hash gate: disabled. Targeted code checks and hook ownership remain enabled.");
            try {
                Directory.CreateDirectory(Reports(folder));
                foreach(string old in new string[]{"hooks.json","runtime.json","exit.json"}) {
                    string oldPath=Path.Combine(Reports(folder),old);if(File.Exists(oldPath))File.Delete(oldPath);
                }
                List<object> installed=new List<object>(),loaded=new List<object>();
                foreach(string file in Directory.GetFiles(folder,"*.dll")) {
                    try { FileVersionInfo info=FileVersionInfo.GetVersionInfo(file);
                        installed.Add(new { file=Path.GetFileName(file),version=info.FileVersion,product=info.ProductName });
                    } catch(Exception ex) { installed.Add(new { file=Path.GetFileName(file),error=ex.Message }); }
                }
                foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                    try { if(!assembly.IsDynamic && String.Equals(Path.GetDirectoryName(assembly.Location),folder,StringComparison.OrdinalIgnoreCase))
                        loaded.Add(new { name=assembly.GetName().Name,version=assembly.GetName().Version.ToString() });
                    } catch { }
                }
                ProcessModule module=game.MainModule;FileVersionInfo executable=FileVersionInfo.GetVersionInfo(module.FileName);
                Report(folder,"environment.json",new { plugin=Version,author="Ace",utc=DateTime.UtcNow.ToString("O"),pid=game.Id,
                    gameStartedUtc=game.StartTime.ToUniversalTime().ToString("O"),gameVersion=executable.FileVersion,
                    imageBase="0x"+module.BaseAddress.ToInt64().ToString("x"),imageSize=module.ModuleMemorySize,
                    os=Environment.OSVersion.ToString(),clr=Environment.Version.ToString(),wholeExeHashGate=false,
                    installedPlugins=installed,loadedPluginAssemblies=loaded,
                    savedDefensiveClock=ClockPreferences.Load(folder),offensiveExtendedPreference=File.Exists(SettingsFiles.PathFor(folder,"DynastyPlayClock.ini"))?File.ReadAllText(SettingsFiles.PathFor(folder,"DynastyPlayClock.ini")).Trim():"default",
                    defenseSettingsPresent=File.Exists(Path.Combine(folder,"OfflinePlaycallSettings","dynasties.json")) });
            } catch(Exception ex) { Write(folder,"diagnostics","Environment inventory incomplete: "+ex); }
        }
        internal static void Hooks(PersistenceContext context)
        {
            List<object> checks=new List<object>();
            foreach(MemoryCheck check in context.Checks) {
                byte[] bytes=Convert.FromBase64String(check.Bytes);
                checks.Add(new { address="0x"+check.Address.ToString("x"),length=bytes.Length,sha256=Digest(bytes) });
            }
            Report(context.PluginFolder,"hooks.json",new { plugin=Version,pid=context.Pid,utc=DateTime.UtcNow.ToString("O"),checks=checks,
                defensiveInitializerRva="0x35f7d31",legacyDefensiveTimerHook=false,wholeExeHashGate=false });
        }
        internal static void Runtime(PersistenceContext context,Func<long,int,byte[]> read)
        {
            try {
                byte[] clock=read(context.ClockData+0x828,0xa8),split=read(context.SplitData+0x1800,4);
                object live=null;
                int? mode=null,matchType=null;
                if(context.ImageBase!=0) {
                    try {
                        mode=BitConverter.ToInt32(read(context.ImageBase+0xd9e3818,4),0);
                        matchType=BitConverter.ToInt32(read(context.ImageBase+0xf3f5d48,4),0);
                        long pointer=BitConverter.ToInt64(read(context.ImageBase+0xf8abb50,8),0);
                        if(pointer>0) { byte[] timer=read(pointer+0x34,10);
                            live=new { maximum=BitConverter.ToSingle(timer,0),remaining=BitConverter.ToSingle(timer,4),hidden=timer[8],paused=timer[9] };
                        }
                    } catch { live=new { status="not available during this game transition" }; }
                }
                Report(context.PluginFolder,"runtime.json",new { plugin=Version,pid=context.Pid,utc=DateTime.UtcNow.ToString("O"),status="active; hook ownership verified",mode=mode,matchType=matchType,
                    offensive60Seconds=BitConverter.ToInt32(clock,0)==1,offensiveResetOverrides=BitConverter.ToUInt32(clock,8),
                    offensiveResetsSeen=BitConverter.ToUInt32(clock,0x10),defensiveChoiceSeconds=BitConverter.ToInt32(clock,0x58),
                    defensiveClockEnabled=BitConverter.ToInt32(clock,0x58)>0,defensivePreferenceRevision=BitConverter.ToUInt32(clock,0x5c),defensiveInitializationsSeen=BitConverter.ToUInt32(clock,0x78),
                    defensiveInitializationsApplied=BitConverter.ToUInt32(clock,0x7c),lastAppliedDefensiveSeconds=BitConverter.ToInt32(clock,0x80),
                    defensiveGateReads=BitConverter.ToUInt32(clock,0x98),lastOffensiveClockModeAtDefensiveGate=BitConverter.ToInt32(clock,0x9c),
                    defensiveGateCorrections=BitConverter.ToUInt32(clock,0xa0),defenseSettingsRevision=BitConverter.ToUInt32(split,0),liveDefensiveTimer=live });
            } catch(Exception ex) { Write(context.PluginFolder,"diagnostics","Runtime snapshot unavailable: "+ex.Message); }
        }
        internal static void Ended(string folder,int pid,int? exitCode)
        {
            Report(folder,"exit.json",new { plugin=Version,pid=pid,utc=DateTime.UtcNow.ToString("O"),exitCode=exitCode,
                exitHex=exitCode.HasValue?"0x"+unchecked((uint)exitCode.Value).ToString("x8"):"unavailable",
                note="Exit code and last runtime snapshot are diagnostic clues; this does not capture a native crash stack." });
        }
        internal static void Bundle(string folder)
        {
            try { lock(sync) {
                string destination=Path.Combine(Reports(folder),"OfflineDynastySettings-BugReport.zip");
                Directory.CreateDirectory(Path.GetDirectoryName(destination));string temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
                try {
                    using(ZipArchive zip=ZipFile.Open(temporary,ZipArchiveMode.Create)) {
                        foreach(string name in new string[]{"OfflineDynastySettings.log","OfflineDynastySettings.log.1","OfflineDynastySettings.Persistence.log","DynastyPlayClock.log","OfflinePlaycall.log"})
                            Add(zip,Path.Combine(SettingsFiles.Folder(folder),name),name);
                        foreach(string name in new string[]{"environment.json","hooks.json","runtime.json","exit.json"})Add(zip,Path.Combine(Reports(folder),name),name);
                    }
                    if(File.Exists(destination))File.Replace(temporary,destination,null);else File.Move(temporary,destination);
                } finally { if(File.Exists(temporary))File.Delete(temporary); }
            } } catch { }
        }
        private static void Add(ZipArchive zip,string path,string name)
        {
            if(!File.Exists(path))return;
            try { using(FileStream file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
                using(Stream output=zip.CreateEntry(name).Open())file.CopyTo(output);
            } catch { }
        }
    }
}
