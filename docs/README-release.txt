OFFLINE DYNASTY SETTINGS v0.2.8
By Ace

0.2.8 fixes settings changes not saving after the Controller Select update.

INSTALL / UPDATE
1. Close the game and MMC Mod Manager.
2. Put OfflineDynastySettings.dll in MMC's Plugins folder, replacing the old DLL.
3. Restart MMC and launch the game through it. Allow about 45 seconds to load.

Keep your existing settings files when updating: DynastyPlayClock.ini,
OfflineDynastySettings.ini, and the OfflinePlaycallSettings folder.
Remove older duplicate DLLs and the separate DynastyPlayClock / OfflinePlaycall
plugins. Disable experimental Offline Dynasty Settings fbmods.

HOW TO USE
Open Dynasty > League Settings > GAME SETTINGS, above Clock Management.

- Offensive / Defensive Playcall Cooldown: Wait 0-10 plays before reusing a play.
- Offensive / Defensive Playcall Limit: Allow 0-20 uses of each play per game.
  Set a cooldown or limit to 0 to turn that restriction off.
- Offensive Play Clock: On, Off At Playcall, Off, or 60 Seconds.
  Off At Playcall pauses it during play selection. Off disables its countdown
  and delay-of-game penalties. Accelerated Clock can shorten the 60-second clock.
- Defensive Play Clock: 3, 5, 15, 20, 30, 45, 60 Seconds, then Off.
  Sets your time to choose a defense after the offense selects its play.
  Off hides the countdown and removes the time limit. Default: 15 seconds.
  This works independently of Offensive Play Clock.

Saved 3- and 5-second defensive clock choices keep their values.

DEFAULTS
Both cooldowns and limits: Off
Offensive Play Clock: On
Defensive Play Clock: 15 Seconds
Existing saved choices are kept when updating.

Cooldowns and limits save separately for each Dynasty. Start a new game after
changing them. Both clock preferences apply across your offline Dynasties.

SWITCH TEAMS
During an offline Dynasty game, open Pause > Options > Controller Select.
Move your controller to either team and confirm. Finish the current play with
your current team; you take over the other team at the next play-selection
screen. The CPU controls the team you leave. You do not need to call a timeout.
If you switch during play selection, choose and run your current team's play
first. Hurry-up can delay the switch until the next play-selection screen.
This feature supports one controller with full-team control, not player lock,
multiple users, practice, or online games.

BUG REPORTS
Logs and both INI files are in Plugins/OfflineDynastySettings. Existing loose
files move there automatically, keeping your saved clock choices.
Logging is automatic. Send OfflineDynastySettings.log from that folder
with a description of the problem and the settings you used.
