param([string]$MmcRoot=$env:MMC_ROOT)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Plugin/build.ps1') -MmcRoot $MmcRoot
if($LASTEXITCODE -ne 0){throw 'Build failed'}
