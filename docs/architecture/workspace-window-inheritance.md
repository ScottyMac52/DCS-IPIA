# Workspace window inheritance

The shared base architecture now supports DCS, Elite Dangerous and Squadrons through native
overrides and registered callbacks. War Thunder retains its existing window and
code. Its later migration must reuse these algorithms rather than copying a
native window.

## Ownership

| Component | Responsibility |
| --- | --- |
| `GameWorkspaceWindow : Window` | Feature registration, lazy view caching, capability checks on navigation, mounting/action bars, serialized asynchronous actions, Save/Discard handlers, pending chrome, switch guard, close guard and resource cleanup |
| `DcsModuleWorkspaceWindow : GameWorkspaceWindow` | DCS composition and XAML events; overrides the native-action and state hooks; supplies picker dialogs and DCS-specific selector/status presentation |
| `DcsWorkspaceSession` | DCS saved/working snapshots, draft subscriptions, structural edits, personal labels, native validation, persistence and discard |
| `DcsWorkspaceOutput` | Validation, snapshot/documentation/complete-release/package output from saved DCS state; destinations and logging are injected |
| `DcsWorkspaceFeatures` | Registers the eight existing feature factories and the Input Test availability policy |
| `DcsWindowContext` | Compatibility facade over the session, shared events and inherited Save/Discard paths; no mutable state in window fields |
| `DcsControllerSelection` | DCS controller rail synchronization and binding cleanup |

The base has no native backend, game ID switch, DCS snapshot, or simulator-specific
state. Feature views retain their existing feature behavior. Splitting a single
window across partial files would not satisfy this ownership boundary.

## Extension contract

A concrete workspace inherits `GameWorkspaceWindow` in both C# and its XAML root.
After `InitializeComponent`, it registers `WorkspaceFeature` factories using
`ConfigureWorkspace`, then connects its common action controls using
`ConfigureWorkspaceCommands`.

The base implements the functions; adapters supply their actions:

| Base algorithm | Adapter extension |
| --- | --- |
| `SaveWorkspaceAsync` / inherited `SaveClicked` | Required `SaveWorkspaceCoreAsync` override |
| Inherited `DiscardClicked` | Required `DiscardWorkspaceCore` override |
| Common Save/Discard availability and pending summary | Required `HasWorkspaceChanges` and `CanSaveWorkspace` properties |
| `RefreshWorkspaceState` | Optional `OnWorkspaceStateChanged` for native feature/output availability |
| Save failure handling for button and switch operations | Optional `ReportWorkspaceError` |
| `ConfirmWorkspaceLeaveAsync` | Injected Save/Discard/Cancel choice; the base invokes the native hooks |
| `OnClosing` / `OnClosed` | Optional `ConfirmDiscardWorkspaceChanges`; registered cleanup actions |
| `NavigateToFeature` | Injected factories, capability explanations, view preparation and feature-selected callbacks |
| `RunWorkspaceActionAsync` | Injected asynchronous operation, used by DCS output |

Save, output and switching cannot overlap through these base entry points. Failed
saves retain pending state, cancellation leaves the window mounted, and resources
are released only after a successful close. Feature factories run once per window;
availability is re-evaluated before reusing a cached view. Common chrome is
refreshed after both successful and failed asynchronous actions.

Blocked Save/Discard calls throw instead of completing as if persistence succeeded.
The base rejects overlapping or closed-window actions; saving a workspace without
write access also fails explicitly. `CanSaveWorkspace` controls common chrome and
leave prompts; the native `SaveWorkspaceCoreAsync` override validates direct save
calls and supplies the specific rejection reason. The base preserves that reason
rather than substituting a generic availability error. DCS disables its feature content/actions while
saving, including after feature navigation. `DcsWorkspaceSession` guards label,
structure and discard operations and holds a `DcsDraftAssignmentService.SuspendEditing`
lease across persistence. The draft blocks every mutation path, including cached
references, until all leases release. Failure retains the original pending state
and undo history; success establishes the persisted baseline before editing resumes.
The delayed-repository Windows smoke covers both outcomes and concurrent callers.

DCS retains its existing two-choice close prompt and three-choice switch prompt.
The `DcsWindowContext` API and constructor parameter contract remain compatible
with existing callers; the DCS window type is now named `DcsModuleWorkspaceWindow`.
Its explicit appearance target retains the persisted `ModuleWorkspaceWindow` key,
so existing backgrounds, artwork opacity and view inheritance remain available. Training accesses its window-owned session and keeps its
isolated repository, input source, catalog and output destinations. Shared context
events now come from that one session, including compatibility facade instances.

## Acceptance and verification

Architecture acceptance is separate from behavior acceptance:

- DCS C# and XAML must inherit the common window; a base class name alone is not
  sufficient. Common algorithms and handlers must live in that base.
- Editing, persistence and native output must not return to DCS code-behind or
  migrate into the base. The architectural checks enforce those boundaries.
- The independent `WorkspaceBaseProbe` subclass supplies non-DCS actions and
  exercises inherited navigation, capabilities, Save/Discard, failed saves,
  overlapping-operation rejection, canceled closing and cleanup.
- Existing DCS consumer, draft notification, output/logging, edition, training,
  labels, controller selection and cached-feature smoke checks remain required.
- Each migration must leave consumers outside its scope unchanged. The ED and
  Squadrons follow-ups migrate those consumers; War Thunder retains its existing window.

Linux can cross-compile the application and smoke project with:

```sh
dotnet build tests/Heimdall.App.Smoke/Heimdall.App.Smoke.csproj -c Release -p:EnableWindowsTargeting=true -m:1
python -m unittest discover -s tests/ci -p 'test_*.py'
```

Cross-compilation does not execute WPF. Before merging, run on Windows:

```powershell
dotnet build -c Release
.\tests\coverage-test-gates.ps1
.\tests\crap-tests.ps1
.\tests\smoke-tests.ps1
```

The 80% line / 80% branch per measured assembly and CRAP ceiling of 50 are
unchanged. The WPF app remains outside the existing measured-assembly list; its
new inheritance tests execute in the Windows smoke runner. No architectural claim
is inferred from a coverage number or a passing native round-trip test.

Assignment features now have a corresponding composable base; see
[assignment-view composition](assignments-view-composition.md) for shared functions,
DCS collaborators and the native override contract.

[Elite Dangerous inheritance](elite-workspace-inheritance.md) documents the ED
consumer, native collaborators, inspector integration and runtime acceptance.

The Squadrons consumer and native extension points are described in [Squadrons workspace inheritance](squadrons-workspace-inheritance.md).
