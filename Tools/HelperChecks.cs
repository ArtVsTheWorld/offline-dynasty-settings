// Compiled as LaunchChecks (friend assembly). Parent/child tests use fake memory
// in a disposable CollegeFB27-named test process, never the real running game.
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using OfflineDynastySettings;
class HelperChecks
{
    static string host,dll,folder;
    static int checks;
    static JavaScriptSerializer json=new JavaScriptSerializer();
    static string Quote(string path) { return "\""+path+"\""; }
    static void Check(bool value,string message) { if(!value)throw new Exception(message);checks++; }
    static void Until(Func<bool> predicate,string message,int timeout=6000)
    { Stopwatch timer=Stopwatch.StartNew();while(timer.ElapsedMilliseconds<timeout) { if(predicate()){Check(true,message);return;}Thread.Sleep(50); }throw new Exception(message); }
    static int Main(string[] args)
    {
        try {
            host=Path.GetFullPath(args[1]);dll=Path.GetFullPath(args[2]);folder=Path.GetFullPath(args[3]);
            AppDomain.CurrentDomain.AssemblyResolve+=(sender,request)=> {
                string name=new AssemblyName(request.Name).Name+".dll";
                foreach(string dir in new string[]{folder,Path.GetDirectoryName(dll),host}) {
                    string path=Path.Combine(dir,name);if(File.Exists(path))return Assembly.LoadFrom(path);
                }return null;
            };
            if(args[0]=="fake")return Fake();
            if(args[0]=="launcher" || args[0]=="launcher-crash")return Launch(args[4],args[0]=="launcher-crash");
            Run();Console.WriteLine("PASS "+checks+" detached helper, real launcher-exit, file-lock, ownership and game-exit checks");return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex);return 1; }
    }
    static Process Start(string exe,string arguments,bool pipes)
    { return Process.Start(new ProcessStartInfo(exe,arguments){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardInput=pipes,RedirectStandardOutput=pipes,RedirectStandardError=pipes}); }
    static string ModeArgs(string mode,string extra="") { return mode+" "+Quote(host)+" "+Quote(dll)+" "+Quote(folder)+" "+extra; }
    [MethodImpl(MethodImplOptions.NoInlining)] static int Fake()
    {
        IntPtr clock=Marshal.AllocHGlobal(0x1000),split=Marshal.AllocHGlobal(0x5000),signature=Marshal.AllocHGlobal(16);
        try {
            Marshal.Copy(new byte[0x1000],0,clock,0x1000);Marshal.WriteInt32(clock,0x880,15);Marshal.Copy(new byte[0x5000],0,split,0x5000);
            byte[] owned=new byte[16];for(int i=0;i<owned.Length;i++)owned[i]=0x90;Marshal.Copy(owned,0,signature,16);
            int table=0x2000;Marshal.WriteInt32(split,table,101);Marshal.WriteInt32(split,table+4,3);Marshal.WriteInt32(split,table+8,5);
            Marshal.WriteInt32(split,table+12,202);Marshal.WriteInt32(split,table+16,-1);Marshal.WriteInt32(split,table+20,2);
            PersistenceContext context=new PersistenceContext { Format=1,Pid=Process.GetCurrentProcess().Id,Started=Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks,ClockData=clock.ToInt64(),SplitData=split.ToInt64(),PluginFolder=Path.Combine(folder,"saved"),Checks=new System.Collections.Generic.List<MemoryCheck>{new MemoryCheck(signature.ToInt64(),owned)} };
            Console.WriteLine(json.Serialize(context));Console.Out.Flush();
            string command;while((command=Console.ReadLine())!=null) {
                if(command=="quit")break;
                if(command=="set") { Marshal.WriteInt32(clock,0x880,30);Marshal.WriteInt32(clock,0x828,1);Marshal.WriteInt32(split,0x1800,1); }
                if(command=="change") { Marshal.WriteInt32(clock,0x880,20);Marshal.WriteInt32(clock,0x828,0);Marshal.WriteInt32(split,table+4,4);Marshal.WriteInt32(split,table+8,0);Marshal.WriteInt32(split,0x1800,2); }
                if(command=="third") { Marshal.WriteInt32(clock,0x880,45);Marshal.WriteInt32(clock,0x828,1);Marshal.WriteInt32(split,table+4,2);Marshal.WriteInt32(split,table+8,5);Marshal.WriteInt32(split,0x1800,3); }
                if(command=="invalid") { Marshal.WriteInt32(split,table+4,999);Marshal.WriteInt32(split,0x1800,4); }
                if(command=="repair") { Marshal.WriteInt32(split,table+4,3);Marshal.WriteInt32(split,0x1800,4); }
                if(command=="foreign")Marshal.WriteByte(signature,0xcc);
                if(command=="normal")Marshal.WriteByte(signature,0x90);
            }
            return 0;
        }finally{Marshal.FreeHGlobal(clock);Marshal.FreeHGlobal(split);Marshal.FreeHGlobal(signature);}
    }
    [MethodImpl(MethodImplOptions.NoInlining)] static int Launch(string descriptor,bool crash)
    {
        PersistenceContext context=json.Deserialize<PersistenceContext>(File.ReadAllText(descriptor));
        using(PersistenceController controller=new PersistenceController(context,s=>Console.WriteLine(s))) {
            Stopwatch timer=Stopwatch.StartNew();bool ready=false;
            while(timer.ElapsedMilliseconds<8000) {
                try { controller.EnsureReady();ready=true;break; }catch(InvalidOperationException){Thread.Sleep(100);}
            }
            if(!ready)throw new Exception("Helper readiness timeout");
            if(crash) {
                int previous=controller.HelperPid;
                using(Process ownHelper=Process.GetProcessById(previous)) { ownHelper.Kill();ownHelper.WaitForExit(); }
                timer.Restart();ready=false;
                while(timer.ElapsedMilliseconds<8000) {
                    try { controller.EnsureReady();ready=true;break; }catch(InvalidOperationException){Thread.Sleep(100);}
                }
                if(!ready || controller.HelperPid==previous)throw new Exception("Crashed helper was not restarted");
                Console.WriteLine("RESTARTED");
            }
            File.WriteAllText(Path.Combine(folder,"helper-pid.txt"),controller.HelperPid.ToString());
        } // Release the launcher's handles, then exit: helper must survive.
        return 0;
    }
    static int Defense(string path,string map,string key)
    { var document=json.DeserializeObject(File.ReadAllText(path)) as System.Collections.Generic.Dictionary<string,object>;var entries=document[map] as System.Collections.Generic.Dictionary<string,object>;return Convert.ToInt32(entries[key]); }
    static void Send(Process fake,string command) { fake.StandardInput.WriteLine(command);fake.StandardInput.Flush(); }
    static Process LaunchHelper(PersistenceContext context,bool crash=false)
    {
        string descriptor=Path.Combine(folder,"launch-context.json");File.WriteAllText(descriptor,json.Serialize(context));
        using(Process launcher=Start(Assembly.GetExecutingAssembly().Location,ModeArgs(crash?"launcher-crash":"launcher",Quote(descriptor)),true)) {
            if(!launcher.WaitForExit(10000))throw new Exception("Launcher did not finish");
            if(launcher.ExitCode!=0)throw new Exception("Launcher failed: "+launcher.StandardError.ReadToEnd()+launcher.StandardOutput.ReadToEnd());
            Check(true,"Launcher completed");
            if(crash)Check(launcher.StandardOutput.ReadToEnd().Contains("RESTARTED"),"Actual controller did not restart its crashed helper");
        }
        Process helper=Process.GetProcessById(int.Parse(File.ReadAllText(Path.Combine(folder,"helper-pid.txt"))));
        Check(helper.Handle!=IntPtr.Zero,"Could not retain helper handle for exit verification");
        Check(!helper.HasExited,"Helper died with its launcher");return helper;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] static void Run()
    {
        Directory.CreateDirectory(folder);string fakeExe=Path.Combine(folder,"CollegeFB27.exe");
        File.Copy(Assembly.GetExecutingAssembly().Location,fakeExe,true);
        string saved=Path.Combine(folder,"saved"),ini=Path.Combine(saved,"OfflineDynastySettings","DynastyPlayClock.ini"),storage=Path.Combine(saved,"OfflinePlaycallSettings","dynasties.json");
        Directory.CreateDirectory(Path.GetDirectoryName(storage));Directory.CreateDirectory(Path.GetDirectoryName(ini));File.WriteAllText(ini,"ExtendedSeconds=0\r\n");
        File.WriteAllText(storage,"{\"FormatVersion\":2,\"DefenseCooldowns\":{\"101\":3},\"DefenseLimits\":{\"101\":5,\"202\":2}}");
        string helperExe=PersistenceController.ExtractHelper();Check(File.Exists(helperExe),"Bundled helper not extracted");
        Check(helperExe==PersistenceController.ExtractHelper(),"Verified helper not reused");
        Process fake=null,helper=null;
        try {
            fake=Start(fakeExe,ModeArgs("fake"),true);PersistenceContext context=json.Deserialize<PersistenceContext>(fake.StandardOutput.ReadLine());
            helper=LaunchHelper(context);Send(fake,"set");
            Until(()=>File.ReadAllText(ini).Trim()=="ExtendedSeconds=60","60-second preference not saved after launcher exited");
            Until(()=>File.Exists(Path.Combine(saved,"OfflineDynastySettings","OfflineDynastySettings.ini")) && File.ReadAllText(Path.Combine(saved,"OfflineDynastySettings","OfflineDynastySettings.ini")).Trim()=="DefensivePlayClockSeconds=30","New defensive clock choice not saved after MMC exit");
            Until(()=>Defense(storage,"DefenseLimits","202")==2,"Inherited/limit-only dynasty not saved");
            using(FileStream locked=new FileStream(ini,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) {
                Send(fake,"change");Until(()=>Defense(storage,"DefenseCooldowns","101")==4 && Defense(storage,"DefenseLimits","101")==0,"INI lock prevented independent defense saving");
            }
            Until(()=>File.ReadAllText(ini).Trim()=="ExtendedSeconds=0","INI save did not recover from lock");
            Until(()=>File.ReadAllText(Path.Combine(saved,"OfflineDynastySettings","OfflineDynastySettings.ini")).Trim()=="DefensivePlayClockSeconds=20","Defensive clock did not save independently of offensive file lock");
            using(FileStream locked=new FileStream(storage,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) {
                Send(fake,"third");Until(()=>File.ReadAllText(ini).Trim()=="ExtendedSeconds=60","Defense lock prevented independent preference saving");
            }
            Until(()=>Defense(storage,"DefenseCooldowns","101")==2,"Defense save did not recover from lock");
            Until(()=>File.ReadAllText(Path.Combine(saved,"OfflineDynastySettings","OfflineDynastySettings.ini")).Trim()=="DefensivePlayClockSeconds=45","Defensive clock did not save independently of defense file lock");
            Check(File.Exists(storage+".bak"),"Atomic defense backup missing");
            string preserved=File.ReadAllText(storage);Send(fake,"invalid");Thread.Sleep(350);
            Check(File.ReadAllText(storage)==preserved,"Invalid remote configuration destroyed saved settings");
            Send(fake,"repair");Until(()=>Defense(storage,"DefenseCooldowns","101")==3,"Invalid-data retry never recovered");
            context.ReadyEvent="Local\\OfflineDynastySettingsNoOwner_"+Guid.NewGuid().ToString("N");
            string direct=Path.Combine(folder,"direct.json");File.WriteAllText(direct,json.Serialize(context));
            using(Process duplicate=Start(helperExe,Quote(direct),false)) { Check(duplicate.WaitForExit(3000)&&duplicate.ExitCode==4,"Duplicate helper did not respect game mutex"); }
            context.Started++;File.WriteAllText(direct,json.Serialize(context));
            using(Process wrong=Start(helperExe,Quote(direct),false)) { Check(wrong.WaitForExit(3000)&&wrong.ExitCode==3,"Wrong process instance accepted"); }
            context.Started--;Send(fake,"foreign");Until(()=>helper.HasExited,"Changed ownership did not stop helper",7500);
            Check(helper.ExitCode==5,"Changed-code stop reason wrong");preserved=File.ReadAllText(storage);Send(fake,"change");Thread.Sleep(350);
            Check(File.ReadAllText(storage)==preserved,"Stopped helper continued saving foreign state");
            helper.Dispose();helper=null;Send(fake,"normal");helper=LaunchHelper(context,true);
            Send(fake,"quit");Check(fake.WaitForExit(3000),"Fake game did not exit");Until(()=>helper.HasExited,"Helper remained after game exit",3500);
            Check(helper.ExitCode==0,"Normal game exit reported error");helper.Dispose();helper=null;fake.Dispose();fake=null;
            fake=Start(fakeExe,ModeArgs("fake"),true);context=json.Deserialize<PersistenceContext>(fake.StandardOutput.ReadLine());
            context.ReadyEvent="Local\\OfflineDynastySettingsClosedBeforeAck_"+Guid.NewGuid().ToString("N");File.WriteAllText(direct,json.Serialize(context));
            string logPath=Path.Combine(saved,"OfflineDynastySettings","OfflineDynastySettings.Persistence.log");int logStart=File.ReadAllText(logPath).Length;
            helper=Start(helperExe,Quote(direct),false);
            Until(()=>File.ReadAllText(logPath).Substring(logStart).Contains("MMC already closed before acknowledgement"),"Early-close helper never became ready");
            Send(fake,"set");Send(fake,"change");
            Until(()=>File.ReadAllText(ini).Trim()=="ExtendedSeconds=0" && Defense(storage,"DefenseCooldowns","101")==4,"Closing launcher before acknowledgement prevented saving");
            Send(fake,"quit");Check(fake.WaitForExit(3000),"Second fake game did not exit");Until(()=>helper.HasExited,"Early-close helper remained after game exit",3500);
            Check(helper.ExitCode==0,"Early-close helper failed");
        } finally {
            if(fake!=null) { if(!fake.HasExited){Send(fake,"quit");if(!fake.WaitForExit(3000))fake.Kill();}fake.Dispose(); }
            if(helper!=null) { if(!helper.HasExited && !helper.WaitForExit(3000))helper.Kill();helper.Dispose(); }
        }
    }
}
