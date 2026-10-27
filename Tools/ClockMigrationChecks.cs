using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
class ClockMigrationChecks
{
    static int checks;
    static void Check(bool condition,string label){if(!condition)throw new Exception(label);checks++;}
    static int Main(string[] args)
    {
        try {
            string host=Path.GetFullPath(args[0]),folder=Path.GetFullPath(args[2]);Directory.CreateDirectory(folder);
            AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=> {string path=Path.Combine(host,new AssemblyName(e.Name).Name+".dll");return File.Exists(path)?Assembly.LoadFrom(path):null;};
            string file=Path.Combine(folder,"OfflineDynastySettings.dll");File.Copy(Path.GetFullPath(args[1]),file,true);
            string oldIni=Path.Combine(folder,"DynastyPlayClock.ini"),newIni=Path.Combine(folder,"OfflineDynastySettings","OfflineDynastySettings.ini"),json=Path.Combine(folder,"dynasties.json");
            string currentIni=Path.Combine(folder,"OfflineDynastySettings","DynastyPlayClock.ini");
            if(File.Exists(currentIni))File.Delete(currentIni); // This test fixture only.
            File.WriteAllText(oldIni,"ExtendedSeconds=60\r\n");
            const string oldJson="{\"FormatVersion\":2,\"DefenseCooldowns\":{\"101\":3,\"202\":0},\"DefenseLimits\":{\"101\":2,\"303\":9}}";File.WriteAllText(json,oldJson);
            if(File.Exists(newIni))File.Delete(newIni); // Fixed, verified fixture folder only.
            Assembly dll=Assembly.LoadFrom(file);BindingFlags f=BindingFlags.NonPublic|BindingFlags.Static;
            Type pref=dll.GetType("OfflineDynastySettings.ClockPreferences"),clock=dll.GetType("DynastyPlayClock.DynastyPlayClockAction"),split=dll.GetType("OfflinePlaycall.SplitSettingsAction");
            Func<int> load=()=> (int)pref.GetMethod("Load",f).Invoke(null,new object[]{folder});
            Check(load()==15 && !File.Exists(newIni),"Old installs default new clock to 15 without writing a preference");
            Check((bool)clock.GetMethod("LoadExtended",f).Invoke(null,null),"Existing 60-second offensive clock choice loads");
            Check(!File.Exists(oldIni) && File.ReadAllText(currentIni)=="ExtendedSeconds=60\r\n","Old offensive INI moves into plugin folder without changing values");
            object document=split.GetMethod("Load",f).Invoke(null,new object[]{json});
            var cds=(Dictionary<string,int>)document.GetType().GetProperty("DefenseCooldowns").GetValue(document,null);
            var limits=(Dictionary<string,int>)document.GetType().GetProperty("DefenseLimits").GetValue(document,null);
            Check(cds["101"]==3 && cds["202"]==0 && limits["101"]==2 && limits["303"]==9,"Existing per-Dynasty values and inherited entries preserved");
            foreach(int value in new[]{0,3,5,15,20,30,45,60}) {
                pref.GetMethod("Save",f).Invoke(null,new object[]{folder,value});Check(load()==value,"New clock saves/reloads "+value);
                Check(File.ReadAllText(currentIni)=="ExtendedSeconds=60\r\n" && File.ReadAllText(json)==oldJson,"New choice does not alter old settings");
            }
            byte[] data=(byte[])clock.GetMethod("BuildData",f).Invoke(null,new object[]{0x200000000L});
            Check(BitConverter.ToInt32(data,0x880)==60 && BitConverter.ToInt32(data,0x95c)==60,"Menu descriptor reloads saved new clock");
            File.WriteAllText(newIni,"DefensivePlayClockSeconds=5\r\n");
            data=(byte[])clock.GetMethod("BuildData",f).Invoke(null,new object[]{0x200000000L});
            Check(load()==5 && BitConverter.ToInt32(data,0x880)==5 && BitConverter.ToInt32(data,0x95c)==5,"Saved 5-second choice stays 5 Seconds");
            Check(File.ReadAllText(newIni)=="DefensivePlayClockSeconds=5\r\n","Loading old choice leaves its original file intact");
            pref.GetMethod("Save",f).Invoke(null,new object[]{folder,3});
            data=(byte[])clock.GetMethod("BuildData",f).Invoke(null,new object[]{0x200000000L});
            Check(load()==3 && BitConverter.ToInt32(data,0x880)==3 && BitConverter.ToInt32(data,0x95c)==3,"3 Seconds persists and displays after reload");
            Check(File.ReadAllText(newIni)=="DefensivePlayClockSeconds=3\r\n","Explicit save writes the new 3-second value");
            pref.GetMethod("Save",f).Invoke(null,new object[]{folder,0});
            data=(byte[])clock.GetMethod("BuildData",f).Invoke(null,new object[]{0x200000000L});
            Check(load()==0 && BitConverter.ToInt32(data,0x880)==0 && BitConverter.ToInt32(data,0x95c)==0,"Off persists and displays after reload");
            Check((bool)clock.GetMethod("LoadExtended",f).Invoke(null,null),"Offensive clock still loads after new setting changes");
            foreach(string bad in new[]{"DefensivePlayClockSeconds=-1","DefensivePlayClockSeconds=16","Other=60","invalid"}) {
                File.WriteAllText(newIni,bad);bool rejected=false;try{load();}catch(TargetInvocationException e){rejected=e.InnerException is InvalidDataException;}
                Check(rejected && File.ReadAllText(newIni)==bad,"Invalid stored choice preserved and rejected");
            }
            File.Delete(newIni);
            Check(File.ReadAllText(currentIni)=="ExtendedSeconds=60\r\n" && File.ReadAllText(json)==oldJson,"Legacy files remain byte-identical after migration tests");
            Console.WriteLine("PASS "+checks+" clock upgrade, old preference, per-Dynasty storage and reload checks");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
