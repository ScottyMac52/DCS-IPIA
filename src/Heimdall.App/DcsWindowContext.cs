using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;
using Heimdall.App.Games.Dcs;

namespace Heimdall.App
{
    /// <summary>Adapts the workspace host to the shared feature-view context contract.</summary>
    public sealed class DcsWindowContext : IDcsModuleWorkspaceContext
    {
        private readonly DcsWorkspaceSession _session;
        private readonly Func<Task> _save;
        private readonly Action _discard;

        #region BEGIN Public
        /// <summary>Connects feature views to the supplied module workspace host.</summary>
        public DcsWindowContext(DcsModuleWorkspaceWindow window)
        {
            _session = window.Session;
            _save = window.SaveWorkspaceAsync;
            _discard = () => window.DiscardClicked(null!, null!);
        }

        /// <summary>Gets only this mounted workspace’s isolated training scope.</summary>
        internal DcsTrainingEnvironment? Training => _session.Training;

        /// <inheritdoc/>
        public DcsEditorWorkspaceSnapshot? Working => _session.Working;
        /// <inheritdoc/>
        public DcsDraftAssignmentService? Draft => _session.Draft;
        /// <inheritdoc/>
        public DcsEditorWorkspaceSnapshot? Saved => _session.Saved;
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

        /// <inheritdoc/>
        public event EventHandler? StateChanged
        {
            add => _session.StateChanged += value;
            remove => _session.StateChanged -= value;
        }
        /// <inheritdoc/>
        public event EventHandler<string>? StatusChanged
        {
            add => _session.StatusChanged += value;
            remove => _session.StatusChanged -= value;
        }

        /// <summary>Notifies feature views of a workspace status message.</summary>
        public void RaiseStatus(string message) => _session.RaiseStatus(message);
        /// <summary>Notifies feature views that shared workspace state changed.</summary>
        public void RaiseStateChanged() => _session.RaiseStateChanged();

        /// <inheritdoc/>
        public void RunStructureEdit(Func<DcsEditorWorkspaceSnapshot, DcsEditorWorkspaceSnapshot> edit)
            => _session.RunStructureEdit(edit);

        /// <inheritdoc/>
        public Task SaveAsync() => _save();

        /// <inheritdoc/>
        public void Discard() => _discard();

        /// <inheritdoc/>
        public void SetStatus(string message) => RaiseStatus(message);

        /// <inheritdoc/>
        public void SetLabel(string key, string? value) => _session.SetLabel(key, value);
        #endregion END Public
    }
}
