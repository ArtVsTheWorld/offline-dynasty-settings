using System;
using System.Collections.Generic;
using System.IO;
using OfflineDynastySettings;

class CoreChecks
{
    static int checks;
    static void Check(bool value,string message) { if(!value)throw new Exception(message);checks++; }
    sealed class Session : ILaunchSession
    {
        internal int Clocks,Splits,Polls,Verifies,Disposals;
        internal bool Available=true,Changed;
        public string InstallClock(){Clocks++;return Available?"Installed test hooks":"Waiting for decrypted code";}
        public bool InstallPlaycall(){Splits++;return Available;}
        public void Poll(){Polls++;}
        public void Verify(){Verifies++;if(Changed)throw new HookOwnershipException("test changed bytes");}
        public void Dispose(){Disposals++;}
    }
    sealed class Env : ILaunchEnvironment
    {
        internal DateTime Clock=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);
        internal GameLaunch Game;
        internal Session Current;
        internal List<string> Messages=new List<string>();
        public DateTime Now{get{return Clock;}}
        public GameLaunch FindGame(){return Game;}
        public bool IsAlive(GameLaunch game){return Game!=null && Game.Same(game);}
        public ILaunchSession Begin(GameLaunch game){return Current=new Session();}
        public void Log(string message){Messages.Add(message);}
        internal void Launch(int pid){Game=new GameLaunch{Pid=pid,Started=Clock.Ticks,StartedUtc=Clock};}
    }
    static void Main(string[] args)
    {
        string folder=Path.GetFullPath(args[0]);Directory.CreateDirectory(folder);
        string ini=Path.Combine(folder,ClockPreferences.FileName);
        if(File.Exists(ini))File.Delete(ini);
        Check(ClockPreferences.Load(folder)==15,"New installations default to 15 seconds");
        Check(!File.Exists(ini),"Reading defaults must not write settings");
        foreach(int value in new[]{15,20,30,45,60,0}){
            ClockPreferences.Save(folder,value);
            Check(ClockPreferences.Load(folder)==value,"Clock preference did not survive reload");
        }
        string saved=File.ReadAllText(ini);
        foreach(int value in new[]{-1,1,14,16,59,61,999}){
            bool rejected=false;try{ClockPreferences.Save(folder,value);}catch(InvalidDataException){rejected=true;}
            Check(rejected && File.ReadAllText(ini)==saved,"Invalid value overwrote a saved choice");
        }
        File.WriteAllText(ini,"DefensivePlayClockSeconds=invalid");
        bool corrupt=false;try{ClockPreferences.Load(folder);}catch(InvalidDataException){corrupt=true;}
        Check(corrupt && File.ReadAllText(ini).Contains("invalid"),"Corrupt preferences must be preserved");
        Env env=new Env();
        using(LaunchCoordinator coordinator=new LaunchCoordinator(env)){
            coordinator.Step();Check(env.Current==null,"Do not activate without a game");
            env.Launch(100);coordinator.Step();Check(env.Current==null,"Respect startup grace");
            env.Clock=env.Clock.AddSeconds(46);coordinator.Step();Session first=env.Current;
            Check(first.Clocks==1 && first.Splits==1 && first.Verifies==1 && first.Polls==1,"Both components must activate and verify");
            env.Clock=env.Clock.AddSeconds(6);coordinator.Step();
            Check(first.Clocks==1 && first.Splits==1 && first.Verifies==2,"Do not reinstall owned hooks");
            first.Changed=true;env.Clock=env.Clock.AddSeconds(6);coordinator.Step();int before=first.Polls;
            env.Clock=env.Clock.AddSeconds(6);coordinator.Step();
            Check(first.Polls==before,"Stop this launch after an ownership change");
            env.Launch(200);coordinator.Step();Check(first.Disposals==1,"Dispose the previous launch");
            env.Clock=env.Clock.AddSeconds(46);coordinator.Step();
            Check(env.Current!=first && env.Current.Verifies==1,"A fresh game can activate after a previous conflict");
            Session second=env.Current;env.Game=null;coordinator.Step();
            Check(second.Disposals==1,"Release the session after the game exits");
        }
        Console.WriteLine("PASS "+checks+" preference and launch-coordinator checks (no game or MMC required)");
    }
}
