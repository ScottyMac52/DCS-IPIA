using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;

namespace Heimdall.App
{
    /// <summary>Adapts the workspace host to the shared feature-view context contract.</summary>
    public sealed class WindowContext : IModuleWorkspaceContext
    {
        private readonly ModuleWorkspaceWindow _window;

        #region BEGIN Public
        /// <summary>Connects feature views to the supplied module workspace host.</summary>
        public WindowContext(ModuleWorkspaceWindow window) => _window = window;

        /// <summary>Gets only this mounted workspace’s isolated training scope.</summary>
        internal TrainingEnvironment? Training => _window._training;

        /// <inheritdoc/>
        public EditorWorkspaceSnapshot? Working => _window._working;
        /// <inheritdoc/>
        public DraftAssignmentService? Draft => _window._draft;
        /// <inheritdoc/>
        public EditorWorkspaceSnapshot? Saved => _window._saved;
        /// <inheritdoc/>
        public IEditorWorkspaceRepository? Repository => _window._repository;
        /// <inheritdoc/>
        public IConnectedDeviceDiscovery DeviceDiscovery => _window._deviceDiscovery;
        /// <inheritdoc/>
        public HardwareInventoryManager? Inventory => _window._inventory;
        /// <inheritdoc/>
        public IUserMappingLibrary? UserMappings => _window._userMappings;
        /// <inheritdoc/>
        public Func<MappingRevisionReview, bool>? MappingReviewConfirmation => _window._mappingReviewConfirmation;
        /// <inheritdoc/>
        public Func<bool, IReadOnlyList<string>?>? CommandFilePicker => _window._commandFilePicker;
        /// <inheritdoc/>
        public Func<string?>? IntegrationFilePicker => _window._integrationFilePicker;
        /// <inheritdoc/>
        public Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? InputSourceFactory => _window._inputSourceFactory;
        /// <inheritdoc/>
        public Action<string, string>? InputLogClipboardWriter => _window._inputLogClipboardWriter;
        /// <inheritdoc/>
        public Func<string?>? AxisDefaultsFilePicker => _window._axisDefaultsFilePicker;
        /// <inheritdoc/>
        public string SavedGamesRoot
        {
            get => _window._savedGamesRoot;
            set => _window._savedGamesRoot = value;
        }
        /// <inheritdoc/>
        public IReadOnlyDictionary<string, string> Labels => _window._labels;

        /// <inheritdoc/>
        public event EventHandler? StateChanged;
        /// <inheritdoc/>
        public event EventHandler<string>? StatusChanged;

        /// <summary>Notifies feature views of a workspace status message.</summary>
        public void RaiseStatus(string message) => StatusChanged?.Invoke(this, message);
        /// <summary>Notifies feature views that shared workspace state changed.</summary>
        public void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

        /// <inheritdoc/>
        public void RunStructureEdit(Func<EditorWorkspaceSnapshot, EditorWorkspaceSnapshot> edit)
            => _window.RunStructureEdit(edit);

        /// <inheritdoc/>
        public Task SaveAsync() => _window.SaveWorkspaceAsync();

        /// <inheritdoc/>
        public void Discard() => _window.DiscardClicked(null!, null!);

        /// <inheritdoc/>
        public void SetStatus(string message) => RaiseStatus(message);

        /// <inheritdoc/>
        public void SetLabel(string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                _window._labels.Remove(key);
            else
                _window._labels[key] = value;
            _window.UpdateBuildAvailability();
            RaiseStateChanged();
        }
        #endregion END Public
    }
}
