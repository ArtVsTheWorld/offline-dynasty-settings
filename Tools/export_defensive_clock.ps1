param([string]$Version='0.2.1',[string]$MmcRoot=$env:MMC_ROOT)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$mmc=$MmcRoot
[Reflection.Assembly]::LoadFrom((Join-Path $mmc 'FrostySdk.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $mmc 'FrostyCore.dll')) | Out-Null
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $root "Release\OfflineDynastySettings-$Version\OfflineDynastySettings.dll"))
$type=$assembly.GetType('DynastyPlayClock.DynastyPlayClockAction',$true)
$flags=[Reflection.BindingFlags]'NonPublic,Static'
$folder=Join-Path $root "Evidence\game-settings-$Version"
New-Item -ItemType Directory -Path $folder -Force | Out-Null
foreach($name in @('BuildCombinedDefensiveTimer','BuildCombinedSectionClockRow','BuildCombinedDefensiveClockRow','BuildWriterUpdated','BuildDurationUpdated','BuildEnumUpdated')) {
    $bytes=$type.GetMethod($name,$flags).Invoke($null,[object[]]@([long]0x140000000,[long]0x200000000))
    [IO.File]::WriteAllBytes((Join-Path $folder "$name.bin"),$bytes)
    "$name`: $($bytes.Length) bytes"
}
$bytes=$type.GetMethod('BuildData',$flags).Invoke($null,[object[]]@([long]0x200000000))
[IO.File]::WriteAllBytes((Join-Path $folder 'BuildData.bin'),$bytes)
$assembly.GetCustomAttributesData() | Where-Object {$_.AttributeType.Name -in @('PluginAuthorAttribute','PluginVersionAttribute')} | ForEach-Object {$_.ToString()}
