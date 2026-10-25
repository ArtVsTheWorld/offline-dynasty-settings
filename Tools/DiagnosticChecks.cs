using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using OfflineDynastySettings;
class DiagnosticChecks
{
    static int checks;
    static void Check(bool condition,string message) { if(!condition)throw new Exception(message);checks++; }
    static void Put(byte[] bytes,int at,int value) { Array.Copy(BitConverter.GetBytes(value),0,bytes,at,4); }
    static int Main(string[] args)
    {
        try {
            string folder=Path.GetFullPath(args[0]);Directory.CreateDirectory(folder);
            string report=Path.Combine(folder,"OfflineDynastySettings","Diagnostics");
            File.WriteAllText(Path.Combine(folder,"DynastyPlayClock.ini"),"ExtendedSeconds=60\r\n");
            string collision=DiagnosticLog.Mismatch("Test hook",0x35f7d31,"expected",new byte[]{0xff,0x25,0,0,0,0});
            Check(collision.Contains("0x35f7d31") && collision.Contains("FF-25") && collision.Contains("expected") && collision.Contains("actual="),"Conflict report identifies address and changed bytes");
            Check(collision.Contains("not a whole-EXE hash requirement"),"Region check is identified correctly");
            DiagnosticLog.Write(folder,"test","exception example: "+new InvalidOperationException("diagnostic-test"));
            string log=File.ReadAllText(Path.Combine(folder,"OfflineDynastySettings.log"));
            Check(log.Contains("[0.2.1]") && log.Contains("[test]") && log.Contains("System.InvalidOperationException"),"Version, component and full exception recorded");
            using(FileStream locked=new FileStream(Path.Combine(folder,"OfflineDynastySettings.log"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                DiagnosticLog.Write(folder,"test","locked log must not throw");
            Check(true,"Locked log does not interrupt work");
            PersistenceContext context=new PersistenceContext { PluginFolder=folder,Pid=1234,ClockData=0x10000,SplitData=0x20000,
                Checks=new List<MemoryCheck>{new MemoryCheck(0x12345,new byte[]{1,2,3})} };
            DiagnosticLog.Hooks(context);
            string hook=File.ReadAllText(Path.Combine(report,"hooks.json"));
            Check(hook.Contains("0x35f7d31") && hook.Contains("\"legacyDefensiveTimerHook\":false") && hook.Contains("\"length\":3"),"Hook inventory identifies active initializer and owned byte ranges");
            byte[] clock=new byte[0xa8];Put(clock,0,1);Put(clock,8,5);Put(clock,0x58,60);Put(clock,0x78,3);Put(clock,0x7c,3);Put(clock,0x80,60);
            Func<long,int,byte[]> read=(address,size)=>address==context.ClockData+0x828?clock:BitConverter.GetBytes(4);
            DiagnosticLog.Runtime(context,read);
            JavaScriptSerializer serializer=new JavaScriptSerializer();
            Dictionary<string,object> runtime=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(report,"runtime.json")));
            Check((int)runtime["defensiveChoiceSeconds"]==60 && (int)runtime["lastAppliedDefensiveSeconds"]==60,"Saved and effective defensive choice captured separately");
            Check((int)runtime["defensiveInitializationsApplied"]==3 && (int)runtime["offensiveResetOverrides"]==5,"Runtime counters survive into report");
            context.ImageBase=0x140000000;
            read=(address,size)=> {
                if(address==context.ClockData+0x828)return clock;
                if(address==context.SplitData+0x1800)return BitConverter.GetBytes(4);
                if(address==context.ImageBase+0xd9e3818)return BitConverter.GetBytes(14);
                if(address==context.ImageBase+0xf3f5d48)return BitConverter.GetBytes(3);
                if(size==8)return BitConverter.GetBytes((long)0x30000);
                byte[] timer=new byte[10];Array.Copy(BitConverter.GetBytes(60f),0,timer,0,4);Array.Copy(BitConverter.GetBytes(44.5f),0,timer,4,4);timer[9]=1;return timer;
            };
            DiagnosticLog.Runtime(context,read);
            string live=File.ReadAllText(Path.Combine(report,"runtime.json"));
            Check(live.Contains("\"maximum\":60") && live.Contains("\"remaining\":44.5") && live.Contains("\"paused\":1"),"Live native countdown and pause state captured");
            read=(address,size)=> { if(address==context.ClockData+0x828)return clock;if(address==context.SplitData+0x1800)return BitConverter.GetBytes(4);throw new IOException("changing match"); };
            DiagnosticLog.Runtime(context,read);
            Check(File.ReadAllText(Path.Combine(report,"runtime.json")).Contains("not available during this game transition"),"Unavailable native timer preserves usable settings snapshot");
            DiagnosticLog.Ended(folder,1234,unchecked((int)0xc0000005));
            string exit=File.ReadAllText(Path.Combine(report,"exit.json"));
            Check(exit.Contains("0xc0000005") && exit.Contains("does not capture a native crash stack"),"Native exit code recorded without claiming a captured stack");
            Directory.CreateDirectory(Path.Combine(folder,"OfflinePlaycallSettings"));
            File.WriteAllText(Path.Combine(folder,"OfflinePlaycallSettings","dynasties.json"),"PRIVATE SAVE DATA");
            DiagnosticLog.Bundle(folder);
            using(ZipArchive zip=ZipFile.OpenRead(Path.Combine(report,"OfflineDynastySettings-BugReport.zip"))) {
                Check(zip.GetEntry("OfflineDynastySettings.log")!=null && zip.GetEntry("runtime.json")!=null && zip.GetEntry("exit.json")!=null,"Bug-report ZIP contains useful evidence");
                foreach(ZipArchiveEntry entry in zip.Entries)Check(!entry.FullName.Contains(".ini") && !entry.FullName.Contains("dynasties.json") && !entry.FullName.Contains(".dll"),"Bundle excludes configuration files, saves and binaries");
            }
            // Exercise the actual bundled helper's saving method against this
            // process only. No CollegeFB27-named fake process or MMC watcher.
            byte[] helperBytes;
            using(Stream resource=typeof(ClockPreferences).Assembly.GetManifestResourceStream("OfflineDynastySettings.Persistence.exe"))
            using(MemoryStream copy=new MemoryStream()) { resource.CopyTo(copy);helperBytes=copy.ToArray(); }
            Assembly helper=Assembly.Load(helperBytes);
            Type helperType=helper.GetType("OfflineDynastySettings.PersistenceHelper",true),contextType=helper.GetType("OfflineDynastySettings.PersistenceContext",true);
            object helperContext=Activator.CreateInstance(contextType);
            IntPtr localClock=Marshal.AllocHGlobal(0x1000);
            try {
                Marshal.Copy(new byte[0x1000],0,localClock,0x1000);
                contextType.GetProperty("PluginFolder").SetValue(helperContext,folder,null);
                contextType.GetProperty("ClockData").SetValue(helperContext,localClock.ToInt64(),null);
                BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Static;
                helperType.GetField("context",flags).SetValue(null,helperContext);
                MethodInfo save=helperType.GetMethod("SaveDefensiveClock",flags);
                ClockPreferences.Save(folder,60);
                object[] parameters={new IntPtr(-1),-1};
                save.Invoke(null,parameters);
                Check((int)parameters[1]==0 && ClockPreferences.Load(folder)==0,"Bundled helper persists defensive Off instead of ignoring zero");
                Check(File.ReadAllText(Path.Combine(folder,"OfflineDynastySettings.log")).Contains("Saved Defensive Play Clock: Off."),"Helper logs Off clearly");
                Marshal.WriteInt32(localClock,0x880,30);save.Invoke(null,parameters);
                Check((int)parameters[1]==30 && ClockPreferences.Load(folder)==30,"Helper switches from Off back to a timed choice");
                Marshal.WriteInt32(localClock,0x880,0);save.Invoke(null,parameters);
                Check(ClockPreferences.Load(folder)==0,"Helper can persist Off again after a timed choice");
                Check(File.ReadAllText(Path.Combine(folder,"DynastyPlayClock.ini"))=="ExtendedSeconds=60\r\n","Defensive Off never resets offensive clock preference");
            } finally { Marshal.FreeHGlobal(localClock); }
            Console.WriteLine("PASS "+checks+" logging, timer snapshot, file-lock, exit-code and bug-report ZIP checks");return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex);return 1; }
    }
}
