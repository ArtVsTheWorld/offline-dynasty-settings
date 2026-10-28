param([string]$Version,[string]$MmcRoot=$env:MMC_ROOT)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
[Reflection.Assembly]::LoadFrom((Join-Path $MmcRoot 'FrostySdk.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $MmcRoot 'FrostyCore.dll')) | Out-Null
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $root "Release/OfflineDynastySettings-$Version/OfflineDynastySettings.dll"))
$type=$assembly.GetType('DynastyPlayClock.DynastyPlayClockAction',$true)
$flags=[Reflection.BindingFlags]'NonPublic,Static'
$folder=Join-Path $root "Evidence/controller-checks-$Version"
New-Item -ItemType Directory -Path $folder -Force | Out-Null
foreach($name in @('Queue','Boundary','Apply','Eligibility')) {
    $bytes=$type.GetMethod("BuildController$name",$flags).Invoke($null,[object[]]@([long]0x140000000,[long]0x210000000,[long]0x210000c00))
    [IO.File]::WriteAllBytes((Join-Path $folder "$name.bin"),$bytes)
    "Controller $name`: $($bytes.Length) bytes"
}
