using System.IO;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;
using Heimdall.Domain;

namespace Heimdall.App.Games.Dcs
{
    /// <summary>Owns DCS saved state, assignment drafts, labels and structural edits independently of a window.</summary>
    internal sealed class DcsWorkspaceSession : IDisposable
    {
        private readonly Dictionary<string, string> _labels = new(StringComparer.Ordinal);
        private bool _structureDirty;
        private bool _disposed;
        private IDisposable? _saveLock;
        private IDisposable? _staleLock;
        private bool _stale;
        private (DcsEditorWorkspaceSnapshot Candidate, DcsDraftAssignmentService Draft)? _preparedBaseline;
        internal IGameWorkspaceContext? Context { get; set; }

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
        internal bool IsSaving => Context?.State.Operation == WorkspaceOperationKind.Save;
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

        internal void SetLabel(string key, string? value) => SetLabels([new(key, value)]);

        internal void SetLabels(IReadOnlyList<KeyValuePair<string, string?>> labels)
        {
            RequireEditable();
            var changed = false;
            foreach (var (key, value) in labels)
            {
                if (string.IsNullOrWhiteSpace(value)) changed |= _labels.Remove(key);
                else if (_labels.GetValueOrDefault(key) != value) { _labels[key] = value; changed = true; }
            }
            if (changed) RaiseStateChanged();
        }

        internal WorkspaceOperationResult RunStructureEdit(Func<DcsEditorWorkspaceSnapshot, DcsEditorWorkspaceSnapshot> edit)
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
                return new(WorkspaceOperationOutcome.Applied, "Workspace edit staged.");
            }
            catch (Exception error) { RaiseStatus(error.Message); return new(WorkspaceOperationOutcome.Blocked, error.Message); }
        }

        /// <summary>Native save hook 1: suspends draft editing and builds the edition-checked, pinned and validated candidate.</summary>
        internal DcsEditorWorkspaceSnapshot PrepareSave()
        {
            if (Context?.State.Operation != WorkspaceOperationKind.Save) throw new InvalidOperationException("Save through the workspace context.");
            if (Repository is null || Saved is null || Draft is null)
                throw new InvalidOperationException("Open a writable DCS workspace before saving.");
            IDisposable? editingLock = Draft.SuspendEditing();
            try
            {
                RaiseStateChanged();
                var updated = EditSnapshot();
                if (Context.State.EditedName != Saved.Configuration.Module.DisplayName)
                {
                    var native = updated.Configuration;
                    updated = updated with
                    {
                        Configuration = new DcsModuleConfiguration(
                        new DcsModule(native.Module.Id, native.Module.DcsId, Context.State.EditedName),
                        native.Profiles, native.Modifiers, native.Bindings, native.Provenance)
                    };
                }
                EditionAccess.RequireWorkspace(updated, DeviceDiscovery, Training);
                updated = DcsPinnedUiLayerProfileRecovery.Restore(updated);
                EditionAccess.RequireWorkspace(updated, DeviceDiscovery, Training);
                DcsSnapshotLuaPayloadRenderer.ValidateUiLayerProfiles(updated);
                DcsModuleIntegrationImport.Instructions(updated);
                // Fallible native baseline construction happens before the durable write; acceptance only installs it.
                _preparedBaseline = (updated, DcsSavedWorkspaceDraft.Create(updated));
                _saveLock = editingLock; editingLock = null;
                return updated;
            }
            catch (Exception error) { RaiseStatus("Database save failed: " + error.Message); RaiseStateChanged(); throw; }
            finally { editingLock?.Dispose(); }
        }
        /// <summary>Native save hook 2: the one repository write, with draft editing still suspended.</summary>
        internal async Task PersistAsync(DcsEditorWorkspaceSnapshot candidate, IWorkspaceOperationLease lease)
        {
            if (Context is null || !Context.OwnsOperation(lease) || lease.Kind != WorkspaceOperationKind.Save) throw new InvalidOperationException("Save through the workspace context.");
            try { await Repository!.SaveAsync(candidate); }
            catch (Exception error)
            {
                await lease.ResumeOnOwnerAsync();
                _preparedBaseline = null; RaiseStatus("Database save failed: " + error.Message); throw;
            }
            finally
            {
                await lease.ResumeOnOwnerAsync();
                // Successful persistence returns its durable receipt before any fallible native notification.
                // AcceptSaved publishes inside the shared post-commit recovery boundary.
                ReleaseSaveLock();
            }
        }
        /// <summary>Native save hook 3: installs the durable snapshot as saved, working and draft baseline.</summary>
        internal void AcceptSaved(DcsEditorWorkspaceSnapshot persisted, IWorkspaceOperationLease lease)
        {
            if (Context is null || !Context.OwnsOperation(lease) || lease.Kind != WorkspaceOperationKind.Save) throw new InvalidOperationException("Save through the workspace context.");
            var prepared = _preparedBaseline; _preparedBaseline = null;
            Saved = persisted; Working = persisted; _structureDirty = false;
            if (prepared is { } baseline && ReferenceEquals(baseline.Candidate, persisted)) SetDraft(baseline.Draft);
            else RebuildDraft(persisted);
            RaiseStateChanged();
        }

        internal void Discard(IWorkspaceOperationLease lease)
        {
            if (Context?.State.Operation != WorkspaceOperationKind.Discard || !Context.OwnsOperation(lease) || lease.Kind != WorkspaceOperationKind.Discard)
                throw new InvalidOperationException("Discard through the current workspace context.");
            RequireEditable();
            Working = Saved; _structureDirty = false;
            if (Saved is not null) RebuildDraft(Saved);
            else Draft?.Discard();
            RestoreLabels(Saved);
            RaiseStateChanged();
        }

        /// <summary>Seals this stale session after a committed save it could not install: the retained draft and
        /// every native edit route refuse mutation until the window is closed or the workspace reopened.</summary>
        internal void SealStale()
        {
            if (_stale) return;
            _stale = true; _staleLock = Draft?.SuspendEditing();
        }

        internal void RaiseStatus(string message) => StatusChanged?.Invoke(this, message);
        internal void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

        /// <summary>Detaches the current draft when the owning workspace closes.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ReleaseSaveLock(); _staleLock?.Dispose(); _staleLock = null; SetDraft(null);
            StateChanged = null; StatusChanged = null;
        }
        #endregion END Public

        #region BEGIN private helpers
        private void ReleaseSaveLock() { var held = _saveLock; _saveLock = null; held?.Dispose(); }
        private void RequireEditable()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_stale) throw new InvalidOperationException(Context?.State.Recovery is { Length: > 0 } reason ? reason : "Reopen this workspace before editing.");
            if (Context?.State.Operation is not (null or WorkspaceOperationKind.NativeEdit or WorkspaceOperationKind.Discard))
                throw new InvalidOperationException("Finish the current workspace operation before editing.");
            if (Context?.State.Activity is not null) throw new InvalidOperationException("Stop input activity before editing.");
        }

        private DcsEditorWorkspaceSnapshot EditSnapshot()
        {
            var workspace = Working ?? Saved ?? throw new InvalidOperationException("Open a saved module to manage devices and modifiers.");
            return workspace with
            {
                Assignments = Draft?.Snapshot ?? workspace.Assignments,
                Labels = new Dictionary<string, string>(_labels, StringComparer.Ordinal)
            };
        }

        private void RebuildDraft(DcsEditorWorkspaceSnapshot workspace)
        {
            // The same native catalog the open path validated: module bindings, stored catalogs and assigned module commands.
            SetDraft(DcsSavedWorkspaceDraft.Create(workspace));
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
