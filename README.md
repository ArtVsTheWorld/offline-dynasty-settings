# Offline Dynasty Settings

By Ace. Adds game settings to offline Dynasty in College Football 27.

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
- **Defensive Play Clock:** 15, 20, 30, 45, 60 Seconds, or Off.

A cooldown or limit of 0 disables that restriction. They save separately for each
Dynasty. Start a new game after changing them. Both clock choices apply across
offline Dynasties. Offensive and defensive clocks work independently.

Keep existing settings files when updating: `DynastyPlayClock.ini`,
`OfflineDynastySettings.ini`, and `OfflinePlaycallSettings/`. Remove older duplicate
DLLs and the separate DynastyPlayClock/OfflinePlaycall plugins. Disable experimental
Offline Dynasty Settings fbmods.

Logging is automatic. Bug reports should include `Plugins/OfflineDynastySettings.log`
and what happened.

## Source

Current version: **0.2.1**. The defensive Off/visibility changes have passed local
checks; in-game confirmation of this revision is still pending.

[Build and test](docs/building.md) | [Updating after an EA patch](docs/updating.md)
