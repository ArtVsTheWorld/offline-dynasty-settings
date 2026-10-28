param([string]$MmcRoot,[switch]$NativeChecks,[string]$BaselineImage,[string]$Python='python')
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Push-Location $root
try {
    New-Item -ItemType Directory -Path Evidence/Checks -Force | Out-Null
    & $compiler /nologo /platform:x64 '/out:Evidence\Checks\CoreChecks.exe' Plugin\LaunchCoordinator.cs Plugin\ClockPreferences.cs Tools\CoreChecks.cs
    if($LASTEXITCODE -ne 0){throw 'Core check compilation failed'}
    & ./Evidence/Checks/CoreChecks.exe (Join-Path $root 'Evidence/Checks/core-settings')
    if($LASTEXITCODE -ne 0){throw 'Core checks failed'}
    if($MmcRoot) {
        $MmcRoot=[IO.Path]::GetFullPath($MmcRoot)
        $versionSource=Get-Content -LiteralPath Plugin/AssemblyInfo.cs -Raw
        if($versionSource -notmatch 'PluginVersion\("([^"\r\n]+)"\)'){throw 'Plugin version missing'}
        $version=$Matches[1]
        $dll=Join-Path $root ("Release/OfflineDynastySettings-$version/OfflineDynastySettings.dll")
        if(!(Test-Path -LiteralPath $dll)){throw 'Build the release DLL first.'}
        function Compile-Check([string]$Source,[string]$Output) {
            $Source=[IO.Path]::GetFullPath($Source.Replace('/','\'));$Output=[IO.Path]::GetFullPath($Output.Replace('/','\'))
            $dir=Split-Path -Parent $Output;New-Item -ItemType Directory -Path $dir -Force | Out-Null
            & $compiler /nologo /platform:x64 /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/reference:$dll" "/reference:$(Join-Path $MmcRoot 'FrostyCore.dll')" "/reference:$(Join-Path $MmcRoot 'FrostySdk.dll')" "/out:$Output" $Source
            if($LASTEXITCODE -ne 0){throw "Check compilation failed: $Source"}
        }
        foreach($name in @('GameSettingsMenuChecks','ClockMigrationChecks','IntegrationChecks','MmcHostChecks')) {
            $exe=Join-Path $root ("Evidence/Checks/$name.exe")
            Compile-Check ("Tools/$name.cs") $exe
            if($name -eq 'GameSettingsMenuChecks'){& $exe $dll $MmcRoot (Join-Path $root 'Evidence/Checks/menu')}
            elseif($name -eq 'ClockMigrationChecks'){& $exe $MmcRoot $dll (Join-Path $root 'Evidence/Checks/migration')}
            else {& $exe $MmcRoot $dll}
            if($LASTEXITCODE -ne 0){throw "Check failed: $name"}
        }
        $launch=Join-Path $root 'Evidence/Checks/lifecycle/LaunchChecks.exe'
        Compile-Check Tools/LaunchChecks.cs $launch
        & $launch $MmcRoot $dll (Join-Path $root 'Evidence/Checks/lifecycle/fixture')
        if($LASTEXITCODE -ne 0){throw 'Launch lifecycle checks failed'}
        $diagnostics=Join-Path $root 'Evidence/Checks/diagnostics/LaunchChecks.exe'
        Compile-Check Tools/DiagnosticChecks.cs $diagnostics
        Copy-Item -LiteralPath $dll -Destination (Join-Path (Split-Path -Parent $diagnostics) 'OfflineDynastySettings.dll') -Force
        & $diagnostics (Join-Path $root 'Evidence/Checks/diagnostics/fixture')
        if($LASTEXITCODE -ne 0){throw 'Diagnostics and helper preference checks failed'}
        & ./Tools/export_controller_select.ps1 -Version $version -MmcRoot $MmcRoot
        if($LASTEXITCODE -ne 0){throw 'Controller bridge export failed'}
        foreach($check in @('test_controller_deferred.py','test_controller_eligibility.py')) {
            & $Python (Join-Path $root "Tools/$check") (Join-Path $root "Evidence/controller-checks-$version")
            if($LASTEXITCODE -ne 0){throw "Controller checks failed: $check"}
        }
        if($NativeChecks) {
            if(!$BaselineImage){throw 'Native settings checks require -BaselineImage with your local decrypted capture.'}
            $env:CFB_BASELINE_IMAGE=[IO.Path]::GetFullPath($BaselineImage)
            & ./Tools/export_defensive_clock.ps1 -Version $version -MmcRoot $MmcRoot
            if($LASTEXITCODE -ne 0){throw 'Native hook export failed'}
            & $Python Tools/test_defensive_clock.py $version
            if($LASTEXITCODE -ne 0){throw 'Native countdown/UI gate checks failed'}
        }
    } elseif($NativeChecks) {throw 'Pass -MmcRoot and build the DLL before native checks.'}
} finally {Pop-Location}
