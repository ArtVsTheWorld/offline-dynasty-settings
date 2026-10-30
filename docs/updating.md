# After an EA update

The current code targets the October 29, 2026 native layout. A game update can move
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
- `Plugin/ControllerSelect.cs`, `ControllerSelectCode.cs`: native eligibility,
  deferred controller confirmation and next-play assignment. Generate the code
  with `python Tools/build_controller_select.py`.
- `Plugin/GameSettingsMenuRows.cs`: generated GAME SETTINGS layout.
- `Plugin/ClockPreferences.cs`, persistence files: saved choices and detached helper.

The defensive gate currently reads the offensive clock enum from a call returning
to `0x35fff18`. Only this caller receives the independent defensive enable choice.
Keep other enum reads tied to the offensive setting. Defensive Off is saved as 0;
it keeps a positive internal native duration and disables countdown at the gate.
Setting the native remaining duration to zero would cause immediate expiry.

Test all offensive clock choices against defensive 3/5/15/20/30/45/60/Off. Check visible
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

The eight defensive clock options use choice objects at 0xb00..0xeff and captions
at 0x700..0x7ff. Keep these clear of the offensive captions below 0x700, telemetry
from 0x800, and defensive heading/description from 0xf00. The choices end pointer
is 0xa40. Stored values are seconds, so adding an option does not shift saves.

## Controller Select

The controller extension has three independent hook sites: eligibility entry
`0x7fae660` (17 bytes), GameSetup virtual commit slot `0xbaced38` (8 bytes,
native target `0x7a9abf0`), and phase setter `0x5d12420` (15 bytes). Eligibility
replays its original prolog through a trampoline outside regular offline Dynasty.
The installer validates bounded hashes and the native slot before patching, holds
threads outside changed instructions, rolls back a failed transaction, and keeps
published allocations until game exit. Its owned entries and bodies are included
in the existing read-only helper verification. A controller compatibility failure
leaves the clock/settings features available and logs the failed contract.

Queue only single controller 0/full-team role 35 changes. Restore the frontend's
current side at confirmation so the current snap remains unchanged. Track the
owning match and seeing phase 3, then consume a pending request before phase 5
initializes the next picker. Move the whole 44-byte role between InputManager
slots 0x148/0x174, changing only current-side +4, clear the old captain, and call
the native input setter. Preserve the native original commit and cancel cache.
Do not force a picker reset or spend a timeout: that auto-selected the user's play.
Hurry-up retains a queued request until the next normal picker boundary.

Do not change shared condition/string-writer hooks, DGA's Resume caption, or its
replay predicates. The repeatable controller tests execute release-DLL machine
code against fake objects in their own processes, never the running game. After
an update, verify both switch directions from the line and picker, fresh matches,
multiple-controller fallback, and clock Off/On behavior with the full plugin active.

The persistence helper must accept the combined ownership manifest (currently
37 checks). Its bounded limit is shared through PersistenceContext.MaximumChecks
(128). Keep the entrypoint regression test: it launches the real embedded helper
against an ordinary test-process identity to distinguish accepted context (exit 3,
wrong process) from rejected context (exit 2). Do not only test save methods through
reflection; that missed the 0.2.7 startup rejection and stale defensive clock INI.
