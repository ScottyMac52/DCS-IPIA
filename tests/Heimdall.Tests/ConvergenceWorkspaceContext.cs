using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>Hosts immutable native state while testing convergent and divergent selection notifications.</summary>
    internal sealed class ConvergenceWorkspaceContext(IWorkspaceScheduler scheduler) : GameWorkspaceContextBase<WorkspaceTestSnapshot>(scheduler)
    {
        private WorkspaceTestSnapshot? _working, _saved;
        #region BEGIN Public
        /// <summary>Opens an independent immutable saved and working native state.</summary>
        public void Open() => ReplaceNativeSession(() => _saved = _working = new("native:key", "Profile", "saved"));
        /// <summary>Stages native data and publishes its content change.</summary>
        public void Edit(string data) { _working = _working! with { Data = data }; NativeStateChanged(); }
        #endregion END Public
        protected override NativeWorkspaceFacts ReadNativeFacts()
        {
            var ready = new WorkspaceAvailability(true, "");
            return new(_working?.Key ?? "", _working is not null, false, "", _working != _saved, false, _saved?.Name ?? "", new(ready, ready, ready, ready, ready, ready, ready));
        }
        protected override WorkspaceTestSnapshot? ReadWorking() => _working;
        protected override WorkspaceTestSnapshot? ReadSaved() => _saved;
        protected override string CaptureNativeFingerprint() => _working?.ToString() ?? "";
        protected override bool NameEquals(string left, string right) => left == right;
        protected override void DisposeNative() { }
    }
}
