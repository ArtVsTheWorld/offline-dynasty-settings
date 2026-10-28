param([string]$MmcRoot=$env:MMC_ROOT)
$ErrorActionPreference = 'Stop'
if(!$MmcRoot){throw 'Pass -MmcRoot with your MMC folder, or set MMC_ROOT.'}
foreach($name in @('FrostyCore.dll','FrostySdk.dll')){if(!(Test-Path -LiteralPath (Join-Path $MmcRoot $name))){throw "Missing $name in MmcRoot"}}
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = Split-Path -Parent $here
$versionSource=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'AssemblyInfo.cs') -Raw
if($versionSource -notmatch 'PluginVersion\("([^"]+)"\)'){throw 'Plugin version missing'}
$version=$Matches[1]
$output = Join-Path $workspace ("Release\OfflineDynastySettings-$version\OfflineDynastySettings.dll")
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Path (Split-Path -Parent $output) -Force | Out-Null
$helper = Join-Path $here '..\Evidence\OfflineDynastySettings.Persistence.exe'
New-Item -ItemType Directory -Path (Split-Path -Parent $helper) -Force | Out-Null
& $compiler /nologo /target:winexe /platform:x64 /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/out:$helper" (Join-Path $here 'PersistenceHelper.cs') (Join-Path $here 'PersistenceContracts.cs') (Join-Path $here 'ClockPreferences.cs') (Join-Path $here 'DiagnosticLog.cs')
if ($LASTEXITCODE -ne 0) { throw "Helper compilation failed: $LASTEXITCODE" }
$sources = @(
    (Join-Path $here 'AssemblyInfo.cs'),
    (Join-Path $here 'CombinedSettingsAction.cs'),
    (Join-Path $here 'LaunchCoordinator.cs'),
    (Join-Path $here 'CombinedClockRuntime.cs'),
    (Join-Path $here 'GameSettingsMenuRows.cs'),
    (Join-Path $here 'DefensivePlayClock.cs'),
    (Join-Path $here 'ControllerSelect.cs'),
    (Join-Path $here 'ControllerSelectCode.cs'),
    (Join-Path $here 'ClockPreferences.cs'),
    (Join-Path $here 'DiagnosticLog.cs'),
    (Join-Path $here 'PersistenceContracts.cs'),
    (Join-Path $here 'PersistenceController.cs'),
    (Join-Path $workspace 'Shared\Clock\DynastyPlayClockAction.cs'),
    (Join-Path $workspace 'Shared\Clock\GameLayouts.cs'),
    (Join-Path $workspace 'Shared\Clock\ExtendedClock.cs'),
    (Join-Path $workspace 'Shared\Playcall\SplitSettingsAction.cs'),
    (Join-Path $workspace 'Shared\Playcall\SplitHookCode.cs')
)
& $compiler /nologo /target:library /platform:x64 /define:COMBINED_DYNASTY_SETTINGS /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/resource:$helper,OfflineDynastySettings.Persistence.exe" "/out:$output" "/reference:$(Join-Path $MmcRoot 'FrostyCore.dll')" "/reference:$(Join-Path $MmcRoot 'FrostySdk.dll')" @sources
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $LASTEXITCODE" }
