using System.Text.Json;
using System.IO;
using Microsoft.Win32;
using Dcs.Ipia.Rendering;
using System.Windows;
using System.Windows.Controls;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;

namespace DcsIpia.Presentation
{
    /// <summary>Reopens saved shared defaults independently of aircraft module editors.</summary>
    public partial class SharedUiLayerWorkspaceWindow : Window
    {
        private SharedUiLayerWorkspace _workspace;
        private SharedUiLayerWorkspace _draft;
        private readonly ISharedUiLayerWorkspaceRepository? _repository;
        private readonly IEditorWorkspaceRepository? _modules;
        private bool _dirty;
        private bool _saving;
        private bool _reviewing;
        private bool _adopting;
        private SharedUiLayerSemanticPlan? _semanticPlan;
        private string[] _semanticPaths = [];
        private WorkspaceHardware[] _outputHardware = [];
        private readonly Dictionary<Guid, EditorWorkspaceSnapshot> _reviewedModules = [];
        /// <summary>Shows a stored revision and its original catalog and Lua source texts.</summary>
        /// <param name="workspace">Saved shared defaults.</param>
        /// <param name="repository">Revision storage, or null for a read-only preview.</param>
        /// <param name="modules">Saved module sources for read-only impact review.</param>
        public SharedUiLayerWorkspaceWindow(SharedUiLayerWorkspace workspace, ISharedUiLayerWorkspaceRepository? repository = null,
            IEditorWorkspaceRepository? modules = null)
        {
            _workspace = workspace;
            _draft = workspace;
            _repository = repository;
            _modules = modules;
            InitializeComponent();
            RevisionText.Text = "Revision " + workspace.Revision;
            SourcePaths.ItemsSource = workspace.Sources.Keys.Order(StringComparer.Ordinal).ToArray();
            SourcePaths.SelectedIndex = 0;
            BindingFields.IsEnabled = repository is not null;
            ProfilePaths.ItemsSource = SharedUiLayerBindingEditor.Profiles(workspace);
            if (repository is not null)
            {
                CatalogRows.ItemsSource = SharedUiLayerCommandEditor.Commands(workspace);
                CommandChoice.ItemsSource = SharedUiLayerBindingEditor.Commands(workspace);
                DeclaredModifiers.Text = "Declared modifiers: " + string.Join(", ", DcsLuaParser.ParseModifiers(
                    workspace.Sources["assets/shared/ui-layer/input/UiLayer/modifiers.lua"]).Select(x => x.Name));
            }
            RefreshProfiles();
            RefreshModifiers();
            ProfilePaths.SelectedIndex = 0;
            Buttons();
            Closing += (_, e) => {
                if (_saving || _reviewing || _adopting || _dirty && MessageBox.Show(this, "Discard pending shared default changes?", "Pending shared defaults",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) e.Cancel = true;
            };
        }
        #region BEGIN Public
        #endregion END Public
        #region BEGIN private helpers
        private void SourceChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SourcePaths.SelectedItem is string path) SourceText.Text = _draft.Sources[path];
        }
        private void Buttons()
        {
            var busy = _saving || _reviewing || _adopting;
            ExportSharedButton.IsEnabled = RebuildSharedButton.IsEnabled = !busy && !_dirty;
            RestoreSharedButton.IsEnabled = _repository is not null && !busy && !_dirty;
            LoadOutputHardwareButton.IsEnabled = !busy;
            SetOutputHardwareButton.IsEnabled = _repository is not null && !busy && _outputHardware.Length > 0;
            ImpactModules.IsEnabled = AdoptionChoices.IsEnabled = !busy;
            var selectedModule = (ImpactModules.SelectedItem as SharedUiLayerModuleImpact)?.ModuleId;
            var canAdopt = !busy && !_dirty && _modules is IEditorWorkspaceUpdateRepository &&
                selectedModule is Guid id && _reviewedModules.ContainsKey(id);
            var hasBaseline = selectedModule is Guid selectedId && _reviewedModules.TryGetValue(selectedId, out var selectedSnapshot) &&
                selectedSnapshot.SharedDefault is not null;
            RecordBaselineButton.IsEnabled = canAdopt && !hasBaseline;
            ReviewSemanticButton.IsEnabled = canAdopt && hasBaseline;
            AdoptDefaultsButton.IsEnabled = canAdopt && hasBaseline && _semanticPlan is not null;
            RollbackAdoptionButton.IsEnabled = canAdopt;
            ImpactRefreshButton.IsEnabled = _modules is not null && !busy;
            SaveDefaultsButton.IsEnabled = _repository is not null && _dirty && !busy;
            DiscardDefaultsButton.IsEnabled = _dirty && !busy;
            BindingFields.IsEnabled = _repository is not null && !busy;
            CatalogFields.IsEnabled = _repository is not null && !busy;
            ModifierFields.IsEnabled = _repository is not null && !busy;
            DeviceFields.IsEnabled = _repository is not null && !busy;
            var deviceEditable = _repository is not null && !busy &&
                DeviceRows.SelectedItem is SharedUiLayerDeviceProfile profile && !profile.Protected;
            RenameProfileButton.IsEnabled = RemoveProfileButton.IsEnabled = deviceEditable;
            var editable = _repository is not null && !busy && ModifierRows.SelectedItem is DcsModifier selected &&
                !SharedUiLayerModifierEditor.IsProtected(selected);
            UpdateModifierButton.IsEnabled = RemoveModifierButton.IsEnabled = editable;
            ClearLayerButton.IsEnabled = _repository is not null && !busy;
        }
        private async void ReviewImpactClicked(object sender, RoutedEventArgs e)
        {
            if (_modules is null || _saving || _reviewing || _adopting) return;
            _reviewing = true;
            ResetImpact("Reading saved module sources…");
            Buttons();
            try
            {
                var modules = new List<EditorWorkspaceSnapshot>();
                foreach (var id in await _modules.ListAsync())
                {
                    var module = await _modules.GetAsync(id);
                    if (module is not null) modules.Add(module);
                }
                var report = SharedUiLayerImpactReviewer.Review(_workspace, _draft, modules);
                foreach (var module in modules) _reviewedModules[module.Configuration.Module.Id] = module;
                ImpactChanges.ItemsSource = report.Changes;
                ImpactModules.ItemsSource = report.Modules;
                ImpactModules.SelectedIndex = 0;
                ImpactStatus.Text = $"Opened revision {report.BaselineRevision}: {report.Changes.Count} proposed source changes; " +
                    $"{report.Modules.Count} saved modules reviewed. Review required: {report.Modules.Count(x => x.Status == "Review required")}. " +
                    "This compares pinned source texts. Module assignments and overlays require separate adoption review.";
            }
            catch (Exception ex) { ResetImpact("Unable to review module impact: " + ex.Message); }
            finally { _reviewing = false; Buttons(); }
        }
        private void ImpactModuleSelected(object sender, SelectionChangedEventArgs e)
        {
            if (ImpactDetails is not null) ImpactDetails.ItemsSource = (ImpactModules.SelectedItem as SharedUiLayerModuleImpact)?.Sources;
            if (AdoptionChoices is null) return;
            AdoptionChoices.ItemsSource = null;
            _semanticPlan = null;
            if (SemanticChanges is not null) SemanticChanges.ItemsSource = null;
            try
            {
                if (ImpactModules.SelectedItem is SharedUiLayerModuleImpact row && _reviewedModules.TryGetValue(row.ModuleId, out var module))
                {
                    AdoptionStatus.Text = module.SharedDefault is null
                        ? "No recorded default baseline. Record this saved revision without changing module edits."
                        : $"Module baseline: revision {module.SharedDefault.Revision}. Target: opened saved revision {_workspace.Revision}.";
                    if (!_dirty && module.SharedDefault is not null)
                        AdoptionChoices.ItemsSource = SharedUiLayerAdoption.Review(module, _workspace)
                            .Select(x => new SharedUiLayerAdoptionChoice(x)).ToArray();
                }
            }
            catch (Exception ex) { AdoptionStatus.Text = ex.Message; }
            Buttons();
        }
        private void ResetImpact(string message = "Draft changed. Refresh the impact review before saving.")
        {
            _reviewedModules.Clear();
            ImpactChanges.ItemsSource = ImpactModules.ItemsSource = ImpactDetails.ItemsSource = null;
            AdoptionChoices.ItemsSource = null;
            _semanticPlan = null;
            if (SemanticChanges is not null) SemanticChanges.ItemsSource = null;
            ImpactStatus.Text = message;
        }
        private EditorWorkspaceSnapshot ReviewedModule() =>
            ImpactModules.SelectedItem is SharedUiLayerModuleImpact row && _reviewedModules.TryGetValue(row.ModuleId, out var module)
                ? module : throw new InvalidOperationException("Refresh impact and select a saved module.");
        private async void RecordBaselineClicked(object sender, RoutedEventArgs e) => await UpdateModuleAsync(module =>
            module.SharedDefault is null ? SharedUiLayerRevisionReference.Attach(module, _workspace) :
                throw new InvalidOperationException("The module already has a recorded baseline."), false);
        private string[] SelectedPaths()
        {
            AdoptionChoices.CommitEdit(DataGridEditingUnit.Cell, true);
            AdoptionChoices.CommitEdit(DataGridEditingUnit.Row, true);
            return (AdoptionChoices.ItemsSource as IEnumerable<SharedUiLayerAdoptionChoice> ?? [])
                .Where(x => x.TakeDefault).Select(x => x.Path).Order(StringComparer.Ordinal).ToArray();
        }
        private void ReviewSemanticClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                _semanticPlan = null;
                _semanticPaths = SelectedPaths();
                _semanticPlan = SharedUiLayerSemanticAdoption.Review(ReviewedModule(), _workspace, _semanticPaths);
                SemanticChanges.ItemsSource = _semanticPlan.Changes;
                AdoptionStatus.Text = $"Reviewed {_semanticPlan.Changes.Count} binding, catalog and modifier decisions. Apply saves this proposal atomically.";
            }
            catch (Exception ex) { AdoptionStatus.Text = "Unable to stage adoption: " + ex.Message; }
            Buttons();
        }
        private async void AdoptDefaultsClicked(object sender, RoutedEventArgs e)
        {
            await UpdateModuleAsync(module => _semanticPlan is not null &&
                module.Configuration.Module.Id == _semanticPlan.Workspace.Configuration.Module.Id &&
                SelectedPaths().SequenceEqual(_semanticPaths, StringComparer.Ordinal)
                    ? _semanticPlan.Workspace : throw new InvalidOperationException("Review assignment/catalog changes again after changing the source choices."), true);
        }
        private async Task UpdateModuleAsync(Func<EditorWorkspaceSnapshot, EditorWorkspaceSnapshot> update, bool validateOutput)
        {
            if (_modules is not IEditorWorkspaceUpdateRepository repository || _saving || _reviewing || _adopting) return;
            try
            {
                if (_dirty) throw new InvalidOperationException("Save or discard the shared draft before adopting a saved revision.");
                var expected = ReviewedModule();
                var updated = update(expected);
                if (validateOutput) SnapshotLuaPayloadRenderer.Generate(updated);
                _adopting = true;
                Buttons();
                await repository.SaveIfUnchangedAsync(expected, updated);
                ResetImpact("Module baseline saved. Refresh impact to review the current state.");
                AdoptionStatus.Text = "Reviewed update saved for the selected module. Prior state retained for rollback.";
            }
            catch (Exception ex) { AdoptionStatus.Text = "Module was not changed: " + ex.Message; }
            finally { _adopting = false; Buttons(); }
        }
        private async void RollbackAdoptionClicked(object sender, RoutedEventArgs e)
        {
            if (_modules is not IEditorWorkspaceUpdateRepository repository || _saving || _reviewing || _adopting) return;
            try
            {
                if (_dirty) throw new InvalidOperationException("Save or discard the shared draft before rollback.");
                var expected = ReviewedModule();
                _adopting = true;
                Buttons();
                var prior = await repository.GetPriorAsync(expected.Configuration.Module.Id) ??
                    throw new InvalidOperationException("No prior reviewed module state is available.");
                await repository.SaveIfUnchangedAsync(expected, prior);
                ResetImpact("Prior module state restored. Refresh impact to review the restored baseline.");
                AdoptionStatus.Text = "Rollback saved. Other modules and shared default history were not changed.";
            }
            catch (Exception ex) { AdoptionStatus.Text = "Module was not changed: " + ex.Message; }
            finally { _adopting = false; Buttons(); }
        }
        private void ProfileChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BindingRows is null) return;
            BindingRows.ItemsSource = ProfilePaths.SelectedItem is string path ? SharedUiLayerBindingEditor.Bindings(_draft, path) : null;
            if (ClearLayerChoice is null || ProfilePaths.SelectedItem is not string) return;
            var selected = ClearLayerChoice.SelectedItem as string ?? "Base";
            ClearLayerChoice.ItemsSource = Enumerable.Repeat("Base", 1).Concat(
                DcsLuaParser.ParseModifiers(_draft.Sources["assets/shared/ui-layer/input/UiLayer/modifiers.lua"]).Select(x => x.Name))
                .Concat((BindingRows.ItemsSource as IEnumerable<DcsBinding> ?? []).Where(x => x.Reformers.Count > 0)
                    .Select(x => string.Join(" + ", x.Reformers.Order(StringComparer.Ordinal))))
                .Distinct(StringComparer.Ordinal).ToArray();
            ClearLayerChoice.SelectedItem = (ClearLayerChoice.ItemsSource as IEnumerable<string>)!.Contains(selected) ? selected : "Base";
        }
        private void BindingSelected(object sender, SelectionChangedEventArgs e)
        {
            if (BindingRows.SelectedItem is not DcsBinding binding || InputKey is null) return;
            InputKey.Text = binding.InputKey;
            ModifierChord.Text = string.Join(" + ", binding.Reformers);
            CommandChoice.SelectedItem = (CommandChoice.ItemsSource as IEnumerable<DraftCommand>)?.FirstOrDefault(x => x.Id == binding.CommandId);
        }
        private void NewBindingClicked(object sender, RoutedEventArgs e)
        {
            BindingRows.SelectedItem = null;
            InputKey.Text = ModifierChord.Text = "";
            CommandChoice.SelectedItem = null;
        }
        private string ProfilePath() => ProfilePaths.SelectedItem as string ?? throw new InvalidOperationException("Select a device profile.");
        private DcsBinding SelectedBinding() => BindingRows.SelectedItem as DcsBinding ?? throw new InvalidOperationException("Select a binding.");
        private void AddBindingClicked(object sender, RoutedEventArgs e) => SetBinding(null);
        private void UpdateBindingClicked(object sender, RoutedEventArgs e) => Edit(() => Set(SelectedBinding()));
        private void SetBinding(DcsBinding? current) => Edit(() => Set(current));
        private SharedUiLayerWorkspace Set(DcsBinding? current) => SharedUiLayerBindingEditor.Set(_draft, ProfilePath(), current,
            (CommandChoice.SelectedItem as DraftCommand)?.Id ?? throw new InvalidOperationException("Select a command."),
            InputKey.Text, ModifierChord.Text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        private void RemoveBindingClicked(object sender, RoutedEventArgs e) => Edit(() => SharedUiLayerBindingEditor.Remove(_draft, ProfilePath(), SelectedBinding()));
        private void ClearLayerClicked(object sender, RoutedEventArgs e) => Edit(() =>
            SharedUiLayerBindingEditor.ClearLayerAssignments(_draft, ProfilePath(),
                ClearLayerChoice.SelectedItem is string layer && layer != "Base" ? layer : ""));
        private void CatalogSelected(object sender, SelectionChangedEventArgs e)
        {
            if (CatalogIdentity is null || CatalogRows.SelectedItem is not SharedUiLayerCommand entry) return;
            CatalogIdentity.Text = entry.Id;
            CatalogIdentity.IsReadOnly = true;
            CatalogCode.Text = entry.Command;
            CatalogLabel.Text = entry.Label;
            CatalogCategory.Text = entry.Category;
        }
        private void NewCommandClicked(object sender, RoutedEventArgs e)
        {
            CatalogRows.SelectedItem = null;
            CatalogIdentity.IsReadOnly = false;
            CatalogIdentity.Text = CatalogCode.Text = CatalogLabel.Text = CatalogCategory.Text = "";
        }
        private SharedUiLayerCommand SelectedCommand() => CatalogRows.SelectedItem as SharedUiLayerCommand ??
            throw new InvalidOperationException("Select a catalog command.");
        private SharedUiLayerCommand CommandFields() => new(CatalogIdentity.Text.Trim(), CatalogCode.Text.Trim(),
            CatalogLabel.Text.Trim(), CatalogCategory.Text.Trim());
        private void AddCommandClicked(object sender, RoutedEventArgs e) => Edit(() =>
            SharedUiLayerCommandEditor.Set(_draft, null, CommandFields()));
        private void UpdateCommandClicked(object sender, RoutedEventArgs e) => Edit(() =>
            SharedUiLayerCommandEditor.Set(_draft, SelectedCommand(), CommandFields()));
        private void RemoveCommandClicked(object sender, RoutedEventArgs e) => Edit(() =>
            SharedUiLayerCommandEditor.Remove(_draft, SelectedCommand()));
        private async void LoadOutputHardwareClicked(object sender, RoutedEventArgs e)
        {
            _reviewing = true;
            Buttons();
            try
            {
                var sources = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
                _outputHardware = JsonSerializer.Deserialize<WorkspaceHardware[]>(sources["config/ipia-hardware.json"]) ?? [];
                OutputHardwareChoice.ItemsSource = _outputHardware;
                EditorStatus.Text = "Select a joystick profile and output hardware, then stage the mapping.";
            }
            catch (Exception ex) { EditorStatus.Text = "Unable to load output hardware: " + ex.Message; }
            finally { _reviewing = false; Buttons(); }
        }
        private void SetOutputHardwareClicked(object sender, RoutedEventArgs e) => Edit(() =>
            SharedUiLayerHardwareMapping.Set(_draft, SelectedProfile().Path,
                OutputHardwareChoice.SelectedItem as WorkspaceHardware ?? throw new InvalidOperationException("Select output hardware.")));
        private async Task<SharedUiLayerOutputSnapshot> FreezeOutputAsync()
        {
            var sources = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
            var hardware = JsonSerializer.Deserialize<WorkspaceHardware[]>(sources["config/ipia-hardware.json"]) ?? [];
            return SharedUiLayerOutputSerializer.Freeze(_workspace, BundledSharedCatalog.SourceCommit(),
                SharedUiLayerHardwareMapping.Resolve(_workspace, hardware));
        }
        private async Task SaveOutputAsync(SharedUiLayerOutputSnapshot snapshot)
        {
            var dialog = new SaveFileDialog { Filter = "Shared defaults ZIP|*.zip", FileName = "IPIA-Shared-Defaults.zip" };
            if (dialog.ShowDialog(this) != true) return;
            var bytes = SharedUiLayerOutputArchive.Build(snapshot,
                SharedUiLayerKneeboardRenderer.Generate(SharedUiLayerOutputSerializer.CreatePreview(snapshot)));
            var temporary = dialog.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllBytesAsync(temporary, bytes);
                File.Move(temporary, dialog.FileName, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            EditorStatus.Text = "Exported shared Lua, device kneeboards, full binding reference and verified snapshot.";
        }
        private async void ExportSharedClicked(object sender, RoutedEventArgs e)
        {
            if (_dirty || _saving || _reviewing || _adopting) return;
            _saving = true;
            Buttons();
            try { await SaveOutputAsync(await FreezeOutputAsync()); }
            catch (Exception ex) { EditorStatus.Text = "Shared output was not exported: " + ex.Message; }
            finally { _saving = false; Buttons(); }
        }
        private async void ReadSharedClicked(object sender, RoutedEventArgs e)
        {
            if (_dirty || _saving || _reviewing || _adopting) return;
            var dialog = new OpenFileDialog { Filter = "Shared defaults ZIP|*.zip" };
            if (dialog.ShowDialog(this) != true) return;
            _saving = true;
            Buttons();
            try
            {
                var snapshot = SharedUiLayerOutputArchive.Read(await File.ReadAllBytesAsync(dialog.FileName));
                if (ReferenceEquals(sender, RebuildSharedButton)) await SaveOutputAsync(snapshot);
                else
                {
                    if (_repository is null) throw new InvalidOperationException("Default revision storage is unavailable.");
                    _workspace = await _repository.SaveAsync(snapshot.Defaults with { Revision = _workspace.Revision, ContentSha256 = null });
                    _draft = _workspace;
                    RevisionText.Text = "Revision " + _workspace.Revision;
                    SourcePaths.ItemsSource = _workspace.Sources.Keys.Order(StringComparer.Ordinal).ToArray();
                    SourcePaths.SelectedIndex = 0;
                    ResetImpact("Restored defaults as a new revision. Refresh module impact before adoption.");
                    RefreshProfiles();
                    RefreshCommands();
                    RefreshModifiers();
                    EditorStatus.Text = "Verified shared defaults restored as a new revision. Modules retain their saved state.";
                }
            }
            catch (Exception ex) { EditorStatus.Text = "Shared snapshot operation failed: " + ex.Message; }
            finally { _saving = false; Buttons(); }
        }
        private void DeviceSelected(object sender, SelectionChangedEventArgs e)
        {
            if (DeviceFileName is null || DeviceRows.SelectedItem is not SharedUiLayerDeviceProfile profile) return;
            DeviceFileName.Text = profile.FileName;
            Buttons();
        }
        private void NewProfileClicked(object sender, RoutedEventArgs e)
        {
            DeviceRows.SelectedItem = null;
            DeviceFileName.Text = "";
            CloneProfileChoice.SelectedItem = null;
            Buttons();
        }
        private SharedUiLayerDeviceProfile SelectedProfile() => DeviceRows.SelectedItem as SharedUiLayerDeviceProfile ??
            throw new InvalidOperationException("Select a device profile.");
        private void AddProfileClicked(object sender, RoutedEventArgs e)
        {
            var file = DeviceFileName.Text;
            Edit(() => SharedUiLayerProfileEditor.Add(_draft, file, CloneProfileChoice.SelectedItem as SharedUiLayerDeviceProfile));
            SelectProfile(file);
        }
        private void RenameProfileClicked(object sender, RoutedEventArgs e)
        {
            var file = DeviceFileName.Text;
            Edit(() => SharedUiLayerProfileEditor.Rename(_draft, SelectedProfile(), file));
            SelectProfile(file);
        }
        private void RemoveProfileClicked(object sender, RoutedEventArgs e) => Edit(() =>
            SharedUiLayerProfileEditor.Remove(_draft, SelectedProfile()));
        private void SelectProfile(string file)
        {
            var profile = (DeviceRows.ItemsSource as IEnumerable<SharedUiLayerDeviceProfile>)?.FirstOrDefault(x => x.FileName == file);
            if (profile is null) return;
            DeviceRows.SelectedItem = profile;
            ProfilePaths.SelectedItem = profile.Path;
            SourcePaths.SelectedItem = profile.Path;
            Buttons();
        }
        private void RefreshProfiles()
        {
            if (_repository is null) return;
            var device = (DeviceRows.SelectedItem as SharedUiLayerDeviceProfile)?.Path;
            var template = (CloneProfileChoice.SelectedItem as SharedUiLayerDeviceProfile)?.Path;
            var binding = ProfilePaths.SelectedItem as string;
            var source = SourcePaths.SelectedItem as string;
            var profiles = SharedUiLayerProfileEditor.Profiles(_draft);
            DeviceRows.ItemsSource = profiles;
            DeviceRows.SelectedItem = profiles.FirstOrDefault(x => x.Path == device);
            CloneProfileChoice.ItemsSource = profiles.Where(x => !x.Protected).ToArray();
            CloneProfileChoice.SelectedItem = profiles.FirstOrDefault(x => x.Path == template && !x.Protected);
            ProfilePaths.ItemsSource = profiles.Select(x => x.Path).ToArray();
            ProfilePaths.SelectedItem = profiles.Any(x => x.Path == binding) ? binding :
                (profiles.Count > 0 ? profiles[0].Path : null);
            SourcePaths.ItemsSource = _draft.Sources.Keys.Order(StringComparer.Ordinal).ToArray();
            SourcePaths.SelectedItem = source is not null && _draft.Sources.ContainsKey(source) ? source :
                _draft.Sources.Keys.Order(StringComparer.Ordinal).FirstOrDefault();
            if (SourcePaths.SelectedItem is null) SourceText.Text = "";
        }
        private void ModifierSelected(object sender, SelectionChangedEventArgs e)
        {
            if (ModifierName is null || ModifierRows.SelectedItem is not DcsModifier modifier) return;
            ModifierName.Text = modifier.Name;
            ModifierDevice.SelectedItem = modifier.Device;
            ModifierInput.Text = modifier.Key;
            ModifierModeChoice.SelectedItem = modifier.Mode;
            Buttons();
        }
        private void NewModifierClicked(object sender, RoutedEventArgs e)
        {
            ModifierRows.SelectedItem = null;
            ModifierName.Text = ModifierInput.Text = "";
            ModifierDevice.SelectedItem = null;
            ModifierModeChoice.SelectedItem = ModifierMode.Hold;
            Buttons();
        }
        private DcsModifier SelectedModifier() => ModifierRows.SelectedItem as DcsModifier ??
            throw new InvalidOperationException("Select a modifier.");
        private DcsModifier ModifierValue() => new(ModifierName.Text,
            ModifierDevice.SelectedItem as string ?? throw new InvalidOperationException("Select a native device."),
            ModifierInput.Text, ModifierModeChoice.SelectedItem is ModifierMode mode ? mode :
                throw new InvalidOperationException("Select Hold or Toggle mode."));
        private void AddModifierClicked(object sender, RoutedEventArgs e) => Edit(() =>
            SharedUiLayerModifierEditor.Set(_draft, null, ModifierValue()));
        private void UpdateModifierClicked(object sender, RoutedEventArgs e) => Edit(() =>
            SharedUiLayerModifierEditor.Set(_draft, SelectedModifier(), ModifierValue()));
        private void RemoveModifierClicked(object sender, RoutedEventArgs e) => Edit(() =>
            SharedUiLayerModifierEditor.Remove(_draft, SelectedModifier()));
        private void RefreshModifiers()
        {
            if (_repository is null) return;
            var name = (ModifierRows.SelectedItem as DcsModifier)?.Name;
            ModifierRows.ItemsSource = SharedUiLayerModifierEditor.Modifiers(_draft);
            ModifierDevice.ItemsSource = SharedUiLayerModifierEditor.Devices(_draft);
            ModifierModeChoice.ItemsSource = Enum.GetValues<ModifierMode>();
            ModifierRows.SelectedItem = (ModifierRows.ItemsSource as IEnumerable<DcsModifier>)!.FirstOrDefault(x => x.Name == name);
            DeclaredModifiers.Text = "Declared modifiers: " + string.Join(", ", SharedUiLayerModifierEditor.Modifiers(_draft).Select(x => x.Name));
        }
        private void RefreshCommands()
        {
            if (_repository is null) return;
            var code = (CommandChoice.SelectedItem as DraftCommand)?.Id;
            var identity = (CatalogRows.SelectedItem as SharedUiLayerCommand)?.Id;
            CatalogRows.ItemsSource = SharedUiLayerCommandEditor.Commands(_draft);
            CatalogRows.SelectedItem = (CatalogRows.ItemsSource as IEnumerable<SharedUiLayerCommand>)!.FirstOrDefault(x => x.Id == identity);
            CommandChoice.ItemsSource = SharedUiLayerBindingEditor.Commands(_draft);
            CommandChoice.SelectedItem = (CommandChoice.ItemsSource as IEnumerable<DraftCommand>)!.FirstOrDefault(x => x.Id == code);
        }
        private void Edit(Func<SharedUiLayerWorkspace> edit)
        {
            try
            {
                _draft = edit();
                _dirty = true;
                ResetImpact();
                RefreshProfiles();
                RefreshCommands();
                RefreshModifiers();
                ProfileChanged(this, new SelectionChangedEventArgs(ComboBox.SelectionChangedEvent, Array.Empty<object>(), Array.Empty<object>()));
                if (SourcePaths.SelectedItem is string path) SourceText.Text = _draft.Sources[path];
                EditorStatus.Text = "Change staged. Save a new revision to keep it, or discard the draft.";
                Buttons();
            }
            catch (Exception ex) { EditorStatus.Text = ex.Message; }
        }
        private async void SaveClicked(object sender, RoutedEventArgs e)
        {
            if (_repository is null || !_dirty || _saving || _reviewing) return;
            _saving = true;
            Buttons();
            try
            {
                SharedUiLayerBindingEditor.Validate(_draft);
                _workspace = await _repository.SaveAsync(_draft);
                _draft = _workspace;
                _dirty = false;
                ResetImpact("Default revision saved. Refresh to compare the new baseline.");
                RevisionText.Text = "Revision " + _workspace.Revision;
                EditorStatus.Text = "Shared default revision saved. Existing module workspaces were not changed.";
            }
            catch (Exception ex) { EditorStatus.Text = "Defaults were not saved: " + ex.Message; }
            finally { _saving = false; Buttons(); }
        }
        private void DiscardClicked(object sender, RoutedEventArgs e)
        {
            _draft = _workspace;
            _dirty = false;
            ResetImpact("Draft discarded. Refresh to compare the restored baseline.");
            RefreshProfiles();
            RefreshCommands();
            RefreshModifiers();
            BindingRows.ItemsSource = ProfilePaths.SelectedItem is string path ? SharedUiLayerBindingEditor.Bindings(_draft, path) : null;
            if (SourcePaths.SelectedItem is string source) SourceText.Text = _draft.Sources[source];
            ProfileChanged(this, new SelectionChangedEventArgs(ComboBox.SelectionChangedEvent, Array.Empty<object>(), Array.Empty<object>()));
            EditorStatus.Text = "Draft discarded.";
            Buttons();
        }
        #endregion END private helpers
    }
}

