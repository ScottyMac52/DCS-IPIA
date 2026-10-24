# Star Wars: Squadrons native adapter — first increment

`Heimdall.Squadrons` implements `ISimProfileBackend` for one explicitly captured
`ProfileOptions_profile_synced` file with `GstKeyBinding.InputDataVersion 4`.
It never discovers, reads, or writes the active game directory. This increment is
a native adapter and API foundation; **Squadrons is not selectable in the desktop
workspace yet**. DCS and ED navigation, profiles and edition behavior are unchanged.

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

## Follow-up increments

1. Extract a game-neutral workspace presentation from the ED-specific view and
   remove backend assumptions from persistence/selection through explicit capabilities.
2. Add Squadrons selection and gated workspace import/save/reopen; maintain Premium
   requirements for non-DCS simulators. Do not enable the starter backend by widening
   backend allowlists before its workflow is wired and tested.
3. Add explicit device identification/association, controller lifecycle operations,
   slot-change review and full-lineup acquisition guidance. Preserve assignment ownership
   when reordering; never infer instance identity from duplicate names.
4. Connect categories/search/type/assignment filters, hardware artwork and fallback
   views, native assignment editing, input testing and printable reference sheets.
5. Verify keyboard/mouse/gamepad editing, combinations and game-specific tuning before
   exposing those operations. Preserve unsupported settings until then.
6. Add backup/conflict-aware game deployment, cloud-sync guidance, Windows UI smoke
   coverage and manual in-game round-trip acceptance with all intended devices.

## Validation

Synthetic regression tests cover exact LF/CRLF round trips, JSON persistence,
multiple devices with duplicate names, extended axis codes, physical button/hat
codes, explicit slot edits, unchanged sibling records and rejection of unsupported
mutations. The adapter is included in the solution, CI/local coverage collection,
and independent 80% line / 80% branch / maximum CRAP 50 gates.
