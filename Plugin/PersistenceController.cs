using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;
using System.Text;

namespace OfflineDynastySettings
{
    internal sealed class PersistenceController : IDisposable
    {
        private readonly PersistenceContext context;
        private readonly Action<string> log;
        private readonly EventWaitHandle ready;
        private DetachedProcess helper;
        private string descriptor;
        internal int HelperPid { get { return helper == null ? 0 : helper.Id; } }
        internal PersistenceController(PersistenceContext context, Action<string> log)
        {
            this.context = context; this.log = log;
            context.ReadyEvent = "Local\\OfflineDynastySettingsReady_" + context.Pid + "_" + context.Started + "_" + Guid.NewGuid().ToString("N");
            ready = new EventWaitHandle(false,EventResetMode.ManualReset,context.ReadyEvent);
        }
        private static string Hash(byte[] bytes) { using(SHA256 hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(); }
        internal static string ExtractHelper()
        {
            byte[] bytes;
            using(Stream resource=typeof(PersistenceController).Assembly.GetManifestResourceStream("OfflineDynastySettings.Persistence.exe")) {
                if(resource==null)throw new InvalidOperationException("Bundled settings helper is missing.");
                using(MemoryStream copy=new MemoryStream()) { resource.CopyTo(copy); bytes=copy.ToArray(); }
            }
            string hash=Hash(bytes);
            string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OfflineDynastySettings","Runtime",hash);
            Directory.CreateDirectory(folder); string exe=Path.Combine(folder,"OfflineDynastySettings.Persistence.exe");
            if(!File.Exists(exe) || Hash(File.ReadAllBytes(exe))!=hash) {
                string temporary=exe+"."+Guid.NewGuid().ToString("N")+".tmp";
                File.WriteAllBytes(temporary,bytes);
                try { if(File.Exists(exe))File.Replace(temporary,exe,null); else File.Move(temporary,exe); }
                finally { if(File.Exists(temporary))File.Delete(temporary); }
            }
            if(Hash(File.ReadAllBytes(exe))!=hash)throw new IOException("Bundled settings helper verification failed.");
            return exe;
        }
        internal void EnsureReady()
        {
            if(helper!=null && helper.HasExited) {
                int exit=helper.ExitCode; helper.Dispose(); helper=null; ready.Reset();
                if(exit==5)throw new HookOwnershipException("The settings helper detected changed plugin code.");
                log("Settings helper exited (code " + exit + "); restarting it while this game is running.");
            }
            if(helper==null) {
                string exe=ExtractHelper();
                descriptor=Path.Combine(Path.GetDirectoryName(exe),"game-"+context.Pid+"-"+context.Started+"-"+Guid.NewGuid().ToString("N")+".json");
                File.WriteAllText(descriptor,new JavaScriptSerializer().Serialize(context));
                try { helper=DetachedProcess.Start(exe,descriptor,log); }
                catch { try { File.Delete(descriptor); } catch { } throw; }
                log("Starting bundled read-only settings helper for PID " + context.Pid + ".");
            }
            if(!ready.WaitOne(0))throw new InvalidOperationException("Waiting for the settings helper to confirm it can save; will retry.");
        }
        public void Dispose()
        {
            // MMC owns only this local process/event handle. Do not kill the
            // helper: its saving loop must survive MMC and end with the game.
            if(helper!=null) { helper.Dispose(); helper=null; }
            ready.Dispose();
        }

        // Process.Start inherits all inheritable handles even when its standard
        // streams are redirected. Use the Windows API with inheritance disabled
        // so the helper cannot keep MMC's pipes, files, or other handles alive.
        private sealed class DetachedProcess : IDisposable
        {
            private IntPtr handle;
            internal int Id;
            private DetachedProcess(IntPtr handle,int id) { this.handle=handle;Id=id; }
            internal int ExitCode { get { uint code;if(!GetExitCodeProcess(handle,out code))throw new Win32Exception(Marshal.GetLastWin32Error());return unchecked((int)code); } }
            internal bool HasExited { get { return ExitCode!=259; } }
            internal static DetachedProcess Start(string exe,string descriptor,Action<string> log)
            {
                StartupInfo startup=new StartupInfo { Size=(uint)Marshal.SizeOf(typeof(StartupInfo)),Flags=1,ShowWindow=0 };
                ProcessInfo info;uint flags=0x08000000;bool inJob;
                if(IsProcessInJob(new IntPtr(-1),IntPtr.Zero,out inJob) && inJob)flags|=0x01000000;
                StringBuilder command=new StringBuilder("\""+exe+"\" \""+descriptor+"\"");
                if(!CreateProcessW(exe,command,IntPtr.Zero,IntPtr.Zero,false,flags,IntPtr.Zero,Path.GetDirectoryName(exe),ref startup,out info)) {
                    int error=Marshal.GetLastWin32Error();
                    if((flags&0x01000000)==0 || error!=5)throw new Win32Exception(error);
                    command=new StringBuilder("\""+exe+"\" \""+descriptor+"\"");
                    if(!CreateProcessW(exe,command,IntPtr.Zero,IntPtr.Zero,false,0x08000000,IntPtr.Zero,Path.GetDirectoryName(exe),ref startup,out info))throw new Win32Exception(Marshal.GetLastWin32Error());
                    log("Windows restricted job breakaway; helper started with handle inheritance disabled.");
                }
                CloseHandle(info.Thread);return new DetachedProcess(info.Process,info.Pid);
            }
            public void Dispose() { if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;} }
        }
        [StructLayout(LayoutKind.Sequential)] private struct StartupInfo
        {
            internal uint Size;internal IntPtr Reserved,Desktop,Title;
            internal uint X,Y,XSize,YSize,XChars,YChars,Fill,Flags;
            internal ushort ShowWindow,ReservedBytes;internal IntPtr ReservedPointer,Input,Output,Error;
        }
        [StructLayout(LayoutKind.Sequential)] private struct ProcessInfo { internal IntPtr Process,Thread;internal int Pid,Tid; }
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool CreateProcessW(string exe,StringBuilder command,IntPtr processAttributes,IntPtr threadAttributes,[MarshalAs(UnmanagedType.Bool)]bool inherit,uint flags,IntPtr environment,string directory,ref StartupInfo startup,out ProcessInfo info);
        [DllImport("kernel32.dll",SetLastError=true)] private static extern bool GetExitCodeProcess(IntPtr process,out uint code);
        [DllImport("kernel32.dll",SetLastError=true)] private static extern bool IsProcessInJob(IntPtr process,IntPtr job,[MarshalAs(UnmanagedType.Bool)]out bool inJob);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    }
}
