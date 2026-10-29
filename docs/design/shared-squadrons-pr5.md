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
