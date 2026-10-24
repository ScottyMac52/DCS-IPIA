# Star Wars: Squadrons desktop workspace

`Heimdall.Squadrons` implements `ISimProfileBackend` for one explicitly captured
`ProfileOptions_profile_synced` file with `GstKeyBinding.InputDataVersion 4`.
It never discovers, reads, or writes the active game directory. The adapter is connected to a desktop workspace for Premium/Owner users.
DCS and ED profiles remain separate; all three games are available through the
opening game cards and guarded workspace game selectors.

## Implemented contract

- Bounded text parsing, duplicate/malformed key detection and complete native
  record validation. Unknown scalar settings and numeric record extensions are
  retained, along with every original line and line ending.
- Native indexed binding records remain indexed records. They are not ED-style
  Primary/Secondary slots, and record counts do not establish a game's binding limit.
- Every named captured joystick slot is represented independently, including
  duplicate device names. Empty slots and all other native data remain in source
  evidence. A slot/name is not a DirectInput instance identity or proof of game acquisition.
- Portable axes/buttons/hats are projected only for decoded joystick records
  without unverified modifier/alternate-button semantics. Keyboard, mouse,
  gamepad, unknown and unassigned records remain inspectable through `ReadBindings`
  and round-trip unchanged, even when not exposed as portable assignments.
- Commands come from captured concepts, including unassigned ones. Menus/general,
  hangar/briefing and flight contexts have display categories; unknown concepts
  remain available under Other. Friendly labels initially split native CamelCase.
- Unchanged import/export retains exact text, including after JSON persistence.
  Unsupported direct changes to the portable projection block export with no files.
- `RebindJoystick` edits only axis/button/deviceid in an existing unchorded joystick
  record and returns an independent reimported profile. It requires a named captured
  destination slot and rejects records with unknown extension fields. Identifiers, negate flags, sibling records, settings and every
  other device's bindings remain untouched. It neither adds devices nor claims that
  the selected native slot will be recognized by the game.

## Verified representation

The `button` field is a mixed native input-code field. In particular, **40 and 41
encode the positive and negative Z-axis directions**, not physical button presses.
The working TQS capture used these codes for continuous throttle response.
X/Y directions use the `axis` field; Z, rotations and sliders use encoded values in
`button`. Hat directions use their own codes. Unassigned sentinels and unknown
encodings are preserved rather than guessed. Axis direction and native `negate`
are separate evidence; the initial API preserves negate when rebinding.

The decoding table was cross-checked against the independently authored
[Squadrons configuration reader](https://github.com/moff-wildfire/squadrons_config)
and tested with synthetic records and local captures. Its input-code conventions
are a reference, not an official EA configuration schema. No third-party source
code or user configuration files are bundled in this repository.

## Hardware acceptance findings

The TQS worked smoothly after changing the connected device lineup. Reconnecting
other devices changed saved slot ownership: slot 5 changed from TQS to an MFD while
throttle records still referenced zero-based deviceid 4. Manually retaining a TQS
name in slot 10 and pointing the throttle there did not produce working input.
Consequently, **simultaneous acquisition of the full controller lineup is not yet
verified**. File presence and offline validation must not be presented as a successful
in-game hardware test, and there is no established device-count limit from these captures.

## Desktop workflow

1. Select **Star Wars: Squadrons** on the opening screen. Free displays Requires
   Premium and cannot open the workspace or edit/export through session APIs.
2. Choose **Import controls** and select a captured settings file. Imports are
   bounded, and a failed import retains the current workspace.
3. Use **Captured controllers**, context, input-type and search filters to find
   an assignment. Slot numbers remain visible when saved names are duplicated.
4. Select an editable joystick record, its destination captured controller and
   physical input/direction, then **Apply assignment**. This changes only that
   native record. Keyboard/mouse/gamepad and unsupported combinations remain
   visible as preserved records with editing disabled.
5. Name and **Save profile**. **Discard changes** restores the saved baseline;
   saved profiles appear in the profile selector and **Continue** reopens the recent
   Squadrons workspace. Save uses the shared optimistic revision check.
6. **Export review copy** creates a new Squadrons-Review folder containing the exact
   native filename. Save first; the exporter blocks default active settings folders
   and the selected import directory. Close the game and back up its settings before
   manually installing a review copy. The app does not deploy into active settings.
7. Changing game/profile, importing another capture or closing with unsaved changes
   offers Save/Discard/Cancel. Cancel retains the current session and selection.

The desktop shows decoded inputs (including continuous Z-axis directions), captured
slot ownership and individual indexed records. It does not claim that a saved slot
was detected by Squadrons or that every connected device can be acquired together.

## Remaining increments

- Explicit physical device identification/association and add/replace/remove lifecycle,
  with reviewed reconciliation when saved slot ownership changes.
- Hardware artwork and fallback surfaces, live input testing and reference sheets.
- Verified keyboard/mouse/gamepad editing, combinations and game-specific tuning.
- Native assignment creation/clearing beyond the adapter's existing-record edits.
- Backup/conflict-aware active-game deployment and full-lineup in-game acceptance.

## Validation

Synthetic regression tests cover exact LF/CRLF round trips, JSON persistence,
multiple devices with duplicate names, extended axis codes, physical button/hat
codes, explicit slot edits, unchanged sibling records and rejection of unsupported
mutations. The adapter is included in the solution, CI/local coverage collection,
and independent 80% line / 80% branch / maximum CRAP 50 gates.
Windows smoke tests exercise game activation, edition gating, native editing,
filters, save/reopen, review export and canceled game transitions.
