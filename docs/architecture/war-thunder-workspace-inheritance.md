# War Thunder workspace inheritance

War Thunder now consumes `GameWorkspaceWindow` and `GameAssignmentsView`, matching
DCS, ED and Squadrons. The previous device, assignment, drawing and workflow
partials have been replaced by independent native collaborators. The remaining
window partial is the code-behind required by its XAML and assembles those objects.

## Shared ownership and native hooks

| Shared algorithm | War Thunder extension |
| --- | --- |
| Feature registration and cached navigation | Registers Devices, Assignments and Reference Sheets; releases foreground input when changing features |
| Save and serialized operation lifetime | `SaveWorkspaceCoreAsync` invokes native profile persistence through `WarThunderProfileOperations.SaveCore` |
| Discard | `DiscardWorkspaceCore` stops input and restores the session baseline and edited name |
| Shared pending/save chrome | Native state overrides derive status from `GameWorkspaceChromeState`; the native hook updates controller, import, duplicate, export and editor availability |
| Leave and close guard | Supplies the existing Save/Discard/Cancel prompt and injected test choice; overlapping save/discard/close operations are rejected |
| Window cleanup | Unsubscribes the session/input events and releases drag, input and mounted feature resources |
| Assignment panel composition | Supplies the command browser, drawing, diagnostics and native editor to the common split and bounded inspector |
| Selection and workflow | Reuses the base selection/workflow; supplies native context, draft snapshot, review and mutation callbacks |
| Assignment save/discard | Delegates to the inherited window operations and reports their actual outcome |
| Assignment refresh subscription | Connects native refresh notifications through the common view's loaded/unloaded lifetime |

Mounted callout menus refresh through `HardwareDiagramPresenter.RefreshActions`
when workspace availability changes. Menus rendered during a save remain blocked
while it is active and regain their native clear alternatives when it ends,
without rebuilding the artwork.

The native editor retains its capability policy and reads the live common action
context, so occurrence support and save/discovery/input gates are combined. Axis
tuning uses the same inherited execution path as native Apply, physical drop and
Clear, including review-time scope/draft checks.

## Native collaborators

All War Thunder WPF types are in `src/Heimdall.App/Games/WarThunder/`, using
`Heimdall.App.Games.WarThunder`. None of the native collaborators depends on the
concrete workspace window.

| Class | Responsibility |
| --- | --- |
| `WarThunderWorkspaceContext` | Native session/revision, selected device/input, shared selection, verified observations, physical map and refresh events |
| `WarThunderControllerPresenter` | Native controller review, physical links, capability discovery, native index mapping and foreground input resources |
| `WarThunderAssignmentBrowser` | Command filters, occurrence record projection and selection |
| `WarThunderAssignmentEditing` | Native editor state, physical assignment/drop, pointed-control alternatives, clear support and compound axis tuning |
| `WarThunderAssignmentDrawing` | Optional drawing projection, controller preview and native diagram interaction callbacks |
| `WarThunderProfileOperations` | Bounded BLK import, session opening/subscriptions, duplication, optimistic persistence and separate review-folder export |

The native workspace, Devices, Assignments, editor and Reference views move with
their XAML/code-behind pairs. The compound `WarThunderAxisTuningEdit` also belongs to
this area. `GameProfileSession` and `WarThunderProfileBackend` continue to own native
validation and mutations; no new session or BLK model is introduced.

## Preserved behavior and presentation

Native occurrence addresses, repeated/nested-record read-only rules, physical
range/index validation, product/instance identity checks, clear-alternative menus,
axis inversion/parameter review, unsupported-source preservation and separate
review-only export remain in their native implementation boundaries.

Save persists a candidate workspace before accepting its edited name into the
session baseline. A failed write preserves the draft, native data, edited name and
revision for retry. Discovery results from obsolete or disposed scans cannot be
accepted; native mutation and persistence gates include discovery state.

Control names, existing appearance identifiers, native backend identifiers, and
`war-thunder.assignments.split`, `war-thunder.assignments.table`,
`war-thunder.controllers.split` and `war-thunder.diagram` layout keys are preserved.
The header consumes the shared compact action style/grouping used by ED and
Squadrons. The assignment inspector is bounded and scrollable, preserving controller
space. The drawing expander consumes `WorkspaceDrawingExpander`, the same bounded
Grid template now shared with Squadrons, rather than measuring artwork inside an
unbounded StackPanel. Its controller reservation includes drawing chrome/margins.

## Acceptance

Architecture checks require actual C#/XAML inheritance, native namespace/folder
ownership, native collaborators without concrete-window dependencies, and removal
of the old window partial implementations. A small window file alone is not
architectural acceptance.

`WarThunderInheritanceSmoke` exercises real temporary persistence through both
shared bases: unopened-save rejection, compact header placement, view mounting,
failed-save retention, assignment-view failure reporting, retry, native profile
retention, overlapping save/discard/mutation rejection, cached navigation, canceled
close, baseline restoration and feature-host cleanup. It isolates layout preferences.

Existing War Thunder smoke scenarios remain responsible for native controller
setup, physical/native input mapping, source retention, native occurrence edits,
clear alternatives, compound axis tuning, foreground-only testing, reference
navigation and review export. Their real-drawing checks now enforce a useful
150-pixel viewport and panel containment at normal/minimum window sizes, with
measured geometry in failure diagnostics.

The app and smoke projects can be cross-built on Linux, but WPF scenarios must run
on Windows before merging. Native backend assemblies and the existing measured
80% line / 80% branch / CRAP <= 50 gates are unchanged.
