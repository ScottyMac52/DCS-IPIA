using Heimdall.Application.Games;

namespace Heimdall.Tests
{
    /// <summary>Injects native steps into the actual context base without replacing its orchestration.</summary>
    public sealed class WorkspaceContextProbe(Heimdall.Application.Interfaces.IWorkspaceScheduler scheduler) : GameWorkspaceContextBase<WorkspaceTestSnapshot>(scheduler)
    {
        private WorkspaceTestSnapshot? _working;
        private WorkspaceTestSnapshot? _saved;
        #region BEGIN Public
        /// <summary>Uses the deterministic test owner queue.</summary>
        public WorkspaceContextProbe(WorkspaceTestScheduler scheduler) : this((Heimdall.Application.Interfaces.IWorkspaceScheduler)scheduler) { }
        /// <summary>Controls a native readonly restriction independently of dirty state.</summary>
        public bool ReadOnly { get; set; }
        /// <summary>Controls a supported capability independently of workspace openness.</summary>
        public bool Supported { get; set; } = true;
        /// <summary>Counts native lifetime release.</summary>
        public int Disposals { get; private set; }
        /// <summary>Gets the managed thread that sealed the stale native session, or zero.</summary>
        public int SealThreadId { get; private set; }
        /// <summary>Opens an independent immutable native session.</summary>
        public void Open(string key = "native:key", string name = "Profile") => ReplaceNativeSession(() => _saved = _working = new(key, name, "saved"));
        /// <summary>Stages native data and raises the native subscription boundary.</summary>
        public void Edit(string data) { _working = _working! with { Data = data }; NativeStateChanged(); }
        /// <summary>Marks a baseline only after a test persistence step succeeds.</summary>
        public void Accept() { _working = _working! with { Name = State.EditedName }; _saved = _working; NativeStateChanged(); }
        /// <summary>Installs a durable persisted candidate as both working and saved baseline.</summary>
        public void Install(WorkspaceTestSnapshot persisted) { _saved = _working = persisted; NativeStateChanged(); }
        /// <summary>Installs a persisted candidate as a fresh native session (the Elite reopen-after-save path).</summary>
        public void Reopen(WorkspaceTestSnapshot persisted) => ReplaceNativeSession(() => _saved = _working = persisted);
        /// <summary>Restores the native saved baseline and invalidates old review targets.</summary>
        public void Restore() { _working = _saved; InvalidateNativeScope(); NativeStateChanged(); }
        #endregion END Public
        /// <inheritdoc/>
        protected override NativeWorkspaceFacts ReadNativeFacts()
        {
            var ready = new WorkspaceAvailability(Supported, Supported ? "" : "Native capability unavailable.");
            return new(_working?.Key ?? "", _working is not null, ReadOnly, "Native readonly policy.", _working != _saved,
                false, _saved?.Name ?? "", new(ready, ready, ready, ready, ready, ready, ready));
        }
        /// <inheritdoc/>
        protected override WorkspaceTestSnapshot? ReadWorking() => _working;
        /// <inheritdoc/>
        protected override WorkspaceTestSnapshot? ReadSaved() => _saved;
        /// <inheritdoc/>
        protected override string CaptureNativeFingerprint() => _working?.ToString() ?? "";
        /// <inheritdoc/>
        protected override bool NameEquals(string left, string right) => string.Equals(left, right, StringComparison.Ordinal);
        /// <inheritdoc/>
        protected override void DisposeNative() => Disposals++;
        /// <inheritdoc/>
        protected override void SealStaleNative() => SealThreadId = Environment.CurrentManagedThreadId;
    }
}
