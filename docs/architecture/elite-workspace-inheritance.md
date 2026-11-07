# Elite Dangerous workspace inheritance

Elite Dangerous is the second native consumer of the shared workspace and
assignment-view bases. `EliteWorkspaceWindow` inherits `GameWorkspaceWindow` in
C# and XAML. `EliteAssignmentsView` inherits `GameAssignmentsView` and assembles
its existing command browser, artwork and native editor through the three panel
properties. DCS, Squadrons and War Thunder keep their current implementations in
this migration.

## Shared functions and ED behavior

| Shared owner/function | ED supplies |
| --- | --- |
| `GameWorkspaceWindow.NavigateToFeature` | Cached Devices, Assignments and Reference factories; native input is stopped before changing features |
| Common Save action lifetime and busy/closed guards | `SaveWorkspaceCoreAsync` delegates native persistence to `EliteProfileOperations.SaveCore` |
| Common Discard and pending summary | `DiscardWorkspaceCore` restores the session and edited name; `WorkspaceStateSummary` uses native `GameWorkspaceChromeState.Status` |
| Common Save/Discard/Cancel switch/close algorithm | Native prompt wording and injected test choices; synchronous profile storage completes within the inherited action lifetime |
| Common successful-close cleanup | Registered, idempotent disposal of controller input, discovery timers, drag/reordering and session subscriptions |
| `GameAssignmentsView` panel assembly | `GameCommandBrowserView`, `EliteAssignmentArtworkView`, `GameAssignmentEditorView` containing native `EliteAssignmentEditorView` fields |
| Common selection/workflow instances | Native controller/section/slot scope and serialized profile snapshot |
| Common assignment review/execution | ED callbacks retain native assignment review, primary/secondary behavior, combinations, axis tuning and native key translation |
| Common refresh and event binding | ED editor refresh callback and `EliteWorkspaceContext.Changed`; cached view remount reattaches and refreshes |
| Common inspector allocation | ED artwork chrome plus drawing-space reservation; the existing `AssignmentInspectorScroll` name points at the base's actual viewport |

The window no longer creates its own feature host, common leave guard or
assignment-workflow instance. Its compatibility `SaveProfile()` returns failure
when persistence is rejected; it cannot silently succeed without an open session
or while another inherited operation is running. Native save failures retain the
current session and edited profile name. The common availability property does
not replace native save validation or substitute a generic rejection reason.

## Native collaborators

| Component | Responsibility |
| --- | --- |
| `EliteWorkspaceContext` | Current native session/revision, command/row projection, shared selection and current selection/gate accessors |
| `EliteProfileOperations` | Import, new/duplicate/rename, local profile save, bindings-directory preferences and reviewed preset export |
| `EliteControllerPresenter` | Identification, physical/native reference linking, replacement/removal review, drawing choice, discovery evidence and input-testing lifetime |
| `EliteAssignmentBrowser` | Command filters/grouping, command/slot selection and command-drag lifetime |
| `EliteAssignmentEditing` | Native input choices, modifiers, editor draft/review, Apply/Clear and native assignment availability |
| `EliteAssignmentDrawing` | Artwork projection, verified key translation, pointed-control actions, axis review and input-activity display |
| `EliteReferencePresenter` | Reference projection, preview cache, page navigation and PNG/PDF export |
| `EliteWorkspaceWindow` | Composition, header/cross-panel coordination, configuration deletion/reset and native base overrides |

These are separate composed objects, not another large partial window. None of
the collaborators references the concrete `EliteWorkspaceWindow`. They read the
current native context and receive their actual controls and callbacks. The
existing `GameProfileSession` remains the domain editing boundary, and the
existing ED-backed `GameWorkspaceServices` API is retained for callers. The old
window Workflow partial is removed; its common workflow state now belongs to the
assignment-view base.

## Compatibility

The existing native backend, profile store, export preparation/revalidation,
backup policy, ED device-reference rules and entitlement boundaries remain in
place. The window constructor and deterministic smoke APIs still route to the
same operations. Native primary/secondary slots, axis bindings, modifiers,
controller order and preset deployment remain ED-owned.

Explicit appearance targets keep `EliteWorkspaceWindow` and `EliteAssignmentsView`
identities stable. `elite.assignments.split`, `elite.diagram`, controller split,
reference/table and other existing presentation keys retain their names.
`AuthoringPanel`, `AssignmentInspectorScroll` and the existing root control aliases
remain available to interaction checks. The native editor supplies fields rather
than nesting a second scroll viewer inside the shared inspector.

The two small base extensions are native pending-summary wording and shared host
release for explicit disposal; defaults preserve DCS behavior. The assignments
base exposes its inspector viewport to inheritors for native namescope/focus
compatibility. No native file-format or simulator policy is added to either base.

## Acceptance

Architecture checks require actual C#/XAML inheritance and explicit native
collaborators. They prohibit a duplicate feature host, common leave guard or
workflow in the ED window and bound the remaining composition code.

`EliteInheritanceSmoke` uses real temporary file persistence behind an intercepted
store. It checks empty/busy save refusal, common busy assignment rejection,
blocked discard/close during persistence, cached navigation, native snapshot/name
retention on failure, retry, baseline restore and successful-close host release.
Existing ED configuration, input/identification, combinations, controller
lifecycle, drawing, pointed-action, axis tuning, reference, export-preflight,
layout/scaling and guarded game/profile-switch scenarios remain required.

Linux can compile those WPF scenarios but cannot execute them. Run the complete
Windows smoke suite before merging. Production measured assemblies and their
80% line / 80% branch / CRAP <= 50 gates are unchanged; WPF remains outside the
existing measured assembly list. Architecture acceptance is checked separately
from unit test counts and native round-trip results.
