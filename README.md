# ExtraSettings by Ace

An independent part of [Dynasty COT](https://github.com/ArtVsTheWorld/DynastyCOT). You do not need the other modules.

[Download published versions](https://github.com/ArtVsTheWorld/offline-dynasty-settings/releases) · [Full feature list](FEATURES.md) · [Release history](RELEASES.md)

**This branch prepares private candidate 0.2.11. Its release assets are not published yet.** The last public version used for comparison is 0.2.10.

## Install or update

1. Close MMC and the game and back up your previous DLL
2. Download the chosen public ZIP and copy ExtraSettings.dll into the versioned Mod Manager's Plugins folder
3. Replace the old copy and launch MMC normally; matching support files prepare automatically

Use one provider per module. The COT installer can retire recognized older copies with backups. Keep all settings/history folders. To roll back, close MMC/game and restore your older DLL.

Current candidates store settings/history/logs in `%LOCALAPPDATA%\Ace\DynastyCOT`, shared across MMC copies. Installation into protected folders still requires permission to replace DLLs.

Want one feature? Download its standalone ZIP. Want several? Use the [optional COT installer](https://github.com/ArtVsTheWorld/DynastyCOT/releases).

## Source and support

Maintained source and compatibility checks are together in [Dynasty COT at 226a6b6c](https://github.com/ArtVsTheWorld/DynastyCOT/tree/226a6b6c6292d202498e7d7fe1c7399335d40f43/Modules/ExtraDynastySettings). This repository's older source tree is historical; use that pinned integration source for current development.

See RELEASE_NOTES.md for the candidate's changes and limits. Automated checks do not establish universal live-game compatibility. Keep your previous working DLL and report your game/mod versions with logs when requesting support.
