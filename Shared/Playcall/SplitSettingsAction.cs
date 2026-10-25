using Frosty.Core;
using FrostySdk.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Web.Script.Serialization;

namespace OfflinePlaycall
{
    // Split settings extension. Complete native UI/writer fingerprints are
    // verified, with normalization only for our gate and an exactly recognized
    // Verified Dynasty Play Clock 0.1.1/0.1.2/0.2.0 hooks. Unknown hooks are never overwritten.
    public sealed class SplitSettingsAction : ExecutionAction
    {
        private static int running;
        private static readonly object logLock = new object();
        public override Action<ILogger, PluginManagerType, CancellationToken> PreLaunchAction
        { get { return (logger, type, token) => { }; } }

        public override Action<ILogger, PluginManagerType, CancellationToken> PostLaunchAction
        {
            get { return (logger, type, token) => {
                if (Interlocked.CompareExchange(ref running, 1, 0) != 0) return;
                Thread thread = new Thread(() => Worker(logger, token));
                thread.IsBackground = true;
                thread.Name = "Offline Playcall split cooldowns and limits";
                thread.Start();
            }; }
        }

        private static void Worker(ILogger logger, CancellationToken token)
        {
            Action<string> log = s => Log(logger, s);
            try
            {
                log("0.6.1 split cooldowns and limits loaded; waiting for a stable foreground game window.");
                DateTime deadline = DateTime.UtcNow.AddMinutes(15);
                int previousPid = 0;
                DateTime readySince = DateTime.MinValue;
                while (!token.IsCancellationRequested && DateTime.UtcNow < deadline)
                {
                    using (Process game = FindGame())
                    {
                        if (game != null)
                        {
                            if (previousPid != game.Id) { previousPid = game.Id; readySince = DateTime.MinValue; }
                            if (Interactive(game))
                            {
                                if (readySince == DateTime.MinValue) readySince = DateTime.UtcNow;
                                if (DateTime.UtcNow - readySince >= TimeSpan.FromSeconds(15)
                                    && DateTime.UtcNow - game.StartTime.ToUniversalTime() >= TimeSpan.FromSeconds(45))
                                {
                                    using (Session session = TryInstall(game, DefaultStorage(), log))
                                        if (session != null) { session.Monitor(game, log); return; }
                                }
                            }
                            else readySince = DateTime.MinValue;
                        }
                    }
                    Thread.Sleep(250);
                }
                log("Split settings installation ended before compatible running code was available.");
            }
            catch (Exception ex) { log("Split settings stopped: " + ex.GetType().Name + ": " + ex.Message); }
            finally { Interlocked.Exchange(ref running, 0); }
        }

        // The local harness keeps this process alive to exercise the same DLL
        // and persistence monitor that MMC will run. It does not install files.
        public static void RunLocalTest(int pid, string storage)
        {
            using (Process game = Process.GetProcessById(pid))
            using (Session session = TryInstall(game, storage, s => { Console.WriteLine(s); Log(null, s); }))
            {
                if (session == null) throw new InvalidOperationException("Compatible code is not available.");
                session.Monitor(game, s => { Console.WriteLine(s); Log(null, s); });
            }
        }

#if COMBINED_DYNASTY_SETTINGS
        internal static Session TryInstallCombined(Process game, Action<string> log)
        { return TryInstall(game, DefaultStorage(), log); }
#endif
        private static Session TryInstall(Process game, string storage, Action<string> log)
        {
            ProcessModule module;
            try { module = game.MainModule; } catch { return null; }
            if (module == null || !String.Equals(module.ModuleName, "CollegeFB27.exe", StringComparison.OrdinalIgnoreCase)) return null;
            IntPtr handle = OpenProcess(0x1438, false, game.Id);
            if (handle == IntPtr.Zero) return null;
            long image = module.BaseAddress.ToInt64();
            Session session = null;
            try
            {
                byte[] uiOriginal = Hex("488b871804000041b80a000000488bd6");
                if (!Equal(Read(handle, image + 0x7993da0, 16), uiOriginal)) return null;
                ClockHooks clock = CheckClockHooks(handle, image);
                if (clock == null && ClockAssemblyLoaded()) return null;
                Region[] regions = {
                    new Region(0x7993aa0, 0x15c0, "e7bdc4d9a6d877daab1f48e46ddebcc94191d8e36b8b7c20b19bbeb672df7a0a"),
                    new Region(0x799be70, 0x2a2, "46f4460b9d7ec4dc67e7ae7a3a11a92fc7fc9a45bd64e47e55cfa5f729003c2a"),
                    new Region(0x86b1e70, 0x4e14, "bc8153280b048be7b93f93dbb6eb0ee8abd372abfb964905d59e087f63254427"),
                    new Region(0x79a2090, 0x2d2c, "a604bc7c58a6103208417817fb68e0c519dbec0ff6470c29b3469bd3c5b71e02"),
                    new Region(0x2e93ee0, 0x3e, "8146614caf492890dfcd045e92ab74e979f775b1554c87d1921690406839a0e8"),
                    new Region(0x4f96740, 0xc4, "cde34c4edcc89f8d52d5b6b7f403532c0ebd98ff202deacf05f71099319ed421"),
                    new Region(0x2e8c240, 0x338, "40d731ea91dbf63e01685a0c8a964debd86b3f352d68be911b84427020662517"),
                    new Region(0x8456250, 3, "01cb47d078b4841b8408ec4fe278efa83115c6f6e101972987507d2b2b57dcf0"),
                    new Region(0x7993a90, 7, "46d3d1a5a933af9e5a5b62beaee8678a87bdb8e7b9b8a4c0b17103ed799e13d4"),
                    new Region(0x7ee1fb0, 0x45, "48c553b0030661e62543ef8fdd4f5ad41155a6dac5c2a8f017b8e12478c49329"),
                    new Region(0x3009b70, 0x10, "c30bc95384a7c1b5b89b00e708e9667120a818f8fa06e52cbd6e9628734e23ca")
                };
                foreach (Region r in regions)
                {
                    byte[] data = Read(handle, image + r.Rva, r.Size);
                    if (data == null) return null;
                    if (r.Rva == 0x7993aa0)
                    {
                        int offset = (int)(0x7993d9e - r.Rva);
                        byte[] currentGate = new byte[] { data[offset], data[offset + 1] };
                        if (!Equal(currentGate, Hex("7458")) && !Equal(currentGate, Hex("9090")))
                            throw new InvalidOperationException("Unrecognized native Playcall gate.");
                        data[offset] = 0x74; data[offset + 1] = 0x58;
                        if (clock != null) Array.Copy(clock.ListOriginal, 0, data, (int)(0x7993e6c - r.Rva), clock.ListOriginal.Length);
                    }
                    if (r.Rva == 0x79a2090 && clock != null)
                        Array.Copy(Hex("83c0fe3ddb0000000f87ac270000"), 0, data, (int)(0x79a2123 - r.Rva), 14);
                    if (Hash(data) != r.Hash) {
#if COMBINED_DYNASTY_SETTINGS
                        throw new InvalidOperationException(OfflineDynastySettings.DiagnosticLog.Mismatch("Playcall settings",r.Rva,r.Hash,data));
#else
                        throw new InvalidOperationException("Unsupported split settings code at RVA " + r.Rva.ToString("x") + "; no split hook applied.");
#endif
                    }
                }
                Site[] sites = {
                    new Site(image + 0x7993da0, 0, uiOriginal),
                    new Site(image + 0x79a2123, 0x600, clock == null ? Hex("83c0fe3ddb0000000f87ac270000") : clock.WriterDetour),
                    new Site(image + 0x2e93f0b, 0x900, Hex("8b81100d00004189008b81140d0000"))
                };
                foreach (Site site in sites)
                    if (!Equal(Read(handle, site.Address, site.Original.Length), site.Original))
                        throw new InvalidOperationException("A split hook site belongs to another patch; no split hook applied.");

                SettingsDocument document = Load(storage);
                IntPtr allocation = VirtualAllocEx(handle, IntPtr.Zero, new UIntPtr(SplitHookCode.AllocationSize), 0x3000, 0x40);
                if (allocation == IntPtr.Zero) throw WinError("VirtualAllocEx");
                long remote = allocation.ToInt64();
                byte[] bytes = SplitHookCode.Create(image, remote, clock == null ? 0 : clock.WriterTarget);
                HashSet<string> ids = Ids(document);
                int index = 0;
                foreach (string key in ids)
                {
                    uint id = UInt32.Parse(key, CultureInfo.InvariantCulture);
                    int cooldown, limit;
                    if (!document.DefenseCooldowns.TryGetValue(key, out cooldown)) cooldown = -1;
                    if (!document.DefenseLimits.TryGetValue(key, out limit)) limit = -1;
                    int offset = SplitHookCode.TableOffset + index * SplitHookCode.EntrySize;
                    Array.Copy(BitConverter.GetBytes(id), 0, bytes, offset, 4);
                    Array.Copy(BitConverter.GetBytes(cooldown), 0, bytes, offset + 4, 4);
                    Array.Copy(BitConverter.GetBytes(limit), 0, bytes, offset + 8, 4);
                    index++;
                }
                Write(handle, remote, bytes);
                if (!Equal(Read(handle, remote, bytes.Length), bytes)) throw new InvalidOperationException("Remote split hook verification failed.");
                foreach (Site site in sites) site.Replacement = Jump(remote + site.Offset, site.Original.Length);
                session = new Session(handle, remote, sites, storage, document);
                try
                {
                    // Install the consumer and writer before exposing the row.
                    foreach (int i in new int[] { 2, 1, 0 }) Patch(handle, sites[i].Address, sites[i].Replacement);
                    Patch(handle, image + 0x7993d9e, Hex("9090"));
                    foreach (Site site in sites)
                        if (!Equal(Read(handle, site.Address, site.Replacement.Length), site.Replacement))
                            throw new InvalidOperationException("Installed split hook verification failed.");
                }
                catch { session.Restore(log); throw; }
                log("Installed native offense/defense cooldown and limit controls in PID " + game.Id + ". " + index + " per-dynasty defense configuration(s) loaded from " + storage + ".");
                if (clock != null) log("Verified Dynasty Play Clock profile hooks; normal settings writes chain to its existing writer.");
                handle = IntPtr.Zero;
                return session;
            }
            catch { if (session != null) { handle = IntPtr.Zero; session.Dispose(); } throw; }
            finally { if (handle != IntPtr.Zero) CloseHandle(handle); }
        }

        public sealed class SettingsDocument
        {
            public int FormatVersion { get; set; }
            public Dictionary<string, int> DefenseCooldowns { get; set; }
            public Dictionary<string, int> DefenseLimits { get; set; }
            public SettingsDocument() {
                FormatVersion = 2;
                DefenseCooldowns = new Dictionary<string, int>();
                DefenseLimits = new Dictionary<string, int>();
            }
        }

        private sealed class ClockHooks
        {
            internal byte[] ListOriginal, WriterDetour;
            internal long WriterTarget;
        }
        private static bool ClockAssemblyLoaded()
        {
#if COMBINED_DYNASTY_SETTINGS
            return true; // The Play Clock action is part of this same DLL.
#else
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetName().Name == "DynastyPlayClock") return true;
            return false;
#endif
        }
        private static ClockHooks CheckClockHooks(IntPtr handle, long image)
        {
            byte[] listOriginal = Hex("488b8f18040000ffc3488b01ff9010050000");
            byte[] list = Read(handle, image + 0x7993e6c, listOriginal.Length);
            byte[] writer = Read(handle, image + 0x79a2123, 14);
            if (Equal(list, listOriginal) && Equal(writer, Hex("83c0fe3ddb0000000f87ac270000"))) return null;
            if (list == null || writer == null || !Equal(Slice(list, 0, 6), Hex("ff2500000000"))
                || !Equal(Slice(writer, 0, 6), Hex("ff2500000000")))
                throw new InvalidOperationException("Unrecognized settings hook; no changes made.");
            long code = BitConverter.ToInt64(list, 6), writerTarget = BitConverter.ToInt64(writer, 6);
            if (writerTarget != code + 0x400 || !Equal(list, Jump(code, listOriginal.Length)))
                throw new InvalidOperationException("Unrecognized Play Clock hook layout.");
#if COMBINED_DYNASTY_SETTINGS
            // Trust our own loaded generator, then verify every byte of the
            // existing detours. No circular self-hash or external DLL is needed.
            Type type = typeof(DynastyPlayClock.DynastyPlayClockAction);
#else
            string core = Path.GetDirectoryName(typeof(ExecutionAction).Assembly.Location);
            string path = Path.Combine(core, "Plugins", "DynastyPlayClock.dll");
            string clockHash = File.Exists(path) ? Hash(File.ReadAllBytes(path)) : "";
            if (clockHash != "8c313cc1c65279fc5af6668960228caa4b1dbfa78892d55ecb4acf735891911b"
                && clockHash != "db814e07bae58f2da34cc919743d26cb1d4e410e2b083ce8b4e7e83a19c35386"
                && clockHash != "071970556d2d37710adaff63c67870c50ccae964bc3ade0211c632182aae3a6f")
                throw new InvalidOperationException("Settings hook is not a verified Dynasty Play Clock 0.1.1, 0.1.2 or 0.2.0 build.");
            Assembly known = Assembly.LoadFrom(path);
            Type type = known.GetType("DynastyPlayClock.DynastyPlayClockAction", true);
#endif
            byte[] probe = Read(handle, code, 0x100);
            int mov = -1;
            for (int i = 0; probe != null && i < probe.Length - 10; i++)
                if (probe[i] == 0x49 && probe[i + 1] == 0xba) { mov = i; break; }
            if (mov < 0) throw new InvalidOperationException("Cannot identify Play Clock descriptor address.");
            long data = BitConverter.ToInt64(probe, mov + 2);
            BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            byte[] expectedList = (byte[])type.GetMethod("BuildListUpdated", flags).Invoke(null, new object[] { image, data });
            byte[] expectedWriter = (byte[])type.GetMethod("BuildWriterUpdated", flags).Invoke(null, new object[] { image, data });
            if (!Equal(Read(handle, code, expectedList.Length), expectedList)
                || !Equal(Read(handle, writerTarget, expectedWriter.Length), expectedWriter))
                throw new InvalidOperationException("Existing Play Clock machine code does not match its verified generator.");
            return new ClockHooks { ListOriginal = listOriginal, WriterDetour = writer, WriterTarget = writerTarget };
        }
        private static byte[] Slice(byte[] source, int start, int length)
        { byte[] b = new byte[length]; Array.Copy(source, start, b, 0, length); return b; }

        private static HashSet<string> Ids(SettingsDocument document)
        {
            HashSet<string> ids = new HashSet<string>(document.DefenseCooldowns.Keys);
            ids.UnionWith(document.DefenseLimits.Keys);
            return ids;
        }
        private static void ValidateValues(Dictionary<string, int> values, int maximum)
        {
            foreach (KeyValuePair<string, int> setting in values)
            {
                uint id;
                if (!UInt32.TryParse(setting.Key, NumberStyles.None, CultureInfo.InvariantCulture, out id)
                    || id == 0 || id.ToString(CultureInfo.InvariantCulture) != setting.Key
                    || setting.Value < 0 || setting.Value > maximum)
                    throw new InvalidDataException("Invalid defense setting; settings file preserved.");
            }
        }
        private static SettingsDocument Load(string path)
        {
            if (!File.Exists(path)) return new SettingsDocument();
            SettingsDocument document = new JavaScriptSerializer().Deserialize<SettingsDocument>(File.ReadAllText(path));
            if (document == null || (document.FormatVersion != 1 && document.FormatVersion != 2)
                || document.DefenseCooldowns == null || document.DefenseLimits == null
                || (document.FormatVersion == 1 && document.DefenseLimits.Count != 0))
                throw new InvalidDataException("Unsupported defense settings file; preserved without modification.");
            ValidateValues(document.DefenseCooldowns, 10);
            ValidateValues(document.DefenseLimits, 20);
            if (Ids(document).Count > SplitHookCode.Capacity)
                throw new InvalidDataException("Too many distinct dynasty IDs; settings file preserved.");
            return document;
        }

        internal sealed class Session : IDisposable
        {
            private IntPtr handle;
            private readonly long remote;
            private readonly Site[] sites;
            private readonly string storage;
            private readonly SettingsDocument document;
            private uint savedSequence;
            internal Session(IntPtr h, long r, Site[] s, string path, SettingsDocument d)
            { handle = h; remote = r; sites = s; storage = path; document = d; }

#if COMBINED_DYNASTY_SETTINGS
            internal void PollCombined(Action<string> log) { SavePending(log); }
            internal long CombinedData { get { return remote; } }
            internal List<OfflineDynastySettings.MemoryCheck> CombinedChecks(long previousWriter)
            {
                List<OfflineDynastySettings.MemoryCheck> checks=new List<OfflineDynastySettings.MemoryCheck>();
                foreach(Site site in sites)checks.Add(new OfflineDynastySettings.MemoryCheck(site.Address,site.Replacement));
                checks.Add(new OfflineDynastySettings.MemoryCheck(sites[0].Address-2,Hex("9090")));
                byte[] code=SplitHookCode.Create(sites[0].Address-0x7993da0,remote,previousWriter);
                byte[] immutable=new byte[0x1400]; Array.Copy(code,immutable,immutable.Length);
                checks.Add(new OfflineDynastySettings.MemoryCheck(remote,immutable));
                return checks;
            }
            internal void VerifyCombined()
            {
                foreach (Site site in sites) {
                    byte[] current = Read(handle, site.Address, site.Replacement.Length);
                    if (current == null) throw new InvalidOperationException("Could not read installed playcall hook; retrying verification.");
                    if (!Equal(current, site.Replacement))
                        throw new OfflineDynastySettings.HookOwnershipException("A playcall hook was changed by another patch.");
                }
                byte[] gate = Read(handle, sites[0].Address - 2, 2);
                if (gate == null) throw new InvalidOperationException("Could not read playcall visibility gate; retrying verification.");
                if (!Equal(gate, Hex("9090"))) throw new OfflineDynastySettings.HookOwnershipException("The playcall visibility gate was changed.");
            }
#endif

            internal void Monitor(Process game, Action<string> log)
            {
                // Launch cancellation is no longer relevant after installation:
                // persistence must remain active for the game's lifetime.
                string control = Path.Combine(Path.GetDirectoryName(storage), "restore-request.txt");
                while (!game.HasExited)
                {
                    try { SavePending(log); }
                    catch (Exception ex) { log("Defense settings could not be saved: " + ex.Message); Thread.Sleep(1000); }
                    if (File.Exists(control))
                    {
                        SavePending(log);
                        Restore(log);
                        File.Delete(control);
                        return;
                    }
                    Thread.Sleep(100);
                    game.Refresh();
                }
                log("Game exited; split settings monitor ended.");
            }

            private void SavePending(Action<string> log)
            {
                byte[] seq = Read(handle, remote + SplitHookCode.SequenceOffset, 4);
                if (seq == null) return;
                uint current = BitConverter.ToUInt32(seq, 0);
                if (current == savedSequence) return;
                byte[] table = Read(handle, remote + SplitHookCode.TableOffset, SplitHookCode.Capacity * SplitHookCode.EntrySize);
                if (table == null) return;
                byte[] after = Read(handle, remote + SplitHookCode.SequenceOffset, 4);
                if (after == null || current != BitConverter.ToUInt32(after, 0)) return;
                SettingsDocument updated = new SettingsDocument();
                HashSet<uint> seen = new HashSet<uint>();
                for (int i = 0; i < SplitHookCode.Capacity; i++)
                {
                    int offset = i * SplitHookCode.EntrySize;
                    uint id = BitConverter.ToUInt32(table, offset);
                    if (id == 0) continue;
                    int cooldown = BitConverter.ToInt32(table, offset + 4);
                    int limit = BitConverter.ToInt32(table, offset + 8);
                    if (cooldown < -1 || cooldown > 10 || limit < -1 || limit > 20
                        || (cooldown == -1 && limit == -1) || !seen.Add(id))
                        throw new InvalidDataException("Invalid remote defense configuration.");
                    string key = id.ToString(CultureInfo.InvariantCulture);
                    if (cooldown >= 0) updated.DefenseCooldowns.Add(key, cooldown);
                    if (limit >= 0) updated.DefenseLimits.Add(key, limit);
                }
                string folder = Path.GetDirectoryName(storage);
                Directory.CreateDirectory(folder);
                string temp = storage + ".tmp";
                using (FileStream file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(file))
                {
                    writer.Write(new JavaScriptSerializer().Serialize(updated));
                    writer.Flush();
                    file.Flush(true);
                }
                if (File.Exists(storage)) File.Replace(temp, storage, storage + ".bak");
                else File.Move(temp, storage);
                document.FormatVersion = updated.FormatVersion;
                document.DefenseCooldowns = updated.DefenseCooldowns;
                document.DefenseLimits = updated.DefenseLimits;
                savedSequence = current;
                log("Saved per-dynasty defense cooldowns and limits; revision " + current + ".");
            }

            internal void Restore(Action<string> log)
            {
                foreach (Site site in sites)
                {
                    byte[] current = Read(handle, site.Address, site.Original.Length);
                    if (Equal(current, site.Replacement)) Patch(handle, site.Address, site.Original);
                    else if (!Equal(current, site.Original)) throw new InvalidOperationException("Split hook ownership changed; refused to overwrite it.");
                }
                foreach (Site site in sites)
                    if (!Equal(Read(handle, site.Address, site.Original.Length), site.Original))
                        throw new InvalidOperationException("Split hook restoration verification failed.");
                // Keep allocated code until process exit: a call could already
                // be executing there when its entry site is restored.
                log("Restored and verified all three split settings hook sites. Native 0.4.1 rows remain enabled.");
            }

            public void Dispose() { if (handle != IntPtr.Zero) { CloseHandle(handle); handle = IntPtr.Zero; } }
        }

        private sealed class Region
        {
            internal long Rva; internal int Size; internal string Hash;
            internal Region(long rva, int size, string hash) { Rva = rva; Size = size; Hash = hash; }
        }
        internal sealed class Site
        {
            internal long Address; internal int Offset; internal byte[] Original, Replacement;
            internal Site(long address, int offset, byte[] original) { Address = address; Offset = offset; Original = original; }
        }
        private static string DefaultStorage()
        { return Path.Combine(Path.GetDirectoryName(typeof(SplitSettingsAction).Assembly.Location), "OfflinePlaycallSettings", "dynasties.json"); }
        private static void Log(ILogger logger, string message)
        {
            lock (logLock)
            {
                try { if (logger != null) logger.Log("[Offline Playcall split] " + message, new object[0]); } catch { }
#if COMBINED_DYNASTY_SETTINGS
                try { File.AppendAllText(OfflineDynastySettings.SettingsFiles.PathFor(Path.GetDirectoryName(typeof(SplitSettingsAction).Assembly.Location), "OfflinePlaycall.log"), DateTime.Now.ToString("O", CultureInfo.InvariantCulture) + " " + message + Environment.NewLine); } catch { }
#else
                try { File.AppendAllText(Path.Combine(Path.GetDirectoryName(typeof(SplitSettingsAction).Assembly.Location), "OfflinePlaycall.log"), DateTime.Now.ToString("O", CultureInfo.InvariantCulture) + " " + message + Environment.NewLine); } catch { }
#endif
            }
        }
        private static Process FindGame()
        {
            Process newest = null;
            foreach (Process p in Process.GetProcessesByName("CollegeFB27"))
            {
                try { if (!p.HasExited && (newest == null || p.StartTime > newest.StartTime)) { if (newest != null) newest.Dispose(); newest = p; } else p.Dispose(); }
                catch { p.Dispose(); }
            }
            return newest;
        }
        private static bool Interactive(Process p)
        {
            IntPtr window = GetForegroundWindow();
            uint pid;
            if (window == IntPtr.Zero || !IsWindowVisible(window) || IsIconic(window)) return false;
            GetWindowThreadProcessId(window, out pid);
            if (pid != p.Id) return false;
            Rectangle bounds;
            return GetClientRect(window, out bounds) && bounds.Right - bounds.Left >= 1024 && bounds.Bottom - bounds.Top >= 600;
        }
        private static byte[] Read(IntPtr h, long address, int length)
        {
            byte[] bytes = new byte[length]; IntPtr count;
            return ReadProcessMemory(h, new IntPtr(address), bytes, new IntPtr(length), out count) && count.ToInt64() == length ? bytes : null;
        }
        private static void Write(IntPtr h, long address, byte[] bytes)
        {
            IntPtr count;
            if (!WriteProcessMemory(h, new IntPtr(address), bytes, new IntPtr(bytes.Length), out count) || count.ToInt64() != bytes.Length) throw WinError("WriteProcessMemory");
            if (!FlushInstructionCache(h, new IntPtr(address), new UIntPtr((uint)bytes.Length))) throw WinError("FlushInstructionCache");
        }
        private static void Patch(IntPtr h, long address, byte[] bytes)
        {
            uint old;
            if (!VirtualProtectEx(h, new IntPtr(address), new UIntPtr((uint)bytes.Length), 0x40, out old)) throw WinError("VirtualProtectEx");
            try { Write(h, address, bytes); }
            finally { uint ignored; VirtualProtectEx(h, new IntPtr(address), new UIntPtr((uint)bytes.Length), old, out ignored); }
        }
        private static byte[] Jump(long address, int length)
        {
            if (length < 14) throw new ArgumentOutOfRangeException("length");
            byte[] bytes = new byte[length];
            for (int i = 14; i < length; i++) bytes[i] = 0x90;
            bytes[0] = 0xff; bytes[1] = 0x25;
            Array.Copy(BitConverter.GetBytes(address), 0, bytes, 6, 8);
            return bytes;
        }
        private static bool Equal(byte[] a, byte[] b)
        { if (a == null || b == null || a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
        private static byte[] Hex(string s)
        { byte[] b = new byte[s.Length / 2]; for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16); return b; }
        private static string Hash(byte[] b)
        { using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(b)).Replace("-", "").ToLowerInvariant(); }
        private static Exception WinError(string name)
        { return new InvalidOperationException(name + " failed: " + Marshal.GetLastWin32Error()); }
        [StructLayout(LayoutKind.Sequential)] private struct Rectangle { internal int Left, Top, Right, Bottom; }
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint rights, bool inherit, int pid);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadProcessMemory(IntPtr h, IntPtr address, byte[] bytes, IntPtr length, out IntPtr count);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool WriteProcessMemory(IntPtr h, IntPtr address, byte[] bytes, IntPtr length, out IntPtr count);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr VirtualAllocEx(IntPtr h, IntPtr address, UIntPtr size, uint type, uint protection);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool VirtualProtectEx(IntPtr h, IntPtr address, UIntPtr size, uint protection, out uint old);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool FlushInstructionCache(IntPtr h, IntPtr address, UIntPtr size);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out Rectangle rectangle);
    }
}

