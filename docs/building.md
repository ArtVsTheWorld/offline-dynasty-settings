# Building

Use 64-bit Windows with .NET Framework 4.x and a matching MMC Mod Manager install.
The build uses MMC's `FrostyCore.dll` and `FrostySdk.dll`. They stay on your computer
and are not included in this repository. No extra DLL goes in the release ZIP.

```powershell
.\Build.ps1 -MmcRoot "C:\Path\To\MMC"
.\Test.ps1
.\Package.ps1
```

You can set `MMC_ROOT` instead of passing `-MmcRoot`. Build output goes into
`Release/`; test output goes into `Evidence/`. Both folders are ignored by Git.

The package command checks the DLL version and makes a ZIP containing only the
plugin DLL and a short `README.txt`. It does not install or launch anything.

`Test.ps1` without an SDK checks preferences and launch coordination. GitHub runs
these tests and checks that the native manifest matches the source.

After building with the real MMC SDK, run the full local checks:

```powershell
.\Test.ps1 -MmcRoot "C:\Path\To\MMC"
python -m pip install -r requirements-dev.txt
.\Test.ps1 -MmcRoot "C:\Path\To\MMC" -NativeChecks -BaselineImage "private/working.pe.bin"
```

Full checks cover menu writers, saved-value reload, hook ownership, launch callbacks,
the bundled settings helper's preference writes, and the generated native timer/UI
gates. The baseline image stays local. Tests do not patch the actual game.

`Tools/HelperChecks.cs` is an optional deeper process-lifecycle harness. It creates a
CollegeFB27-named test process. Close both MMC and the game before using it; it is
not part of Test.ps1 or GitHub checks. Default helper preference tests do not create
that process.

To regenerate the GAME SETTINGS menu block, run
`python Tools/build_game_settings_menu.py` and review the generated C# diff. Normal
builds use the checked-in generated source and do not need Python.
