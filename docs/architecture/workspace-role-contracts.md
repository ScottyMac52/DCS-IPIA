# Workspace role contracts: proposed API and behavior

This is the normative design for [ADR-0002](../adr/0002-enforced-game-workspace-role-contracts.md), issue #357 PR 1. PR 1 introduced no production declarations. PR 2 implements the context declarations; other roles remain proposed until their migration PRs. See [context implementation](workspace-context-implementation.md). Read with the [source/migration map](workspace-role-migration.md) and [acceptance design](workspace-role-acceptance.md). Baseline: `0fa0e28b8e50c23d57a3d14b4d47d35b7c1d3db0`.

## Contract placement and dependency direction

| Location | Responsibility | Allowed dependencies |
| --- | --- | --- |
| `Heimdall.Application/Interfaces` | Seven role contracts; operation/activity, projection and typed persistence ports | Application/Domain/BCL only; no WPF or native backend references |
| `Heimdall.Application/Games` | Context, profiles, browser, editing and controller bases; immutable shared values | Role interfaces and existing assignment engine; no App/backend references |
| `Heimdall.App/Games` | Drawing/devices WPF bases; existing window/assignment-view bases | UI-free role interfaces, shared WPF controls/rendering |
| `Heimdall.App/Games/<game>` | Native inheritors, typed ports, XAML and composition roots | Shared contracts/bases, that game's backend, existing infrastructure ports |
| Native backend projects | Native parser, projection, review, mutation and output semantics | Existing inward dependencies; no reference to App |

The non-generic contract names below are the public orchestration boundaries. Generic extensions add compile-time native values; they are not `object` envelopes. DTOs are immutable; lists use immutable copies. `TSnapshot`, `TCommit`, `TCommand`, `TRecord`, `TFilter`, `TDrawing`, `TObservation` and request/result types are closed by the native composition root. Constraints are `where TSnapshot : class`; other parameters have no common inheritance requirement. Native backend types are type arguments at the App edge, never imported by an Application base. All public base implementations below are nonvirtual. Only listed protected hooks are abstract/virtual. Constructors take interfaces, never concrete sibling collaborators or `Func<NativeSibling>`.

Every role implements `IDisposable`; disposal is idempotent. Context and WPF integration are owned by one window lifetime. Role disposal releases its subscriptions/resources, not the context or siblings. The window disposes roles in reverse construction order, then context/native session. Unloading a cached view detaches view subscriptions/input activity; it does not dispose the reusable workspace. Common bases run state publication on the owning dispatcher/synchronization context captured through `IWorkspaceScheduler` (`bool CheckAccess(); void VerifyAccess(); void Post(Action action);`). Async native work returns data; application of results occurs on that owner. No blocking `.GetAwaiter().GetResult()` is introduced on UI paths.

## Shared values and availability

These signatures define the vocabulary; XML summaries/implementation bodies are deferred to their owning PR.

```csharp
public readonly record struct WorkspaceIdentity(string Key, long Generation);
public sealed record WorkspaceSelection(string? ControllerId, string? CommandId,
    string? SlotId, string? AlternativeId, string? InputId, string NativeScope);
public sealed record WorkspaceAvailability(bool Enabled, string Reason);
public sealed record WorkspaceCapabilities(WorkspaceAvailability Save,
    WorkspaceAvailability Discard, WorkspaceAvailability Edit,
    WorkspaceAvailability Scan, WorkspaceAvailability Identify,
    WorkspaceAvailability Test, WorkspaceAvailability Link);
public enum WorkspaceOperationKind { Save, Discard, Replace, Import, Export, ControllerChange, NativeEdit }
public enum WorkspaceActivityKind { Scan, Identify, Test }
public enum WorkspaceOperationOutcome { Applied, NoChange, Blocked, Cancelled }
public sealed record WorkspaceOperationResult(WorkspaceOperationOutcome Outcome,
    string Message, bool Committed = false);
public sealed record WorkspaceState(WorkspaceIdentity Identity, long Version,
    bool Open, bool ReadOnly, bool Dirty, string EditedName,
    WorkspaceSelection Selection, WorkspaceOperationKind? Operation,
    WorkspaceActivityKind? Activity, WorkspaceCapabilities Capabilities);
public interface IWorkspaceOperationLease {
    WorkspaceIdentity Identity { get; }
    WorkspaceOperationKind Kind { get; }
    CancellationToken Cancellation { get; }
}
public interface IWorkspaceActivityLease : IDisposable {
    WorkspaceIdentity Identity { get; }
    WorkspaceActivityKind Kind { get; }
    CancellationToken Cancellation { get; }
    void RegisterCleanup(Action cleanup);
}
public interface IWorkspaceOperationLeaseOwner {
    bool OwnsOperation(IWorkspaceOperationLease lease);
}
public interface IGameWorkspaceContext : IWorkspaceAssignmentHost, IWorkspaceOperationLeaseOwner, IDisposable {
    WorkspaceState State { get; }
    string Status { get; }
    WorkspaceAssignmentWorkflow AssignmentWorkflow { get; }
    event EventHandler? StateChanged;
    event EventHandler<string>? StatusChanged;
    void Select(WorkspaceSelection selection);
    void SetEditedName(string name);
    void RefreshNativeState();
    IDisposable DeferRefresh();
    void ReportStatus(string message);
    WorkspaceOperationResult RunOperation(WorkspaceOperationKind kind,
        Func<IWorkspaceOperationLease, WorkspaceOperationResult> action);
    Task<WorkspaceOperationResult> RunOperationAsync(WorkspaceOperationKind kind,
        Func<IWorkspaceOperationLease, Task<WorkspaceOperationResult>> action,
        CancellationToken cancellationToken);
    WorkspaceAvailability TryStartActivity(WorkspaceActivityKind kind,
        out IWorkspaceActivityLease? lease);
    void StopActivity();
}
public interface IGameWorkspaceContext<TSnapshot> : IGameWorkspaceContext
    where TSnapshot : class {
    TSnapshot? Working { get; }
    TSnapshot? Saved { get; }
}
```

`WorkspaceAvailability(false, reason)` requires a nonempty user-facing reason. Unsupported capability is a disabled availability, not a fake successful no-op or a throwing dummy implementation. Shared capability values describe common actions only. Optional import/export/new/duplicate/tuning/structural actions are exposed through explicit native extension contracts, with their own availability (see profiles and native mapping). `ReadOnly` is independent of `Dirty`: retained edits in a newly restricted edition are not silently lost.

`Key` is an opaque nonempty native stable ID: DCS module/configuration identity, others persisted workspace ID. `Generation` is a context-issued monotonic epoch incremented on session replacement, import, discard/rebuild that invalidates targets, and disposal. Reopening the same native ID gets a new epoch. `Version` advances for each externally visible state transition. Native revision tokens stay native; they are neither the identity nor the review fingerprint. Empty context uses key `""`, open=false and a valid generation.

## Context: `GameWorkspaceContextBase<TSnapshot>`

Constructor: `(IWorkspaceScheduler scheduler)`. Required protected hooks:

```csharp
protected abstract NativeWorkspaceFacts ReadNativeFacts();
protected abstract TSnapshot? ReadWorking();
protected abstract TSnapshot? ReadSaved();
protected abstract string CaptureNativeFingerprint();
protected abstract bool NameEquals(string left, string right);
protected abstract void DisposeNative();
protected void ReplaceNativeSession(Action replace);
// Called by typed native adapter after successful install/restore, never by a view.
```

`NativeWorkspaceFacts` is `record(string Key, bool Open, bool ReadOnly, string ReadOnlyReason, bool Dirty, bool New, string SavedName, WorkspaceCapabilities Capabilities)`. Native adapter reports facts, not a second busy flag. Context's `Working`/`Saved` call native read hooks; snapshots obey existing immutable/copy semantics. Mutations are through typed session ports or assignment operations, never setters on the common context. `ReplaceNativeSession` uses an admitted Replace/Import/Discard operation or the same synchronous Replace admission when called by a native install port while idle. Save may install its prepared accepted baseline. It batches refresh, invalidates selection and advances the process-wide epoch for that snapshot family. Native facts must be consistent before it returns.

Base-owned functions and invariants:

1. `RefreshNativeState`: capture native facts once; calculate dirty as native Dirty OR New OR edited-name difference; intersect native capability facts with common gates. Update immutable State only if changed and publish StateChanged once. Status is separate and does not dirty the workspace. No view callback is used to read authoritative selection/name/operation state.
2. `Select`: validate stable IDs with native role projections before calling; base rejects changes during an exclusive operation, resets dependent input/alternative when controller/scope changes, and updates the one existing `GameAssignmentSelection`. The ID-based `RefreshIds` method extends that class so DCS need not manufacture `SimCommand` objects. Expose an immutable `WorkspaceSelection`; keep the mutable engine instance internal to context/editing integration. Slot defaults come from native projection; `Primary` is not a universal default. Selecting/focusing alone never dirties a draft.
3. `IWorkspaceAssignmentHost.Context`: derive the existing `WorkspaceAssignmentContext` from State. Scope is an unambiguous length-prefixed encoding of identity key, generation, controller and NativeScope/slot/alternative; do not concatenate ambiguous colon-separated native IDs. Open/read-only/busy/testing are fresh. Existing blocked-reason precedence stays closed → readonly → testing → busy. `CaptureSnapshot()` delegates to canonical native fingerprint, including all assignment-affecting labels/structural/input/name state used by that operation; it is not a version counter or serialized view tree.
4. `RunOperationAsync`: verify owner access, capability/disposal and idle state using the admission table below; reject reentry immediately as Blocked (never enqueue stale requests). Reserve the operation before stopping active identify/test input, so native cleanup cannot reenter admission; an in-flight Scan blocks admission until cancelled/completed. Reserve operation once, publish busy, invoke action with an unforgeable lease, then release in `finally` and publish final availability. Lease identity and reference ownership are checked before accepting async native results; a durable receipt can still reconcile during disposal. Consumers cannot construct a lease or set Busy. The delegate is not called on blocked/pre-cancelled requests. A nested public profile/window Save is blocked; internal hooks receive the existing lease. The assignment workflow's synchronous reentry guard remains subordinate to this coordinator; no second save lock remains in window/view/native session after migration.
5. Activities: Scan/Identify/Test are mutually exclusive and incompatible with an operation. `TryStartActivity` returns a reason and null lease when denied; successful lease emits activity state. Releasing it cancels native input subscriptions and clears activity exactly once; stale disposal cannot clear a newer lease. Scan uses cancellation + generation checks before publishing observations. `StopActivity` cancels and disposes active resources through registered lease cleanup, then publishes; late results are ignored. Disposal cancels activities and operation token, invalidates generation, and suppresses future UI events. Native writes already beyond commit cannot be undone by disposal; receipts still reconcile persistence (profile rules below).
6. `DeferRefresh`: depth-counted scope, not a busy lock. Native/selection transitions mark pending recomputation; outermost disposal publishes the final distinct state once. Admission busy and final release notifications are never hidden across an await by refresh batching. View-local population guards remain allowed, but cannot own workspace gates or discard missed refreshes. Listener-triggered refresh is scheduled after publication; notifications are not recursively raised.
7. `ReportStatus`: updates Status and emits StatusChanged only when text changes. State notifications are raised only for changed immutable state. Subscriber exceptions are isolated/logged by the publication boundary and cannot turn a durable successful save into a persistence failure. Releasing scopes and disposal must run even if a native hook throws. Native session event subscription is attached once and detached before replacement/disposal; a legacy facade forwards the same authority during migration.

`SetEditedName` checks owner/disposal and rejects while an operation is active, stores the editable overlay and recomputes dirty/state using NameEquals. Native validation/uniqueness happens in PrepareSave or the typed rename command, not on each keystroke. Installation/restore resets the overlay from native facts. The base's AssignmentWorkflow is the one existing engine instance; it exposes no new assignment semantics. Activity RegisterCleanup attaches idempotent resource release to the lease; registering after cancellation immediately runs cleanup. A native draft's editing suspension during persistence may remain as transaction protection under the same lease; it must not become another independent workspace operation authority.

| Operation | Shared admission beyond idle/not disposed | Native policy |
| --- | --- | --- |
| Save | Open and Save capability; stop input before reservation | Writable/edition/repository validation; Dirty controls write vs NoChange, not eligibility |
| Discard | Open and Discard capability; stop input | Restore retained edits may be allowed readonly; Dirty controls restore vs NoChange |
| NativeEdit | Open and Edit capability | Typed structural action validates native invariants; no persistence |
| ControllerChange | Open and Link capability | Selected controller's CanLink + native reviewed change |
| Export | Open; stop input | Typed command Availability enforces native access/save/output rules |
| Import / Replace | May start with no open session; stop input | Typed command Availability and completed leave guard decide replacement/new/import support |

Capabilities represent eligibility without the transient Dirty requirement; UI Save/Discard enablement also checks Dirty. Busy/activity intersections disable incompatible actions, but admission evaluates eligibility after stopping identify/test so a normal Save can safely stop live input. Scan must finish/cancel before admission. All operations disallow simultaneous assignment mutation via context.Context.Busy. Activities use their corresponding native capability plus idle/activity checks. There is no unguarded direct native edit route. DCS structure commands use a typed `IGameProfileCommand<DcsStructureRequest,WorkspaceOperationResult>` with Kind=NativeEdit; its request contains `Func<DcsEditorWorkspaceSnapshot,DcsEditorWorkspaceSnapshot> Edit`, and its native prepared candidate passes existing edition/structure validation before atomic draft installation. It never writes files.

Dispose immediately marks the context closed/disposed, cancels work, invalidates generation and detaches UI listeners. If a durable operation is already in flight, defer DisposeNative until its receipt/finally reconciles; call it once afterward, without publishing UI state. A cancelled pre-write action can dispose immediately. Do not dispose the session/draft underneath a committing write or rebuild it into a newer workspace.

Unexpected native exceptions propagate to the entry boundary after status/state cleanup; cancellation before durable commit yields Cancelled. A native action that refuses has an explicit Blocked result. Error status describes retained edits; the base never clears dirty state on failure. State/event callbacks must not start a nested operation synchronously.

## Profiles: `IGameProfileOperations` and `GameProfileOperationsBase<TSnapshot,TCommit>`

```csharp
public interface IGameProfileOperations : IDisposable {
    IGameWorkspaceContext Context { get; }
    Task<WorkspaceOperationResult> SaveAsync(CancellationToken cancellationToken);
    Task<WorkspaceOperationResult> DiscardAsync(CancellationToken cancellationToken);
}
public interface IWorkspaceInputLifetime {
    void Stop(); // synchronous: releases acquisition/handlers before persistence
}
public sealed record WorkspaceCommit<TSnapshot,TCommit>(TSnapshot Snapshot, TCommit Receipt)
    where TSnapshot : class;
```

Base constructor: `(IGameWorkspaceContext<TSnapshot> context, IWorkspaceInputLifetime input)`. Hooks:

```csharp
protected abstract TSnapshot PrepareSave();
protected abstract Task<WorkspaceCommit<TSnapshot,TCommit>> PersistAsync(
    TSnapshot candidate, IWorkspaceOperationLease lease);
protected abstract void AcceptCommit(WorkspaceCommit<TSnapshot,TCommit> commit);
protected abstract void RestoreSaved(IWorkspaceOperationLease lease);
protected abstract void RefreshAfterPersistence();
protected abstract string SavedMessage { get; }
protected abstract string DiscardedMessage { get; }
```

Save's nonvirtual sequence is capability check → context Save admission → stop input → prepare/validate candidate without changing saved baseline/name/revision → persist once → accept receipt/baseline once → refresh native state → mark result Applied, Committed=true → refresh browser/drawing/actions → success status → release busy/final availability. A clean writable save returns NoChange without writing. An unsaveable workspace returns Blocked even if the draft is clean. Native save validates again inside persistence when required (DCS edition/training/UI-layer rules). Failed preparation/write never accepts a baseline or emits success. Edited name/revision/draft survive a store conflict or repository failure. Retry prepares from current state rather than replaying a stale candidate. All fallible baseline/session construction is prepared before writing; acceptance installs prepared state. If the owning context closes during a durable write, reconcile the receipt in the native persistence/session owner, suppress UI publication and never install it into a different generation/session.

`TCommit` is a native receipt: ED/SQ/WT new store revision, DCS durable saved snapshot/transaction receipt. Persist must return only after durable success and must not throw a cancellation after committing. AcceptCommit installs that known receipt without further I/O or validation and is required to be nonthrowing; if it cannot install, close editing and report committed/recovery-required rather than retrying a write. A post-commit refresh exception returns Applied with Committed=true and a refresh-warning message, logs the refresh failure, retains the accepted baseline and releases operation state. It must never say “save failed” or write twice. No success is reported on blocked/no-change/failed persistence as if a write occurred.

Discard's sequence is capability → context Discard admission → stop input → restore baseline/draft/name/revision through hook → invalidate scope/generation → refresh state/panels/actions → report Applied (Committed=false). Nothing is written to database or game files. Clean state returns NoChange. Failure preserves a usable session and reports the native error; native restoration must prepare a replacement before swapping it atomically. Discard of a never-saved native session follows its existing empty/new policy, explicitly tested per game. Native restore must not leave a half-rebuilt draft installed.

Optional native profile work uses a typed common extension, not flags or generic payloads:

```csharp
public interface IGameProfileCommand<TRequest,TResult> {
    WorkspaceAvailability Availability { get; }
    Task<TResult> ExecuteAsync(TRequest request, CancellationToken cancellationToken);
}
// Application base: GameProfileCommandBase<TRequest,TPrepared,TResult>
protected abstract WorkspaceOperationKind Kind { get; }
protected abstract WorkspaceAvailability ReadAvailability();
protected abstract TPrepared Prepare(TRequest request);
protected abstract Task<TResult> RunNativeAsync(TPrepared prepared, IWorkspaceOperationLease lease);
protected abstract void Accept(TResult result);
```

Constructor takes `IGameWorkspaceContext` and `IWorkspaceInputLifetime`. Its nonvirtual Execute checks availability, admits one operation, stops input, prepares/reviews, rechecks lease + fingerprint after review, runs native action, accepts successful result, refreshes context and releases in finally. Disabled capability throws a documented `InvalidOperationException(Availability.Reason)` for this typed-result API before any hook; common Save/Discard instead return Blocked. Cancellation throws `OperationCanceledException` for this typed-result API and never installs a prepared candidate. Native typed results distinguish user cancellation where pickers/review are involved; `Accept` runs only for an actual successful result, determined by a required `protected abstract bool IsSuccessful(TResult result)` hook. Output writers own atomic/backup/no-overwrite semantics and produce typed durable receipts. User-facing entry translates cancellation to a cancelled status. Import/replacement leave decisions occur before Execute admission; they call Save/Discard as separate completed operations, never nest them inside the replacement lease. New/duplicate/name changes use Replace and the same reviewed install boundary. Optional commands exist only where native support already exists; consumers get an explicit availability instead of casting profiles to a concrete class.

## Browser: `IGameAssignmentBrowser` and `GameAssignmentBrowserBase<TCommand,TRecord,TFilter>`

```csharp
public interface IGameAssignmentBrowser : IDisposable {
    void Refresh(string? preferredCommandId = null);
    void Search(string text);
    void SelectCommand(string? commandId);
    void SelectRecord(string? recordId);
}
public interface IGameAssignmentBrowser<TCommand,TRecord,TFilter> : IGameAssignmentBrowser {
    IReadOnlyList<TCommand> Commands { get; }
    IReadOnlyList<TRecord> Records { get; }
    TFilter Filter { get; }
    event EventHandler? ProjectionChanged;
    void SetFilter(TFilter filter);
}
```

Constructor: `(IGameWorkspaceContext context, TFilter initialFilter)`. Required hooks:

```csharp
protected abstract IReadOnlyList<TCommand> ReadCommands();
protected abstract IReadOnlyList<TRecord> ReadRecords();
protected abstract string CommandId(TCommand command);
protected abstract string RecordId(TRecord record);
protected abstract bool IncludeCommand(TCommand command, TFilter filter, string search);
protected abstract bool IncludeRecord(TRecord record, TFilter filter, string search);
protected abstract WorkspaceSelection SelectionForRecord(TRecord record);
protected abstract WorkspaceSelection SelectionForCommand(TCommand command);
```

Refresh nonvirtually reads native catalog/records once, filters with native predicates (base trims search), preserves a valid preferred/current command by stable ID, clears invalid record/input targets, publishes immutable Commands/Records and ProjectionChanged once if changed. It never selects by row number or display label and never mutates a native draft. Native projection order is retained; no common sorting of ordered source declarations. Search/SetFilter update query then invoke this sequence; invalid IDs clear selection rather than silently targeting another command. Selecting an existing record maps through a typed hook to controller/command/slot/alternative in the common context. Context state changes refresh through one subscription; reentrant requests coalesce into one final refresh. Projection exception retains last good rows, reports status, clears unsafe targets and permits retry; no partial list publish. Dispose detaches subscription and drag adapters; view-specific routed handlers live at the WPF edge, invoking these methods. DCS command management remains a native structural panel; browsing does not absorb catalog import/edit transactions.

## Editing: `IGameAssignmentEditing` and `GameAssignmentEditingBase`

```csharp
public interface IGameAssignmentEditing : IDisposable {
    WorkspaceAssignmentActionState ActionState(WorkspaceAssignmentAction action);
    event EventHandler<DraftResult>? Completed;
    DraftResult Execute(WorkspaceAssignmentAction action, IWorkspaceAssignmentOperation operation);
    void RefreshActions();
}
```

Constructor: `(IGameWorkspaceContext context)`. Execute uses `context.AssignmentWorkflow` (one per context). Required hooks:

```csharp
protected abstract WorkspaceAssignmentActionSupport ReadNativeSupport(WorkspaceAssignmentAction action);
protected abstract void RefreshNativeEditor();
protected abstract void ReportNativeResult(DraftResult result);
```

The base's Execute calls the existing workflow with context as `IWorkspaceAssignmentHost`, never applies a mutation outside that workflow or stores files. ActionState calls existing `WorkspaceAssignmentActionState.For(action, context.Context, ReadNativeSupport(action))` with fresh state before display and dispatch; action states are per action, not one boolean for the editor. Execute wraps the native Review with that action-specific availability check, returning an invalid review with its reason when disabled; native Apply remains invoked only by the existing engine. Existing workflow performs pre-gates → before fingerprint → native Review → post-gates/scope/fingerprint → Apply once → actual-change fingerprint → result. Its existing semantics remain: admitted normal changed/no-change/blocked attempts emit workflow Completed once; thrown native exceptions and early reentrant rejection do not emit it. The editing base forwards that single completion, refreshes context/actions/editor once and reports native wording; it does not manufacture a second completion. On exception it refreshes gates in finally and propagates to the UI status boundary. Blocked/stale review clears unsafe targets and never applies. No-change cannot mark dirty just because an adapter returned Applied.

Assign, clear, undo, tuning, label, drag/drop and native inspector operations construct typed native `IWorkspaceAssignmentOperation`s in native extensions and call Execute. Target includes current context scope. A narrow extension may expose `ReviewAssign(NativeRequest)`, `Clear(NativeTarget)` etc.; no concrete editing sibling is needed by drawing/controller/browser. Operations not expressible as assignment mutations (DCS structural transactions, profile import) go to their own typed profile/context ports; they do not bypass native validation or pretend to be assignments. Existing native draft histories remain native; base does not add an undo stack. Any native review dialog may pump messages: stale fingerprint/gates must be rechecked after it returns.

## Drawing: `IGameAssignmentDrawing` and `GameAssignmentDrawingBase<TDrawing>`

```csharp
public interface IGameAssignmentDrawing : IDisposable {
    void Refresh();
    void RefreshActions();
    void SelectInput(string? inputId);
    void Mount();
    void Unmount();
}
```

WPF base constructor: `(IGameWorkspaceContext context, IGameAssignmentEditing editing, IGameControllerPresenter controllers)`. Required hooks:

```csharp
protected abstract TDrawing ProjectDrawing();
protected abstract void PresentDrawing(TDrawing drawing);
protected abstract void PresentActions(IReadOnlyDictionary<WorkspaceAssignmentAction, WorkspaceAssignmentActionState> actions);
protected abstract WorkspaceSelection SelectionForInput(string? inputId);
protected abstract void AttachNativeInteractions();
protected abstract void DetachNativeInteractions();
```

Refresh base algorithm captures context identity/version, projects native artwork/callouts/labels using typed native values, rejects obsolete projection before installation, presents once, then always recomputes action availability. It uses existing `HardwareDiagramPresenter`, `DiagramViewport`, `HardwareDiagramInteractions` and action/menu mechanisms where applicable; DCS retains its native artwork preview behind PresentDrawing. RefreshActions is cheap and must run on operation/activity release even when no redraw is needed (#356 regression). SelectInput updates context after native mapping; drawing never acquires a device independently. Mount attaches context/editing/controller notifications and native interaction handlers once; Unmount detaches, stops controller activity owned by that surface and invalidates pending projection. Failure keeps the last good drawing, disables unsafe actions and reports status; retry rebuilds from authoritative state. Zoom/pan/viewport/focus are UI-local state, not draft dirty. Native tuning/modifier/label menus are typed commands from editing extensions. Optional artwork absence shows native explanatory text, never guessed hardware identity.

## Controllers: `IGameControllerPresenter` and `GameControllerPresenterBase<TObservation,TLink>`

```csharp
public sealed record WorkspaceController(string Id, string Name,
    WorkspaceAvailability CanLink, string Evidence);
public sealed record WorkspaceControllerLinkReview(GameAssignmentReview Review, bool Cancelled);
public interface IGameControllerPresenter : IWorkspaceInputLifetime, IDisposable {
    IReadOnlyList<WorkspaceController> Controllers { get; }
    event EventHandler? ControllersChanged;
    Task ScanAsync(CancellationToken cancellationToken);
    void Select(string? controllerId);
    WorkspaceAvailability Start(WorkspaceActivityKind activity);
    Task<WorkspaceOperationResult> LinkAsync(string logicalId, string physicalId,
        CancellationToken cancellationToken);
}
```

Constructor: `(IGameWorkspaceContext context)`. Hooks:

```csharp
protected abstract Task<IReadOnlyList<TObservation>> DiscoverAsync(CancellationToken token);
protected abstract IReadOnlyList<WorkspaceController> ProjectControllers(IReadOnlyList<TObservation> observations);
protected abstract WorkspaceAvailability CanStart(WorkspaceActivityKind activity);
protected abstract IDisposable StartNative(WorkspaceActivityKind activity, IWorkspaceActivityLease lease);
protected abstract TLink PrepareLink(string logicalId, string physicalId);
protected abstract WorkspaceControllerLinkReview ReviewLink(TLink candidate);
protected abstract WorkspaceOperationResult ApplyLink(TLink candidate, IWorkspaceOperationLease lease);
protected abstract void ReleaseNative();
```

Scan admits a Scan activity, awaits discovery, checks token/generation, projects stable native IDs/evidence, preserves valid selected controller or clears selection, publishes one ControllersChanged, releases in finally. A discovery failure retains previous observations as stale/explained evidence and allows retry; it does not create a confident physical match. Select validates ID, stops active input, updates context and dependent scope once. Start only accepts Identify or Test (Scan has its async API), checks native + common capabilities, acquires context lease, then native resource; startup failure disposes both. The base registers native resource disposal on the activity lease. Stop is idempotent, releases native resource before lease and clears activity. Link checks per-controller capability, enters ControllerChange operation, stops input, captures native fingerprint/selection, prepares and reviews typed native consequences, rechecks lease/open/access/scope/fingerprint after review, then invokes ApplyLink once only if valid/changed. It refreshes context/controllers and releases in finally. Invalid/cancelled/stale review returns Blocked/Cancelled without applying. Native hooks do not own orchestration, persist or set busy. No physical instance is inferred from product token, slot number, joystick index or display name alone. Unsupported link/Test/Identify produces explicit disabled availability. Native callbacks capture lease identity and ignore input after stop/disposal; disposal cancels discovery, releases acquisition/log handlers and detaches events exactly once.

## Devices view: `IGameDevicesView` and WPF `GameDevicesView`

```csharp
public interface IGameDevicesView : IDisposable {
    void Refresh();
    void Mount();
    void Unmount();
}
```

`GameDevicesView : UserControl, IGameDevicesView` constructor is parameterless for XAML. One protected nonvirtual `BindDevices(IGameWorkspaceContext context, IGameControllerPresenter controllers)` after InitializeComponent rejects rebinding to a second lifetime. WPF dependency properties `UIElement? ControllerListPanel`, `UIElement? DetailsPanel`, `UIElement? ActionsPanel` assemble the shared bounded layout. Required hooks:

```csharp
protected abstract void PopulateNativeDetails();
protected abstract void PresentAvailability(WorkspaceCapabilities capabilities);
```

Mount subscribes once; Refresh coalesces reentrant view notifications, preserves selection/focus, populates details under a view-local guard, presents context-derived availability and controller state. Unmount detaches and stops view-owned controller activity; remount reconstructs current state; Dispose calls Unmount once and disconnects panels. Native exception retains context/draft, reports status and leaves a retryable view. Discovery, link review, ordering and hardware mutation are controller/typed operation responsibilities; devices code-behind only connects native controls and typed commands. DCS's extra inventory/device structure panels remain native, supplied in slots. Shared sizing/keyboard/focus behavior belongs to this base or existing shared controls, not four copied event handlers.

## Existing workspace and assignments composition

Extend `GameWorkspaceWindow` and `GameAssignmentsView`; do not add parallel replacements. The existing protected native snapshot/save/discard hooks are transitional only. Final window binds one context/profiles contract via protected nonvirtual `BindWorkspace(IGameWorkspaceContext context, IGameProfileOperations profiles)` and takes availability/dirty/status from context. Its existing public entry points delegate once to profiles; `_runningAction` is removed as an independent workspace lock (view-click debounce may remain without altering gates). Existing leave guard owns prompt sequence, calls profile Save/Discard and checks result/dirty before navigating; `_confirmingLeave` remains prompt reentry state only. Cancel/blocked/failed save keeps current game/profile and selections. Close stops input, detaches feature host and disposes owned roles/context in reverse order only after successful leave. Feature cache and entitlement reevaluation remain in the existing base.

Assignments base uses protected nonvirtual `BindAssignments(IGameWorkspaceContext context, IGameProfileOperations profiles, IGameAssignmentBrowser browser, IGameAssignmentEditing editing, IGameAssignmentDrawing drawing)` and existing panel properties/sizing. It shares context selection/workflow instead of constructing independent instances. Feature Save and window Save call the same profile entry directly, with one operation admission and one feedback result. Local `_saving` no longer supplies a separate edit gate; success is read from the result, and drawing/actions refresh through the common completion/state path. `_refreshing`, subscription and unmounted fields stay only for UI recursion/lifetime. The final seven abstract snapshot/persistence/refresh hooks currently on assignments base are replaced by bound contracts and explicit native panel refresh callbacks where needed; no orphan duplicate workflow remains. Common browser/drawing/editing consumers must not resolve sibling types via a service locator or cast context to each game in a switch.

## Capability subsets and typed native extensions

The table describes role support, subject to actual native access/edition/session/evidence. A disabled action is represented with a reason at runtime even where the game supports that category.

| Surface | DCS | ED | Squadrons | War Thunder |
| --- | --- | --- | --- | --- |
| Save/discard | Repository + structural/draft/labels | Store revision + edited name | Store revision + edited name | Store revision + edited name |
| Browse/edit/draw | Module/UI-layer commands, profiles/layers | Catalog, primary/secondary/axis | Concepts and ordered native records | Catalog and occurrence-addressed records |
| Scan/select | Native GUID discovery/inventory | Native reference vs physical identity | Logical slot vs physical link | Native index/range vs physical evidence |
| Identify/test/link | Existing DCS training/input/inventory gates | Existing link/review/input gates | Only verified capabilities; system sources readonly | Only verified physical mapping; ambiguity explained |
| Native profile extensions | Saved Games/module/integration/structure imports; snapshot/package outputs; module operations stay native | Import, new, duplicate, rename, reviewed preset export/backup, bindings directory | Import/template import/new, load, review export | Import/load/duplicate/review export |
| Native assignment extensions | Reserved modifiers, labels, UI layers, native undo/structure | Combinations, invert/deadzone, axis tuning, modifier choices | Paired axes/reversal, slot ordering, concept/native record review | Chords, multiple alternatives, compound tuning, native occurrence identity |

No common delete/reorder/new/export API is invented for a game lacking an existing action. Typed optional commands declare their availability and do not force no-op hooks into all native implementations. Existing native extension interfaces may be retained, renamed or split as in the migration map; they extend common roles, never replace them.
