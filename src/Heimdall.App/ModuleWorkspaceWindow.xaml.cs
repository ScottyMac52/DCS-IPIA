using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;
using Heimdall.App.Features.Assignments;
using Heimdall.App.Features.Commands;
using Heimdall.App.Features.Devices;
using Heimdall.App.Features.Documentation;
using Heimdall.App.Features.InputTest;
using Heimdall.App.Features.Kneeboards;
using Heimdall.App.Features.Modifiers;
using Heimdall.App.Features.Pages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Heimdall.App
{
    /// <summary>Hosts the module feature views and coordinates shared draft, save and build operations.</summary>
    public partial class ModuleWorkspaceWindow : Window
    {
        private bool _changingSelection;
        private readonly WindowContext _context;
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
        private readonly INavigationService _navigation;
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
        private readonly Dictionary<string, UserControl> _views = new(StringComparer.Ordinal);
        private readonly string[] _knownKeys = { "Devices", "Modifiers", "Assignments", "InputTest", "Commands", "Pages", "Kneeboards", "Documentation" };

        internal readonly ModuleWorkspaceViewModel _viewModel;
        internal readonly TrainingEnvironment? _training;
        private readonly ILogger<ModuleWorkspaceWindow> _logger;
        private readonly IAppSettingsService? _settings;
        internal readonly IEditorWorkspaceRepository? _repository;
        internal EditorWorkspaceSnapshot? _saved;
        internal DraftAssignmentService? _draft;
        internal EditorWorkspaceSnapshot? _working;
        internal readonly IConnectedDeviceDiscovery _deviceDiscovery;
        internal readonly HardwareInventoryManager? _inventory;
        internal readonly IUserMappingLibrary? _userMappings;
        internal readonly Func<MappingRevisionReview, bool>? _mappingReviewConfirmation;
        internal readonly Func<bool, IReadOnlyList<string>?>? _commandFilePicker;
        private readonly Func<bool, string?>? _outputPathPicker;
        private readonly Func<string?>? _documentationFolderPicker;
        private readonly Func<string?>? _completeReleaseFolderPicker;
        internal readonly Func<string?>? _integrationFilePicker;
        internal readonly Func<string?>? _axisDefaultsFilePicker;
        internal string _savedGamesRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games");
        private bool _structureDirty;
        private bool _managementRefreshing;
        internal readonly Dictionary<string, string> _labels = new(StringComparer.Ordinal);
        private bool _building;
        internal readonly Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? _inputSourceFactory;
        internal readonly Action<string, string>? _inputLogClipboardWriter;

        public ModuleWorkspaceWindow(ModuleWorkspaceViewModel viewModel, IAppSettingsService? settings = null,
                                     IEditorWorkspaceRepository? repository = null, IConnectedDeviceDiscovery? deviceDiscovery = null,
                                     Func<bool, IReadOnlyList<string>?>? commandFilePicker = null, Func<bool, string?>? outputPathPicker = null,
                                     Func<string?>? documentationFolderPicker = null, Func<string?>? completeReleaseFolderPicker = null,
                                     Func<string?>? integrationFilePicker = null, Func<string?>? axisDefaultsFilePicker = null,
                                     Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? inputSourceFactory = null,
                                     Action<string, string>? inputLogClipboardWriter = null, HardwareInventoryManager? inventory = null, IUserMappingLibrary? userMappings = null, Func<MappingRevisionReview, bool>? mappingReviewConfirmation = null,
                                     ILogger<ModuleWorkspaceWindow>? logger = null, bool persistLayout = true, TrainingEnvironment? training = null, bool allowGameSwitch = false, IReadOnlyList<SavedModuleChoice>? moduleChoices = null)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            training?.RequireRepository(repository);
            training?.RequireWorkspace(viewModel.Workspace ?? throw new InvalidOperationException("No training workspace."));
            _training = training;
            _logger = logger ?? NullLogger<ModuleWorkspaceWindow>.Instance;
            _inventory = training is null ? inventory : null;
            _userMappings = training is null ? userMappings : null;
            _mappingReviewConfirmation = mappingReviewConfirmation;
            _settings = training is null ? settings : null;
            _repository = repository;
            _deviceDiscovery = training is null ? deviceDiscovery ?? new WindowsConnectedDeviceDiscovery() : new TrainingDiscovery();
            _commandFilePicker = training is null ? commandFilePicker : _ => [Path.Combine(training.Root, "commands.json")];
            _outputPathPicker = training is null ? outputPathPicker : snapshot => snapshot
                ? Path.Combine(training.Outputs, "training.heimdall-module.json") : training.Outputs;
            _documentationFolderPicker = training is null ? documentationFolderPicker : () => training.Outputs;
            _completeReleaseFolderPicker = training is null ? completeReleaseFolderPicker : () => training.Outputs;
            _integrationFilePicker = training is null ? integrationFilePicker : () => null;
            _axisDefaultsFilePicker = training is null ? axisDefaultsFilePicker : () => null;
            _inputSourceFactory = training is null ? inputSourceFactory : training.CreateInputSource;
            _inputLogClipboardWriter = inputLogClipboardWriter;
            _saved = viewModel.Workspace;
            _working = _saved;
            _draft = viewModel.Draft;
            foreach (var pair in viewModel.Labels)
                _labels[pair.Key] = pair.Value;
            InitializeComponent();
            _changingSelection = true;
            WorkspaceGamePicker.SelectedIndex = 0;
            WorkspaceProfilePicker.ItemsSource = moduleChoices ?? [];
            WorkspaceProfilePicker.SelectedItem = moduleChoices?.FirstOrDefault(x => x.Id == _saved?.Configuration.Module.Id);
            GameWorkspaceSelectors.Visibility = allowGameSwitch && training is null ? Visibility.Visible : Visibility.Collapsed;
            _changingSelection = false;
            WorkspaceLayout.SetPersistenceEnabled(WorkspaceRoot, training is null && persistLayout);
            DataContext = _viewModel;
            WorkspaceSettingsButton.IsEnabled = _settings is not null;
            RefreshManagement();
            Loaded += (_, _) => { };
            Closing += ConfirmClose;
            UpdateBuildAvailability();

            _navigation = new NavigationService(_knownKeys);
            _context = new WindowContext(this);
            SetDraft(_draft);
            Closed += (_, _) =>
            {
                FeatureActions.ClearActions();
                SetDraft(null);
            };
            _navigation.CurrentFeatureChanged += (_, _) => LoadCurrentView();
            FeatureNav.SelectedIndex = 0;
        }

        #region BEGIN Public
        /// <summary>Gets the explicit requested game after a guarded close.</summary>
        public string? RequestedGame { get; private set; }
        /// <summary>Gets an explicit requested saved DCS profile after a guarded close.</summary>
        public Guid? RequestedModule { get; private set; }
        #endregion END Public

        private async void WorkspaceGameChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_changingSelection || WorkspaceGamePicker.SelectedIndex != 1) return;
            if (await ConfirmWorkspaceSwitch()) { RequestedGame = "elite-dangerous"; Close(); }
            else { _changingSelection = true; WorkspaceGamePicker.SelectedIndex = 0; _changingSelection = false; }
        }
        private async void WorkspaceProfileChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_changingSelection || WorkspaceProfilePicker.SelectedItem is not SavedModuleChoice choice || choice.Id == _saved?.Configuration.Module.Id) return;
            if (await ConfirmWorkspaceSwitch()) { RequestedModule = choice.Id; Close(); }
            else { _changingSelection = true; WorkspaceProfilePicker.SelectedItem = WorkspaceProfilePicker.Items.Cast<SavedModuleChoice>().FirstOrDefault(x => x.Id == _saved?.Configuration.Module.Id); _changingSelection = false; }
        }
        private async Task<bool> ConfirmWorkspaceSwitch()
        {
            if (!HasPendingChanges()) return true;
            var choice = ThemeMessageBox.Show(this, "Save pending edits before switching? Yes saves, No discards, Cancel stays here.", "Switch workspace", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
            if (choice == MessageBoxResult.Cancel) return false;
            if (choice == MessageBoxResult.No) { DiscardClicked(this, new()); return true; }
            if (_repository is null) return false;
            try { await SaveWorkspaceAsync(); return !HasPendingChanges(); }
            catch (Exception error) { EditorMessage.Text = "Unable to save before switching: " + error.Message; return false; }
        }
        private void FeatureNavChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FeatureNav.SelectedItem is ListBoxItem item)
                _navigation.NavigateTo(item.Tag as string ?? "Devices");
        }

        private void OutputMenuOpened(object? sender, EventArgs e) => PackageVersion.Focus();

        private void OutputMenuKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Escape) return;
            OutputMenu.IsOpen = false;
            OutputMenuButton.Focus();
            e.Handled = true;
        }

        private void LoadCurrentView()
        {
            var key = _navigation.CurrentFeatureKey ?? "Devices";
            RefreshAssignmentMode(key);
            FeatureContent.IsEnabled = FeatureActions.IsEnabled = EditionAccess.AllowsWorkspace(_working, _training);
            if (!FeatureContent.IsEnabled) EditorMessage.Text = "Reserved hardware: this workspace is read-only in Free Edition. Saved data is retained. Premium availability TBA.";
            if (key == "InputTest" && !EditionAccess.AllowsInputTest(_context))
            {
                FeatureContent.Content = new TextBlock { Text = "Live input testing is reserved for Premium. Availability and pricing TBA.", TextWrapping = TextWrapping.Wrap };
                FeatureActions.ClearActions(); return;
            }
            if (_views.TryGetValue(key, out var cached))
            {
                FeatureContent.Content = cached;
                LinkControllerSelection(cached);
                FeatureActions.ShowActions(cached);
                return;
            }
            FeatureContent.Content = key switch
            {
                "Devices" => CreateDevicesView(),
                "Modifiers" => CreateModifiersView(),
                "Assignments" => CreateAssignmentsView(),
                "InputTest" => CreateInputTestView(),
                "Commands" => CreateCommandsView(),
                "Pages" => CreatePagesView(),
                "Kneeboards" => CreateKneeboardsView(),
                "Documentation" => CreateDocumentationView(),
                _ => null
            };
            if (FeatureContent.Content is UserControl view)
            {
                WorkspaceLayout.SetPersistenceEnabled(view, WorkspaceLayout.GetPersistenceEnabled(WorkspaceRoot));
                _views[key] = view;
                LinkControllerSelection(view);
                FeatureActions.ShowActions(view);
            }
            else FeatureActions.ClearActions();
        }

        private DevicesView CreateDevicesView()
        {
            var view = new DevicesView();
            view.Initialize(_context);
            return view;
        }

        private ModifiersView CreateModifiersView()
        {
            var view = new ModifiersView();
            view.Initialize(_context);
            return view;
        }

        private AssignmentsView CreateAssignmentsView()
        {
            var view = new AssignmentsView();
            view.Initialize(_context);
            return view;
        }

        private InputTestView CreateInputTestView()
        {
            var view = new InputTestView();
            view.Initialize(_context);
            return view;
        }

        private CommandsView CreateCommandsView()
        {
            var view = new CommandsView();
            view.Initialize(_context);
            return view;
        }

        private PagesView CreatePagesView()
        {
            var view = new PagesView();
            view.Initialize(_context);
            return view;
        }

        private KneeboardsView CreateKneeboardsView()
        {
            var view = new KneeboardsView();
            view.Initialize(_context);
            return view;
        }

        private DocumentationView CreateDocumentationView()
        {
            var view = new DocumentationView();
            view.Initialize(_context);
            return view;
        }

        private EditorWorkspaceSnapshot EditSnapshot()
        {
            var workspace = _working ?? _saved ?? throw new InvalidOperationException("Open a saved module to manage devices and modifiers.");
            return workspace with
            {
                Assignments = _draft?.Snapshot ?? workspace.Assignments,
                Labels = new Dictionary<string, string>(_labels, StringComparer.Ordinal)
            };
        }

        private void RebuildDraft(EditorWorkspaceSnapshot workspace)
        {
            var commands = workspace.ModuleCommands.Select(x => new DraftCommand(x.Id, x.Name, x.IsAxis, AssignmentLayer.Module))
                .Concat(workspace.UiCommands.Select(x => new DraftCommand(x.DcsCommandId, x.Name, x.IsAxis, AssignmentLayer.UiLayer)));
            SetDraft(new DraftAssignmentService(workspace.Configuration, commands, workspace.Assignments,
                WorkspaceStructureEditor.UiModifierNames(workspace)));
        }

        private void SetDraft(DraftAssignmentService? draft)
        {
            if (_draft is not null) _draft.Changed -= DraftChanged;
            _draft = draft;
            if (_draft is not null) _draft.Changed += DraftChanged;
        }

        private void DraftChanged(object? sender, EventArgs e)
        {
            UpdateBuildAvailability();
            _context.RaiseStateChanged();
        }

        private void RefreshManagement()
        {
            if (_managementRefreshing) return;
            _managementRefreshing = true;
            try
            {
                var workspace = _working;
                var state = workspace is null ? null : WorkspaceStructureEditor.Describe(workspace);
            }
            finally { _managementRefreshing = false; }
        }

        internal void RunStructureEdit(Func<EditorWorkspaceSnapshot, EditorWorkspaceSnapshot> edit)
        {
            try
            {
                var updated = edit(EditSnapshot());
                EditionAccess.RequireWorkspace(updated, _deviceDiscovery, _training);
                RebuildDraft(updated);
                _working = updated;
                _structureDirty = true;
                _labels.Clear();
                foreach (var pair in updated.Labels) _labels[pair.Key] = pair.Value;
                RefreshManagement();
                UpdateBuildAvailability();
                _context.RaiseStateChanged();
            }
            catch (Exception ex)
            {
                _context.RaiseStatus(ex.Message);
                EditorMessage.Text = ex.Message;
            }
        }

        private void SettingsClicked(object sender, RoutedEventArgs e)
        {
            if (_settings is null) return;
            var dialog = new SettingsWindow(_settings) { Owner = this };
            dialog.ShowDialog();
            ThemeManager.Apply(_settings.Current.Theme.ToString());
        }

        internal async void SaveClicked(object sender, RoutedEventArgs e) => await SaveWorkspaceAsync();

        internal async Task SaveWorkspaceAsync()
        {
            if (_repository is null || _saved is null || _draft is null) return;
            try
            {
                var updated = (_working ?? _saved) with
                {
                    Assignments = _draft.Snapshot,
                    Labels = new Dictionary<string, string>(_labels, StringComparer.Ordinal)
                };
                EditionAccess.RequireWorkspace(updated, _deviceDiscovery, _training);
                updated = PinnedUiLayerProfileRecovery.Restore(updated);
                EditionAccess.RequireWorkspace(updated, _deviceDiscovery, _training);
                SnapshotLuaPayloadRenderer.ValidateUiLayerProfiles(updated);
                ModuleIntegrationImport.Instructions(updated);
                await _repository.SaveAsync(updated);
                _saved = updated;
                _working = updated;
                _structureDirty = false;
                RebuildDraft(updated);
                RefreshManagement();
                UpdateBuildAvailability();
                _context.RaiseStateChanged();
            }
            catch (Exception ex)
            {
                EditorMessage.Text = "Database save failed: " + ex.Message;
                _context.RaiseStatus(EditorMessage.Text);
                UpdateBuildAvailability();
                throw;
            }
        }

        internal void DiscardClicked(object sender, RoutedEventArgs e)
        {
            _working = _saved;
            _structureDirty = false;
            if (_saved is not null) RebuildDraft(_saved);
            else _draft?.Discard();
            _labels.Clear();
            if (_saved is not null)
                foreach (var pair in _saved.Labels)
                    _labels[pair.Key] = pair.Value;
            RefreshManagement();
            UpdateBuildAvailability();
            _context.RaiseStateChanged();
        }

        private bool HasPendingChanges() =>
            _structureDirty || _draft?.HasChanges == true ||
            _saved is not null &&
            (_labels.Count != _saved.Labels.Count ||
             _labels.Any(x => !_saved.Labels.TryGetValue(x.Key, out var original) || original != x.Value));

        private void BuildVersionChanged(object sender, TextChangedEventArgs e) => UpdateBuildAvailability();

        internal void UpdateBuildAvailability()
        {
            if (BuildButton is null || PackageVersion is null) return;
            SaveWorkspaceButton.IsEnabled = !_building && _repository is not null && _saved is not null && HasPendingChanges();
            DiscardWorkspaceButton.IsEnabled = !_building && HasPendingChanges();
            PendingSummary.Text = HasPendingChanges() ? "Pending assignment or authoring changes" : "No pending assignment changes";
            BuildButton.IsEnabled = !_building && _repository is not null && _saved is not null &&
                                    EditionAccess.AllowsWorkspace(_saved, _training) && !_viewModel.HasErrors && !HasPendingChanges() &&
                                    !string.IsNullOrWhiteSpace(PackageVersion.Text);
            ValidateModuleButton.IsEnabled = BuildButton.IsEnabled;
            ExportModuleSnapshotButton.IsEnabled = BuildButton.IsEnabled;
            ExportModuleDocumentationButton.IsEnabled = BuildButton.IsEnabled;
            ExportCompleteReleaseButton.IsEnabled = BuildButton.IsEnabled;
        }

        private async void ValidateModuleClicked(object sender, RoutedEventArgs e) => await RunOutputAsync(null);
        private async void ExportModuleSnapshotClicked(object sender, RoutedEventArgs e) => await RunOutputAsync(true);
        private async void BuildClicked(object sender, RoutedEventArgs e) => await RunOutputAsync(false);
        private async void ExportModuleDocumentationClicked(object sender, RoutedEventArgs e) => await RunOutputAsync(false, true);
        private async void ExportCompleteReleaseClicked(object sender, RoutedEventArgs e) => await RunOutputAsync(false, false, true);

        private async Task RunOutputAsync(bool? snapshotExport, bool documentation = false, bool completeRelease = false)
        {
            if (_saved is null || !BuildButton.IsEnabled) return;
            _building = true;
            UpdateBuildAvailability();
            BuildStatus.Text = "Validating saved assignments, physical devices, pinned assets and generated output…";
            try
            {
                var saved = _saved;
                EditionAccess.RequireWorkspace(saved, _deviceDiscovery, _training);
                var version = PackageVersion.Text.Trim();
                var snapshot = await Task.Run(() => ModuleSnapshotWorkflow.Freeze(saved, version));
                if (snapshotExport is null)
                {
                    BuildStatus.Text = $"Validated {saved.Configuration.Module.DisplayName}; Lua, device/summary pages and OvGME layout are complete. Snapshot SHA-256 {snapshot.Workspace.Sha256}";
                    return;
                }
                var export = snapshotExport.Value;
                var path = completeRelease ? (_completeReleaseFolderPicker is null ? ChooseCompleteReleaseFolder() : _completeReleaseFolderPicker())
                    : documentation ? (_documentationFolderPicker is null ? ChooseDocumentationFolder() : _documentationFolderPicker())
                    : _outputPathPicker is null ? ChooseOutputPath(export) : _outputPathPicker(export);
                if (path is null)
                {
                    BuildStatus.Text = "Output cancelled; no files were written.";
                    return;
                }
                if (completeRelease)
                {
                    var destination = Path.Combine(path, $"DCS-{saved.Configuration.Module.DcsId}-Components-{version}-Complete");
                    await Task.Run(() => ModuleCompleteReleaseWorkflow.Write(snapshot, destination));
                    BuildStatus.Text = $"Exported Complete release: {destination} · Verified frozen inputs SHA-256 {snapshot.Workspace.Sha256}";
                }
                else if (documentation)
                {
                    var destination = Path.Combine(path, $"DCS-{saved.Configuration.Module.DcsId}-Components-{version}-Documentation");
                    await Task.Run(() => ModuleDocumentationWorkflow.Write(snapshot, destination));
                    BuildStatus.Text = $"Exported module documentation: {destination} · Snapshot SHA-256 {snapshot.Workspace.Sha256}";
                }
                else if (export)
                {
                    await Task.Run(() => ModuleSnapshotWorkflow.Write(snapshot, path));
                    BuildStatus.Text = $"Exported module snapshot: {path} · SHA-256 {snapshot.Workspace.Sha256}. Restore preserves its module identity and package version.";
                }
                else
                {
                    var package = await Task.Run(() => ModuleSnapshotWorkflow.Build(snapshot));
                    var written = OvGmeArtifactWriter.Write(package, path);
                    BuildStatus.Text = $"Wrote {written.ArchivePath} and {written.ChecksumPath} · SHA-256 {written.Sha256}";
                }
            }
            catch (Exception ex)
            {
                var operation = snapshotExport is null ? "Validate" : completeRelease ? "Export Complete release" :
                    documentation ? "Export documentation" : snapshotExport.Value ? "Export module snapshot" : "Build OvGME package";
                ApplicationLog.ModuleOutputFailure(_logger, operation, _saved.Configuration.Module.Id,
                    _saved.Configuration.Module.DcsId, PackageVersion.Text.Trim(), ex);
                BuildStatus.Text = "Module output failed: " + ex.Message;
                OutputFailureDiagnostic.Text = operation + ": " + ex.Message;
            }
            finally { _building = false; UpdateBuildAvailability(); }
        }

        private string? ChooseOutputPath(bool snapshot)
        {
            if (snapshot)
            {
                var picker = new ThemeBrowseDialog { Save = true,
                    Title = "Export module snapshot to a new file",
                    Filter = "HEIMDALL module snapshot|*.heimdall-module.json",
                    FileName = _saved!.Configuration.Module.DcsId + ".heimdall-module.json",
                    OverwritePrompt = false
                };
                return picker.ShowDialog(this) == true ? picker.FileName : null;
            }
            var folder = new ThemeBrowseDialog { SelectFolder = true, Title = "Select output folder for the OvGME ZIP and SHA256SUMS.txt" };
            return folder.ShowDialog(this) == true ? folder.FolderName : null;
        }

        private string? ChooseDocumentationFolder()
        {
            var folder = new ThemeBrowseDialog { SelectFolder = true, Title = "Select parent folder for a new module documentation folder" };
            return folder.ShowDialog(this) == true ? folder.FolderName : null;
        }

        private string? ChooseCompleteReleaseFolder()
        {
            var folder = new ThemeBrowseDialog { SelectFolder = true, Title = "Select parent folder for a new Complete release and checksums" };
            return folder.ShowDialog(this) == true ? folder.FolderName : null;
        }

        private void ConfirmClose(object? sender, CancelEventArgs e)
        {
            if (HasPendingChanges() &&
                ThemeMessageBox.Show(this, "Discard unsaved command, page, device, modifier, assignment, and label changes?", "Pending changes",
                                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                e.Cancel = true;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
