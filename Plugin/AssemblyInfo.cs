using System.Reflection;
using System.Runtime.InteropServices;
using Frosty.Core.Attributes;
[assembly: AssemblyTitle("Offline Dynasty Settings")]
[assembly: AssemblyDescription("Play Clock and offensive/defensive playcall cooldowns and limits in offline Dynasty")]
[assembly: AssemblyProduct("Offline Dynasty Settings")]
[assembly: AssemblyVersion("0.2.6.0")]
[assembly: AssemblyFileVersion("0.2.6.0")]
[assembly: PluginAuthor("Ace")]
[assembly: PluginVersion("0.2.6")]
[assembly: PluginDisplayName("Offline Dynasty Settings")]
[assembly: RegisterExecutionAction(typeof(OfflineDynastySettings.CombinedSettingsAction))]
[assembly: ComVisible(false)]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("LaunchChecks")]
