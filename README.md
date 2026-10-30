# Offline Dynasty Settings

By Ace. Adds game settings to offline Dynasty in College Football 27.

Updated for the October 29, 2026 game patch.

Also adds **Pause > Options > Controller Select** during offline Dynasty games.
Move your controller to either team and confirm. You keep your current team for
the rest of that play, then take over the other team at the next play-selection
screen. The CPU takes over the team you leave. No timeout is needed.

Team switching currently supports one controller with full-team control. It is
not enabled for practice, online games, multiple users, or player-locked control.

## Install

Download the ZIP from [Releases](https://github.com/ArtVsTheWorld/offline-dynasty-settings/releases).
Close the game and MMC, copy `OfflineDynastySettings.dll` into MMC's `Plugins`
folder, then restart MMC and launch the game through it. Allow about 45 seconds
for the plugin to load.

Open **Dynasty > League Settings > GAME SETTINGS**, above Clock Management.

- **Offensive Playcall Cooldown:** 0-10 plays before reusing a play.
- **Offensive Playcall Limit:** 0-20 uses per game.
- **Offensive Play Clock:** On, Off At Playcall, Off, or 60 Seconds.
- **Defensive Playcall Cooldown:** 0-10 plays before reusing a play.
- **Defensive Playcall Limit:** 0-20 uses per game.
- **Defensive Play Clock:** 3, 5, 15, 20, 30, 45, 60 Seconds, or Off.

New settings start with both cooldowns and limits **Off**, Offensive Play Clock
**On**, and Defensive Play Clock **15 Seconds**. Existing saved choices are kept.

Saved 3- and 5-second defensive clock choices keep their values.

A cooldown or limit of 0 disables that restriction. They save separately for each
Dynasty. Start a new game after changing them. Both clock choices apply across
offline Dynasties. Offensive and defensive clocks work independently.

Logs and both INI files are stored in `Plugins/OfflineDynastySettings/`.
Existing loose files move there automatically when updating, with your clock
choices preserved. Keep the `OfflinePlaycallSettings/` folder for per-Dynasty values. Remove older duplicate
DLLs and the separate DynastyPlayClock/OfflinePlaycall plugins. Disable experimental
Offline Dynasty Settings fbmods.

Logging is automatic. Bug reports should include `Plugins/OfflineDynastySettings/OfflineDynastySettings.log`
and what happened.

## Source

Current version: **0.2.9**.

Includes the fix for settings changes not saving after the Controller Select update.

Thanks to Sdmart3 for the Controller Select reference source for this patch.

[Build and test](docs/building.md) | [Updating after an EA patch](docs/updating.md)
