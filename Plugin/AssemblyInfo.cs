using System.Reflection;
using System.Runtime.InteropServices;
using Frosty.Core.Attributes;
[assembly: AssemblyTitle("Offline Dynasty Settings")]
[assembly: AssemblyDescription("Game settings and deferred Controller Select in offline Dynasty")]
[assembly: AssemblyProduct("Offline Dynasty Settings")]
[assembly: AssemblyVersion("0.2.8.0")]
[assembly: AssemblyFileVersion("0.2.8.0")]
[assembly: PluginAuthor("Ace")]
[assembly: PluginVersion("0.2.8")]
[assembly: PluginDisplayName("Offline Dynasty Settings")]
[assembly: RegisterExecutionAction(typeof(OfflineDynastySettings.CombinedSettingsAction))]
[assembly: ComVisible(false)]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("LaunchChecks")]
