# Extra Settings by Ace

A standalone set of controls for **offline Dynasty**. It does not require the other COT modules.

- Set **offensive and defensive playcall cooldowns independently**: decide how many plays must pass before repeating a play. Zero turns the restriction off.
- Set **offensive and defensive playcall limits independently**: limit how many times the same play can be used in a game. Zero turns the restriction off.
- Choose the offensive play-clock mode independently from the defensive clock. Choose **On, Off At Playcall, Off, or 60 Seconds**.
- Choose a defensive clock of **3, 5, 15, 20, 30, 45 or 60 seconds**, or Off.
- Clocks are saved **per Dynasty**, using its verified LeagueID. A different save starts with its own choices. New Dynasties start with offensive On and defensive 15 seconds; playcall restrictions start off.
- Find the controls in **Dynasty > League Settings > Game Settings**. Start a fresh game after changing gameplay settings.
- A verified settings-saving helper can keep persistence working after MMC closes. It exits with its matching game session; it does not write settings into another running Dynasty by filename guessing.
- The existing deferred Controller Select bridge remains included for supported offline Dynasty contexts. This does not grant control of unsupported modes or bypass native restrictions.
- Settings, legacy preference backups and diagnostic reports remain in the existing `Plugins/OfflineDynastySettings` data folder. Installing the standalone does not replace saved choices with a template.
- Native fingerprints, exact hook ownership, controller eligibility and the helper's compiled-in SHA256 checks stay enabled. Unsupported or foreign hooks are rejected rather than overwritten.
- Current Beyond the Field and Expanded Practice retain the existing shared-menu/controller compatibility contracts. Older or modified clock/playcall plugins can conflict; do not keep their DLLs alongside this plugin.
- This is offline Dynasty functionality, not a Play Now or online clock-settings mod. The new one-DLL packaging is a preview pending clean-launch live acceptance.
