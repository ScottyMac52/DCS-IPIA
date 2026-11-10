using Heimdall.Application;
using Heimdall.Application.Games;

namespace Heimdall.Tests
{
    /// <summary>Hosts the production DCS browser over a real native snapshot with common state and invalidation.</summary>
    public sealed class DcsBrowserContext : GameWorkspaceContextBase<DcsEditorWorkspaceSnapshot>
    {
        private DcsEditorWorkspaceSnapshot _working;
        private bool _open = true;
        private readonly DcsEditorWorkspaceSnapshot _saved;
        /// <summary>Opens a native snapshot under the production shared context algorithm.</summary>
        public DcsBrowserContext(DcsEditorWorkspaceSnapshot snapshot) : base(new WorkspaceTestScheduler())
        { _working = _saved = snapshot; ReplaceNativeSession(() => { }); }
        #region BEGIN Public
        /// <summary>Closes the native workspace through the common identity transition.</summary>
        public void Close() => ReplaceNativeSession(() => _open = false);
        /// <summary>Reopens its native snapshot without reusing the old generation.</summary>
        public void Reopen() => ReplaceNativeSession(() => _open = true);
        /// <summary>Gets how many native working snapshots the browser requested.</summary>
        public int Reads { get; private set; }
        /// <summary>Stages real native label data and advances the authoritative content revision.</summary>
        public void Edit(string value)
        {
            _working = _working with { Labels = new Dictionary<string, string>(_working.Labels) { ["browser"] = value } };
            NativeStateChanged();
        }
        #endregion END Public
        protected override NativeWorkspaceFacts ReadNativeFacts()
        {
            var yes = new WorkspaceAvailability(true, "");
            return new(_working.Configuration.Module.Id.ToString("N"), _open, false, "", _working != _saved, false,
                _saved.Configuration.Module.DisplayName, new(yes, yes, yes, yes, yes, yes, yes));
        }
        protected override DcsEditorWorkspaceSnapshot? ReadWorking() { if (!_open) return null; Reads++; return _working; }
        protected override DcsEditorWorkspaceSnapshot ReadSaved() => _saved;
        protected override string CaptureNativeFingerprint() => System.Text.Json.JsonSerializer.Serialize(_working);
        protected override bool NameEquals(string left, string right) => left == right;
        protected override void DisposeNative() { }
    }
}
