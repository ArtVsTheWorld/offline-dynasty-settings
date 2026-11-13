# Extra Settings by Ace

Optional controls for offline Dynasty. Other COT modules are not required.

- Set offensive and defensive playcall cooldowns independently, controlling how soon a play can repeat
- Set offensive and defensive playcall limits independently, controlling the number of uses; zero disables the restriction
- Choose offensive play clock On, Off At Playcall, Off or 60 Seconds
- Choose defensive clock 3, 5, 15, 20, 30, 45 or 60 seconds, or Off
- Clock choices belong to each verified Dynasty; new Dynasties start with offense On and defense 15 seconds
- Find the controls in Dynasty League Settings > Game Settings; start a fresh game to check changed gameplay settings
- A verified persistence helper saves supported choices after MMC closes and exits with its matching game session
- Settings, diagnostics and verified runtime files use %LOCALAPPDATA%\Ace\DynastyCOT\ExtraSettings across MMC copies
- Legacy data is copied without deleting originals; explicit AppData preferences take precedence and conflicts are archived
- Copy ExtraSettings.dll into Plugins, or let COT install that same DLL; runtime support prepares automatically
- Supported controller-selection/menu bridges coexist with the current companion modules without granting control of unsupported modes
- Offline scope, exact fingerprints, hook ownership and helper hash verification remain enforced
- Do not also load old clock/playcall/settings providers; COT handles recognized legacy copies with backups
- This does not add clock settings to online play or Play Now
