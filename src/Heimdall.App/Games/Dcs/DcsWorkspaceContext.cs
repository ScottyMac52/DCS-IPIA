using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;

namespace Heimdall.App.Games.Dcs
{
    /// <summary>Supplies DCS session, repository and draft behavior to the common workspace context.</summary>
    public sealed class DcsWorkspaceContext : GameWorkspaceContextBase<DcsEditorWorkspaceSnapshot>, IDcsModuleWorkspaceContext
    {
        private readonly DcsWorkspaceSession _session;
        private readonly Func<Task> _save;
        private readonly Action _discard;
        private DcsDraftAssignmentService? _observedDraft;
        /// <summary>Connects the single native DCS session to its window entry points.</summary>
        internal DcsWorkspaceContext(DcsWorkspaceSession session, IWorkspaceScheduler scheduler, Func<Task> save, Action discard) : base(scheduler)
        {
            _session = session; _save = save; _discard = discard;
            _observedDraft = _session.Draft; _session.Context = this;
            _session.StateChanged += SessionChanged; _session.StatusChanged += SessionStatus;
            ReplaceNativeSession(() => { });
        }
        #region BEGIN Public
        /// <summary>Gets only this mounted workspace’s isolated training scope.</summary>
        internal DcsTrainingEnvironment? Training => _session.Training;

        /// <inheritdoc/>
        public DcsDraftAssignmentService? Draft => _session.Draft;
        /// <inheritdoc/>
        public IDcsEditorWorkspaceRepository? Repository => _session.Repository;
        /// <inheritdoc/>
        public IConnectedDeviceDiscovery DeviceDiscovery => _session.DeviceDiscovery;
        /// <inheritdoc/>
        public HardwareInventoryManager? Inventory => _session.Inventory;
        /// <inheritdoc/>
        public IUserMappingLibrary? UserMappings => _session.UserMappings;
        /// <inheritdoc/>
        public Func<DcsMappingRevisionReview, bool>? MappingReviewConfirmation => _session.MappingReviewConfirmation;
        /// <inheritdoc/>
        public Func<bool, IReadOnlyList<string>?>? CommandFilePicker => _session.CommandFilePicker;
        /// <inheritdoc/>
        public Func<string?>? IntegrationFilePicker => _session.IntegrationFilePicker;
        /// <inheritdoc/>
        public Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? InputSourceFactory => _session.InputSourceFactory;
        /// <inheritdoc/>
        public Action<string, string>? InputLogClipboardWriter => _session.InputLogClipboardWriter;
        /// <inheritdoc/>
        public Func<string?>? AxisDefaultsFilePicker => _session.AxisDefaultsFilePicker;
        /// <inheritdoc/>
        public string SavedGamesRoot
        {
            get => _session.SavedGamesRoot;
            set => _session.SavedGamesRoot = value;
        }
        /// <inheritdoc/>
        public IReadOnlyDictionary<string, string> Labels => _session.Labels;

        /// <summary>Publishes native status through the inherited notification owner.</summary>
        public void RaiseStatus(string message) => ReportStatus(message);
        /// <summary>Recomputes native state without multiplying subscribers.</summary>
        public void RaiseStateChanged() => RefreshNativeState();
        /// <inheritdoc/>
        public WorkspaceOperationResult RunStructureEdit(Func<DcsEditorWorkspaceSnapshot, DcsEditorWorkspaceSnapshot> edit)
        {
            var result = RunOperation(WorkspaceOperationKind.NativeEdit, _ => _session.RunStructureEdit(edit));
            if (result.Outcome == WorkspaceOperationOutcome.Blocked) ReportStatus(result.Message);
            return result;
        }
        /// <inheritdoc/>
        public Task SaveAsync() => _save();
        /// <inheritdoc/>
        public void Discard() => _discard();
        /// <inheritdoc/>
        public void SetStatus(string message) => ReportStatus(message);
        /// <inheritdoc/>
        public void SetLabel(string key, string? value) => RunEdit(() => _session.SetLabel(key, value));
        #endregion END Public
        protected override NativeWorkspaceFacts ReadNativeFacts()
        {
            var opened = _session.Saved is not null;
            var access = EditionAccess.WorkspaceAccess(_session.Working, _session.Training);
            var edit = new WorkspaceAvailability(opened && access.Enabled, opened ? access.Reason : "Open a DCS workspace before editing.");
            var save = new WorkspaceAvailability(edit.Enabled && _session.Repository is not null, "Open a writable DCS workspace before saving.");
            var input = new WorkspaceAvailability(opened && (_session.Training is not null || EditionAccess.Policy.HasPremium), "Owner entitlement or an isolated training workspace is required for input testing.");
            return new(_session.Saved?.Configuration.Module.Id.ToString("N") ?? "", opened, opened && !access.Enabled, access.Reason,
                _session.HasPendingChanges, false, _session.Saved?.Configuration.Module.DisplayName ?? "",
                new(save, new(opened, "Open a DCS workspace."), edit, new(true, ""), input, input, edit));
        }
        protected override DcsEditorWorkspaceSnapshot? ReadWorking() => _session.Working;
        protected override DcsEditorWorkspaceSnapshot? ReadSaved() => _session.Saved;
        protected override string CaptureNativeFingerprint() => JsonSerializer.Serialize(new
        {
            Workspace = _session.Working,
            Assignments = _session.Draft?.Snapshot.OrderBy(x => x.Slot.Key, StringComparer.Ordinal).ToArray(),
            Labels = _session.Labels.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray()
        });
        protected override bool NameEquals(string left, string right) => string.Equals(left, right, StringComparison.Ordinal);
        protected override void DisposeNative()
        {
            _session.StateChanged -= SessionChanged; _session.StatusChanged -= SessionStatus; _session.Dispose();
        }
        #region BEGIN private helpers
        private void SessionChanged(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (!ReferenceEquals(_observedDraft, _session.Draft)) { _observedDraft = _session.Draft; InvalidateNativeScope(); }
            NativeStateChanged();
        }
        private void SessionStatus(object? sender, string message) => ReportStatus(message);
        private void RunEdit(Action edit)
        {
            var result = RunOperation(WorkspaceOperationKind.NativeEdit, _ => { edit(); return new(WorkspaceOperationOutcome.Applied, ""); });
            if (result.Outcome == WorkspaceOperationOutcome.Blocked) throw new InvalidOperationException(result.Message);
        }
        #endregion END private helpers
    }
}
