// Executes the actual generated x64 hooks against fake objects in this process.
// Does not open the game, MMC, or any dynasty save.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
class GameSettingsMenuChecks
{
    static string mmc;
    static Assembly dll;
    static long image, remote, capture;
    static int selected;
    static IntPtr clockData, menuWriter;
    static List<string> headerFields=new List<string>();
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void HeaderString(IntPtr writer,int index,IntPtr name,IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void HeaderInt(IntPtr writer,int index,IntPtr name,int value);
    static HeaderString headerString=(w,i,n,v)=>headerFields.Add(i+":"+Marshal.PtrToStringAnsi(n)+"="+Marshal.PtrToStringAnsi(v));
    static HeaderInt headerInt=(w,i,n,v)=>headerFields.Add(i+":"+Marshal.PtrToStringAnsi(n)+"="+v);

    static int checks;
    static List<int[]> rows = new List<int[]>();
    static List<string> labels = new List<string>();
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void Getter(IntPtr info, int side, IntPtr cooldown, IntPtr limit);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void UI(IntPtr adapter, IntPtr writer, int index, int online);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void Serialize(IntPtr adapter, IntPtr writer, int key, IntPtr descriptor, int index);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int Value(IntPtr source, int index, IntPtr name);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void Writer(IntPtr source, int index, int key);
    static Serialize serialize = OnSerialize;
    static Value value = OnValue;
    static int Main(string[] args)
    {
        try
        {
            if (args.Length != 3) throw new ArgumentException("DLL MMC-root output-directory");
            mmc = args[1]; AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            dll = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            Directory.CreateDirectory(args[2]);
            image = VirtualAlloc(IntPtr.Zero, new UIntPtr(0x10000000), 0x2000, 4).ToInt64();
            remote = VirtualAlloc(IntPtr.Zero, new UIntPtr(0x5000), 0x3000, 0x40).ToInt64();
            capture = Marshal.AllocHGlobal(8).ToInt64();
            if (image == 0 || remote == 0) throw new Exception("allocation failed");
            foreach (long rva in new long[] { 0xf55c998, 0xf905618, 0x2e94d0a, 0x799fa10, 0x7997998, 0x7997950, 0xad960e0, 0x79a847d, 0x79a5cd1, 0xd9eb808, 0xf3fddbc, 0xdaa6758, 0xdb06d40, 0x27a8710 })
                if (VirtualAlloc(new IntPtr(image + (rva & ~4095L)), new UIntPtr(4096), 0x1000, 0x40) == IntPtr.Zero) throw new Exception("page commit failed");
            Code(image + 0x2e94d0a, Hex("418901c3"));
            Code(image + 0xad960e0, System.Text.Encoding.ASCII.GetBytes("value\0"));
            Code(image + 0x799fa10, Jump(Marshal.GetFunctionPointerForDelegate(serialize).ToInt64()));
            Code(image + 0x7997998, Concat(MovR10(capture), Hex("41891a4883c4205b5f5ec3")));
            Code(image + 0x7997950, Concat(MovR10(capture), Hex("41c702630000004883c4205b5f5ec3")));
            Code(image + 0x79a847d, Hex("4883c428415f5ec3"));
            Code(image + 0x79a5cd1, Concat(MovR10(capture), Hex("418902"), Jump(image + 0x79a847d)));
            Code(remote + 0x1400, Concat(MovR10(capture), Hex("418902"), Jump(image + 0x79a847d)));
            clockData=Allocate(0x1000);Marshal.WriteInt64(clockData,0x10,Marshal.StringToHGlobalAnsi("Offensive Play Clock").ToInt64());
            dll.GetType("DynastyPlayClock.DynastyPlayClockAction").GetField("dataAddress",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,clockData.ToInt64());
            IntPtr profile=Allocate(8),profileVt=Allocate(0x18);Marshal.WriteInt64(profile,profileVt.ToInt64());
            Code(remote+0x1700,Hex("b801000000c3"));Marshal.WriteInt64(profileVt,0x10,remote+0x1700);
            Code(image+0x27a8710,Concat(Hex("48b8"),BitConverter.GetBytes(profile.ToInt64()),Hex("c3")));
            menuWriter=Allocate(8);IntPtr writerVt=Allocate(0x38);Marshal.WriteInt64(menuWriter,writerVt.ToInt64());
            Marshal.WriteInt64(writerVt,0x20,Marshal.GetFunctionPointerForDelegate(headerInt).ToInt64());Marshal.WriteInt64(writerVt,0x30,Marshal.GetFunctionPointerForDelegate(headerString).ToInt64());
            Marshal.WriteInt64(new IntPtr(image+0xdaa6758),Marshal.StringToHGlobalAnsi("label").ToInt64());
            Marshal.WriteInt64(new IntPtr(image+0xdb06d40),Marshal.StringToHGlobalAnsi("key").ToInt64());
            Marshal.WriteInt64(new IntPtr(image+0xdaa6768),Marshal.StringToHGlobalAnsi("type").ToInt64());
            Marshal.WriteInt64(new IntPtr(image+0xdaa6798),Marshal.StringToHGlobalAnsi("header").ToInt64());
            Marshal.WriteInt32(clockData,0x880,15);Marshal.WriteInt64(clockData,0x910,Marshal.StringToHGlobalAnsi("Defensive Play Clock").ToInt64());
            InstallCode(0);
            SetId(101); SetEntry(0, 101, 2, 3); SetEntry(1, 202, 10, 20);
            Marshal.WriteInt32(new IntPtr(image + 0xd9eb808), 14);
            Marshal.WriteInt32(new IntPtr(image + 0xf3fddbc), 3);
            IntPtr info = Allocate(0xd20), outCd = Allocate(4), outLimit = Allocate(4);
            Marshal.WriteInt32(info, 0xd10, 8); Marshal.WriteInt32(info, 0xd14, 7);
            IntPtr adapter = Allocate(0x430), response = Allocate(0x150), vt = Allocate(0xa18);
            Marshal.WriteInt64(adapter, 0x418, response.ToInt64()); Marshal.WriteInt64(response, vt.ToInt64());
            Marshal.WriteInt64(vt, 0xa10, image + 0x8459ff0);
            Marshal.WriteInt64(new IntPtr(image + 0xf905618), adapter.ToInt64());
            Getter getter = (Getter)Marshal.GetDelegateForFunctionPointer(new IntPtr(remote + 0x900), typeof(Getter));
            getter(info, 1, outCd, outLimit); Check(Marshal.ReadInt32(outCd) == 2 && Marshal.ReadInt32(outLimit) == 3, "defense cooldown and limit override together");
            SetEntry(0, 101, 0, 3); getter(info, 1, outCd, outLimit); Check(Marshal.ReadInt32(outCd) == -1 && Marshal.ReadInt32(outLimit) == 3, "cooldown can be disabled independently");
            SetEntry(0, 101, 2, 0); getter(info, 1, outCd, outLimit); Check(Marshal.ReadInt32(outCd) == 2 && Marshal.ReadInt32(outLimit) == -1, "limit can be disabled independently");
            SetEntry(0, 101, 0, 0); getter(info, 1, outCd, outLimit); Check(Marshal.ReadInt32(outCd) == -1 && Marshal.ReadInt32(outLimit) == -1, "both zeros become native disabled sentinels");
            SetEntry(0, 101, 2, -1); getter(info, 1, outCd, outLimit); Check(Marshal.ReadInt32(outCd) == 2 && Marshal.ReadInt32(outLimit) == -1, "unsaved defensive limit defaults Off");
            SetEntry(0, 101, -1, 3); getter(info, 1, outCd, outLimit); Check(Marshal.ReadInt32(outCd) == -1 && Marshal.ReadInt32(outLimit) == 3, "unsaved defensive cooldown defaults Off");
            SetId(202); getter(info, 1, outCd, outLimit); Check(Marshal.ReadInt32(outCd) == 10 && Marshal.ReadInt32(outLimit) == 20, "another dynasty keeps its own cooldown and limit");
            SetId(303); getter(info, 1, outCd, outLimit); Check(Marshal.ReadInt32(outCd) == -1 && Marshal.ReadInt32(outLimit) == -1, "unconfigured dynasty defaults both defensive restrictions Off");
            SetId(0); getter(info, 1, outCd, outLimit); Check(Marshal.ReadInt32(outCd) == 8 && Marshal.ReadInt32(outLimit) == 7, "missing league ID retains native behavior");
            SetId(101); SetEntry(0, 101, 2, 3);
            Marshal.WriteInt32(new IntPtr(image + 0xf3fddbc), 4); getter(info, 1, outCd, outLimit); Check(Marshal.ReadInt32(outCd) == 8 && Marshal.ReadInt32(outLimit) == 7, "online match retains native behavior");
            Marshal.WriteInt32(new IntPtr(image + 0xf3fddbc), 3);
            Marshal.WriteInt32(new IntPtr(image + 0xd9eb808), -1); getter(info, 1, outCd, outLimit); Check(Marshal.ReadInt32(outCd) == 8 && Marshal.ReadInt32(outLimit) == 7, "other local modes retain native behavior");
            Marshal.WriteInt32(new IntPtr(image + 0xd9eb808), 14);
            Marshal.WriteInt64(new IntPtr(image + 0xf905618), 0); getter(info, 1, outCd, outLimit); Check(Marshal.ReadInt32(outCd) == 2 && Marshal.ReadInt32(outLimit) == 3, "offline Dynasty override works after menu response is released");
            Marshal.WriteInt64(new IntPtr(image + 0xf905618), adapter.ToInt64());

            IntPtr controls = Allocate(0x3b90), descriptor = new IntPtr(controls.ToInt64() + 0x3a30), limitDescriptor = new IntPtr(controls.ToInt64() + 0x3ae0);
            Marshal.WriteInt64(response, 0x140, controls.ToInt64());
            Marshal.WriteInt32(descriptor, 0x5c, 8); Marshal.WriteInt32(limitDescriptor, 0x5c, 5);
            byte[] before = new byte[0x160]; Marshal.Copy(descriptor, before, 0, before.Length);
            Code(remote + 0x1500, Concat(Hex("5657534883ec204889d64889cf4489c34489c8"), Jump(remote)));
            UI ui = (UI)Marshal.GetDelegateForFunctionPointer(new IntPtr(remote + 0x1500), typeof(UI));
            ui(adapter, menuWriter, 5, 0);
            Check(rows.Count == 6 && rows[0][0] == 0xa && rows[1][0] == 0xb && rows[2][0] == 0x1000 && rows[3][0] == 0xf0 && rows[4][0] == 0xf1 && rows[5][0] == 0x1002 && rows[0][1] == 6 && rows[1][1] == 7 && rows[2][1] == 8 && rows[3][1] == 9 && rows[4][1] == 10 && rows[5][1] == 11, "UI uses four dynamic consecutive indices and distinct keys");
            Check(rows[0][2] == 8 && rows[1][2] == 5 && rows[2][2] == 1 && rows[3][2] == 2 && rows[4][2] == 3 && rows[5][2] == 15 && labels[0] == "Offensive Playcall Cooldown" && labels[1] == "Offensive Playcall Limit" && labels[2] == "Offensive Play Clock" && labels[3] == "Defensive Playcall Cooldown" && labels[4] == "Defensive Playcall Limit" && labels[5] == "Defensive Play Clock", "all four UI labels and values are independent");
            byte[] after = new byte[0x160]; Marshal.Copy(descriptor, after, 0, after.Length);
            Check(Equal(before, after) && Marshal.ReadInt32(new IntPtr(capture)) == 12, "both original descriptors remain unchanged and next native row index is preserved");
            Check(headerFields.Count==3 && headerFields[0]=="5:label=GAME SETTINGS" && headerFields[1]=="5:key=4097" && headerFields[2]=="5:type=header","native GAME SETTINGS header precedes all six options");
            Check(rows[2][2]==1 && rows[5][2]==15,"Native clock defaults display On and 15 Seconds");
            Marshal.WriteInt32(clockData,0x828,1);rows.Clear();labels.Clear();ui(adapter,menuWriter,5,0);
            Check(rows[2][2]==3,"60-second clock preference displays correctly in moved row");Marshal.WriteInt32(clockData,0x828,0);
            rows.Clear(); labels.Clear(); SetEntry(0, 101, 2, -1); ui(adapter, menuWriter, 5, 0); Check(rows[4][2] == 0, "missing defensive limit displays Off");
            rows.Clear(); labels.Clear(); SetEntry(0, 101, -1, 3); ui(adapter, menuWriter, 5, 0); Check(rows[3][2] == 0 && rows[4][2] == 3, "missing defensive cooldown displays Off");
            SetId(303); rows.Clear(); labels.Clear(); ui(adapter, menuWriter, 5, 0);
            Check(rows[0][2] == 8 && rows[1][2] == 5 && rows[3][2] == 0 && rows[4][2] == 0, "unsaved defense defaults Off without altering saved offense");
            Marshal.WriteInt32(descriptor, 0x5c, 0); Marshal.WriteInt32(limitDescriptor, 0x5c, 0);
            rows.Clear(); labels.Clear(); ui(adapter, menuWriter, 5, 0);
            Check(rows[0][2] == 0 && rows[1][2] == 0 && rows[3][2] == 0 && rows[4][2] == 0 && rows[2][2] == 1 && rows[5][2] == 15, "fresh defaults are four Off restrictions, offensive On, defensive 15 Seconds");
            Marshal.WriteInt32(descriptor, 0x5c, 8); Marshal.WriteInt32(limitDescriptor, 0x5c, 5); SetId(101);
            headerFields.Clear();rows.Clear(); labels.Clear(); ui(adapter, menuWriter, 5, 1); Check(rows.Count == 0 && headerFields.Count==0 && Marshal.ReadInt32(new IntPtr(capture)) == 99, "online UI replays original route");
            SetEntry(0, 101, 2, 3);

            IntPtr source = Allocate(8), sourceVt = Allocate(0x20);
            Marshal.WriteInt64(source, sourceVt.ToInt64()); Marshal.WriteInt64(sourceVt, 0x18, Marshal.GetFunctionPointerForDelegate(value).ToInt64());
            Code(remote + 0x1600, Concat(Hex("5641574883ec284889ce4189d74489c0"), Jump(remote + 0x600)));
            Writer writer = (Writer)Marshal.GetDelegateForFunctionPointer(new IntPtr(remote + 0x1600), typeof(Writer));
            selected = 4; writer(source, 7, 0xf0); Check(ReadEntry(101, 4) == 4 && ReadEntry(101, 8) == 3 && Marshal.ReadInt32(new IntPtr(remote + 0x1800)) == 1, "cooldown writer preserves limit and publishes revision");
            selected = 9; writer(source, 7, 0xf1); Check(ReadEntry(101, 4) == 4 && ReadEntry(101, 8) == 9 && Marshal.ReadInt32(new IntPtr(remote + 0x1800)) == 2, "limit writer preserves cooldown and publishes revision");
            SetId(303); selected = 6; writer(source, 7, 0xf1); Check(ReadEntry(303, 4) == -1 && ReadEntry(303, 8) == 6, "new limit-only entry initializes cooldown as unspecified");
            selected = 1; writer(source, 7, 0xf0); Check(ReadEntry(303, 4) == 1 && ReadEntry(303, 8) == 6 && ReadEntry(101, 8) == 9, "adding cooldown preserves that dynasty's limit and another dynasty's values");
            SetId(404); selected = 7; writer(source, 7, 0xf0); Check(ReadEntry(404, 4) == 7 && ReadEntry(404, 8) == -1, "new cooldown-only entry initializes limit as unspecified");
            SetId(101); selected = 11; writer(source, 7, 0xf0); Check(ReadEntry(101, 4) == 4, "out-of-range cooldown is rejected");
            selected = -1; writer(source, 7, 0xf0); Check(ReadEntry(101, 4) == 4, "negative cooldown is rejected");
            selected = 21; writer(source, 7, 0xf1); Check(ReadEntry(101, 8) == 9, "out-of-range limit is rejected");
            selected = -1; writer(source, 7, 0xf1); Check(ReadEntry(101, 8) == 9, "negative limit is rejected");
            selected = 20; writer(source, 7, 0xf1); Check(ReadEntry(101, 8) == 20, "maximum limit 20 is accepted");
            SetId(0); selected = 0; int sequence = Marshal.ReadInt32(new IntPtr(remote + 0x1800)); writer(source, 7, 0xf1); Check(Marshal.ReadInt32(new IntPtr(remote + 0x1800)) == sequence, "missing league ID rejects private writes"); SetId(101);
            Marshal.WriteInt32(new IntPtr(capture), -123); writer(source, 7, 5); Check(Marshal.ReadInt32(new IntPtr(capture)) == 3, "ordinary settings replay native dispatch");
            Marshal.WriteInt32(new IntPtr(capture), -123); writer(source, 7, 0xb); Check(Marshal.ReadInt32(new IntPtr(capture)) == 9, "offensive limit follows original native key dispatch");
            InstallCode(remote + 0x1400); SetEntry(0, 101, 4, 9); SetEntry(1, 202, 10, 20);
            writer(source, 7, 0x1000); Check(Marshal.ReadInt32(new IntPtr(capture)) == 0x1000, "Play Clock chaining preserves the original key");
            for (int i = 0; i < 1024; i++) SetEntry(i, i + 1000, 1, 2);
            SetId(9999); selected = 3; writer(source, 7, 0xf1); Check(ReadEntry(9999, 8) == -1 && ReadEntry(1000, 8) == 2 && Marshal.ReadInt32(new IntPtr(remote + 0x1800)) == 0, "full table refuses an extra dynasty without corrupting existing settings");
            InstallCode(0);
            Persistence(args[2]);
            Console.WriteLine("PASS: " + checks + " checks; actual generated x64 code and DLL storage logic exercised locally.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Persistence(string folder)
    {
        string path = Path.GetFullPath(Path.Combine(folder, "check-dynasties.json"));
        Type action = dll.GetType("OfflinePlaycall.SplitSettingsAction");
        MethodInfo load = action.GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Static);
        object doc = Activator.CreateInstance(action.GetNestedType("SettingsDocument", BindingFlags.Public));
        Type sessionType = action.GetNestedType("Session", BindingFlags.NonPublic);
        IntPtr own = OpenProcess(0x410, false, System.Diagnostics.Process.GetCurrentProcess().Id);
        object session = sessionType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0].Invoke(new object[] { own, remote, null, path, doc });
        MethodInfo save = sessionType.GetMethod("SavePending", BindingFlags.Instance | BindingFlags.NonPublic);
        Action<string> log = s => Console.WriteLine(s);
        try
        {
            SetEntry(0, 101, 4, 9); SetEntry(1, 202, 10, -1); SetEntry(2, 303, -1, 2); Marshal.WriteInt32(new IntPtr(remote + 0x1800), 1);
            save.Invoke(session, new object[] { log });
            object loaded = load.Invoke(null, new object[] { path });
            Dictionary<string, int> values = (Dictionary<string, int>)loaded.GetType().GetProperty("DefenseCooldowns").GetValue(loaded, null);
            Dictionary<string, int> limits = (Dictionary<string, int>)loaded.GetType().GetProperty("DefenseLimits").GetValue(loaded, null);
            Check(values["101"] == 4 && values["202"] == 10 && limits["101"] == 9 && limits["303"] == 2 && !limits.ContainsKey("202") && !values.ContainsKey("303"), "DLL saves and reloads both maps without materializing inherited values");
            SetEntry(0, 101, 0, 0); Marshal.WriteInt32(new IntPtr(remote + 0x1800), 2); save.Invoke(session, new object[] { log });
            Check(File.Exists(path + ".bak") && File.ReadAllText(path).Contains("\"101\":0"), "atomic replacement preserves backup and disabled values");
            string previous = File.ReadAllText(path);
            SetEntry(0, 101, 0, 21); Marshal.WriteInt32(new IntPtr(remote + 0x1800), 3);
            bool rejected = false;
            try { save.Invoke(session, new object[] { log }); } catch (TargetInvocationException ex) { rejected = ex.InnerException is InvalidDataException; }
            Check(rejected && File.ReadAllText(path) == previous, "invalid remote limit leaves previous storage intact");
            string bad = Path.Combine(folder, "invalid.json"); File.WriteAllText(bad, "{\"FormatVersion\":2,\"DefenseCooldowns\":{\"101\":11},\"DefenseLimits\":{}}");
            rejected = false;
            try { load.Invoke(null, new object[] { Path.GetFullPath(bad) }); } catch (TargetInvocationException ex) { rejected = ex.InnerException is InvalidDataException; }
            Check(rejected, "invalid stored cooldown is rejected before installation");
            File.WriteAllText(bad, "{\"FormatVersion\":2,\"DefenseCooldowns\":{},\"DefenseLimits\":{\"101\":21}}");
            rejected = false; try { load.Invoke(null, new object[] { Path.GetFullPath(bad) }); } catch (TargetInvocationException ex) { rejected = ex.InnerException is InvalidDataException; }
            Check(rejected, "invalid stored limit is rejected before installation");
            File.WriteAllText(bad, "{\"FormatVersion\":1,\"DefenseCooldowns\":{\"101\":2}}");
            loaded = load.Invoke(null, new object[] { Path.GetFullPath(bad) });
            values = (Dictionary<string, int>)loaded.GetType().GetProperty("DefenseCooldowns").GetValue(loaded, null);
            limits = (Dictionary<string, int>)loaded.GetType().GetProperty("DefenseLimits").GetValue(loaded, null);
            Check(values["101"] == 2 && limits.Count == 0, "0.5.0 legacy cooldown file loads without changing its values");
            Dictionary<string, int> manyCd = new Dictionary<string, int>(), manyLimits = new Dictionary<string, int>();
            for (int i = 1; i <= 512; i++) manyCd.Add(i.ToString(), 1);
            for (int i = 513; i <= 1025; i++) manyLimits.Add(i.ToString(), 2);
            File.WriteAllText(bad, new JavaScriptSerializer().Serialize(new { FormatVersion = 2, DefenseCooldowns = manyCd, DefenseLimits = manyLimits }));
            rejected = false; try { load.Invoke(null, new object[] { Path.GetFullPath(bad) }); } catch (TargetInvocationException ex) { rejected = ex.InnerException is InvalidDataException; }
            Check(rejected, "capacity limit counts the union of cooldown and limit dynasty IDs");
        }
        finally { ((IDisposable)session).Dispose(); }
    }
    static void OnSerialize(IntPtr adapter, IntPtr writer, int key, IntPtr descriptor, int index)
    { rows.Add(new int[] { key, index, Marshal.ReadInt32(descriptor, 0x5c) }); labels.Add(Marshal.PtrToStringAnsi(new IntPtr(Marshal.ReadInt64(descriptor, 0x10)))); }
    static int OnValue(IntPtr source, int index, IntPtr name) { return selected; }
    static void InstallCode(long prior)
    {
        byte[] code = (byte[])dll.GetType("OfflinePlaycall.SplitHookCode").GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { image, remote, prior });
        Code(remote, code);
        Code(remote+0x1700,Hex("b801000000c3"));
        Code(remote + 0x1400, Concat(MovR10(capture), Hex("418902"), Jump(image + 0x79a847d)));
        Code(remote + 0x1600, Concat(Hex("5641574883ec284889ce4189d74489c0"), Jump(remote + 0x600)));
    }
    static void SetId(int id) { Marshal.WriteInt32(new IntPtr(image + 0xf55c998), id); }
    static void SetEntry(int index, int id, int cd, int limit) {
        Marshal.WriteInt32(new IntPtr(remote + 0x2000 + index * 12), id);
        Marshal.WriteInt32(new IntPtr(remote + 0x2004 + index * 12), cd);
        Marshal.WriteInt32(new IntPtr(remote + 0x2008 + index * 12), limit);
    }
    static int ReadEntry(int id, int field) {
        for (int i = 0; i < 1024; i++)
            if (Marshal.ReadInt32(new IntPtr(remote + 0x2000 + i * 12)) == id)
                return Marshal.ReadInt32(new IntPtr(remote + 0x2000 + i * 12 + field));
        return -1;
    }
    static void Check(bool success, string message) { if (!success) throw new Exception("FAIL: " + message); checks++; Console.WriteLine("PASS " + message); }
    static IntPtr Allocate(int size) { IntPtr p = Marshal.AllocHGlobal(size); Marshal.Copy(new byte[size], 0, p, size); return p; }
    static void Code(long address, byte[] bytes) { Marshal.Copy(bytes, 0, new IntPtr(address), bytes.Length); FlushInstructionCache(GetCurrentProcess(), new IntPtr(address), new UIntPtr((uint)bytes.Length)); }
    static bool Equal(byte[] a, byte[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    static byte[] Hex(string s) { byte[] b = new byte[s.Length / 2]; for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16); return b; }
    static byte[] Jump(long addr) { return Concat(Hex("ff2500000000"), BitConverter.GetBytes(addr)); }
    static byte[] MovR10(long addr) { return Concat(Hex("49ba"), BitConverter.GetBytes(addr)); }
    static byte[] Concat(params byte[][] items) { List<byte> b = new List<byte>(); foreach (byte[] item in items) b.AddRange(item); return b.ToArray(); }
    static Assembly Resolve(object sender, ResolveEventArgs args) { string path = Path.Combine(mmc, new AssemblyName(args.Name).Name + ".dll"); return File.Exists(path) ? Assembly.LoadFrom(path) : null; }
    [DllImport("kernel32.dll")] static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protection);
    [DllImport("kernel32.dll")] static extern bool FlushInstructionCache(IntPtr handle, IntPtr address, UIntPtr size);
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
}
