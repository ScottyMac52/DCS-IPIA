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
3. On **Devices**, select a controller, identify/link its physical hardware and
   choose an optional drawing. On **Assignments & Testing**, use the shared
   command browser to select a command. The visible command category also controls
   the drawing, its inspection and its clear actions. **Captured assignments and
   diagnostics** and its record filters are separate advanced disclosures; their
   filters affect the native record table only. Selecting a record synchronizes
   its command with the editor heading.
4. Click a drawing control or choose a verified physical input, then **Apply
   assignment**. Clicking selects without writing; dragging a command onto a
   control assigns directly. Apply and drag use the same native plan and update
   both directions of an analog axis atomically. The normal picker offers each
   physical axis once. Complete axes use one command label (Pitch, Roll, Yaw,
   Throttle, or the imported catalog name); incomplete axes keep an explicit
   direction suffix. Keyboard/mouse/gamepad and unsupported combinations remain
   read-only. Individual native edits are explicitly named in the advanced record
   menu: **Apply input to this native record only** preserves its native direction,
   while **Clear this native record…** removes only that record.
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

## Blank-slate command creation

A fresh profile with declared controllers and no binding rows supports every
known catalog command directly in HEIMDALL, including Shield Front/Back/Balance.
No game-side seed binding or personal template import is required. The adapter
supplies 149 native descriptors for the 125 known concepts and materializes only
the explicitly requested assignment. Existing/archived user evidence takes
precedence over factory alternatives. See
[descriptor provenance and game-validation limits](squadrons-custom-record-provenance.md).

## Physical controller links

1. Select a numbered captured slot under **Captured controllers**. **All devices and
   inputs** is a filter and cannot receive a link.
2. Use **Rescan** after connecting hardware. Each physical choice displays its name
   and DirectInput instance GUID, including devices with identical names/products.
   No choice is selected automatically from discovery order or a saved name.
3. Select the known unit, or choose **Identify…** and press a button, move a hat or
   move an axis noticeably on one unit. Held switches are ignored until pressed
   again; simultaneous activity remains ambiguous. Identification selects the
   physical unit and releases input handles. It does not identify a Squadrons slot.
4. Verify the selected slot in Squadrons, then choose **Link / replace…**. Review
   the captured slot and previous/new instance GUIDs before staging. Fresh connected
   instance/product evidence is checked again after review; disconnections, duplicate
   observations, changed products or stale workspace previews block the change.
5. **Save profile** persists the association. **Discard changes** restores saved
   links. Reopening retains them even when a device is unplugged; the selected slot
   shows unavailable until **Rescan** confirms its exact instance and product.
6. **Unlink…** reviews removal of just the physical association. It works offline
   and preserves the captured slot, its native name and every binding. To move one
   physical unit to another captured slot, explicitly unlink its previous slot first.

Identification stops on focus loss, slot/profile/import changes, discard and close.
There is no automatic relinking or migration when the connected lineup changes.
Replacement links do not translate button locations or verify input compatibility.
Use **Rescan** to enumerate connected devices' actual controls without acquiring
them. In **Review captured records (advanced)**, the **Hardware check** column distinguishes **Verified device input**,
**Not on linked device**, **Hardware unverified**, preserved system inputs and
preserved native encodings. Use the **Verified device inputs** filter to inspect
supported records only. The captured-slot label describes the file's reference,
not proof that its assigned input exists on the linked physical hardware.

For example, an axis-only pedal unit may occupy a slot that still has generic or
stale button/hat records in the capture. These remain available as unsupported
records in capture review; they do not become pedal controls. HEIMDALL does not delete or guess a
different owner for those records. New edits require verified destination controls
through the application session as well as the UI. Missing, failed or obsolete
scans never fall back to the generic joystick input list. A disconnected or changed
instance/product blocks application even if an older capability scan succeeded.
Physical linking alone retains the native names and bindings; the links are
HEIMDALL workspace metadata and do not establish game acquisition.

To add a controller omitted by Squadrons, open **Devices**, choose its connected
physical hardware and select **Add controller…**. An existing captured slot need
not be selected. The suggested game name combines the Windows/DirectInput name with the first
unused occurrence suffix seen in the native naming convention, for example
`Viper TQS 1` or `F16 MFD 1 1`. A product/model number remains part of the base
name. Existing complete game names are retained; occurrence numbers are separate
from the exported slot numbers. Review or adjust the proposal if game-side
instance enumeration differs.

The review stages one unused native declaration and links the exact physical
instance in one operation. Blank or absent slots can be used within the supported
1–10 range; a slot referenced by any joystick record is reserved even if its name
is blank. Existing device names and bindings are retained. Duplicate native names,
already-linked physical instances, full capacity, stale reviews and missing or
changed hardware are rejected. Cancel keeps the draft unchanged. Save persists the
new slot/link, and export produces the native declaration for game testing. Choose
a drawing and assign verified inputs normally after adding the controller.

For an existing bare name, select its linked slot on **Devices**, review the
suggested **Game device name**, and choose **Update game name…**. This stages the
name change without recreating the device or its assignments. The same action can
regroup declarations even when the name is already correct. Cancel leaves the
saved state intact; Save persists the repair before export.

Add, Update game name and native Reorder group all `GstInput.JoystickDeviceN`
declarations together at the existing device block, before binding records. Keys
are sorted lexically like game-generated captures (`1`, `10`, `2`, …). Other
settings, binding records and their raw line endings are preserved.

Game-side name matching, recognition and retention after restart require manual
validation. Reimporting a fresh game capture remains an alternative reconciliation
workflow and creates a separate workspace with no inherited physical links.

## Remaining increments

- Native slot add/remove and binding transfer, with reviewed reconciliation when
  saved slot ownership changes across captures.
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
Physical-link tests cover duplicate products, stale reviews, reconnect/product
checks, offline unlink, persistence and unchanged native output. Windows smoke
also covers input identification, cancel/replace/unlink, discard and handle cleanup.
Axis-only controller regressions cover unsupported buttons/hats, physical key
mapping, capability mismatch/failure, picker restrictions and preserved native
records. Windows smoke also exercises asynchronous failure, obsolete completion
and closing during a capability scan.
Presentation regressions cover compatible-only authoring, explicit raw-record
review, numeric controller ordering and unchanged native output. Windows smoke
checks the selected-controller layout at 1200×810 and 900×620, including command
width, wrapping, collapsed setup, empty state and diagnostic column visibility.
