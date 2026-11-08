using System.Text.Json;
using Heimdall.Application.Interfaces;

namespace Heimdall.Application.Games
{
    /// <summary>Adapts the existing multi-sim session to the shared context without imposing it on DCS.</summary>
    public abstract class GameProfileWorkspaceContextBase(IWorkspaceScheduler scheduler) : GameWorkspaceContextBase<GameWorkspace>(scheduler), IGameProfileSessionOwner
    {
        private GameProfileSession? _session;
        private GameWorkspace? _saved;
        private string? _revision;
        #region BEGIN Public
        /// <summary>Gets or installs the native multi-sim session, detaching the previous subscription exactly once.</summary>
        public GameProfileSession? Session
        {
            get => _session;
            set
            {
                if (ReferenceEquals(_session, value)) return;
                ReplaceNativeSession(() =>
                {
                    if (_session is not null) _session.Changed -= SessionChanged;
                    _session = value; _saved = value?.Snapshot;
                    if (_session is not null) _session.WorkspaceContext = this;
                    if (_session is not null) _session.Changed += SessionChanged;
                });
            }
        }
        /// <summary>Gets or accepts the native store revision; null means this session has never been persisted.</summary>
        public string? Revision { get => _revision; set { _revision = value; RefreshNativeState(); } }
        /// <summary>Gets the context-owned engine exposed temporarily to native browser/editor adapters.</summary>
        public GameAssignmentSelection NativeSelection => SelectionEngine;
        /// <summary>Gets shared profile chrome from the authoritative context state.</summary>
        public GameWorkspaceChromeState Chrome => new(State.Open, State.Dirty,
            State.Dirty && State.Capabilities.Save.Enabled, State.Dirty && State.Capabilities.Discard.Enabled,
            State.Open && !State.Dirty && !State.ReadOnly && State.Operation is null && State.Activity is null,
            !State.Open ? "No profile" : State.Dirty ? "Unsaved changes" : "Saved");
        #endregion END Public
        /// <summary>Maps native session facts while leaving capability policy to the native adapter.</summary>
        protected NativeWorkspaceFacts ProfileFacts(WorkspaceCapabilities capabilities) => new(
            _session?.Id.ToString("N") ?? "", _session is not null, _session?.AccessMessage.Length > 0,
            _session?.AccessMessage ?? "", _session?.HasChanges == true, _session is not null && Revision is null,
            _saved?.Name ?? "", capabilities);
        /// <summary>Combines native profile openness with explicit native input/link support.</summary>
        protected WorkspaceCapabilities ProfileCapabilities(bool inputAllowed, bool linksAllowed)
        {
            var opened = new WorkspaceAvailability(Session is not null, Session is null ? "Open a profile." : "");
            var input = new WorkspaceAvailability(Session is not null && inputAllowed, "Owner entitlement and an open profile are required for live input.");
            var link = new WorkspaceAvailability(Session is not null && linksAllowed, "Controller linking is unavailable in this profile.");
            return new(opened, opened, opened, new(true, ""), input, input, link);
        }
        /// <inheritdoc/>
        protected override GameWorkspace? ReadWorking() => _session?.Snapshot;
        /// <inheritdoc/>
        protected override GameWorkspace? ReadSaved() => _saved;
        /// <inheritdoc/>
        protected override string CaptureNativeFingerprint() => _session?.ContentFingerprint ?? "null";
        /// <inheritdoc/>
        protected override bool NameEquals(string left, string right) => string.Equals(left.Trim(), right, StringComparison.Ordinal);
        /// <inheritdoc/>
        protected override void DisposeNative() { if (_session is not null) _session.Changed -= SessionChanged; }
        #region BEGIN private helpers
        private void SessionChanged(object? sender, EventArgs e)
        {
            if (_session?.HasChanges == false) _saved = _session.Snapshot;
            NativeStateChanged();
        }
        #endregion END private helpers
    }
}
