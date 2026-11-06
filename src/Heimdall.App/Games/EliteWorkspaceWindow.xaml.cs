using System.IO;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;
using Heimdall.App.Games.Elite;

namespace Heimdall.App.Games
{
    /// <summary>Assembles ED-native collaborators and supplies native hooks to the shared workspace base.</summary>
    public sealed partial class EliteWorkspaceWindow : GameWorkspaceWindow, IDisposable
    {
        private readonly EliteDevicesView _devicesView = new();
        private readonly EliteAssignmentsView _assignmentsView = new();
        private readonly EliteReferenceSheetsView _referenceView = new();
        private readonly EliteWorkspaceContext _context;
        private readonly EliteControllerPresenter _controllers;
        private readonly EliteAssignmentEditing _editing;
        private readonly EliteAssignmentBrowser _browser;
        private readonly EliteAssignmentDrawing _drawing;
        private readonly EliteReferencePresenter _references;
        private readonly EliteProfileOperations _profiles;
        private readonly GameControllerReordering _reordering;
        private readonly Func<MessageBoxResult>? _pendingChoice;
        private bool _allowClose;
        private bool _disposed;
        private GameProfileSession? _session { get => _context.Session; set => _context.Session = value; }
        private string? _revision { get => _context.Revision; set => _context.Revision = value; }
        private bool _refreshing { get => _context.Refreshing; set => _context.Refreshing = value; }
        private bool _busy => WorkspaceActionRunning;
        private GameWorkspaceServices _services => _context.Services;
        private IReadOnlyList<WorkspaceHardware> _hardware => _context.Hardware;
        private GameAssignmentSelection _selection => _assignmentsView.AssignmentSelection;
        private IReadOnlyList<SimCommand> _commands { get => _context.Commands; set => _context.Commands = value; }
        private IReadOnlyList<EliteAssignmentRow> _rows { get => _context.Rows; set => _context.Rows = value; }
        private GameDeviceChoice? Device => _context.Device;
        private SimCommand? Command => _context.Command;
        private GameWorkspaceChromeState Chrome => _context.Chrome;
        private bool Pending => Chrome.Pending;
        /// <summary>Assembles native ED collaborators around inherited workspace and assignment algorithms.</summary>
        public EliteWorkspaceWindow(GameWorkspaceServices services, IReadOnlyList<WorkspaceHardware> hardware,
            Guid? initial = null, Func<string?>? importPicker = null, Func<string?>? exportPicker = null,
            Func<MessageBoxResult>? pendingChoice = null, Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? inputFactory = null, Func<GameControllerChange, bool>? controllerReview = null)
        {
            EditionAccess.Policy.RequireSimulator(services.Backend.SimId);
            _pendingChoice = pendingChoice;
            _context = new(services, hardware, _assignmentsView.AssignmentSelection,
                () => DevicePicker?.SelectedItem as GameDeviceChoice,
                () => (SectionPicker?.SelectedItem as ListBoxItem)?.Content.ToString() ?? "All",
                () => ProfileName?.Text.Trim() ?? "", () => FeaturePicker?.SelectedIndex ?? -1,
                () => WorkspaceActionRunning, () => _controllers?.Input.Running == true, () => _controllers?.Identifying == true);
            InitializeComponent();
            _assignmentsView.ConfigureBrowser(EliteCommandBrowser.InputTypes, EliteCommandBrowser.AssignmentStatuses);
            RegisterFeatureNames(_devicesView); RegisterFeatureNames(_assignmentsView); RegisterFeatureNames(_referenceView);
            RegisterName("DiagramZoom", _assignmentsView.DiagramZoom);
            RegisterName("InputActivityHint", _assignmentsView.InputActivityHint);
            RegisterName("InputActivity", _assignmentsView.InputActivity);
            RegisterName("DiagramHint", _assignmentsView.DiagramHint);
            RegisterName("DiagramScroll", _assignmentsView.DiagramScroll);
            RegisterName("DiagramCanvas", _assignmentsView.DiagramCanvas);
            // Deferred callbacks run after all native collaborators have been assembled.
            _drawing = new(_context, this, _assignmentsView, _devicesView, StopLiveInput, row => _browser!.SelectRow(row), key => _editing!.SelectInput(key),
                () => _editing!.ApplyClicked(this, new()), () => _editing!.ClearClicked(this, new()), Run, Status);
            _controllers = new(_context, this, _devicesView, DevicePicker, _drawing.Presenter, _drawing.ShowInputActivity,
                activity => _controllers!.ShowIdentification(activity), () => _controllers!.ClearIdentification(), inputFactory,
                controllerReview ?? (change => new ControllerChangeReviewWindow(this, change).ShowDialog() == true),
                () => _editing!.RefreshInputChoices(), () => _editing!.Actions.Start, RunBusy, UpdateAvailability, Run, Status);
            _editing = new(_context, _assignmentsView, DevicePicker, () => _controllers.Evidence, UpdateAvailability, Run, Status);
            _browser = new(_context, _assignmentsView, UpdateAvailability, Status);
            _references = new(_context, this, _referenceView, _drawing.RenderDrawing, Run, Status);
            _profiles = new(_context, this, ProfilePicker, ProfileName, BindingsDirectoryText, ExportToReviewFolder, ExportState,
                importPicker, exportPicker, OpenSession, ReloadChoices, Remember, ConfirmLeave, StopLiveInput,
                _controllers.ReadReplacementInputs, _controllers.ReviewControllerChange, RunBusy, Run, Status);
            _assignmentsView.Initialize(_context, _editing.LoadAssignment, _drawing.RenderDevice,
                SaveWorkspaceAsync, () => DiscardClicked(this, new()), Status);
            ConfigureWorkspace(FeatureContent, FeatureActions,
                [new("Devices", () => _devicesView), new("Assignments", () => _assignmentsView), new("Reference", () => _referenceView)]);
            ConfigureWorkspaceCommands(SaveProfileButton, DiscardProfileButton, SaveState);
            _reordering = new(DevicePicker, (source, target, after) => Run(() => _session?.MoveController(source, target, after)));
            _devicesView.IdentifyControllerButton.Click += _controllers.IdentifyControllerClicked;
            _devicesView.CancelIdentificationButton.Click += _controllers.CancelIdentificationClicked;
            _devicesView.ConfirmIdentificationButton.Click += _controllers.ConfirmIdentificationClicked;
            _devicesView.ConnectedPicker.SelectionChanged += _controllers.ConnectedChanged;
            _devicesView.ReplacementReferencePicker.SelectionChanged += (_, _) => { if (!_refreshing) Run(_controllers.UpdateControllerAvailability); };
            _devicesView.RescanControllerButton.Click += _controllers.RescanClicked;
            _devicesView.AddControllerButton.Click += _controllers.AddControllerClicked;
            _devicesView.ReplaceControllerButton.Click += _controllers.ReplaceControllerClicked;
            _devicesView.RemoveControllerButton.Click += _controllers.RemoveControllerClicked;
            _devicesView.LinkReferenceButton.Click += _controllers.LinkReferenceClicked;
            _devicesView.ChangeDrawingButton.Click += _controllers.ChangeDrawingClicked;
            _devicesView.UnlinkControllerButton.Click += _controllers.UnlinkControllerClicked;
            _devicesView.UseDrawingButton.Click += _controllers.UseDrawingClicked;
            _devicesView.NoDrawingButton.Click += _controllers.NoDrawingClicked;
            _assignmentsView.StartInputButton.Click += _controllers.StartInputClicked;
            _assignmentsView.StopInputButton.Click += _controllers.StopInputClicked;
            _assignmentsView.ExportSheetButton.Click += _references.ExportSheetClicked;
            _assignmentsView.FitDiagramButton.Click += _drawing.FitDiagramClicked;
            _assignmentsView.AssignmentsGrid.SelectionChanged += _browser.AssignmentSelected;
            _assignmentsView.CommandCategoryPicker.SelectionChanged += _browser.CommandFilterChanged;
            _assignmentsView.CommandTypePicker.SelectionChanged += _browser.CommandFilterChanged;
            _assignmentsView.AssignmentStatusPicker.SelectionChanged += _browser.CommandFilterChanged;
            _assignmentsView.InputKey.SelectionChanged += _editing.AssignmentModeChanged;
            _assignmentsView.AxisDeadzone.TextChanged += _editing.AssignmentTextChanged;
            _assignmentsView.ModePicker.SelectionChanged += _editing.AssignmentModeChanged;
            _assignmentsView.InvertAxis.Checked += _editing.AssignmentOptionChanged; _assignmentsView.InvertAxis.Unchecked += _editing.AssignmentOptionChanged;
            _assignmentsView.CommandToggle.Checked += _editing.AssignmentOptionChanged; _assignmentsView.CommandToggle.Unchecked += _editing.AssignmentOptionChanged;
            _assignmentsView.ClearCommandFiltersButton.Click += _browser.ClearCommandFiltersClicked;
            _assignmentsView.CommandSearch.TextChanged += _browser.SearchChanged;
            _assignmentsView.CommandsList.SelectionChanged += _browser.CommandChanged;
            _assignmentsView.SlotPicker.SelectionChanged += _browser.SlotChanged;
            _assignmentsView.AddCombinationButton.Click += _editing.AddModifierClicked;
            _assignmentsView.ModifierDevicePicker.SelectionChanged += (_, _) => { if (!_refreshing) _editing.RefreshCombinationInputs(); };
            _assignmentsView.RemoveCombinationButton.Click += _editing.RemoveModifierClicked;
            _assignmentsView.ApplyAssignmentButton.Click += _editing.ApplyClicked;
            _assignmentsView.ClearAssignmentButton.Click += _editing.ClearClicked;
            _referenceView.ReferenceLayoutPicker.SelectionChanged += _references.ReferenceOptionsChanged;
            _referenceView.ReferencePaperPicker.SelectionChanged += _references.ReferenceOptionsChanged;
            _referenceView.ReferenceOrientationPicker.SelectionChanged += _references.ReferenceOptionsChanged;
            _referenceView.ReferenceAssignedOnly.Click += _references.ReferenceAssignedChanged;
            _referenceView.ReferencePreviousButton.Click += _references.ReferencePreviousClicked;
            _referenceView.ReferenceNextButton.Click += _references.ReferenceNextClicked;
            _referenceView.ReferencePdfButton.Click += _references.ReferencePdfClicked;
            _referenceView.ReferencePngButton.Click += _references.ReferencePngClicked;
            _refreshing = true;
            BindingsDirectoryText.Text = _profiles.BindingsDirectory;
            ExportToReviewFolder.IsChecked = exportPicker is not null;
            _assignmentsView.AssignmentStatusPicker.SelectedItem = EliteCommandBrowser.AssignmentStatuses.Single(x => x.Status == GameCommandAssignmentStatus.All);
            GamePicker.SelectedIndex = 1; SectionPicker.SelectedIndex = 0; _assignmentsView.SlotPicker.SelectedIndex = 0; _assignmentsView.ModePicker.SelectedIndex = 0;
            _assignmentsView.CommandCategoryPicker.ItemsSource = new[] { "All categories" }; _assignmentsView.CommandCategoryPicker.SelectedIndex = 0; _assignmentsView.CommandTypePicker.SelectedItem = EliteCommandBrowser.InputTypes.Single(x => x.Type is null);
            _referenceView.ReferenceLayoutPicker.SelectedIndex = 0; _referenceView.ReferencePaperPicker.SelectedIndex = 0; _referenceView.ReferenceOrientationPicker.SelectedIndex = 0;
            _devicesView.HardwarePicker.ItemsSource = hardware.Where(x => EditionAccess.Policy.AllowsHardware(x.DeviceId)).ToArray();
            _refreshing = false;
            ReloadChoices(initial);
            FeaturePicker.SelectedIndex = 0;
            RegisterWorkspaceCleanup(Dispose);
            AppearanceManager.ObserveArtwork(this, () => Run(() => { _drawing.RenderDevice(); _references.InvalidateReferenceSheets(); }));
            _controllers.Rescan(); UpdateAvailability(); _controllers.Start();
        }
        #region BEGIN Public
        /// <summary>Releases native subscriptions and input handles exactly once.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ClearWorkspaceFeatures(); _browser.Dispose(); _reordering.Dispose(); _controllers.Dispose();
            if (_session is not null) _session.Changed -= SessionChanged;
            GC.SuppressFinalize(this);
        }
        /// <summary>Gets a guarded request to return to DCS.</summary>
        public bool DcsRequested { get; private set; }
        /// <summary>Gets a guarded request to open Squadrons.</summary>
        public bool SquadronsRequested { get; private set; }
        /// <summary>Gets a guarded request to open War Thunder.</summary>
        public bool WarThunderRequested { get; private set; }
        internal GameProfileSession? Session => _session;
        internal WorkspaceAssignmentWorkflow AssignmentWorkflow => _assignmentsView.AssignmentWorkflow;
        internal GameAssignmentSelection AssignmentSelection => _assignmentsView.AssignmentSelection;
        internal DataObject ControllerDrag(string id) => _reordering.Payload(id);
        internal bool DropController(IDataObject data, string target, bool after) => _reordering.DropAt(data, target, after);
        internal void SampleInput() => _controllers.Input.Sample();
        internal void SampleIdentification() => _controllers.SampleIdentification();
        internal void ImportFile(string path) => _profiles.ImportFile(path);
        internal void ExportReference(string path) => _references.ExportReference(path);
        internal byte[] ReferencePdf() => _references.ReferencePdf();
        internal byte[] ReferencePng() => _references.ReferencePng();
        internal void SetBindingsDirectory(string? path) => _profiles.SetBindingsDirectory(path);
        internal HardwareDiagramInteractions DiagramInteractions() => _drawing.DiagramInteractions();
        internal Func<EliteAxisTuningWindow, bool?>? AxisTuningReview { get => _drawing.AxisTuningReview; set => _drawing.AxisTuningReview = value; }
        internal Func<string, bool>? ConfirmPresetReplacement { get => _profiles.ConfirmPresetReplacement; set => _profiles.ConfirmPresetReplacement = value; }
        internal Func<IReadOnlyList<GameExportIssue>, GameExportResolution?>? ExportReview { get => _profiles.ExportReview; set => _profiles.ExportReview = value; }
        internal Func<string?>? BindingsDirectoryPicker { get => _profiles.BindingsDirectoryPicker; set => _profiles.BindingsDirectoryPicker = value; }
        internal Func<string, string, string?>? ConfigurationNamePicker { get => _profiles.ConfigurationNamePicker; set => _profiles.ConfigurationNamePicker = value; }
        internal Func<string, bool>? ConfirmConfigurationDeletion { get; set; }
        internal bool SaveProfile()
        {
            try { SaveWorkspaceAsync().GetAwaiter().GetResult(); return true; }
            catch (Exception error) { ReportWorkspaceError(error); return false; }
        }
        #endregion END Public
        protected override bool HasWorkspaceChanges => Pending;
        protected override bool CanSaveWorkspace => Chrome.CanSave;
        protected override string WorkspaceStateSummary => Chrome.Status;
        protected override Task SaveWorkspaceCoreAsync() { _profiles.SaveCore(); return Task.CompletedTask; }
        protected override void DiscardWorkspaceCore()
        {
            _session?.Discard();
            if (_session is not null) { _refreshing = true; ProfileName.Text = _session.Snapshot.Name; _refreshing = false; }
        }
        protected override bool CanCloseWorkspace() => !WorkspaceActionRunning && (_allowClose || ConfirmLeave());
        protected override void ReportWorkspaceError(Exception exception) => Status("Save failed; pending edits are retained: " + exception.Message);
        protected override void OnWorkspaceStateChanged() => UpdateNativeAvailability();
        #region BEGIN private helpers
        private void RegisterFeatureNames(DependencyObject root)
        {
            if (root is FrameworkElement { Name.Length: > 0 } element) RegisterName(element.Name, element);
            if (root is GameDrawingView) return;
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) RegisterFeatureNames(child);
        }

        private void DeleteConfigurationClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (_session is null || _revision is null || _busy) return;
            var workspace = _session.Snapshot;
            var confirmed = ConfirmConfigurationDeletion?.Invoke(workspace.Name) ?? ThemeMessageBox.Show(this,
                "Delete HEIMDALL configuration: " + workspace.Name + "? Pending edits will also be discarded. Exported ED preset files remain in place.",
                "Delete configuration", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
            if (!confirmed) return;
            StopLiveInput(); _services.Store.Delete(workspace.Id, _revision);
            _session.Changed -= SessionChanged; _session = null; _revision = null;
            _refreshing = true; ProfileName.Text = ""; DevicePicker.ItemsSource = null;
            _assignmentsView.CommandsList.ItemsSource = null; _assignmentsView.AssignmentsGrid.ItemsSource = null; _refreshing = false;
            _assignmentsView.ModifierDevicePicker.ItemsSource = null; _editing.RefreshCombinationInputs();
            _assignmentsView.DiagramCanvas.Children.Clear(); _drawing.Presenter.Regions.Clear();
            _selection.Reset(); _assignmentsView.Selection.ShowCommand(null);
            _assignmentsView.AssignmentDraftStatus.Text = "Choose a configuration.";
            _rows = []; _commands = []; _references.Clear(); _references.MarkDirty();
            ReloadChoices(null);
            var next = ProfilePicker.Items.Cast<GameWorkspaceEntry>().FirstOrDefault();
            if (next is not null) { LoadProfile(next.Id); ReloadChoices(next.Id); }
            else _services.Remember(_services.Recent() with { BackendId = "elite-dangerous", WorkspaceId = null, DisplayName = "" });
            UpdateAvailability(); Status("HEIMDALL configuration deleted. Exported ED presets remain in place.");
        });

        private void MoveControllerUpClicked(object sender, RoutedEventArgs e) => _reordering.MoveSelected(-1);

        private void MoveControllerDownClicked(object sender, RoutedEventArgs e) => _reordering.MoveSelected(1);

        private void Run(Action action) { try { action(); } catch (Exception error) { Status(error.Message); } }

        private void OpenSession(GameWorkspace workspace, string? revision)
        {
            StopLiveInput(); _selection.Reset(); _references.MarkDirty();
            _devicesView.ReferenceChoiceTools.Visibility = _devicesView.DrawingTools.Visibility = Visibility.Collapsed;
            var opened = new GameProfileSession(_services.Backend, workspace, EditionAccess.Policy, _hardware.Select(x => x.DeviceId).ToArray(), _services.Discover, _services.DeviceReferences);
            if (_session is not null) _session.Changed -= SessionChanged;
            ExportState.Text = "Workspace open in HEIMDALL. Export writes a named ED preset to the selected destination.";
            _session = opened; _revision = revision; _session.Changed += SessionChanged;
            _refreshing = true; ProfileName.Text = workspace.Name; _refreshing = false;
            Refresh();
        }

        private void SessionChanged(object? sender, EventArgs e) { StopLiveInput(); _references.MarkDirty(); Refresh(); _context.NotifyChanged(); }

        private void ReloadChoices(Guid? selected)
        {
            _refreshing = true;
            try
            {
                var choices = _services.Store.List().Where(x => x.BackendId == "elite-dangerous").ToArray();
                ProfilePicker.ItemsSource = choices;
                ProfilePicker.SelectedItem = choices.FirstOrDefault(x => x.Id == selected);
            }
            catch (Exception error)
            {
                ProfilePicker.ItemsSource = Array.Empty<GameWorkspaceEntry>();
                Status("Saved profiles need attention. You can import a separate profile; retained files are unchanged. " + error.Message);
            }
            finally { _refreshing = false; }
            if (_session is null && selected is { } id) Run(() => LoadProfile(id));
        }

        private void LoadProfile(Guid id)
        {
            var saved = _services.Store.Load(id); OpenSession(saved.Workspace, saved.Revision); Remember(saved.Workspace);
        }

        private void Remember(GameWorkspace workspace) => _services.Remember(_services.Recent() with { BackendId = "elite-dangerous", WorkspaceId = workspace.Id, DisplayName = workspace.Name });

        private void Refresh()
        {
            if (_session is null) { UpdateAvailability(); return; }
            var document = _session.Snapshot;
            var previousDevice = Device?.Id; var previousCommand = Command?.Id;
            var previousCombinationDevice = (_assignmentsView.ModifierDevicePicker.SelectedItem as GameDeviceChoice)?.Id;
            _commands = _session.Catalog.Commands;
            _rows = EliteWorkspaceProjection.Rows(document.Profile, _session.Catalog);
            _refreshing = true;
            var devices = GameControllerOrder.For(document).Select(id => new GameDeviceChoice(id, EliteWorkspaceContext.ControllerName(document, id))).ToArray();
            DevicePicker.ItemsSource = devices; DevicePicker.SelectedItem = devices.FirstOrDefault(x => x.Id == previousDevice) ?? devices.FirstOrDefault();
            _assignmentsView.ModifierDevicePicker.ItemsSource = devices;
            _assignmentsView.ModifierDevicePicker.SelectedItem = devices.FirstOrDefault(x => x.Id == previousCombinationDevice)
                ?? devices.FirstOrDefault(x => x.Id == Device?.Id);
            _refreshing = false;
            _editing.RefreshCombinationInputs();
            _browser.FilterCommands(previousCommand); _drawing.RenderDevice(); _controllers.UpdateReplacementReferences(); UpdateAvailability();
        }

        private void GameChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing || GamePicker.SelectedIndex is not 0 and not 2 and not 3) return;
            if (!ConfirmLeave()) { _refreshing = true; GamePicker.SelectedIndex = 1; _refreshing = false; return; }
            DcsRequested = GamePicker.SelectedIndex == 0; SquadronsRequested = GamePicker.SelectedIndex == 2; WarThunderRequested = GamePicker.SelectedIndex == 3; _allowClose = true; Close();
        }

        private void ProfileChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing || ProfilePicker.SelectedItem is not GameWorkspaceEntry entry || entry.Id == _session?.Snapshot.Id) return;
            if (ConfirmLeave())
            {
                try { LoadProfile(entry.Id); return; }
                catch (Exception error) { Status("Unable to open the selected profile; the current profile is retained: " + error.Message); }
            }
            _refreshing = true; ProfilePicker.SelectedItem = ProfilePicker.Items.Cast<GameWorkspaceEntry>().FirstOrDefault(x => x.Id == _session?.Snapshot.Id); _refreshing = false;
        }

        private void ProfileNameChanged(object sender, TextChangedEventArgs e) { if (!_refreshing && SaveState is not null) { _references.MarkDirty(); UpdateAvailability(); } }

        private void SectionChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing && _session is not null) Run(() => { _references.MarkDirty(); _browser.FilterCommands(); _drawing.RenderDevice(); }); }

        private void DeviceChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing || _session is null) return;
            Run(() =>
            {
                _references.MarkDirty(); _devicesView.DrawingTools.Visibility = Visibility.Collapsed;
                _devicesView.HardwarePicker.SelectedItem = null;
                _browser.FilterCommands(Command?.Id); _drawing.RenderDevice();
                Status("Selected controller: " + (Device?.Name ?? "none"));
            });
        }
        private void Status(string message) => WorkspaceStatus.Text = message;
        private void UpdateAvailability() => RefreshWorkspaceState();
        private void StopLiveInput() => _controllers?.StopLiveInput();
        private void RunBusy(Action action) => RunWorkspaceActionAsync(() => { action(); return Task.CompletedTask; }).GetAwaiter().GetResult();
        private bool ConfirmLeave()
        {
            StopLiveInput();
            return ConfirmWorkspaceLeaveAsync(() => (_pendingChoice?.Invoke() ?? ThemeMessageBox.Show(this,
                "Save this profile before leaving? Yes saves, No discards pending edits, Cancel stays here.", "Unsaved game profile",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel)) switch
                { MessageBoxResult.Yes => GameWorkspaceLeaveDecision.Save, MessageBoxResult.No => GameWorkspaceLeaveDecision.Discard, _ => GameWorkspaceLeaveDecision.Cancel })
                .GetAwaiter().GetResult();
        }
        private void FeatureChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_controllers is null) return;
            var key = FeaturePicker.SelectedIndex switch { 1 => "Assignments", 2 => "Reference", _ => "Devices" };
            if (CurrentFeatureKey != key) StopLiveInput();
            NavigateToFeature(key); UpdateAvailability();
        }
        private void NewConfigurationClicked(object sender, RoutedEventArgs e) => _profiles.NewConfigurationClicked(sender, e);
        private void DuplicateConfigurationClicked(object sender, RoutedEventArgs e) => _profiles.DuplicateConfigurationClicked(sender, e);
        private void RenameConfigurationClicked(object sender, RoutedEventArgs e) => _profiles.RenameConfigurationClicked(sender, e);
        private void ImportClicked(object sender, RoutedEventArgs e) => _profiles.ImportClicked(sender, e);
        private void ExportClicked(object sender, RoutedEventArgs e) => _profiles.ExportClicked(sender, e);
        private void BrowseBindingsDirectoryClicked(object sender, RoutedEventArgs e) => _profiles.BrowseBindingsDirectoryClicked(sender, e);
        private void ResetBindingsDirectoryClicked(object sender, RoutedEventArgs e) => _profiles.ResetBindingsDirectoryClicked(sender, e);
        private void UpdateNativeAvailability()
        {
            if (_profiles is null) return;
            var opened = _session is not null;
            AccessStatus.Text = _session?.AccessMessage ?? "Import or select a saved Elite controls profile.";
            ExportPresetButton.IsEnabled = Chrome.CanExport;
            NewConfigurationButton.IsEnabled = !_busy && (!opened || AccessStatus.Text.Length == 0);
            DuplicateConfigurationButton.IsEnabled = opened && !_busy;
            RenameConfigurationButton.IsEnabled = opened && !_busy;
            DeleteConfigurationButton.IsEnabled = opened && _revision is not null && !_busy;
            ProfileName.IsEnabled = opened;
            _controllers.UpdateControllerAvailability();
            var document = _session?.Snapshot;
            var pin = Device is not null ? document?.Hardware.GetValueOrDefault(Device.Id) : null;
            var drawing = document is not null && Device is not null ? _context.DrawingFor(document, Device.Id) : null;
            _devicesView.DrawingStatus.Text = drawing is not null ? "Drawing: " + drawing.Label : pin is null ? "Select a controller in this profile to view its drawing." : "No drawing · assignments table available";
            MoveControllerUpButton.IsEnabled = DevicePicker.SelectedIndex > 0;
            MoveControllerDownButton.IsEnabled = DevicePicker.SelectedIndex >= 0 && DevicePicker.SelectedIndex + 1 < DevicePicker.Items.Count;
            _editing.UpdateAvailability(); _drawing.UpdateInputActivityVisibility();
            if (FeaturePicker.SelectedIndex == 2) _references.RefreshReferenceSheets();
        }
        #endregion END private helpers
    }
}
