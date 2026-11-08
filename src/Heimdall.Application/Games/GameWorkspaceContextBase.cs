using System.Runtime.ExceptionServices;
using System.Diagnostics;
using System.Globalization;
using Heimdall.Application.Interfaces;

namespace Heimdall.Application.Games
{
    /// <summary>Owns identity, selection, operations, activities, notifications and disposal for every game.</summary>
    public abstract class GameWorkspaceContextBase<TSnapshot> : IGameWorkspaceContext<TSnapshot>, IWorkspaceSelectionEngineOwner where TSnapshot : class
    {
        private readonly IWorkspaceScheduler _scheduler;
        private WorkspaceState _state = new(new("", 0), 0, false, false, false, "", new(null, null, null, null, null, ""), null, null, Unavailable("Open a workspace."));
        private WorkspaceState? _published;
        private WorkspaceActivityLease? _activity;
        private WorkspaceOperationLease? _operation;
        private CancellationTokenSource? _operationCancellation;
        private string _editedName = "";
        private string? _fingerprint;
        private long _contentVersion;
        private static long _epochs;
        private long _generation = Interlocked.Increment(ref _epochs);
        private int _deferred;
        private bool _publishing;
        private bool _posted;
        private bool _nativeChanged;
        private bool _disposed;
        private bool _nativeDisposed;
        private bool _selecting;

        /// <summary>Creates one scheduler-bound selection and assignment workflow for this workspace.</summary>
        protected GameWorkspaceContextBase(IWorkspaceScheduler scheduler)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            SelectionEngine.Changed += SelectionChanged;
        }
        #region BEGIN Public
        /// <inheritdoc/>
        public WorkspaceState State => _state;
        /// <inheritdoc/>
        public long ContentVersion => _contentVersion;
        /// <inheritdoc/>
        GameAssignmentSelection IWorkspaceSelectionEngineOwner.SelectionEngine => SelectionEngine;
        /// <inheritdoc/>
        public string Status { get; private set; } = "";
        /// <inheritdoc/>
        public bool OwnsOperation(IWorkspaceOperationLease lease) => _operation is not null && ReferenceEquals(_operation, lease);
        /// <inheritdoc/>
        public WorkspaceAssignmentWorkflow AssignmentWorkflow { get; } = new();
        /// <inheritdoc/>
        public TSnapshot? Working => ReadWorking();
        /// <inheritdoc/>
        public TSnapshot? Saved => ReadSaved();
        /// <inheritdoc/>
        public event EventHandler? StateChanged;
        /// <inheritdoc/>
        public event EventHandler<string>? StatusChanged;
        /// <inheritdoc/>
        public WorkspaceAssignmentContext Context
        {
            get
            {
                RefreshNativeState();
                var selected = State.Selection;
                var scope = Encode(State.Identity.Key) + Encode(State.Identity.Generation.ToString(CultureInfo.InvariantCulture)) +
                    Encode(selected.ControllerId) + Encode(selected.NativeScope) + Encode(selected.SlotId) + Encode(selected.AlternativeId);
                return new(scope, State.Open, State.ReadOnly || SelectionReadOnly, State.Operation is not null || State.Activity is WorkspaceActivityKind.Scan or WorkspaceActivityKind.Identify, State.Activity == WorkspaceActivityKind.Test);
            }
        }
        /// <inheritdoc/>
        public string CaptureSnapshot() => Encode(CaptureNativeFingerprint()) + Encode(_editedName);
        /// <inheritdoc/>
        public void Select(WorkspaceSelection selection)
        {
            ArgumentNullException.ThrowIfNull(selection); RequireIdle();
            var previous = State.Selection;
            using var refresh = DeferRefresh();
            SelectionEngine.SelectScope(selection.NativeScope);
            SelectionEngine.SelectController(selection.ControllerId);
            SelectionEngine.SelectCommand(selection.CommandId);
            SelectionEngine.SelectSlot(selection.SlotId ?? "");
            SelectionEngine.SelectAlternative(previous.ControllerId == selection.ControllerId && previous.NativeScope == selection.NativeScope ? selection.AlternativeId : null);
            if (previous.ControllerId == selection.ControllerId && previous.NativeScope == selection.NativeScope &&
                previous.CommandId == selection.CommandId && previous.SlotId == selection.SlotId && previous.AlternativeId == selection.AlternativeId)
                SelectionEngine.SelectInput(selection.InputId);
        }
        /// <inheritdoc/>
        public void SetEditedName(string name)
        {
            ArgumentNullException.ThrowIfNull(name); RequireIdle();
            _editedName = name; RefreshNativeState();
        }
        /// <inheritdoc/>
        public void RefreshNativeState()
        {
            _scheduler.VerifyAccess();
            if (_disposed) return;
            var native = ReadNativeFacts();
            var selection = new WorkspaceSelection(SelectionEngine.ControllerId, SelectionEngine.CommandId, SelectionEngine.Slot,
                SelectionEngine.AlternativeId, SelectionEngine.InputKey, SelectionEngine.Scope);
            var candidate = new WorkspaceState(new(native.Key, _generation), State.Version, native.Open, native.ReadOnly,
                native.Dirty || native.New || native.Open && !NameEquals(_editedName, native.SavedName), _editedName, selection,
                _operation?.Kind, _activity?.Kind, Capabilities(native));
            if (candidate != State || _nativeChanged)
            {
                _state = candidate with { Version = State.Version + 1 }; _nativeChanged = false;
            }
            Publish(false);
        }
        /// <inheritdoc/>
        public IDisposable DeferRefresh()
        {
            VerifyAlive(); _deferred++;
            return new WorkspaceRefreshScope(() => { _scheduler.VerifyAccess(); _deferred--; if (!_disposed) RefreshNativeState(); });
        }
        /// <inheritdoc/>
        public void ReportStatus(string message)
        {
            ArgumentNullException.ThrowIfNull(message); _scheduler.VerifyAccess();
            if (_disposed || Status == message) return;
            Status = message;
            var wasPublishing = _publishing; _publishing = true;
            try
            {
                if (StatusChanged is not null)
                    foreach (EventHandler<string> handler in StatusChanged.GetInvocationList()) Notify(() => handler(this, message));
            }
            finally { _publishing = wasPublishing; }
        }
        /// <inheritdoc/>
        public WorkspaceOperationResult RunOperation(WorkspaceOperationKind kind, Func<IWorkspaceOperationLease, WorkspaceOperationResult> action)
        {
            ArgumentNullException.ThrowIfNull(action); _scheduler.VerifyAccess();
            if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            var reason = Admission(kind);
            if (reason.Length != 0) return new(WorkspaceOperationOutcome.Blocked, reason);
            var lease = BeginOperation(kind, default);
            try { var result = action(lease); CompleteOperation(kind, result); return result; }
            finally { EndOperation(); }
        }
        /// <inheritdoc/>
        public async Task<WorkspaceOperationResult> RunOperationAsync(WorkspaceOperationKind kind,
            Func<IWorkspaceOperationLease, Task<WorkspaceOperationResult>> action, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(action); _scheduler.VerifyAccess();
            if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (cancellationToken.IsCancellationRequested) return new(WorkspaceOperationOutcome.Cancelled, "Operation cancelled.");
            var reason = Admission(kind);
            if (reason.Length != 0) return new(WorkspaceOperationOutcome.Blocked, reason);
            var lease = BeginOperation(kind, cancellationToken);
            WorkspaceOperationResult? result = null; ExceptionDispatchInfo? failure = null;
            try { result = await action(lease); }
            catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
            // Owner-thread contract: callers start on the owner (the WPF dispatcher in production), whose SynchronizationContext
            // normally resumes here. If a caller lacked that context, completion is finished centrally on the owner so the
            // operation is never stranded and a durable result is never hidden; the access checks themselves are unchanged.
            if (_scheduler.CheckAccess()) return FinishOperation(kind, lease, result, failure);
            Trace.TraceWarning("Workspace {0} operation resumed off the owner thread; completing on the owner.", kind);
            var finished = new TaskCompletionSource<WorkspaceOperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _scheduler.Post(() =>
            {
                try { finished.SetResult(FinishOperation(kind, lease, result, failure)); }
                catch (Exception error) { finished.SetException(error); }
            });
            return await finished.Task;
        }
        /// <inheritdoc/>
        public WorkspaceAvailability TryStartActivity(WorkspaceActivityKind kind, out IWorkspaceActivityLease? lease)
        {
            _scheduler.VerifyAccess(); lease = null;
            if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (_disposed) return new(false, "The workspace is closed.");
            RefreshNativeState();
            var replacesScan = kind == WorkspaceActivityKind.Scan && _activity?.Kind == WorkspaceActivityKind.Scan;
            if (_operation is not null || _activity is not null && !replacesScan) return new(false, "Finish the current operation or input activity first.");
            // A running scan suppresses UI capabilities; replacement must still satisfy the fresh native scan policy.
            var eligible = replacesScan ? ReadNativeFacts().Capabilities.Scan : kind switch
            { WorkspaceActivityKind.Scan => State.Capabilities.Scan, WorkspaceActivityKind.Identify => State.Capabilities.Identify, _ => State.Capabilities.Test };
            if (!eligible.Enabled) return string.IsNullOrEmpty(eligible.Reason) ? eligible with { Reason = "This activity is unavailable." } : eligible;
            var previous = _activity;
            _activity = new(State.Identity, kind, ActivityReleased); lease = _activity;
            // Reserve the new lease first: old cleanup cannot publish an idle gap or admit a save between scans.
            previous?.Dispose();
            RefreshNativeState(); return new(true, "");
        }
        /// <inheritdoc/>
        public void StopActivity() { _scheduler.VerifyAccess(); _activity?.Dispose(); }
        /// <inheritdoc/>
        public void Dispose()
        {
            _scheduler.VerifyAccess();
            if (_disposed) return;
            _disposed = true; _generation = Interlocked.Increment(ref _epochs); StateChanged = null; StatusChanged = null;
            SelectionEngine.Changed -= SelectionChanged;
            try { _operationCancellation?.Cancel(); }
            catch (AggregateException error) { Trace.TraceError("Workspace cancellation failed: {0}", error); }
            finally
            {
                StopActivity();
                _state = State with { Identity = State.Identity with { Generation = _generation }, Open = false, Activity = null, Capabilities = Unavailable("The workspace is closed.") };
                if (_operation is null) ReleaseNative();
            }
            GC.SuppressFinalize(this);
        }
        #endregion END Public
        /// <summary>Gets the existing mutable selection engine for native adapters during role migration.</summary>
        protected GameAssignmentSelection SelectionEngine { get; } = new("");
        /// <summary>Gets whether lifetime teardown has begun.</summary>
        protected bool IsDisposed => _disposed;
        /// <summary>Gets native selection restrictions, such as captured system records.</summary>
        protected virtual bool SelectionReadOnly => false;
        /// <summary>Reads native session eligibility and dirty facts without owning common state.</summary>
        protected abstract NativeWorkspaceFacts ReadNativeFacts();
        /// <summary>Reads the typed working snapshot.</summary>
        protected abstract TSnapshot? ReadWorking();
        /// <summary>Reads the typed saved baseline.</summary>
        protected abstract TSnapshot? ReadSaved();
        /// <summary>Captures canonical native draft state for stale review and change detection.</summary>
        protected abstract string CaptureNativeFingerprint();
        /// <summary>Compares names using native identity rules.</summary>
        protected abstract bool NameEquals(string left, string right);
        /// <summary>Detaches native session subscriptions after any admitted write has drained.</summary>
        protected abstract void DisposeNative();
        /// <summary>Installs a native session using the same operation owner; permits save/discard baseline rebuilds.</summary>
        protected void ReplaceNativeSession(Action replace)
        {
            ArgumentNullException.ThrowIfNull(replace); VerifyAlive();
            if (_operation is not null && _operation.Kind is not (WorkspaceOperationKind.Save or WorkspaceOperationKind.Discard or WorkspaceOperationKind.Replace or WorkspaceOperationKind.Import))
                throw new InvalidOperationException("Finish the current operation before replacing the workspace.");
            var ownsOperation = _operation is null;
            if (ownsOperation)
            {
                var reason = Admission(WorkspaceOperationKind.Replace);
                if (reason.Length > 0) throw new InvalidOperationException(reason);
                BeginOperation(WorkspaceOperationKind.Replace, default);
            }
            try
            {
                using var refresh = DeferRefresh();
                replace(); _generation = Interlocked.Increment(ref _epochs); SelectionEngine.Reset();
                _editedName = ReadNativeFacts().SavedName; _fingerprint = CaptureNativeFingerprint(); _contentVersion++;
                RefreshNativeState();
            }
            finally { if (ownsOperation) EndOperation(); }
        }
        /// <summary>Coalesces actual native content changes, including changes while a draft was already dirty.</summary>
        protected void NativeStateChanged()
        {
            if (_disposed) return;
            var fingerprint = CaptureNativeFingerprint();
            if (_fingerprint != fingerprint) { _nativeChanged = true; _contentVersion++; }
            _fingerprint = fingerprint;
            RefreshNativeState();
        }
        /// <summary>Invalidates reviewed targets after restoring a native baseline.</summary>
        protected void InvalidateNativeScope()
        {
            VerifyAlive(); _generation = Interlocked.Increment(ref _epochs); _contentVersion++; SelectionEngine.Reset(); RefreshNativeState();
        }
        #region BEGIN private helpers
        private void CompleteOperation(WorkspaceOperationKind kind, WorkspaceOperationResult result)
        {
            if (result.Outcome != WorkspaceOperationOutcome.Applied || _disposed) return;
            if (kind == WorkspaceOperationKind.Save && result.Committed) _editedName = ReadNativeFacts().SavedName;
            if (kind != WorkspaceOperationKind.Discard) return;
            _editedName = ReadNativeFacts().SavedName; _generation = Interlocked.Increment(ref _epochs); _contentVersion++; SelectionEngine.Reset();
        }
        private WorkspaceOperationResult FinishOperation(WorkspaceOperationKind kind, WorkspaceOperationLease lease, WorkspaceOperationResult? result, ExceptionDispatchInfo? failure)
        {
            _scheduler.VerifyAccess();
            try
            {
                if (failure is null) { CompleteOperation(kind, result!); return result!; }
                if (failure.SourceException is OperationCanceledException && lease.Cancellation.IsCancellationRequested)
                    return new(WorkspaceOperationOutcome.Cancelled, "Operation cancelled; the workspace is retained.");
                failure.Throw(); throw failure.SourceException;
            }
            finally { EndOperation(); }
        }
        private WorkspaceOperationLease BeginOperation(WorkspaceOperationKind kind, CancellationToken token)
        {
            RefreshNativeState();
            _operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            _operation = new(State.Identity, kind, _operationCancellation.Token);
            try
            {
                RefreshNativeState();
                StopActivity();
                RefreshNativeState(); Publish(true); return _operation;
            }
            catch
            {
                _operation = null; _operationCancellation.Dispose(); _operationCancellation = null;
                _state = State with { Operation = null, Activity = _activity?.Kind, Version = State.Version + 1 };
                Publish(true); throw;
            }
        }
        private void EndOperation()
        {
            _scheduler.VerifyAccess(); _operation = null;
            _operationCancellation!.Dispose(); _operationCancellation = null;
            _state = State with { Operation = null, Version = State.Version + 1 };
            if (_disposed) ReleaseNative(); else { RefreshNativeState(); Publish(true); }
        }
        private static string Encode(string? text) => (text?.Length ?? 0).ToString(CultureInfo.InvariantCulture) + ":" + text;
        private static WorkspaceCapabilities Unavailable(string reason)
        {
            var no = new WorkspaceAvailability(false, reason); return new(no, no, no, no, no, no, no);
        }
        private WorkspaceCapabilities Capabilities(NativeWorkspaceFacts native)
        {
            if (_operation is not null) return Unavailable("Finish the current workspace operation.");
            if (_activity is not null) return Unavailable("Stop the current input activity first.");
            var no = new WorkspaceAvailability(false, native.ReadOnlyReason.Length == 0 ? "This workspace is read-only." : native.ReadOnlyReason);
            return native.ReadOnly ? native.Capabilities with { Save = no, Edit = no, Link = no } : native.Capabilities;
        }
        private string Admission(WorkspaceOperationKind kind)
        {
            if (_disposed) return "The workspace is closed.";
            if (_operation is not null || _publishing) return "Another workspace operation is still running.";
            if (_activity?.Kind == WorkspaceActivityKind.Scan) return "Finish controller scanning before continuing.";
            var native = ReadNativeFacts();
            if (kind is WorkspaceOperationKind.Import or WorkspaceOperationKind.Replace) return "";
            if (!native.Open) return "Open a workspace before continuing.";
            var eligibility = kind switch
            {
                WorkspaceOperationKind.Save => native.Capabilities.Save,
                WorkspaceOperationKind.Discard => native.Capabilities.Discard,
                WorkspaceOperationKind.NativeEdit => native.Capabilities.Edit,
                WorkspaceOperationKind.ControllerChange => native.Capabilities.Link,
                _ => new WorkspaceAvailability(true, "")
            };
            if (native.ReadOnly && kind is WorkspaceOperationKind.Save or WorkspaceOperationKind.NativeEdit or WorkspaceOperationKind.ControllerChange)
                return native.ReadOnlyReason.Length == 0 ? "This workspace is read-only." : native.ReadOnlyReason;
            return eligibility.Enabled ? "" : string.IsNullOrEmpty(eligibility.Reason) ? "This operation is unavailable." : eligibility.Reason;
        }
        private void VerifyAlive() { _scheduler.VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this); }
        private void RequireIdle()
        {
            VerifyAlive();
            if (_operation is not null) throw new InvalidOperationException("Finish the current workspace operation first.");
        }
        private void SelectionChanged(object? sender, EventArgs e)
        {
            if (_selecting || _disposed) return;
            _selecting = true;
            try { RefreshNativeState(); }
            finally { _selecting = false; }
        }
        private void ActivityReleased(WorkspaceActivityLease activity)
        {
            if (!ReferenceEquals(_activity, activity)) return;
            _activity = null; if (!_disposed) RefreshNativeState();
        }
        private void Publish(bool force)
        {
            if (_disposed || (!force && (_deferred > 0 || _operation is not null)) || _published == State) return;
            if (_publishing)
            {
                if (!_posted) { _posted = true; _scheduler.Post(() => { _posted = false; RefreshNativeState(); }); }
                return;
            }
            _published = State; _publishing = true;
            try
            {
                if (StateChanged is not null)
                    foreach (EventHandler handler in StateChanged.GetInvocationList()) Notify(() => handler(this, EventArgs.Empty));
            }
            finally { _publishing = false; }
        }
        private void ReleaseNative()
        {
            if (_nativeDisposed) return;
            _nativeDisposed = true; DisposeNative();
        }
        private static void Notify(Action handler)
        {
            try { handler(); }
            catch (Exception error) { Trace.TraceError("Workspace notification failed: {0}", error); }
        }
        #endregion END private helpers
    }
}
