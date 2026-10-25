using System;
using System.IO;
using System.Reflection;
using System.Threading;
class MmcHostChecks
{
    static string root;
    static int Main(string[] args)
    {
        try {
            root = Path.GetFullPath(args[0]);
            AppDomain.CurrentDomain.AssemblyResolve += (sender, request) => {
                string name = new AssemblyName(request.Name).Name + ".dll";
                foreach (string folder in new string[] { root, Path.Combine(root, "ThirdParty"), Path.Combine(root, "Plugins") }) {
                    string path = Path.Combine(folder, name); if (File.Exists(path)) return Assembly.LoadFrom(path);
                }
                return null;
            };
            Assembly core = Assembly.LoadFrom(Path.Combine(root, "FrostyCore.dll"));
            Type execution = core.GetType("Frosty.Core.ExecutionAction", true);
            Type manager = core.GetType("Frosty.Core.PluginManagerType", true);
            object mode = Enum.Parse(manager, "ModManager");
            Assembly plugin = Assembly.LoadFrom(Path.GetFullPath(args[1]));
            Type action = plugin.GetType("OfflineDynastySettings.CombinedSettingsAction", true);
            if (!execution.IsAssignableFrom(action)) throw new Exception("ExecutionAction ABI mismatch.");
            object instance = Activator.CreateInstance(action);
            foreach (string property in new string[] { "PreLaunchAction", "PostLaunchAction" }) {
                Delegate callback = (Delegate)action.GetProperty(property).GetValue(instance, null);
                if (callback == null) throw new Exception("Null callback: " + property);
                ParameterInfo[] p = callback.Method.GetParameters();
                if (p.Length != 3 || p[0].ParameterType.FullName != "FrostySdk.Interfaces.ILogger"
                    || p[1].ParameterType != manager || p[2].ParameterType != typeof(CancellationToken)) throw new Exception("Callback ABI mismatch.");
                // ABI inspection must not start a production watcher against a live game.
                Console.WriteLine("PASS " + property + " resolves with host ABI");
            }
            bool registered = false;
            foreach (CustomAttributeData attr in plugin.GetCustomAttributesData())
                if (attr.AttributeType.Name.StartsWith("RegisterExecutionAction") && (Type)attr.ConstructorArguments[0].Value == action) registered = true;
            if (!registered) throw new Exception("Registration did not resolve.");
            Console.WriteLine("PASS execution registration resolves");
            if (args.Length > 2) {
                Assembly stadium = Assembly.LoadFrom(Path.GetFullPath(args[2]));
                Type t = stadium.GetType("StadiumViewerPlugin.StadiumExecutionAction", true);
                Delegate callback = (Delegate)t.GetProperty("PostLaunchAction").GetValue(Activator.CreateInstance(t), null);
                if (callback == null) throw new Exception("Stadium fix still returns null.");
                callback.DynamicInvoke(null, mode, CancellationToken.None);
                Console.WriteLine("PASS Stadium no-op post-launch executes");
            }
            Console.WriteLine("PASS host " + root + " | " + core.FullName);
            return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

