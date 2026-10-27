# After an EA update

The current code targets the October 13, 2026 native layout. A game update can move
functions, globals, vtables and jump tables by different amounts. A single address
offset is not enough.

1. Keep an unmodified decrypted capture of the working build under `private/`.
2. Launch the updated game without native plugins. Let it reach the menus.
3. Capture only its code/read-only sections. This command opens the game read-only:

```powershell
python Tools/native_tools.py capture --pid <game-pid> --output private/new.pe.bin
python Tools/native_tools.py verify private/new.pe.bin
python Tools/native_tools.py map private/old.pe.bin private/new.pe.bin --output private/address-map.json
```

The map is a list of suggestions. It does not change source or patch the game.
Unique instruction matches include paired RIP-relative globals and call targets.
Unresolved or cloned functions need manual review. Use the disassembler as needed:

```powershell
python Tools/native_tools.py disasm private/new.pe.bin 0x123456 --length 0x200
```

4. Review every native reference in `docs/native-layout.json`, not just hook entries.
   Check instruction boundaries, preserved registers, return addresses, constant
   values, object offsets, vtable slots, jump tables and mode/side flags.
5. Update the source files listed below and the corresponding tests. Recalculate
   only the contracts whose new code you have inspected. Keep third-party hook
   exclusions narrow. Never disable compatibility checks just to make a build load.
6. Run `python Tools/export_layout.py`, review its diff, build, run tests, and package.
7. Test a fresh launch in the actual game with other plugins disabled, then repeat
   with the supported plugin combination. Check settings/stats, re-entry and exits.
8. Bump the version, update the short release README, commit, tag, and attach the
   two-file ZIP to a GitHub release. GitHub tests do not replace gameplay testing.

Captures, native reference binaries, SDK files, saves and logs stay local. They are
ignored by Git. Python inspection tools use `requirements-dev.txt` (Capstone).

## Source map

- `Shared/Clock/GameLayouts.cs`: current native region hashes and address mapping.
- `Shared/Clock/DynastyPlayClockAction.cs`, `ExtendedClock.cs`: clock hooks and enum reads.
- `Shared/Playcall/SplitSettingsAction.cs`, `SplitHookCode.cs`: playcall hooks, ownership and saved maps.
- `Plugin/DefensivePlayClock.cs`: defensive duration and the caller-specific enable check.
- `Plugin/GameSettingsMenuRows.cs`: generated GAME SETTINGS layout.
- `Plugin/ClockPreferences.cs`, persistence files: saved choices and detached helper.

The defensive gate currently reads the offensive clock enum from a call returning
to `0x35fd7d8`. Only this caller receives the independent defensive enable choice.
Keep other enum reads tied to the offensive setting. Defensive Off is saved as 0;
it keeps a positive internal native duration and disables countdown at the gate.
Setting the native remaining duration to zero would cause immediate expiry.

Test all offensive clock choices against defensive 5/15/20/30/45/60/Off. Check visible
countdown, expiry, pause, CPU timing, saved-value reload and separate Dynasty values.
Keep `DynastyPlayClock.ini`, `OfflineDynastySettings.ini`, native menu keys and
`OfflinePlaycallSettings/dynasties.json` compatible across upgrades.

Fresh settings use native offensive On and defensive 15 Seconds. Missing defensive
cooldown/limit entries default to Off independently of offense. Preserve explicit
saved values; only a missing field (-1) falls back to Off. The combined menu
generator patches both the row defaults and defensive getter to match.

The combined build's source is fully included in this repository. `Shared/` is
not an external sibling checkout. The old Split generator is template input for
`Tools/build_game_settings_menu.py`; running it directly is intentionally disabled.


## Settings and logs

The two clock INIs and plugin logs now live in `Plugins/OfflineDynastySettings/`.
Use `SettingsFiles.PathFor` with the DLL's containing Plugins directory, including
in the detached helper. Keep its context PluginFolder pointed at Plugins so
plugin inventory and the existing per-Dynasty JSON store still resolve correctly.
Legacy loose files are moved automatically. An existing file in the new folder
wins; conflicting old files are retained under `OfflineDynastySettings/Legacy/`.
Never replace a locked/unreadable old preference with default values. Only the
explicit known filenames are migrated; other plugins' files are left alone.
`OfflinePlaycallSettings/dynasties.json` keeps its previous location.

The seven defensive clock options use choice objects at 0xb00..0xe7f and captions
at 0x700..0x7df. Keep these clear of the offensive captions below 0x700, telemetry
from 0x800, and defensive heading/description from 0xf00. The choices end pointer
is 0xa38. Stored values are seconds, so adding an option does not shift saves.
