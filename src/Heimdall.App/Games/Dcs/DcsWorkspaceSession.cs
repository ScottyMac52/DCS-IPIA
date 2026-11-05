using System.IO;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;

namespace Heimdall.App.Games.Dcs
{
    /// <summary>Owns DCS saved state, assignment drafts, labels and structural edits independently of a window.</summary>
    internal sealed class DcsWorkspaceSession : IDisposable
    {
        private readonly Dictionary<string, string> _labels = new(StringComparer.Ordinal);
        private bool _structureDirty;
        private bool _disposed;
        private bool _saving;

        internal DcsWorkspaceSession(DcsModuleWorkspaceViewModel viewModel, IDcsEditorWorkspaceRepository? repository,
            IConnectedDeviceDiscovery? deviceDiscovery, DcsTrainingEnvironment? training,
            HardwareInventoryManager? inventory, IUserMappingLibrary? userMappings,
            Func<DcsMappingRevisionReview, bool>? mappingReviewConfirmation,
            Func<bool, IReadOnlyList<string>?>? commandFilePicker, Func<string?>? integrationFilePicker,
            Func<string?>? axisDefaultsFilePicker, Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? inputSourceFactory,
            Action<string, string>? inputLogClipboardWriter)
        {
            training?.RequireRepository(repository);
            training?.RequireWorkspace(viewModel.Workspace ?? throw new InvalidOperationException("No training workspace."));
            Training = training; Repository = repository;
            Inventory = training is null ? inventory : null;
            UserMappings = training is null ? userMappings : null;
            MappingReviewConfirmation = mappingReviewConfirmation;
            DeviceDiscovery = training is null ? deviceDiscovery ?? new WindowsConnectedDeviceDiscovery() : new DcsTrainingDiscovery();
            CommandFilePicker = training is null ? commandFilePicker : _ => [Path.Combine(training.Root, "commands.json")];
            IntegrationFilePicker = training is null ? integrationFilePicker : () => null;
            AxisDefaultsFilePicker = training is null ? axisDefaultsFilePicker : () => null;
            InputSourceFactory = training is null ? inputSourceFactory : training.CreateInputSource;
            InputLogClipboardWriter = inputLogClipboardWriter;
            Saved = viewModel.Workspace; Working = Saved;
            foreach (var pair in viewModel.Labels) _labels[pair.Key] = pair.Value;
            SetDraft(viewModel.Draft);
        }

        #region BEGIN Public
        internal bool IsSaving => _saving;
        internal DcsTrainingEnvironment? Training { get; }
        internal IDcsEditorWorkspaceRepository? Repository { get; }
        internal IConnectedDeviceDiscovery DeviceDiscovery { get; }
        internal HardwareInventoryManager? Inventory { get; }
        internal IUserMappingLibrary? UserMappings { get; }
        internal Func<DcsMappingRevisionReview, bool>? MappingReviewConfirmation { get; }
        internal Func<bool, IReadOnlyList<string>?>? CommandFilePicker { get; }
        internal Func<string?>? IntegrationFilePicker { get; }
        internal Func<string?>? AxisDefaultsFilePicker { get; }
        internal Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? InputSourceFactory { get; }
        internal Action<string, string>? InputLogClipboardWriter { get; }
        internal string SavedGamesRoot { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games");
        internal DcsEditorWorkspaceSnapshot? Saved { get; private set; }
        internal DcsEditorWorkspaceSnapshot? Working { get; private set; }
        internal DcsDraftAssignmentService? Draft { get; private set; }
        internal IReadOnlyDictionary<string, string> Labels => _labels;
        internal bool HasPendingChanges => _structureDirty || Draft?.HasChanges == true ||
            Saved is not null && (_labels.Count != Saved.Labels.Count ||
                _labels.Any(pair => !Saved.Labels.TryGetValue(pair.Key, out var original) || original != pair.Value));
        internal event EventHandler? StateChanged;
        internal event EventHandler<string>? StatusChanged;

        internal void SetLabel(string key, string? value)
        {
            RequireEditable();
            if (string.IsNullOrWhiteSpace(value)) _labels.Remove(key);
            else _labels[key] = value;
            RaiseStateChanged();
        }

        internal void RunStructureEdit(Func<DcsEditorWorkspaceSnapshot, DcsEditorWorkspaceSnapshot> edit)
        {
            try
            {
                RequireEditable();
                var updated = edit(EditSnapshot());
                EditionAccess.RequireWorkspace(updated, DeviceDiscovery, Training);
                RebuildDraft(updated);
                Working = updated; _structureDirty = true;
                RestoreLabels(updated);
                RaiseStateChanged();
            }
            catch (Exception error) { RaiseStatus(error.Message); }
        }

        internal async Task SaveAsync()
        {
            RequireEditable();
            if (Repository is null || Saved is null || Draft is null)
                throw new InvalidOperationException("Open a writable DCS workspace before saving.");
            _saving = true;
            try
            {
                using var editingLock = Draft.SuspendEditing();
                RaiseStateChanged();
                var updated = EditSnapshot();
                EditionAccess.RequireWorkspace(updated, DeviceDiscovery, Training);
                updated = DcsPinnedUiLayerProfileRecovery.Restore(updated);
                EditionAccess.RequireWorkspace(updated, DeviceDiscovery, Training);
                DcsSnapshotLuaPayloadRenderer.ValidateUiLayerProfiles(updated);
                DcsModuleIntegrationImport.Instructions(updated);
                await Repository.SaveAsync(updated);
                Saved = updated; Working = updated; _structureDirty = false;
                RebuildDraft(updated);
            }
            catch (Exception error)
            {
                RaiseStatus("Database save failed: " + error.Message);
                throw;
            }
            finally { _saving = false; RaiseStateChanged(); }
        }

        internal void Discard()
        {
            RequireEditable();
            Working = Saved; _structureDirty = false;
            if (Saved is not null) RebuildDraft(Saved);
            else Draft?.Discard();
            RestoreLabels(Saved);
            RaiseStateChanged();
        }

        internal void RaiseStatus(string message) => StatusChanged?.Invoke(this, message);
        internal void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

        /// <summary>Detaches the current draft when the owning workspace closes.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            SetDraft(null);
            StateChanged = null; StatusChanged = null;
        }
        #endregion END Public

        #region BEGIN private helpers
        private void RequireEditable()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_saving) throw new InvalidOperationException("The workspace is being saved. Wait for saving to finish before editing.");
        }

        private DcsEditorWorkspaceSnapshot EditSnapshot()
        {
            var workspace = Working ?? Saved ?? throw new InvalidOperationException("Open a saved module to manage devices and modifiers.");
            return workspace with { Assignments = Draft?.Snapshot ?? workspace.Assignments,
                Labels = new Dictionary<string, string>(_labels, StringComparer.Ordinal) };
        }

        private void RebuildDraft(DcsEditorWorkspaceSnapshot workspace)
        {
            var commands = workspace.ModuleCommands.Select(command => new DcsDraftCommand(command.Id, command.Name, command.IsAxis, AssignmentLayer.Module))
                .Concat(workspace.UiCommands.Select(command => new DcsDraftCommand(command.DcsCommandId, command.Name, command.IsAxis, AssignmentLayer.UiLayer)));
            SetDraft(new DcsDraftAssignmentService(workspace.Configuration, commands, workspace.Assignments,
                DcsWorkspaceStructureEditor.UiModifierNames(workspace)));
        }

        private void SetDraft(DcsDraftAssignmentService? draft)
        {
            if (Draft is not null) Draft.Changed -= DraftChanged;
            Draft = draft;
            if (Draft is not null) Draft.Changed += DraftChanged;
        }

        private void RestoreLabels(DcsEditorWorkspaceSnapshot? workspace)
        {
            _labels.Clear();
            if (workspace is not null)
                foreach (var pair in workspace.Labels) _labels[pair.Key] = pair.Value;
        }

        private void DraftChanged(object? sender, EventArgs e) => RaiseStateChanged();
        #endregion END private helpers
    }
}
