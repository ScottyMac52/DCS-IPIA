using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;

namespace DcsIpia.Presentation
{
    public sealed class WindowContext : IModuleWorkspaceContext
    {
        private readonly ModuleWorkspaceWindow _window;

        public WindowContext(ModuleWorkspaceWindow window) => _window = window;

        public EditorWorkspaceSnapshot? Working => _window._working;
        public DraftAssignmentService? Draft => _window._draft;
        public EditorWorkspaceSnapshot? Saved => _window._saved;
        public IEditorWorkspaceRepository? Repository => _window._repository;
        public IConnectedDeviceDiscovery DeviceDiscovery => _window._deviceDiscovery;
        public HardwareInventoryManager? Inventory => _window._inventory;
        public IUserMappingLibrary? UserMappings => _window._userMappings;
        public Func<MappingRevisionReview, bool>? MappingReviewConfirmation => _window._mappingReviewConfirmation;
        public Func<string?>? AxisDefaultsFilePicker => _window._axisDefaultsFilePicker;
        public string SavedGamesRoot
        {
            get => _window._savedGamesRoot;
            set => _window._savedGamesRoot = value;
        }
        public IReadOnlyDictionary<string, string> Labels => _window._labels;

        public event EventHandler? StateChanged;
        public event EventHandler<string>? StatusChanged;

        public void RaiseStatus(string message) => StatusChanged?.Invoke(this, message);
        public void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

        public void RunStructureEdit(Func<EditorWorkspaceSnapshot, EditorWorkspaceSnapshot> edit)
            => _window.RunStructureEdit(edit);

        public Task SaveAsync()
        {
            _window.SaveClicked(null!, null!);
            return Task.CompletedTask;
        }

        public void Discard() => _window.DiscardClicked(null!, null!);

        public void SetStatus(string message) => RaiseStatus(message);

        public void SetLabel(string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                _window._labels.Remove(key);
            else
                _window._labels[key] = value;
            RaiseStateChanged();
        }
    }
}