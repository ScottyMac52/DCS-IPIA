# Shared Squadrons workspace — PR 5 (#323)

## Provenance and scope

Base: merged PR 4, `main` at `fb347109ac452303cd09aba7f713f5191901b06d`.
Selective source: protected `fix/squadrons-controller-associations` at
`18babcc6134d2fb7adb362765aef3646b410e37e` (unchanged from the PR 0 snapshot).
Neither that branch nor `feature/war-thunder-adapter-foundation` is modified.

PR 1A already integrated native Squadrons capabilities. This PR connects them to
shared UI components rather than restoring the source branch's obsolete Elite
presenter, command browser, lifecycle code or viewport implementation.

| Source behavior | Migrated integration |
| --- | --- |
| Known and imported command catalog, including unbound commands | `GameCommandBrowserView`, native `SquadronsCommandFilter`, shared `GameAssignmentSelection` |
| Explicit drawing choice for a physically linked captured slot | Installed shared catalog; persisted `GameProfileSession.ChangeDrawing` pin; hide and table-only fallback |
| Drawing labels, partial native axes and collisions | Existing simulator-owned `SquadronsDrawingProjection`; common `HardwareDiagramPresenter` |
| Click and drag native authoring | One `SquadronsDrawingAssignment` plan in the simulator adapter; verified isolated session operations |
| Whole analog axis | Both unambiguous native direction records required; one atomic `RebindNativeAxisPair`; identifier and negate remain native |
| Reviewed clear and native slot renumbering/removal | Existing preview/apply session boundary; shared `GameControllerReordering` with joystick-only eligibility |
| Native record inspector | Existing verified device/input fields inside `GameAssignmentEditorView`; read-only system input inspection |
| Measured drawing scroll fix | Direct Canvas in ScrollViewer; source branch's arranged-pane height clamp; current shared wheel/zoom policy retained; middle-button pan migrated as an optional common viewport behavior |
| Save, discard and guarded navigation | Existing shared chrome and leave guard retained from PR 1/4 |

A physical controller link is distinct from the chosen drawing and captured slot
identity. Selecting controllers, filtering commands, inspecting labels and scrolling
never stage a native assignment or reassociate hardware. Changing slots clears
prior control selection and assignment confirmation. Missing drawings leave the
native table and editor available.

Right-clicking a native table row selects that row before its actions open.
Every drawing callout offers View assignments without assigning the selected
command, including empty axes. A bound callout also offers Clear assignments. The
review lists every matching record in the current controller/context, including
both native axis directions and any colliding commands. Acceptance stages that
explicit set atomically; cancel or validation failure leaves the draft unchanged.
The table still supports selecting one specific native record to clear. Squadrons axis
tuning is unsupported and is not advertised as an action. Unknown native record
creation still fails through the adapter's existing verified-template validation.

## Shared navigation correction

The initial PR 5 UI retained too much of the standalone Squadrons layout. The
revised consumer uses the same profile chrome, horizontal feature navigation,
`GameWorkspaceFeatureHost` and `FeatureActionBar` as Elite. Devices opens first;
its cached `SquadronsDevicesView` owns physical linking, native slot review and
drawing choice. `SquadronsAssignmentsView` owns Assignments & Testing, using the
shared command browser and editor around a large drawing. Native captured records
are available in a collapsed diagnostic panel below the drawing.

Actions are routed from the active feature through the common toolbar. Input
feedback is the existing Elite implementation generalized to `HardwareInputFeedback`,
with the same exact-instance readiness, entitlement, native disposal and focus-loss
behavior. Without a drawing it displays raw activity text. Navigating features or
changing controllers/profiles stops acquisition and clears stale feedback; testing
does not edit native bindings. Reference-sheet generation is not implemented for
Squadrons and is not advertised as a working feature.

A new Windows smoke verifies the actual mounted views, device and assignment action
sets, cached filters, toolbar execution, live text fallback, native disposal on
navigation/controller/focus changes, and unchanged native exports. Existing smoke
navigates to the assignment feature before testing its layout and authoring.

## Generic clear and reassignment

Native joystick records retain their own identifiers, negate values and original
addresses. Clearing now retains a simulator-owned template archive in HEIMDALL's
native evidence. This archive is persisted with the workspace, survives unrelated
edits and slot operations, and is excluded from the game's exported settings.
Assignment planning considers retained templates as authoring candidates, while
assignment rows/status/export use active records only. Both axis directions are
restored and rebound together through the existing verified session operation;
button records use the same restoration path. No command names, identifiers or
physical slot numbers are hard-coded by this mechanism.

For configurations cleared by older versions, open **Configuration and export
settings → Import templates…** and choose the original Squadrons
capture that still contains the joystick assignments. This imports authoring
metadata only: current native bindings, controller declarations and physical links
remain unchanged. Save the configuration, then retry the assignment. A native file
that already lost the records cannot recover their unknown identifiers on its own.

Templates must contain actual captured joystick metadata. An axis with missing,
unsupported or ambiguous directional evidence is still rejected; keyboard or
mouse identifiers are never reused as guessed joystick identifiers. Existing
hardware enumeration, collision, edition, identity and atomicity checks apply to
restored records. The shared session now stages native edits through one identity
and validation helper, including the optional template-import adapter port.

The directional fixture retains only five control concepts from the supplied
capture, with synthetic controller declarations and no personal scalar settings.
Regression tests cover real distinct identifiers, Pitch/Roll/Yaw/Throttle and an
unknown renamed concept, button restoration, old-file recovery, actual file-store
reopen, discard, collisions, missing hardware and malformed archive evidence.

## First assignment from a fresh game

A game-generated Steam reset capture contains `GstInput` settings and controller
slots but no `GstKeyBinding` rows or input-version marker. Import accepts this
specific empty-table state without modifying its text; versioned tables must still
be version four and complete. The first explicit assignment adds the version-four
marker and only the requested joystick record(s).

The adapter embeds 95 joystick record templates extracted from the complete table
written by Squadrons after the first Sol-R Pitch assignment. The source captures
were supplied on 2026-10-29: the fresh 1,951-byte file and `SOL-R.PITCH`. Pitch's
identifiers agree with the earlier independent capture despite moving from slot
three to slot seven. The embedded data contains no personal settings or controller
names; template device references are normalized and always replaced by the
explicitly selected, verified target. This is command metadata, not active default
assignments. Unknown identifiers are not calculated or borrowed across concepts.

Captured records and workspace archives take precedence. Adapter templates are
offered only for concepts with no existing joystick records; incomplete captured
pairs and occupied native addresses still fail without a partial write. The same
native/session validation path handles fresh creation and captured reassignment.
Digital button and POV controls can replace each other through this path.
Import, catalog inspection and hardware enumeration never materialize assignments.

A user need not configure controls in Squadrons or import a personal template file
before assigning a supported command. Hardware enumeration provides valid physical
inputs; explicit workspace links associate the intended device with its native
slot. The catalog and templates remain simulator-owned and independent of hardware
names and ordering. This does not claim that every known command has verified
creation metadata; unsupported or ambiguous commands remain unavailable.

Regression tests cover nine distinct flight/hangar/spectator axis concepts, digital
buttons and POVs, exact Pitch identifiers, untouched native settings, file-store
reopen, discard and malformed/version/address rejection. Windows smoke uses the
actual shared workspace drop route from an empty table and verifies save/reopen.
Actual game acceptance of minimally materialized exported records remains a manual
check; unit tests and a WPF cross-build do not establish game acceptance.

## Device order owns exported slots

Dragging a named controller in HEIMDALL determines the contiguous native device
order on export. Slot declarations, all joystick `deviceid` references, physical
links and drawing pins move together after review. Bindings stay with their
controllers; moving a device does not adopt another device's default bindings.
Blank native declarations are appended after named devices instead of being
required in the visible device list. This also permits reordering a fresh profile
with no binding table. The adapter supports slots 1–10; the captures do not prove
that ten is the game's absolute hardware limit or that all slots are usable in
its menus simultaneously.

## Add a controller omitted by the game

The Devices toolbar includes **Add controller…**. Connected hardware selection and
identification are available without choosing an existing captured slot. The game
name proposal combines the Windows/DirectInput base name with the first unused
occurrence suffix, preserving model numbers and existing complete native names.
It remains editable because native game-instance ordering requires validation.

The optional `INativeControllerCreator` adapter port creates one native declaration;
`GameProfileSession.PreviewAddNativeController` combines it with a reviewed exact
physical link. The normal stale-review, entitlement, product identity, save,
reopen, discard and native export validation apply. The identity guard is shared
with other native session writes. No command assignments are invented by addition.

Squadrons selects the first blank or absent unreferenced slot in its supported
1–10 range. Referenced blank slots remain reserved. Invalid/multiline names,
duplicate game names, already-linked physical units and full capacity fail before
publication. Regression tests cover omitted slot ten with CRLF/LF, cancel/accept,
axis assignment, file-store reopen, slot reorder, discard, native line retention,
identity failures, stale review and unsupported adapters. An unattended Windows
smoke uses the real shared toolbar from All devices and verifies cancellation,
creation, verified axis assignment and saved reopening. Actual game recognition
remains a manual acceptance check.

## Native controller names and declaration layout

Observed game captures use occurrence-qualified names such as `Viper TQS 1` and
`F16 MFD 1 1`. The base model number, occurrence number and native slot number are
separate. Suggestions preserve complete captured names; an existing bare name can
be repaired explicitly through **Devices → Update game name…** without recreating
its controller, physical link, drawing or assignments. The optional
`INativeControllerNames` port uses the shared reviewed session boundary and
identity guard. Cancellation, stale reviews and duplicate names remain guarded.

Add, Rename and Reorder write a contiguous native device declaration block at the
existing block location, or before binding records if the declarations were all
appended later. Keys follow the lexical ordering seen in game-generated captures.
Only the reviewed name and declaration layout change; all non-device lines retain
their exact bytes and relative order, including unknown settings and LF/CRLF.
Unedited imports still export their original text exactly. Update game name can
also normalize a scattered block when the existing name is already correct.

Regressions use a sanitized version of the reported shape: a bare TQS declaration
after an existing two-direction throttle pair. They cover occurrence/model names,
existing-name stability, grouped keys, binding/physical-link retention, actual
file-store persistence, discard, newline handling, entitlement and stale reviews.
The unattended Windows addition smoke also opens and repairs a saved bare name,
cancels then accepts review, and checks that the axis pair and link survive.
Real-game name matching and acceptance remain manual checks.

## Verification

- New native-plan regression tests cover whole axes, partial-axis rejection,
  replacement versus creation, invalid sources and non-mutation.
- Existing Squadrons tests cover collision labels, native record preservation,
  verified hardware, captured slot edits, save/reopen/discard and lossless export.
- Windows smoke adds real shared-presenter click routing, drop routing, paired-axis
  editing, pointed clear independent of command selection, saved drawing reopen,
  controller-switch isolation and measured scroll extents at 1340×860 and 1050×650.
- Existing layout smoke now recognizes explicit read-only system sources and uses
  the migrated workspace's minimum size; verified-hardware filtering stays intact.

Linux can cross-build WPF but cannot execute these Windows GUI checks. Local test,
coverage/CRAP and cross-build results are recorded in the PR. Inherited global
C# layout diagnostics and unavailable format/CI checks are reported separately.

## Windows acceptance checklist

- [ ] Run `dotnet build -c Release` and `tests/smoke-tests.ps1`.
- [ ] Run `tests/coverage-test-gates.ps1`, `tests/crap-tests.ps1`, then `tests/generate-test-reports.ps1`.
- [ ] Open a previously saved bare-name TQS profile, select the TQS in Devices, apply Update game name, save/reopen/export and inspect its grouped declaration and unchanged assignments.
- [ ] From Devices / All devices, select an omitted connected controller, cancel then accept Add controller, choose a drawing, assign an axis, save/reopen and export. Validate the native name and acquisition in Squadrons.
- [ ] Import a fresh game capture with no binding table; link a declared slot, choose a drawing, and assign an axis without importing templates. Save/reopen/export and validate both directions in Squadrons.
- [ ] Import a real Squadrons capture and link the correct captured slot to physical hardware.
- [ ] Choose a shared drawing; save/reopen; hide/show; confirm a slot without drawing remains usable in the table.
- [ ] Select a command and assign by callout click, then drag; verify errors for unsupported controls and unknown creation templates.
- [ ] Reassign a full axis and inspect both native direction records; reject a collision without changing the draft.
- [ ] Change controller with a command selected; confirm callouts, rows and feedback belong to the selected slot.
- [ ] Review/cancel/accept clear-record and clear-slot operations, reorder slots, and discard to restore source.
- [ ] Resize, fit, zoom, middle-drag pan, scroll vertically and horizontally to the bottom/right callouts; verify hit alignment.
- [ ] Inspect Mouse/Keyboard/Gamepad records; no authoring or linking is offered for these sources.
- [ ] Save/export a review copy and validate in Squadrons after backing up active game settings.
- [ ] Confirm existing DCS and Elite smoke/manual workflows remain intact.

War Thunder UI implementation remains outside PR 5; its readiness audit stays in PR 6.
