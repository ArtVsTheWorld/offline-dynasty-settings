$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$source=Get-Content -LiteralPath (Join-Path $root 'Plugin/AssemblyInfo.cs') -Raw
if($source -notmatch 'PluginVersion\("([^"\r\n]+)"\)'){throw 'Plugin version missing'}
$version=$Matches[1]
$folder=Join-Path $root ("Release/OfflineDynastySettings-"+$version)
$dll=Join-Path $folder 'OfflineDynastySettings.dll'
if(!(Test-Path -LiteralPath $dll)){throw 'Build the DLL first with Build.ps1.'}
if([Diagnostics.FileVersionInfo]::GetVersionInfo($dll).FileVersion -ne ($version+'.0')){throw 'DLL version differs from source'}
Copy-Item -LiteralPath (Join-Path $root 'docs/README-release.txt') -Destination (Join-Path $folder 'README.txt') -Force
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$destination=Join-Path $root ("Release/OfflineDynastySettings-"+$version+'.zip')
$temporary=$destination+'.'+[Guid]::NewGuid().ToString('N')+'.tmp'
try {
    $zip=[IO.Compression.ZipFile]::Open($temporary,[IO.Compression.ZipArchiveMode]::Create)
    try {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$dll,'OfflineDynastySettings.dll') | Out-Null
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $folder 'README.txt'),'README.txt') | Out-Null
    } finally {$zip.Dispose()}
    if(Test-Path -LiteralPath $destination){[IO.File]::Replace($temporary,$destination,[System.Management.Automation.Language.NullString]::Value)}
    else {[IO.File]::Move($temporary,$destination)}
} finally {if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary -Force}}
Write-Host "Package: $destination"
