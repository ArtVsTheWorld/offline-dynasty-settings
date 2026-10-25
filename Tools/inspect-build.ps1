param([string]$MmcRoot=$env:MMC_ROOT, [string]$Version, [string]$EvidenceFolder = 'Evidence')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if(!$MmcRoot){throw 'Pass -MmcRoot or set MMC_ROOT.'}
if(!$Version){
    $source=Get-Content -LiteralPath (Join-Path $root 'Plugin/AssemblyInfo.cs') -Raw
    if($source -notmatch 'PluginVersion\("([^"\r\n]+)"\)'){throw 'Plugin version missing'}
    $Version=$Matches[1]
}
New-Item -ItemType Directory -Path (Join-Path $root $EvidenceFolder) -Force | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $MmcRoot 'FrostySdk.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $MmcRoot 'FrostyCore.dll')) | Out-Null
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $root "Release\OfflineDynastySettings-$Version\OfflineDynastySettings.dll"))
$type = $assembly.GetType('DynastyPlayClock.DynastyPlayClockAction', $true)
$flags = [Reflection.BindingFlags]'NonPublic,Static'
foreach ($name in @('BuildList', 'BuildWriter', 'BuildRuntime', 'BuildListUpdated', 'BuildWriterUpdated', 'BuildRuntimeUpdated', 'BuildEnum', 'BuildEnumUpdated', 'BuildDuration', 'BuildDurationUpdated', 'BuildCap', 'BuildCapUpdated', 'BuildCpu', 'BuildCpuUpdated', 'BuildTransition', 'BuildTransitionUpdated', 'BuildData')) {
    $argsForMethod = if ($name -eq 'BuildData') { [object[]]@([long]0x200000000) } else { [object[]]@([long]0x140000000, [long]0x200000000) }
    $bytes = $type.GetMethod($name, $flags).Invoke($null, $argsForMethod)
    [IO.File]::WriteAllBytes((Join-Path $root "$EvidenceFolder\$name.bin"), $bytes)
    "$name`: $($bytes.Length) bytes"
}
$assembly.GetCustomAttributesData() | Where-Object { $_.AttributeType.Name -in @('PluginDisplayNameAttribute','PluginVersionAttribute','RegisterExecutionActionAttribute') } | ForEach-Object { $_.ToString() }
