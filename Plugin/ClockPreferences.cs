using System;
using System.Globalization;
using System.IO;
namespace OfflineDynastySettings
{
    internal static class ClockPreferences
    {
        internal const string FileName="OfflineDynastySettings.ini";
        internal static bool Valid(int seconds) { return seconds==0 || seconds==15 || seconds==20 || seconds==30 || seconds==45 || seconds==60; }
        internal static int Load(string folder)
        {
            string path=Path.Combine(folder,FileName);
            if(!File.Exists(path))return 15;
            string text=File.ReadAllText(path).Trim();int seconds;
            const string prefix="DefensivePlayClockSeconds=";
            if(!text.StartsWith(prefix,StringComparison.Ordinal) || !Int32.TryParse(text.Substring(prefix.Length),NumberStyles.None,CultureInfo.InvariantCulture,out seconds) || !Valid(seconds))
                throw new InvalidDataException("Unreadable Defensive Play Clock preference; saved file preserved.");
            return seconds;
        }
        internal static void Save(string folder,int seconds)
        {
            if(!Valid(seconds))throw new InvalidDataException("Invalid Defensive Play Clock choice.");
            string path=Path.Combine(folder,FileName),temporary=path+".tmp";
            using(FileStream file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
            using(StreamWriter writer=new StreamWriter(file)) { writer.Write("DefensivePlayClockSeconds="+seconds.ToString(CultureInfo.InvariantCulture)+"\r\n");writer.Flush();file.Flush(true); }
            if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
        }
    }
}
