# Composable assignment views

`GameAssignmentsView` is the reusable assignments-view base. Its functions own
panel composition, refresh reentrancy, state-event binding, assignment execution
and Save/Discard feedback. Games inherit those functions and supply their native
panels, gates, snapshots and actions. The base has no game-name branches or native
workspace types.

## Shared functions and native overrides

| Shared function or component | Game supplies |
| --- | --- |
| `CommandsPanel` | A command browser; no required command or catalog model |
| `ControllerPanel` | A drawing, physical-input list or another native controller presentation |
| `EditorPanel` | Native assignment fields; DCS uses the existing `GameAssignmentEditorView` frame |
| Shared split and local adorner layer | Native content, drag handlers and persisted layout key |
| `RefreshAssignments` | `RefreshAssignmentsCore`; recursive selection events are suppressed |
| `BindAssignmentChanges` | Subscribe/unsubscribe callbacks for the current workspace's event |
| `ExecuteAssignment` | Fresh `CaptureAssignmentContext`, `CaptureAssignmentSnapshot`, an immutable target, a native mutation callback and optional native review |
| `AssignmentSelection` / `AssignmentWorkflow` | Native selection/capability mapping; shared policies and execution stay in their existing common classes |
| `SaveAssignmentsAsync` | `SaveAssignmentsCoreAsync`, `RefreshAssignmentDrawing`, `ShowAssignmentStatus`, optional success/failure wording |
| `DiscardAssignments` | `DiscardAssignmentsCore`, drawing refresh and feedback |

Save/Discard return explicit success values. A rejected action never reports
success. A pending view save blocks another view save, discard and common
assignment mutation. Native persistence must still validate access and serialize
with other workspace operations: DCS delegates to `DcsWindowContext` and the
inherited `GameWorkspaceWindow` action lifetime. DCS's session and draft edit
suspension continue to protect cached references and other mutation paths.

Binding replaces the previous event source. A newly initialized view is ready to
receive state changes before its first mount. Unloading detaches notifications;
remounting a cached view attaches once and refreshes from authoritative state.
Rebinding while unmounted stays detached until the next mount. This does not
replace the native child panels' own subscription management.

## DCS assembly

`DcsAssignmentsView.xaml` derives from `GameAssignmentsView` and supplies its
three panels through the base properties. The code-behind constructs the native
collaborators and provides native choice projection, action policy and overrides.
The former Selection/Workflow partial classes are removed; shared workflow and
selection instances belong to the base.

| DCS component | Responsibility |
| --- | --- |
| `DcsAssignmentViewState` | Current native context, controller/layer/chord and selected input; modifier-reservation lookup |
| `DcsAssignmentDiagram` | Artwork and callout construction, picker population, interaction wiring |
| `DcsAssignmentInspector` | Selected-input details, saved/pending comparison and label-editor synchronization |
| `DcsAssignmentEditing` | Assign, clear, clear layer, undo, palette assignment, conflict confirmation and axis tuning |
| `DcsAssignmentDragDrop` | Pointer/adorner lifetime, cursor feedback, move/copy and drop routing |
| `DcsAssignmentLabels` | Callout label edits, search/replace, authored-label import and generic-control naming |
| `DcsAssignmentPreview` | Current-draft kneeboard preview |

These are composed objects, not additional partial files of the original view.
They receive native selection state, the controls they use, and explicit
callbacks. They do not reference the concrete `DcsAssignmentsView`. Shared
execution remains injected into the DCS editing component.

The existing `AssignmentsView` appearance target, `assignments.split`,
`assignments.inspector` and `assignments.zoom` persistence keys remain unchanged.
Existing named controls and the `SelectedCallout` / `AssignmentWorkspace` namescope
entries remain available to training and smoke checks. The two named native
Save/Discard buttons still address the Assignments handlers specifically.

Elite Dangerous, Squadrons and War Thunder remain on their existing views. Their
migration can supply different panels and override native behavior without
putting their file formats, controller rules or catalogs in this base.

## Acceptance

Architecture checks verify real C#/XAML inheritance, game-neutral base ownership,
composed DCS collaborators and removal of the old partials. A second consumer,
`AssignmentsBaseProbe`, supplies unrelated native panels/actions in the Windows
smoke runner. It exercises real panel mounting, native callbacks, stale/busy
mutation rejection, no-change detection, persistence feedback, overlapping-action
rejection, rebind/unload/remount/close subscriptions and cached-view refresh.

Existing DCS assignment, cached-command, labels, drag/drop, axis tuning, delayed
save, appearance/layout and training smoke scenarios remain required. Linux
cross-compilation verifies wiring and compilation; it does not execute WPF.
Coverage and CRAP gates keep their existing scope and exclude the WPF app.

## Shared feature action layout

`FeatureActionBar` wraps complete buttons into as many rows as the available
width requires. Its height follows those rows; the shell's Auto row keeps the
native feature content below the action bar. The former fixed-height horizontal
scroll strip is removed. A minimum empty-row height remains, and changing a
feature's action count may move the feature content to accommodate more rows.
Visible buttons use automatic height with a 30-DIP minimum. Their access-key
captions can wrap within the styled button width, preserving full text at larger
font sizes. Native commands, availability, tooltips and optional game styles
continue to come from the same source actions.

`FeatureActionLayoutSmoke` checks rendered button and caption bounds, row growth
and recovery on widening, overlap and native action routing at 1100, 700 and 420
window widths, default/custom toolbar styles and 12/18-DIP fonts. These WPF checks
require execution on Windows.
