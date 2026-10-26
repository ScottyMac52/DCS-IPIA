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
- The command browser combines 125 known v4 native concept identifiers with any additional concepts captured in the imported file. Clearing the last binding does not delete a command from the catalog. The catalog contains names only and does not invent native binding records. Menus/general,
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
3. Choose a controller, then use context, input-type and search filters to find
   an assignment. The default **Flight** view shows only inputs compatible with
   verified linked hardware. A selected controller needs only **Command** and
   **Input** columns; command names wrap instead of being truncated. Controller
   choices use numeric slot order and retain slot numbers for duplicate names.
   **Controller setup** is collapsed until needed for linking/identification.
   **Review captured records (advanced)** exposes preserved, unverified and
   incompatible records plus hardware/slot/binding diagnostics. Context and search
   still apply; select **All** contexts to inspect the full capture. Unlinked or
   empty views explain how to link/rescan or open capture review. The assignment
   editor appears after selecting a record.
4. Select an editable joystick record, its destination captured controller and
   physical input/direction, then **Apply assignment**. The destination must have
   an explicit connected physical link and verified input enumeration; the input
   picker offers only controls actually enumerated on that device. This changes only that
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

## Command-first assignment workflow (in progress)

Command catalog filtering now follows the DCS/Elite pattern: context,
input type (axes, buttons/hats, unknown or unbound), assignment status
(on the selected controller or anywhere), and case-insensitive name/native
concept search. These filters are computed by the game-specific pure
`SquadronsCommandFilter` and do not hide unassigned commands merely because
they have no native record. A command without any captured input has unknown
type; HEIMDALL does not guess that Pitch/Yaw are axis-only without evidence.
The catalog and captured-binding table have separate filters and independent
counts. **Clear filters** restores the complete known/imported command list.

The Squadrons workspace now includes a **Command** browser that uses the adapter's
captured command catalog rather than inventing commands in WPF. **Show assignments**
selects the command's context and reveals its existing native records. The binding
table has **Edit selected assignment** and **Clear selected assignment…** context
actions; clearing previews the exact native record, uses the shared session's
stale-review protection, and preserves every unrelated setting and record.

This is **not yet an Add binding operation**: a previously unbound concept may have
no safely reusable native joystick record. The writer still requires a captured
record for assignment replacement, and no arbitrary binding index, `identifier`,
native default, or grip-dependent control is synthesized. Complete creation, shared
hardware drawings, drawing callouts, drag/drop commands, label overrides, and
context-menu assignment to an illustrated control remain follow-on work. Drawings
should use `GameWorkspaceServices.HardwareAsync` and the canonical diagram
presenter, and must never imply controller recognition or a particular attached grip.

## Read-only native system assignments

The controller selector includes separate **Mouse**, **Keyboard**, and **Gamepad**
entries whenever a Squadrons profile is loaded. Selecting one changes the captured
binding view to **All** contexts and exposes the matching records even with
advanced review disabled. These entries cannot be linked, dragged to reorder native
joystick slots, or edited/cleared as joystick bindings. They are kept in original
native evidence and round-trip without mutation.

The table now decodes supported native read-only input codes to friendly
labels: DirectInput keyboard scan codes (for example, Space and W), mouse X/Y
movement and wheel directions, and mapped gamepad face buttons, D-pad, triggers,
shoulders and stick axes. It retains numeric values for unrecognized inputs;
mouse button codes without a verified button-name correspondence use numbered
Button labels rather than guessing left/right/middle. Display labels do not
grant edit rights or imply a particular device layout. The mapping is informed by
the community squadrons_config parser (moff-wildfire/squadrons_config) and needs
acceptance testing against actual in-game captures. The Squadrons toolbar now uses a single 8-pixel gap between its shared
ToolbarComboBox and action buttons; the profile-name field matches the 30-pixel
toolbar control height.

## Smart navigation for controller drawings

Squadrons uses the existing shared `DiagramViewport` with opt-in smart
navigation. **Fit** automatically scales artwork to the resized drawing pane;
the **Zoom** slider supports 10–300% manual scale and retains the existing
workspace layout persistence rules. **Ctrl+mouse wheel** zooms around the
pointer, ordinary wheel explicitly scrolls vertically, **Shift+wheel** scrolls horizontally,
and **middle-button drag** pans the enlarged artwork. Scrolling requires the scaled\nartwork to extend beyond the viewport (Fit mode may have no scrollable extent). Mouse-based navigation
is scoped to the drawing viewport, not the command filters or assignment grid.
Existing click-to-select and drag-to-assign drawing controls remain intact.
This does not modify native Squadrons records.

## Resizable filters, assignments, and drawing

The Squadrons workspace separates the top command-filter region from the
captured-assignment grid with a draggable horizontal splitter. A second
horizontal splitter sits between the assignment grid and the hardware drawing.
The filters scroll within their allocated height; the assignment list remains
independently scrollable. When the drawing expands, its row becomes star-sized
so the lower splitter genuinely redistributes space between assignments and
artwork; the drawing scroll viewer fills the resized viewport instead of
enforcing a fixed 250-pixel height. Collapsing the drawing hides its splitter,
restores its row to Auto, and returns the available height to assignments.

Once a linked joystick slot is associated with a shared drawing, its existing
captured native joystick bindings automatically populate the diagram's callout
labels through `SquadronsDrawingProjection`. Each canonical physical input key
maps to every matching command on the selected slot and context; axis
directions remain distinguished. Hovering a control lists the captured
assignments, and selecting a callout selects the matching visible binding
record. A drawing is a presentation of saved assignments, not a separate
authoritative binding store. Unknown native input encodings remain visible
in the assignment table rather than being guessed onto a callout.

## Choosing a shared controller drawing

The drawing panel uses the same bundled shared hardware definitions as DCS and
Elite, but **linking a physical controller does not automatically choose artwork**.
After linking a captured joystick slot, expand **Controller drawing and assignment
drop targets**, choose its exact **Hardware drawing** from the shared catalog,
then **Save profile**. A saved drawing selection is distinct from the controller's
physical instance GUID and native Squadrons slot; it remains associated with the
same logical slot. **Hide drawing** hides artwork without unlinking the hardware.
If the shared catalog cannot be loaded, the picker remains unavailable and the
native controller bindings remain intact.

The drawing catalog loads asynchronously. The shared session registers its
verified catalog IDs once loaded; this does not modify any saved workspace.
Only an explicitly chosen bundled drawing can be pinned—controller names are
not used to infer artwork or grip configuration.

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
Native exports contain the same captured slot names and binding records; links are
HEIMDALL workspace metadata and never repair game acquisition by themselves.

To add another native slot, bind an input on that device in Squadrons and import a
fresh capture. This creates a separate workspace with no inherited physical links;
review slot ownership again. Native slot add/remove, binding transfer and capture
reconciliation remain separate work because saved names cannot establish identity.

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
