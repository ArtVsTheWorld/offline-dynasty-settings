# **November 17 Update**

**Practice Injuries**
- Added **Practice Injuries** under **League Settings > Game Settings**:
  - **Default** keeps the game's normal practice-injury rules.
  - **Off for QBs** prevents new weekly-practice injuries for quarterbacks on every team.
  - **Off** prevents new weekly-practice injuries for everyone.
- Each Dynasty keeps its own choice. Existing and new profiles start on **Default**; your clocks and other settings carry forward.
- The Weekly Practice Plan injury-risk preview follows the selected protection. Existing injuries, game injuries, reps, XP and fatigue stay unchanged.

**Testing**
- Automated checks passed for all 21 positions, the risk preview, saving/reloading, menu choices and existing formation substitutions. Actual weekly-advance and installed-menu acceptance for this feature remain pending.

**Install**
- Close MMC and the game, then update to **0.2.22** in Dynasty COT or replace **ExtraSettings.dll** in your usual MMC **Plugins** folder. Keep the other standalone modules and your existing user data.

---

# **November 17 Update**

**Game Update Compatibility**
- Restored the extra Dynasty settings rows, play clocks, playcall rules and offline Controller Select for the November 17 game update. Existing league settings carry forward.

**Special Teams Formation Subs**
- **Kickoff** and **Kickoff Return** now appear automatically under **Special Teams** in supported offline Dynasty Formation Subs. Kickoff is on offense and Kickoff Return is on defense. The return preview fits the full unit on the field.
- If you downloaded the first 0.2.21 build, download it again or run **Install / Update** in Dynasty COT. The revised build ignores any old FormationSubs.ini and leaves it on disk.

**Install**
- Close MMC and the game, then update to **0.2.21** in Dynasty COT or replace **ExtraSettings.dll** in your usual MMC **Plugins** folder.

---

# Aceâ€™s Extra Settings 0.2.12

Changes since public release 0.2.10.

- Your Windows user folder now holds the settings and reports. Copies of MMC on the same account can share your choices, including when the MMC program folder has limited write access.
- The report now tells you whether Extra Settings is waiting for an offline Dynasty, which controls are ready and where a setting could not be saved. It is easier to tell a startup problem from a saving problem.
- The league-specific clocks from 0.2.10 carry forward. Fresh leagues still start with the offensive clock On, the defensive clock at 15 seconds and the four playcall restrictions off.

Download the standalone ZIP, close MMC and the game, and copy **ExtraSettings.dll** into MMCâ€™s **Plugins** folder. The ZIP includes a full feature list and an illustrated player guide.

---

# Extra Settings 0.2.10 - standalone release

Compared with the last published standalone release, **v0.2.1**.

- **A clearer name:** Offline Dynasty Settings is now Extra Settings.
- **Clocks belong to each Dynasty:** offensive and defensive clock choices save by verified LeagueID. New Dynasties begin with offensive On and defensive 15 seconds. Old global files are retained, but their choices are not copied into every Dynasty.
- **Shorter defensive choices:** 3 and 5 seconds join 15, 20, 30, 45 and 60 seconds, plus Off. Offensive On, Off At Playcall, Off and 60 Seconds remain available.
- **Settings and menu compatibility:** retains the current COT game-settings rows and deferred Controller Select bridge, including its exact supported controller/ownership checks.
- **Safer persistence and diagnostics:** the settings-saving helper is checked against a compiled-in SHA256, and old preference/log files stay in the module's data folder.
- **One-DLL download:** place `ExtraSettings.dll` in Plugins. Its matching helper is embedded and prepared automatically; settings are not shipped as defaults that overwrite your choices.

Offensive/defensive playcall cooldowns and limits are retained features, not new additions in this release. Find everything under Dynasty > League Settings > Game Settings; start a fresh game after changing gameplay options.

Remove old `OfflineDynastySettings.dll`, `DynastyPlayClock.dll` and `OfflinePlaycall.dll` copies from Plugins, keeping all data folders. If keeping COT, disable its Extra Settings module before using the standalone. Current Beyond the Field and Expanded Practice retain the corresponding shared-menu/controller contracts; older or modified hooks can be rejected.

Validation: all-four packaging/coexistence and COT install/uninstall fixtures passed; 86 per-Dynasty profile checks cover all 32 clock pairs, isolation/reload/invalid data/backups, and 54 preference/launch-coordinator checks passed. The helper matches COT 1.0.2 byte-for-byte. **Ace reported all four standalone releases verified working on November 9, 2026.** No live installation was performed for this build.

Source comparison: old tag `v0.2.1`, commit `51f48a5dac0ed23fb542597aa96d7b4ac2d8a31c`; current maintained source is pinned in `source-release.json`. Checked changes include `DynastyClockProfiles.cs`, `ClockPreferences.cs`, `GameSettingsMenuRows.cs`, `ControllerSelect.cs`, `PersistenceController.cs` and `RuntimeFiles.cs`.

Stable promotion: the ZIP and DLL are unchanged from the tested preview. Enclosed preview labels describe the build-time status. Acceptance is user-reported for the tested setup; other game/overhaul combinations are not automatically certified.
