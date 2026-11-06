# Squadrons workspace inheritance

Squadrons now consumes the same workspace and assignments-view bases as DCS and
Elite Dangerous. Its window assembles native components and connects header events;
it no longer owns the controller, browser, editing or rendering implementations.
The native backend, application session and file formats are unchanged.

## Shared algorithms and native hooks

| Shared owner | Squadrons implementation |
| --- | --- |
| `GameWorkspaceWindow.ConfigureWorkspace` / `NavigateToFeature` | Registers Devices and Assignments factories; stops native input when changing features; reuses cached views and their filters |
| `SaveWorkspaceAsync` / inherited `SaveClicked` | `SaveWorkspaceCoreAsync` delegates to `SquadronsProfileOperations.SaveCore`, using optimistic store revisions and native access/name validation |
| Inherited `DiscardClicked` | `DiscardWorkspaceCore` stops sampling, restores the session baseline and edited name, and clears an unsaved import |
| `RefreshWorkspaceState` | Native pending, save and status overrides use `GameWorkspaceChromeState`; `OnWorkspaceStateChanged` updates native controller and export availability |
| `ConfirmWorkspaceLeaveAsync` / `OnClosing` | Supplies the existing Squadrons Save/Discard/Cancel prompt or injected test choice; overlapping saves, discard, switches and close are guarded |
| `RegisterWorkspaceCleanup` / `OnClosed` | Releases native identification/input resources, command drag, reordering and mounted actions; obsolete discovery scans cannot update closed windows |
| `GameAssignmentsView` panel composition | Supplies command browser, controller drawing and native inspector/diagnostics panels; the common split and bounded inspector preserve controller space |
| `GameAssignmentsView.AssignmentSelection` | Reuses one selection for command, native record and physical input paths |
| `GameAssignmentsView.ExecuteAssignment` | Supplies native scope, serialized draft snapshot, review and mutation delegates; the common workflow checks fresh gates and actual changes |
| `SaveAssignmentsAsync` / `DiscardAssignments` | Delegate to the inherited window operations and report their actual outcome |
| `BindAssignmentChanges` | Connects coordinated native refresh notifications with the common view subscription lifetime |

A failed store write leaves the session snapshot, native records, edited profile
name and revision available for retry. The profile name is accepted into the saved
baseline only after persistence succeeds. Direct compatibility `SaveProfile` calls
use the same inherited operation lifetime as the Save button.

## Native components

All native components are in `src/Heimdall.App/Games/Squadrons/`. They receive the
state, view surfaces and callbacks they use; none depends on the concrete window.

| Component | Responsibility |
| --- | --- |
| `SquadronsWorkspaceContext` | Native session/revision, shared selection, drawing catalog, verified controller evidence, live selection scope and refresh notifications |
| `SquadronsControllerPresenter` | Physical linking, native slot add/rename/clear/remove/reorder review, identification, capability discovery and foreground input testing |
| `SquadronsAssignmentBrowser` | Native catalog filters, captured-record diagnostics, command/record selection and read-only system sources |
| `SquadronsAssignmentEditing` | Physical input choices, paired-axis assignment/reversal, explicit native-record edits, pointed-control clear/tune and drag/drop actions through the shared workflow |
| `SquadronsAssignmentDrawing` | Controller/system drawing projection, diagram interactions, zoom/pan, optional artwork association and device preview |
| `SquadronsProfileOperations` | Bounded native import, template recovery, independent unassigned profile creation, persistence and separate review-copy export |

`GameProfileSession`, `SquadronsProfileBackend` and their native validators remain
the authoritative editing and format boundaries. This phase does not introduce a
new session model or alter native slot identity inference. New-profile creation
retains its source session across a Discard choice even when the common discard
path clears an unsaved import from the window.

## Compatibility and layout

The constructor and existing smoke entry points remain available. Root control
names, native context-menu actions, appearance targets and the persisted
`squadrons.assignments.split`, `squadrons.assignments.table` and
`squadrons.diagram` keys are preserved. Header controls and shared feature actions
retain their current wrapping behavior.

The shared assignment base supplies the inspector viewport instead of an unbounded
native Auto-height row. Diagnostics and native fields can scroll while retaining
usable controller space. The drawing expander uses a local Grid template with an
Auto header and a star-sized content row, so the shared controller allocation
reaches the artwork as a finite height. A visible drawing reserves its chrome
(including its outer margin) plus a 160-pixel
viewport where the window has sufficient height. System keyboard/mouse/gamepad
surfaces remain read-only. Collapsed/fallback artwork does not cause reservation
growth on repeated layout passes.

Native command encodings, saved slot ownership, Windows physical identities,
primary capture records, paired-axis direction values and export declaration order
are unchanged. Export still writes a separate reviewed folder and never installs
active game settings. DCS, ED and War Thunder implementations are left in place.

## Acceptance

Architecture regression checks require C# and XAML inheritance, focused native
collaborators without a concrete-window dependency, no duplicated feature host or
leave guard, and removal of the old workflow partial. The window must remain below
450 lines; meeting that limit alone is not architectural acceptance.

`SquadronsInheritanceSmoke` exercises the actual WPF classes and real temporary
profile persistence. It checks unopened-save rejection, base-view mounting,
compact drawing/inspector geometry, reentrant save/discard/mutation rejection,
failed window and assignment-view saves, retry, native profile retention, cached
navigation, Cancel, baseline discard and feature-host cleanup.

Existing Squadrons smoke scenarios continue to cover authoring, controller linking,
new slots, native names, templates, reverse-axis round trips, drawing actions,
system inputs, foreground testing and review-copy export. Run the full
`tests/smoke-tests.ps1` suite on Windows before merging. Cross-building the WPF
projects does not execute these scenarios.

The refactor changes only WPF presentation code, architecture checks, smoke
fixtures and documentation. Measured production assemblies and their existing
80% line / 80% branch / CRAP <= 50 gates are unchanged.
