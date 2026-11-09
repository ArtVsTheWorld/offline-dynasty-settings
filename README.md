# Extra Settings

**By Ace.** Separate offensive/defensive playcall controls and clocks saved for each Dynasty.

**[Download 0.2.10 ZIP](https://github.com/ArtVsTheWorld/offline-dynasty-settings/releases/download/v0.2.10/ExtraSettings-0.2.10.zip)** · [Full feature list](FEATURES.md) · [What's new](RELEASES.md)

**Standalone preview:** the new wrapper still needs clean-launch live acceptance.

## Install or update

1. Close the game and MMC Mod Manager.
2. Put **ExtraSettings.dll** directly in the versioned Mod Manager's **Plugins** folder. The ZIP contains that DLL, README.txt and FEATURES.md. Matching support files are embedded and prepared automatically.
3. Move older duplicate plugin DLLs outside Plugins, keeping settings/history folders. Restart MMC and launch normally. Use a writable MMC folder outside Program Files.

Read README.txt in the ZIP for this module's menu, old filenames, logs and removal steps. Use one provider of each feature. If keeping COT for other modules, disable this feature in COT before using the standalone. You can install any combination of matching current standalone modules.

**Want one feature? Download its standalone ZIP. Want the collection? [Download Dynasty COT](https://github.com/ArtVsTheWorld/DynastyCOT/releases).** See the [compatibility and saved-data guide](https://github.com/ArtVsTheWorld/DynastyCOT/blob/e9322de06b3309b113d1e98b169a76a68ad22b25/docs/standalones.md).

## Source and verification

This repository is the standalone download/help front door. Current releases are built from the [single maintained COT workspace](https://github.com/ArtVsTheWorld/DynastyCOT/tree/e9322de06b3309b113d1e98b169a76a68ad22b25), including [this module's source](https://github.com/ArtVsTheWorld/DynastyCOT/tree/e9322de06b3309b113d1e98b169a76a68ad22b25/Modules/ExtraDynastySettings) and shared build/packaging code. `source-release.json` pins the exact source revision, baseline and artifact hashes.

The older source tree retained in this repository is historical; it is **not** the build input for this standalone release. Fixes are maintained in COT rather than separately editing four forks. Release assets include integrity metadata; successful offline checks do not certify every game/overhaul combination.
