using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
class IntegrationChecks
{
    [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protect);
    [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    static byte[] Hex(string s) { byte[] b=new byte[s.Length/2];for(int i=0;i<b.Length;i++)b[i]=Convert.ToByte(s.Substring(i*2,2),16);return b; }
    static byte[] Jump(long address, int size) { byte[] b=new byte[size];for(int i=0;i<size;i++)b[i]=0x90;b[0]=0xff;b[1]=0x25;for(int i=2;i<6;i++)b[i]=0;Array.Copy(BitConverter.GetBytes(address),0,b,6,8);return b; }
    static void Write(long address, byte[] bytes) { Marshal.Copy(bytes,0,new IntPtr(address),bytes.Length); }
    static void Allocate(long address) { if(VirtualAlloc(new IntPtr(address),new UIntPtr(0x10000),0x3000,4).ToInt64()!=address)throw new Exception("Test allocation failed"); }
    static int Main(string[] args)
    {
        try {
            string host=Path.GetFullPath(args[0]);
            AppDomain.CurrentDomain.AssemblyResolve+=(sender,request)=> {
                string path=Path.Combine(host,new AssemblyName(request.Name).Name+".dll");
                return File.Exists(path)?Assembly.LoadFrom(path):null;
            };
            Assembly assembly=Assembly.LoadFrom(Path.GetFullPath(args[1]));
            int registrations=0;
            foreach(CustomAttributeData a in assembly.GetCustomAttributesData())if(a.AttributeType.Name.StartsWith("RegisterExecutionAction")) {
                registrations++;
                if(((Type)a.ConstructorArguments[0].Value).FullName!="OfflineDynastySettings.CombinedSettingsAction")throw new Exception("Unexpected standalone action registration");
            }
            if(registrations!=1)throw new Exception("Expected exactly one MMC execution action");
            Type split=assembly.GetType("OfflinePlaycall.SplitSettingsAction",true), clock=assembly.GetType("DynastyPlayClock.DynastyPlayClockAction",true);
            BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Static;
            if(!(bool)split.GetMethod("ClockAssemblyLoaded",flags).Invoke(null,null))throw new Exception("Merged worker would install before Clock");
            long image=0x140000000, code=0x50000000, data=0x200000000, list=image+0x7993e6c, writer=image+0x79a2123;
            Allocate(list&~0xffffL);Allocate(writer&~0xffffL);Allocate(code);
            byte[] originalList=Hex("488b8f18040000ffc3488b01ff9010050000"), originalWriter=Hex("83c0fe3ddb0000000f87ac270000");
            MethodInfo check=split.GetMethod("CheckClockHooks",flags);
            IntPtr handle=OpenProcess(0x1038,false,Process.GetCurrentProcess().Id);
            if(handle==IntPtr.Zero)throw new Exception("Test process handle failed");
            try {
                Write(list,originalList);Write(writer,originalWriter);
                if(check.Invoke(null,new object[]{handle,image})!=null)throw new Exception("Original code must wait for Clock");
                byte[] listBody=(byte[])clock.GetMethod("BuildListUpdated",flags).Invoke(null,new object[]{image,data});
                byte[] writerBody=(byte[])clock.GetMethod("BuildWriterUpdated",flags).Invoke(null,new object[]{image,data});
                Write(code,listBody);Write(code+0x400,writerBody);Write(list,Jump(code,18));Write(writer,Jump(code+0x400,14));
                object recognized=check.Invoke(null,new object[]{handle,image});
                if(recognized==null || (long)recognized.GetType().GetField("WriterTarget",flags|BindingFlags.Instance).GetValue(recognized)!=code+0x400)throw new Exception("Own generated Clock hooks were not recognized");
                listBody[0]^=1;Write(code,listBody);
                bool rejected=false;
                try{check.Invoke(null,new object[]{handle,image});}catch(TargetInvocationException e){rejected=e.InnerException is InvalidOperationException;}
                if(!rejected)throw new Exception("Changed generated code was accepted");
                listBody[0]^=1;Write(code,listBody);Write(writer,Jump(code+0x800,14));rejected=false;
                try{check.Invoke(null,new object[]{handle,image});}catch(TargetInvocationException e){rejected=e.InnerException is InvalidOperationException;}
                if(!rejected)throw new Exception("Unrecognized writer chain was accepted");
            } finally { CloseHandle(handle); }
            Console.WriteLine("PASS single MMC registration, merged worker ordering, native wait, own hook recognition, changed-body rejection and unknown-writer rejection");
            return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
