# Extra Settings 0.2.10 - standalone preview

Compared with the last published standalone release, **v0.2.1**.

- **A clearer name:** Offline Dynasty Settings is now Extra Settings.
- **Clocks belong to each Dynasty:** offensive and defensive clock choices save by verified LeagueID. New Dynasties begin with offensive On and defensive 15 seconds. Old global files are retained, but their choices are not copied into every Dynasty.
- **Shorter defensive choices:** 3 and 5 seconds join 15, 20, 30, 45 and 60 seconds, plus Off. Offensive On, Off At Playcall, Off and 60 Seconds remain available.
- **Settings and menu compatibility:** retains the current COT game-settings rows and deferred Controller Select bridge, including its exact supported controller/ownership checks.
- **Safer persistence and diagnostics:** the settings-saving helper is checked against a compiled-in SHA256, and old preference/log files stay in the module's data folder.
- **One-DLL download:** place `ExtraSettings.dll` in Plugins. Its matching helper is embedded and prepared automatically; settings are not shipped as defaults that overwrite your choices.

Offensive/defensive playcall cooldowns and limits are retained features, not new additions in this release. Find everything under Dynasty > League Settings > Game Settings; start a fresh game after changing gameplay options.

Remove old `OfflineDynastySettings.dll`, `DynastyPlayClock.dll` and `OfflinePlaycall.dll` copies from Plugins, keeping all data folders. If keeping COT, disable its Extra Settings module before using the standalone. Current Beyond the Field and Expanded Practice retain the corresponding shared-menu/controller contracts; older or modified hooks can be rejected.

Validation: all-four packaging/coexistence and COT install/uninstall fixtures passed; 86 per-Dynasty profile checks cover all 32 clock pairs, isolation/reload/invalid data/backups, and 54 preference/launch-coordinator checks passed. The helper matches COT 1.0.2 byte-for-byte. **This new wrapper is a preview: clean-launch live acceptance remains pending.** No live installation was performed for this build.

Source comparison: old tag `v0.2.1`, commit `51f48a5dac0ed23fb542597aa96d7b4ac2d8a31c`; current maintained source is pinned in `source-release.json`. Checked changes include `DynastyClockProfiles.cs`, `ClockPreferences.cs`, `GameSettingsMenuRows.cs`, `ControllerSelect.cs`, `PersistenceController.cs` and `RuntimeFiles.cs`.
