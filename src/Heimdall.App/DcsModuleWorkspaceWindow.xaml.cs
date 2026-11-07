using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.App.Games;
using Heimdall.App.Games.Dcs;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Heimdall.App
{
    /// <summary>Supplies DCS actions and feature registrations to the inherited workspace lifecycle.</summary>
    public partial class DcsModuleWorkspaceWindow : GameWorkspaceWindow, IDisposable
    {
        private bool _changingSelection;
        private readonly DcsWorkspaceContext _context;
        private readonly DcsModuleWorkspaceViewModel _viewModel;
        private readonly IAppSettingsService? _settings;
        private readonly DcsWorkspaceOutput _output;

        public DcsModuleWorkspaceWindow(DcsModuleWorkspaceViewModel viewModel, IAppSettingsService? settings = null,
                                     IDcsEditorWorkspaceRepository? repository = null, IConnectedDeviceDiscovery? deviceDiscovery = null,
                                     Func<bool, IReadOnlyList<string>?>? commandFilePicker = null, Func<bool, string?>? outputPathPicker = null,
                                     Func<string?>? documentationFolderPicker = null, Func<string?>? completeReleaseFolderPicker = null,
                                     Func<string?>? integrationFilePicker = null, Func<string?>? axisDefaultsFilePicker = null,
                                     Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? inputSourceFactory = null,
                                     Action<string, string>? inputLogClipboardWriter = null, HardwareInventoryManager? inventory = null, IUserMappingLibrary? userMappings = null, Func<DcsMappingRevisionReview, bool>? mappingReviewConfirmation = null,
                                     ILogger<DcsModuleWorkspaceWindow>? logger = null, bool persistLayout = true, DcsTrainingEnvironment? training = null, bool allowGameSwitch = false, IReadOnlyList<DcsSavedModuleChoice>? moduleChoices = null)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            _viewModel = viewModel;
            _settings = training is null ? settings : null;
            Session = new(viewModel, repository, deviceDiscovery, training, inventory, userMappings, mappingReviewConfirmation,
                commandFilePicker, integrationFilePicker, axisDefaultsFilePicker, inputSourceFactory, inputLogClipboardWriter);
            _context = new DcsWorkspaceContext(Session, new DispatcherWorkspaceScheduler(Dispatcher), SaveWorkspaceAsync, () => DiscardClicked(null!, null!));
            InitializeComponent();
            BindWorkspaceContext(_context);
            BindWorkspaceStatus(EditorMessage);
            var outputLogger = logger ?? NullLogger<DcsModuleWorkspaceWindow>.Instance;
            _output = new(Session,
                training is null ? outputPathPicker ?? ChooseOutputPath : snapshot => snapshot
                    ? Path.Combine(training.Outputs, "training.heimdall-module.json") : training.Outputs,
                training is null ? documentationFolderPicker ?? ChooseDocumentationFolder : () => training.Outputs,
                training is null ? completeReleaseFolderPicker ?? ChooseCompleteReleaseFolder : () => training.Outputs,
                (operation, saved, version, error) => ApplicationLog.ModuleOutputFailure(outputLogger, operation,
                    saved.Configuration.Module.Id, saved.Configuration.Module.DcsId, version, error));
            var controllerSelection = new DcsControllerSelection(ControllerPicker, () => Session.Working);
            var persistence = training is null && persistLayout;
            WorkspaceLayout.SetPersistenceEnabled(WorkspaceRoot, persistence);
            ConfigureWorkspace(FeatureContent, FeatureActions, DcsWorkspaceFeatures.Create(_context, persistence),
                controllerSelection.Link, FeatureSelected);
            RegisterWorkspaceCleanup(Dispose);
            RegisterWorkspaceCleanup(controllerSelection.Dispose);
            _context.StateChanged += SessionChanged;
            _context.StatusChanged += SessionStatusChanged;
            _output.Changed += OutputChanged;
            RegisterWorkspaceCleanup(() =>
            {
                _context.StateChanged -= SessionChanged;
                _context.StatusChanged -= SessionStatusChanged;
                _output.Changed -= OutputChanged;
            });
            ConfigureWorkspaceCommands(SaveWorkspaceButton, DiscardWorkspaceButton, PendingSummary);
            _changingSelection = true;
            WorkspaceGamePicker.SelectedIndex = 0;
            ((ComboBoxItem)WorkspaceGamePicker.Items[1]).IsEnabled = EditionAccess.Policy.AllowsSimulator("elite-dangerous");
            ((ComboBoxItem)WorkspaceGamePicker.Items[2]).IsEnabled = EditionAccess.Policy.AllowsSimulator("star-wars-squadrons");
            ((ComboBoxItem)WorkspaceGamePicker.Items[3]).IsEnabled = EditionAccess.Policy.AllowsSimulator("war-thunder");
            WorkspaceProfilePicker.ItemsSource = moduleChoices ?? [];
            WorkspaceProfilePicker.SelectedItem = moduleChoices?.FirstOrDefault(choice => choice.Id == Session.Saved?.Configuration.Module.Id);
            GameWorkspaceSelectors.Visibility = allowGameSwitch && training is null ? Visibility.Visible : Visibility.Collapsed;
            _changingSelection = false;
            DataContext = viewModel;
            WorkspaceSettingsButton.IsEnabled = _settings is not null;
            FeatureNav.SelectedIndex = 0;
        }

        #region BEGIN Public
        /// <summary>Releases the single context and all mounted input features when this native workspace is disposed.</summary>
        public void Dispose() { ClearWorkspaceFeatures(); _context.Dispose(); GC.SuppressFinalize(this); }
        /// <summary>Gets the explicit requested game after a guarded close.</summary>
        public string? RequestedGame { get; private set; }
        /// <summary>Gets an explicit requested saved DCS profile after a guarded close.</summary>
        public Guid? RequestedModule { get; private set; }
        internal DcsWorkspaceSession Session { get; }
        internal DcsWorkspaceContext DcsContext => _context;
        internal WorkspaceOperationResult RunStructureEdit(Func<DcsEditorWorkspaceSnapshot, DcsEditorWorkspaceSnapshot> edit) => _context.RunStructureEdit(edit);
        internal void UpdateBuildAvailability() => RefreshWorkspaceState();
        #endregion END Public

        /// <inheritdoc/>
        protected override bool HasWorkspaceChanges => Session.HasPendingChanges;
        /// <inheritdoc/>
        protected override bool CanSaveWorkspace => Session.Repository is not null && Session.Saved is not null &&
            EditionAccess.AllowsWorkspace(Session.Working, Session.Training);
        /// <inheritdoc/>
        protected override Task SaveWorkspaceCoreAsync(IWorkspaceOperationLease lease) => Session.SaveAsync(lease);
        /// <inheritdoc/>
        protected override void DiscardWorkspaceCore(IWorkspaceOperationLease lease) => Session.Discard(lease);
        /// <inheritdoc/>
        protected override void ReportWorkspaceError(Exception exception) => EditorMessage.Text = "Unable to save workspace: " + exception.Message;
        /// <inheritdoc/>
        protected override bool ConfirmDiscardWorkspaceChanges() => ThemeMessageBox.Show(this,
            "Discard unsaved command, page, device, modifier, assignment, and label changes?", "Pending changes",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        /// <inheritdoc/>
        protected override void OnWorkspaceStateChanged()
        {
            RefreshFeatureAvailability();
            if (_output is null || BuildButton is null || PackageVersion is null) return;
            BuildButton.IsEnabled = !WorkspaceActionRunning && _output.CanRun(_viewModel.HasErrors, PackageVersion.Text);
            ValidateModuleButton.IsEnabled = ExportModuleSnapshotButton.IsEnabled = ExportModuleDocumentationButton.IsEnabled =
                ExportCompleteReleaseButton.IsEnabled = BuildButton.IsEnabled;
        }

        #region BEGIN private helpers
        private void SessionChanged(object? sender, EventArgs e) => RefreshWorkspaceState();
        private void SessionStatusChanged(object? sender, string message) => EditorMessage.Text = message;
        private void OutputChanged(object? sender, EventArgs e)
        {
            BuildStatus.Text = _output.Status;
            OutputFailureDiagnostic.Text = _output.FailureDiagnostic;
        }

        private Task<bool> ConfirmWorkspaceSwitch() => ConfirmWorkspaceLeaveAsync(() => ThemeMessageBox.Show(this,
            "Save pending edits before switching? Yes saves, No discards, Cancel stays here.", "Switch workspace",
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) switch
            {
                MessageBoxResult.Yes => GameWorkspaceLeaveDecision.Save,
                MessageBoxResult.No => GameWorkspaceLeaveDecision.Discard,
                _ => GameWorkspaceLeaveDecision.Cancel
            });

        private async void WorkspaceGameChanged(object sender, SelectionChangedEventArgs e)
        {
            var game = WorkspaceGamePicker.SelectedIndex switch { 1 => "elite-dangerous", 2 => "star-wars-squadrons", 3 => "war-thunder", _ => null };
            if (_changingSelection || game is null || !EditionAccess.Policy.AllowsSimulator(game)) return;
            if (await ConfirmWorkspaceSwitch()) { RequestedGame = game; Close(); }
            else { _changingSelection = true; WorkspaceGamePicker.SelectedIndex = 0; _changingSelection = false; }
        }

        private async void WorkspaceProfileChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_changingSelection || WorkspaceProfilePicker.SelectedItem is not DcsSavedModuleChoice choice || choice.Id == Session.Saved?.Configuration.Module.Id) return;
            if (await ConfirmWorkspaceSwitch()) { RequestedModule = choice.Id; Close(); }
            else
            {
                _changingSelection = true;
                WorkspaceProfilePicker.SelectedItem = WorkspaceProfilePicker.Items.Cast<DcsSavedModuleChoice>().FirstOrDefault(item => item.Id == Session.Saved?.Configuration.Module.Id);
                _changingSelection = false;
            }
        }

        private void FeatureSelected(string key)
        {
            AssignmentModes.Visibility = key is "Assignments" or "InputTest" ? Visibility.Visible : Visibility.Collapsed;
            AssignMode.IsChecked = key == "Assignments";
            TestMode.IsChecked = key == "InputTest";
            RefreshFeatureAvailability();
        }

        private void RefreshFeatureAvailability()
        {
            if (FeatureContent is null || FeatureActions is null) return;
            var allowed = EditionAccess.AllowsWorkspace(Session.Working, Session.Training);
            FeatureContent.IsEnabled = FeatureActions.IsEnabled = allowed && !WorkspaceActionRunning && !Session.IsSaving &&
                _context.State.Activity != WorkspaceActivityKind.Scan;
            if (!allowed) EditorMessage.Text = "Reserved hardware: this workspace is read-only in Free Edition. Saved data is retained. Premium availability TBA.";
        }

        private void FeatureNavChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FeatureNav.SelectedItem is ListBoxItem { Tag: string key }) NavigateToFeature(key);
        }

        private void AssignmentModeClicked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton { Tag: string key }) return;
            FeatureNav.SelectedItem = FeatureNav.Items.Cast<ListBoxItem>().Single(item => Equals(item.Tag, "Assignments"));
            NavigateToFeature(key);
        }

        private void SettingsClicked(object sender, RoutedEventArgs e)
        {
            if (_settings is null) return;
            new SettingsWindow(_settings) { Owner = this }.ShowDialog();
            ThemeManager.Apply(_settings.Current.Theme.ToString());
        }

        private void BuildVersionChanged(object sender, TextChangedEventArgs e) => UpdateBuildAvailability();
        private async void ValidateModuleClicked(object sender, RoutedEventArgs e) => await RunOutputAsync(null);
        private async void ExportModuleSnapshotClicked(object sender, RoutedEventArgs e) => await RunOutputAsync(true);
        private async void BuildClicked(object sender, RoutedEventArgs e) => await RunOutputAsync(false);
        private async void ExportModuleDocumentationClicked(object sender, RoutedEventArgs e) => await RunOutputAsync(false, true);
        private async void ExportCompleteReleaseClicked(object sender, RoutedEventArgs e) => await RunOutputAsync(false, false, true);
        private Task RunOutputAsync(bool? snapshotExport, bool documentation = false, bool completeRelease = false)
        {
            if (!BuildButton.IsEnabled) return Task.CompletedTask;
            var version = PackageVersion.Text.Trim();
            return RunWorkspaceActionAsync(() => _output.RunAsync(version, snapshotExport, documentation, completeRelease), WorkspaceOperationKind.Export);
        }

        private void OutputMenuOpened(object? sender, EventArgs e) => PackageVersion.Focus();
        private void OutputMenuKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Escape) return;
            OutputMenu.IsOpen = false; OutputMenuButton.Focus(); e.Handled = true;
        }

        private string? ChooseOutputPath(bool snapshot)
        {
            if (snapshot)
            {
                var picker = new ThemeBrowseDialog { Save = true, Title = "Export module snapshot to a new file",
                    Filter = "HEIMDALL module snapshot|*.heimdall-module.json",
                    FileName = Session.Saved!.Configuration.Module.DcsId + ".heimdall-module.json", OverwritePrompt = false };
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

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        #endregion END private helpers
    }
}
