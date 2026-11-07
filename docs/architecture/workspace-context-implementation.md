# Shared workspace contexts — issue #357 PR 2

Implementation baseline: main `92439a7bb0c1a9dd33707ae56ff39faa62cfb82a`, including merged #350 and #358. This implements the context role from [ADR-0002](../adr/0002-enforced-game-workspace-role-contracts.md). The other collaborator-role migrations remain in PRs 3–10; this is not architecture completion.

## Enforced inheritance and native boundaries

| Native adapter | Inherited behavior | Typed native extension |
| --- | --- | --- |
| `Games/Dcs/DcsWorkspaceContext` | `GameWorkspaceContextBase<DcsEditorWorkspaceSnapshot>` | `IDcsWorkspaceContext`; existing feature name `IDcsModuleWorkspaceContext` forwards to that contract until PR 9 |
| `Games/Elite/EliteWorkspaceContext` | `GameProfileWorkspaceContextBase` → `GameWorkspaceContextBase<GameWorkspace>` | `IEliteWorkspaceContext<EliteDangerousProfileBackend, EliteAssignmentRow>` |
| `Games/Squadrons/SquadronsWorkspaceContext` | Same profile-family base → common context base | `ISquadronsWorkspaceContext<SquadronsProfileBackend, SquadronsWorkspaceRow>` |
| `Games/WarThunder/WarThunderWorkspaceContext` | Same profile-family base → common context base | `IWarThunderWorkspaceContext<WarThunderProfileBackend, WarThunderWorkspaceRow>` |

The common role and base have no WPF or simulator-native dependency. Native backend/row types are closed generic arguments at the App edge. The profile-family base adapts the already shared `GameProfileSession`: it owns native-session subscription replacement, baseline capture, revision integration and profile chrome. DCS retains its own session/repository/draft/labels and does not implement that profile-family model.

`DcsWindowContext` is removed, and callers use the single window-owned `DcsWorkspaceContext`. Smoke callers no longer construct additional facade/context instances for the same window. DCS training lookup uses the typed native context. The naming inventory records the new class/location; existing persisted module/hardware/appearance identities are unchanged.

## Ownership after this PR

| Concern | One owner | Native/presentation work retained |
| --- | --- | --- |
| Identity / generation / version | Common context base | Native stable key; a process-wide epoch per snapshot family prevents same-ID targets crossing context instances |
| Selection and workflow | Context's existing `GameAssignmentSelection` and `WorkspaceAssignmentWorkflow` | Native projections validate native addresses; windows feed selection events into context; browser internals temporarily use the explicit engine bridge |
| Dirty / edited name / eligibility | Common immutable `WorkspaceState`, calculated from native facts | DCS structural/draft/label state; multi-sim session diff, saved revision and native access policy |
| Operation admission / finally release | Common context base | Native persistence receives the issued lease; no native save flag owns availability |
| Scan / identify / test | Common activity lease | Native discovery/input construction, target evidence and foreground callbacks |
| Notifications / refresh batching / scheduler | Common context base | WPF dispatcher adapter; native view/control population guards only |
| Status | Context `ReportStatus` / `StatusChanged` | Temporary WPF status bridge projects existing native status controls into the contract |
| Disposal | Context cancels activities and drains a committing operation before native teardown | Native resource cleanup and native session event detach; windows still own feature/control disposal |

Deleted independent state: window `_runningAction`, assignments `_saving`, DCS session `_saving`, context busy/testing/identification control delegates, mutable context scanning/disposed flags, and per-presenter scan counters. `WorkspaceActionRunning`, DCS `IsSaving`, and native scanning/testing properties now project the same context state. Window `_confirmingLeave` remains prompt recursion state; `_refreshingChrome`, native panel `Refreshing` and assignment refresh/unmount flags remain presentation guards, not operation locks.

`IWorkspaceSelectionEngineOwner` is an explicit temporary migration port. The common context role exposes immutable selection; the port lends the *same* existing engine to browser/editor code until PRs 4/5 and final PR 9 cleanup remove direct native mutations. No view creates another selection/workflow. It is not an alternative operation authority or a simulator switch in a shared consumer.

## Exact API clarifications from implementation

The [role specification](workspace-role-contracts.md) includes these additions, reviewed as part of this PR:

- `RunOperation(kind, Func<IWorkspaceOperationLease, WorkspaceOperationResult>)` is the synchronous counterpart to `RunOperationAsync`. Both use the same admission/reservation/release functions. Existing synchronous discard/native structure/label operations do not block on a newly introduced async UI task.
- `IWorkspaceOperationLeaseOwner.OwnsOperation(lease)` checks the actual issued lease by reference. Native persistence cannot fabricate a matching identity/kind to accept a baseline. An already durable receipt remains reconcilable while the context is disposing.
- `GameProfileWorkspaceContextBase` shares real native profile-session adaptation across ED/Squadrons/War Thunder. Generic native extensions expose their session/backend/rows without importing backend assemblies into Application.
- `GameAssignmentSelection.RefreshIds` accepts native IDs without manufacturing `SimCommand` rows. A distinct name avoids making existing `Refresh(..., [])` calls ambiguous. The engine's default slot is constructor-supplied; context-owned engines start with an empty slot, and native editing chooses valid slot names.
- `SelectionReadOnly` is a protected native hook for a readonly selection such as Squadrons system records. It gates the assignment host without incorrectly making the entire profile unsaveable.
- Existing window save/discard hooks now receive `IWorkspaceOperationLease`. Profile operations remain native until PR 3, but use the already admitted lease to accept a baseline or restore it. Their persistence template is not duplicated in the context.

The profile-family native session rejects edits through retained references during save/discard/input activity, and rejects references from a retired session even when a reopened session has the same native key. DCS retains draft editing suspension during its repository transaction as native transaction protection; the shared lease is the availability owner.

## Entry paths and notification rules

Window / assignment-feature Save → context admission → native window hook with issued lease → existing native validation/write → native baseline acceptance → common release → shared state/view/action refresh. There is no nested second save lock. Discard uses the synchronous entry, restores native baseline/name and invalidates scope. Cancelled or rejected leave retains the current window/workspace. Profile/controller sequences themselves are scheduled for later role PRs.

Admission reserves the operation *before* stopping foreground input, preventing cleanup callbacks from entering another write. Scan must finish/cancel before save admission. Native scan callbacks check lease cancellation and generation before installing results. Input resource cleanup is registered on the activity lease and runs once before the activity is released.

A supported new scan replaces an outstanding scan through `TryStartActivity`. The context reserves the new lease before cancelling the old one, so stale cleanup cannot publish an idle gap or admit a save. Replacement rechecks native scan eligibility; it cannot replace identification, testing or a workspace operation. Native callbacks still check cancellation and generation before accepting results, so late completion of the older request cannot replace current capabilities.

A direct distinct native change publishes one state transition, including changes while already dirty. Nested refresh scopes coalesce changes. An admitted operation publishes one busy transition and one final release transition; native changes during the operation are combined into that final publication. Blocked and pre-cancelled requests never invoke native actions or publish a false success. Repeated status text is not re-emitted; status never dirties data. Publication isolates listener failures and posts recursive refreshes. Disposal suppresses later UI publication and releases native lifetime once.

## Acceptance evidence and remaining Windows gate

`WorkspaceContextTests` tests common C01–C09 algorithms, including review fingerprints, distinct scope encoding, cross-context epochs, input-cleanup reentry, cancellation, publication recursion, wrong-owner calls and disposal during a durable receipt. `ProfileWorkspaceContextTests` uses real native sessions to exercise borrowed leases, failure/retry, baseline acceptance, readonly/empty sessions and retired-reference rejection.

`WorkspaceContextConformanceSmoke` runs one reusable set of assertions against **all four production context adapters**, real native sessions and isolated stores/repository. It checks inherited type identity, dirty/name/selection state, same-key invalidation, failed/reentrant saves, native-reference bypass rejection, native baseline readback/acceptance, discard, activity cleanup, scan gating, readonly state, subscription detach and disposal. Existing native window/assignment/training smoke cases remain wired in. Existing probe smoke cases now bind actual common contexts instead of relying on window/view-owned flags.

The Windows conformance and full WPF suite must run on Windows before merging. A Linux cross-build is not runtime UI evidence. The independent nine-assembly 80% line / 80% branch and CRAP ≤50 gates remain unchanged. Pre-existing repository-wide layout/migration-checker failures must be reported separately from regressions; no checker, threshold or native assertion is weakened to obtain a pass.
