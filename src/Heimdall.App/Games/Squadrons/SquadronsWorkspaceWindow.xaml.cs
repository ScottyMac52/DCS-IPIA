using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Heimdall.Domain.MultiSim;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Microsoft.Win32;

namespace Heimdall.App.Games.Squadrons
{
    /// <summary>Assembles native Squadrons components over shared workspace navigation, persistence and lifetime.</summary>
    public sealed partial class SquadronsWorkspaceWindow : GameWorkspaceWindow, IDisposable
    {
        private readonly SquadronsDevicesView _devicesView = new();
        private readonly SquadronsAssignmentsView _assignmentsView = new();
        private readonly SquadronsWorkspaceContext _context;
        private readonly SquadronsControllerPresenter _controllers;
        private readonly SquadronsAssignmentBrowser _browser;
        private readonly SquadronsAssignmentEditing _editing;
        private readonly SquadronsAssignmentDrawing _drawing;
        private readonly SquadronsProfileOperations _profiles;
        private readonly GameCommandDragSource _commandDrag;
        private readonly GameControllerReordering _reordering;
        private readonly Func<MessageBoxResult>? _pendingChoice;
        private bool _disposed;
        private bool _allowClose;
        #region BEGIN Public
        /// <summary>Opens an explicitly selected native workspace without importing active game settings.</summary>
        public SquadronsWorkspaceWindow(GameWorkspaceServices services, Guid? initial = null, Func<MessageBoxResult>? pendingChoice = null,
            Func<GameControllerChange, bool>? reviewChange = null, Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? identificationFactory = null,
            IReadOnlyList<WorkspaceHardware>? drawings = null,
            Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? inputFactory = null)
        {
            EditionAccess.Policy.RequireSimulator("star-wars-squadrons");
            _pendingChoice = pendingChoice;
            _context = new(services);
            _context.Drawings = drawings ?? [];
            InitializeComponent(); AppearanceManager.Attach(this);
            BindWorkspaceContext(_context, () => Refresh());
            BindWorkspaceStatus(StatusText);
            foreach (var (name, control) in new (string, object)[]
            {
                ("RescanControllerButton", RescanControllerButton), ("IdentifyControllerButton", IdentifyControllerButton), ("CancelIdentificationButton", CancelIdentificationButton),
                ("LinkControllerButton", LinkControllerButton), ("AddControllerButton", AddControllerButton), ("RenameControllerButton", RenameControllerButton),
                ("NativeControllerName", NativeControllerName), ("UnlinkControllerButton", UnlinkControllerButton), ("ClearSlotButton", ClearSlotButton),
                ("RemoveUnusedSlotButton", RemoveUnusedSlotButton), ("ClearDrawingButton", ClearDrawingButton), ("ControllerSetup", ControllerSetup),
                ("LinkStatus", LinkStatus), ("ConnectedPicker", ConnectedPicker), ("IdentificationStatus", IdentificationStatus),
                ("DrawingPicker", DrawingPicker), ("FitDrawingButton", FitDrawingButton), ("ApplyButton", ApplyButton),
                ("StartInputButton", StartInputButton), ("StopInputButton", StopInputButton), ("ShowCommandButton", ShowCommandButton),
                ("CommandHint", CommandHint), ("SectionPicker", SectionPicker), ("TypePicker", TypePicker),
                ("Search", Search), ("ReviewCaptured", ReviewCaptured), ("CountText", CountText),
                ("CommandBrowser", CommandBrowser), ("CapturedAssignments", CapturedAssignments), ("BindingsGrid", BindingsGrid),
                ("EditNativeRecordItem", EditNativeRecordItem), ("ClearNativeRecordItem", ClearNativeRecordItem), ("ApplyNativeRecordItem", ApplyNativeRecordItem),
                ("ContextColumn", ContextColumn), ("HardwareColumn", HardwareColumn), ("ControllerColumn", ControllerColumn),
                ("BindingColumn", BindingColumn), ("EmptyState", EmptyState), ("DrawingPanel", DrawingPanel),
                ("DrawingZoom", DrawingZoom), ("DrawingDropFeedback", DrawingDropFeedback), ("DrawingScroll", DrawingScroll),
                ("DrawingCanvas", DrawingCanvas), ("InputActivity", InputActivity), ("DrawingHint", DrawingHint),
                ("SelectionEditor", SelectionEditor), ("AssignmentEditor", AssignmentEditor), ("AssignmentHint", AssignmentHint),
                ("TargetDevice", TargetDevice), ("InputPicker", InputPicker), ("ReverseAxisButton", ReverseAxisButton),
            }) RegisterName(name, control);
            _profiles = new(_context, () => _controllers!, resetName => Refresh(resetName), Run, ConfirmLeave, StatusText, ProfilePicker, NewProfileButton, _assignmentsView, this);
            _browser = new(_context, () => _controllers!, () => _editing!, () => _drawing!, Run, DevicePicker, _devicesView, _assignmentsView);
            _editing = new(_context, () => _controllers!, () => _browser!, resetName => Refresh(resetName), Run, StatusText, _assignmentsView, this);
            _drawing = new(_context, () => _editing!, resetName => Refresh(resetName), Run, DevicePicker, StatusText, _devicesView, _assignmentsView, this, () => _controllers!);
            _controllers = new(_context, () => _editing!, resetName => Refresh(resetName), Run, DevicePicker, StatusText, ControllerSummary, FeaturePicker, _devicesView, _assignmentsView, this, reviewChange, identificationFactory, inputFactory, _drawing.Presenter);
            _drawing.Start();
            _controllers.Start();
            _assignmentsView.Initialize(_context, _editing.UpdateEditor, _editing.UpdateApplyAvailability, _drawing.RenderDrawing,
                SaveWorkspaceAsync, () => DiscardClicked(this, new()), message => StatusText.Text = message);
            ConfigureWorkspace(FeatureContent, FeatureActions,
                [new("devices", () => _devicesView), new("assignments", () => _assignmentsView)]);
            ConfigureWorkspaceCommands(SaveButton, DiscardButton, SaveState);
            RegisterWorkspaceCleanup(Dispose);
            RescanControllerButton.Click += _controllers.RescanClicked;
            IdentifyControllerButton.Click += _controllers.IdentifyClicked;
            CancelIdentificationButton.Click += _controllers.StopIdentificationClicked;
            LinkControllerButton.Click += _controllers.LinkClicked;
            AddControllerButton.Click += _controllers.AddControllerClicked;
            RenameControllerButton.Click += _controllers.RenameControllerClicked;
            NativeControllerName.TextChanged += (_, _) => { if (!_context.Refreshing) _controllers.UpdateControllerAvailability(); };
            UnlinkControllerButton.Click += _controllers.UnlinkClicked;
            ClearSlotButton.Click += _controllers.ClearSlotClicked;
            RemoveUnusedSlotButton.Click += _controllers.RemoveUnusedSlotClicked;
            _devicesView.ChooseDrawingButton.Click += (_, _) => ControllerLinkPresentation.OpenDrawingPicker(DrawingPicker);
            ClearDrawingButton.Click += _drawing.ClearDrawingClicked;
            ControllerSetup.Collapsed += _controllers.SetupCollapsed;
            ConnectedPicker.SelectionChanged += _controllers.ConnectedChanged;
            DrawingPicker.SelectionChanged += _drawing.DrawingChanged;
            FitDrawingButton.Click += _drawing.FitDrawingClicked;
            ApplyButton.Click += _editing.ApplyClicked;
            ReverseAxisButton.Click += _editing.ReverseAxisClicked;
            ShowCommandButton.Click += _browser.ShowCommandClicked;
            SectionPicker.SelectionChanged += _browser.FilterChanged;
            TypePicker.SelectionChanged += _browser.FilterChanged;
            Search.TextChanged += _browser.SearchChanged;
            ReviewCaptured.Checked += _browser.CaptureReviewChanged;
            ReviewCaptured.Unchecked += _browser.CaptureReviewChanged;
            BindingsGrid.SelectionChanged += _browser.BindingChanged;
            BindingsGrid.PreviewMouseRightButtonDown += _browser.RecordRightClicked;
            BindingsGrid.ContextMenuOpening += _browser.RecordContextOpening;
            EditNativeRecordItem.Click += _browser.EditSelectedClicked;
            ClearNativeRecordItem.Click += _editing.ClearSelectedClicked;
            ApplyNativeRecordItem.Click += _editing.ApplyNativeRecordClicked;
            DrawingPanel.Expanded += _drawing.DrawingExpanded;
            TargetDevice.SelectionChanged += _editing.TargetChanged;
            InputPicker.SelectionChanged += _editing.InputChanged;
            _reordering = new(DevicePicker, (source, target, after) =>
            {
                if (!source.StartsWith("squadrons.joystick.", StringComparison.Ordinal) ||
                    !target.StartsWith("squadrons.joystick.", StringComparison.Ordinal)) return;
                Run(() => _controllers.ReviewNativeChange(_context.Session!.PreviewMoveCapturedSlot(source, target, after)));
            }, id => id.StartsWith("squadrons.joystick.", StringComparison.Ordinal));
            StartInputButton.Click += _controllers.StartInputClicked;
            StopInputButton.Click += _controllers.StopInputClicked;
            GamePicker.SelectedIndex = 2;
            SectionPicker.ItemsSource = new[] { "All", "Flight", "Menus & General", "Hangar & Briefing Room", "Other" }; SectionPicker.SelectedIndex = 0;
            TypePicker.ItemsSource = new[] { "All inputs", "Supported physical inputs", "Axes", "Buttons and hats", "Preserved inputs" }; TypePicker.SelectedIndex = 0;
            CommandBrowser.Configure(
                [new("All types", null), new("Axes", CommandType.Axis), new("Buttons and hats", CommandType.Button), new("Unknown / unbound", CommandType.Other)],
                [new("All", GameCommandAssignmentStatus.All), new("Assigned to selected controller", GameCommandAssignmentStatus.AssignedToController), new("Not assigned to selected controller", GameCommandAssignmentStatus.NotAssignedToController), new("Assigned anywhere", GameCommandAssignmentStatus.AssignedAnywhere), new("Unassigned everywhere", GameCommandAssignmentStatus.UnassignedEverywhere)]);
            CommandContextFilter.SelectionChanged += (_, _) => { if (!_context.Refreshing) SyncContextSelection(); };
            CommandContextFilter.SelectionChanged += _browser.CommandFiltersChanged;
            CommandInputFilter.SelectionChanged += _browser.CommandFiltersChanged;
            CommandStatusFilter.SelectionChanged += _browser.CommandFiltersChanged;
            CommandSearch.TextChanged += _browser.CommandSearchChanged;
            CommandBrowser.ClearCommandFiltersButton.Click += _browser.ResetCommandFiltersClicked;
            CommandPicker.SelectionChanged += _browser.CommandChanged;
            _commandDrag = new(CommandPicker, SquadronsWorkspaceContext.CommandDragFormat,
                () => !WorkspaceActionRunning && _context.Session?.AccessMessage.Length == 0 && !_context.ScanningInputs && !_controllers.Identification.Running && !_controllers.Input.Running,
                command => DrawingDropFeedback.Text = "Drag " + command.DisplayName + " onto the outlined hardware control.",
                effect =>
                {
                    if (effect == DragDropEffects.None && !DrawingDropFeedback.Text.StartsWith("Drop onto ", StringComparison.Ordinal))
                        DrawingDropFeedback.Text = "Drop canceled or outside a supported callout. Aim for an outlined input on the drawing.";
                });
            CommandContextFilter.ItemsSource = new[] { "All", "Flight", "Menus & General", "Hangar & Briefing Room", "Other" };
            CommandContextFilter.SelectedIndex = CommandInputFilter.SelectedIndex = CommandStatusFilter.SelectedIndex = 0;
            Loaded += async (_, _) =>
            {
                await _controllers.RefreshControllerInputsAsync();
                try
                {
                    _context.Drawings = drawings ?? await GameWorkspaceServices.HardwareAsync();
                    if (!_disposed)
                    {
                        _context.Session?.SetDrawingCatalog(_context.Drawings.Select(x => x.DeviceId).ToArray());
                        Run(_drawing.RenderDrawing);
                    }
                }
                catch (Exception error) { if (!_disposed) DrawingHint.Text = "Shared drawing catalog unavailable: " + error.Message; }
            };

            _context.Refreshing = false;
            _profiles.ReloadChoices();
            if (initial is { } id) Run(() => _profiles.LoadProfile(id));
            else Run(() => Refresh());
            Run(_controllers.RescanControllers);
            FeaturePicker.SelectedIndex = 0;
        }
        /// <summary>Supplies the shared name prompt in unattended workspace smoke tests.</summary>
        internal Func<string, string, string?>? ConfigurationNamePicker { get => _profiles.ConfigurationNamePicker; set => _profiles.ConfigurationNamePicker = value; }
        /// <summary>Gets the requested game after the common leave guard accepts the switch.</summary>
        public string? RequestedGame { get; private set; }
        internal GameProfileSession? Session => _context.Session;
        internal WorkspaceAssignmentWorkflow AssignmentWorkflow => _assignmentsView.AssignmentWorkflow;
        internal GameAssignmentSelection AssignmentSelection => _assignmentsView.AssignmentSelection;
        internal void SampleIdentification() => _controllers.Identification.Sample();
        internal void SampleInput() => _controllers.Input.Sample();
        internal Task RefreshControllerInputsAsync() => _controllers.RefreshControllerInputsAsync();
        internal HardwareDiagramInteractions DrawingInteractions() => _editing.DrawingInteractions();
        internal void ImportFile(string path) => _profiles.ImportFile(path);
        internal void ImportTemplatesFile(string path) => _profiles.ImportTemplatesFile(path);
        internal bool NewProfile(string name) => _profiles.NewProfile(name);
        internal string ExportReview(string parent) => _profiles.ExportReview(parent);
        /// <summary>Saves through the inherited action lifetime; failures retain the draft and native name edit.</summary>
        internal bool SaveProfile()
        {
            try { SaveWorkspaceAsync().GetAwaiter().GetResult(); return true; }
            catch (Exception error) { ReportWorkspaceError(error); return false; }
        }
        /// <summary>Releases native input resources, drag subscriptions and the shared mounted feature.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ClearWorkspaceFeatures(); _commandDrag.Dispose(); _controllers.Dispose(); _reordering.Dispose();
            _context.Dispose();
            GC.SuppressFinalize(this);
        }
        #endregion END Public
        #region BEGIN private helpers
        private void SyncContextSelection()
        {
            if (WorkspaceActionRunning) return;
            _context.AssignmentSource = SelectedAssignmentSource;
            _context.DrawingSection = DrawingSection;
            _context.Select(_context.State.Selection with { ControllerId = SelectedAssignmentSource });
        }

        protected override bool HasWorkspaceChanges => _context.Chrome.Pending;
        protected override bool CanSaveWorkspace => _context.Chrome.CanSave;
        protected override string WorkspaceStateSummary => _context.Chrome.Status;
        protected override Task SaveWorkspaceCoreAsync(IWorkspaceOperationLease lease)
        {
            _profiles.SaveCore(lease);
            return Task.CompletedTask;
        }
        protected override void DiscardWorkspaceCore(IWorkspaceOperationLease lease)
        {
            _controllers.StopLiveInput();
            _context.Session?.Discard(lease);
            if (_context.Revision is null) _context.Session = null;
            Refresh(); StatusText.Text = "Draft changes discarded.";
            SelectionEditor.AssignmentDraftStatus.Text = DrawingDropFeedback.Text = "";
        }
        protected override void ReportWorkspaceError(Exception exception) => StatusText.Text = "Unable to save: " + exception.Message;
        private bool ConfirmLeave()
        {
            _controllers.StopLiveInput();
            return ConfirmWorkspaceLeaveAsync(() =>
                (_pendingChoice?.Invoke() ?? ThemeMessageBox.Show(this, "Save changes to this Squadrons profile?", "Unsaved changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question)) switch
                {
                    MessageBoxResult.Yes => GameWorkspaceLeaveDecision.Save,
                    MessageBoxResult.No => GameWorkspaceLeaveDecision.Discard,
                    _ => GameWorkspaceLeaveDecision.Cancel
                }).GetAwaiter().GetResult();
        }
        protected override bool CanCloseWorkspace() => !WorkspaceActionRunning && (_allowClose || ConfirmLeave());
        private void FeatureChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_controllers is null) return;
            var key = FeaturePicker.SelectedIndex == 1 ? "assignments" : "devices";
            if (key != CurrentFeatureKey) _controllers.StopLiveInput();
            NavigateToFeature(key);
            Run(() => { _drawing.RenderDrawing(); UpdateAvailability(); });
        }
        private void Run(Action action)
        {
            try { if (WorkspaceActionRunning) throw new InvalidOperationException("Another workspace operation is still running."); action(); }
            catch (Exception error) { StatusText.Text = error.Message; }
        }
        private string? SelectedController => DevicePicker.SelectedItem is GameDeviceChoice choice &&
            choice.Id.StartsWith("squadrons.joystick.", StringComparison.Ordinal) ? choice.Id : null;
        private string? SelectedAssignmentSource => (DevicePicker.SelectedItem as GameDeviceChoice)?.Id;
        private string DrawingSection => CommandContextFilter.SelectedItem as string ?? "All";
        private void ImportTemplatesClicked(object sender, RoutedEventArgs e) => _profiles.ImportTemplatesClicked(sender, e);
        private void ImportClicked(object sender, RoutedEventArgs e) => _profiles.ImportClicked(sender, e);
        private void ExportClicked(object sender, RoutedEventArgs e) => _profiles.ExportClicked(sender, e);
        private void NewProfileClicked(object sender, RoutedEventArgs e) => _profiles.NewProfileClicked(sender, e);
        private void FilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_context.Refreshing && !WorkspaceActionRunning) SyncContextSelection();
            _browser.FilterChanged(sender, e);
        }
        private void Refresh(bool resetName = true)
        {
            if (WorkspaceActionRunning) return;
            var selected = (DevicePicker.SelectedItem as GameDeviceChoice)?.Id;
            var target = (TargetDevice.SelectedItem as GameDeviceChoice)?.Id;
            _context.Refreshing = true;
            var snapshot = _context.Session?.Snapshot;
            // The context retains pending names on failure and restores the saved name on discard.
            if (resetName) ProfileName.Text = _context.State.EditedName;
            var devices = snapshot is null ? [] : SquadronsWorkspaceProjection.OrderedDevices(snapshot.Profile).Select(x => new GameDeviceChoice(x.LogicalId, "Slot " + x.Identity!.SimSpecificIds["squadrons.slot"] + " · " + x.Identity.WindowsName)).ToArray();
            var systemDevices = new[]
            {
                new GameDeviceChoice("system.0", "Mouse · read-only"),
                new GameDeviceChoice("system.2", "Keyboard · read-only"),
                new GameDeviceChoice("system.1", "Gamepad · read-only")
            };
            DevicePicker.ItemsSource = new[] { new GameDeviceChoice("all", "All devices and inputs") }
                .Concat(devices).Concat(snapshot is null ? [] : systemDevices).ToArray();
            DevicePicker.SelectedItem = DevicePicker.Items.Cast<GameDeviceChoice>().FirstOrDefault(x => x.Id == selected) ?? DevicePicker.Items[0];
            _browser.RefreshCommandCatalog(snapshot);
            if (snapshot is null) { _context.FocusedConcept = null; _context.InspectedControl = null; }
            TargetDevice.ItemsSource = devices;
            TargetDevice.SelectedItem = devices.FirstOrDefault(x => x.Id == target);
            var verified = new Dictionary<string, IReadOnlyCollection<string>>();
            if (snapshot is not null)
                foreach (var (id, pin) in snapshot.Hardware)
                    if (GameControllerInputs.VerifiedKeys(pin, _context.ControllerInputs) is { } keys) verified[id] = keys;
            _context.Rows = snapshot is null ? [] : SquadronsWorkspaceProjection.ReviewInputs(SquadronsWorkspaceProjection.Rows(snapshot.Profile), verified);
            _context.Refreshing = false; SyncContextSelection(); _controllers.RefreshNativeControllerName(); _browser.RefreshRows(); UpdateAvailability(); _controllers.UpdateControllerAvailability(); _drawing.RenderDrawing(); _context.NotifyChanged();
        }

        private void UpdateAvailability()
        {
            if (_context.Refreshing || SaveState is null) return;
            RefreshWorkspaceState();
        }
        protected override void OnWorkspaceStateChanged()
        {
            if (_context.Refreshing || _controllers is null) return;
            _controllers.UpdateControllerAvailability();
            ExportButton.IsEnabled = !WorkspaceActionRunning && _context.Chrome.CanExport;
            ImportButton.IsEnabled = !WorkspaceActionRunning;
            ImportTemplatesButton.IsEnabled = !WorkspaceActionRunning && _context.Session?.AccessMessage.Length == 0;
            NewProfileButton.IsEnabled = !WorkspaceActionRunning && _context.Session?.AccessMessage.Length == 0 && !_context.ScanningInputs;
            ProfileName.IsEnabled = !WorkspaceActionRunning && _context.Session is not null;
            _editing.UpdateApplyAvailability();
        }

        private void ProfileChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_context.Refreshing || ProfilePicker.SelectedItem is not GameWorkspaceEntry entry || entry.Id == _context.Session?.Snapshot.Id) return;
            if (ConfirmLeave()) Run(() => _profiles.LoadProfile(entry.Id));
            _context.Refreshing = true; ProfilePicker.SelectedItem = ProfilePicker.Items.Cast<GameWorkspaceEntry>().FirstOrDefault(x => x.Id == _context.Session?.Snapshot.Id); _context.Refreshing = false;
        }

        private void GameChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_context.Refreshing || GamePicker.SelectedItem is not ComboBoxItem { Tag: string game } || game == "star-wars-squadrons") return;
            if (!ConfirmLeave()) { _context.Refreshing = true; GamePicker.SelectedIndex = 2; _context.Refreshing = false; return; }
            RequestedGame = game; _allowClose = true; Close();
        }

        private void NameChanged(object sender, TextChangedEventArgs e) { if (!_context.Refreshing && !WorkspaceActionRunning) { _context.SetEditedName(ProfileName.Text); UpdateAvailability(); } }
        private ComboBox CommandContextFilter => CommandBrowser.CommandCategoryPicker;
        private ComboBox CommandInputFilter => CommandBrowser.CommandTypePicker;
        private ComboBox CommandStatusFilter => CommandBrowser.AssignmentStatusPicker;
        private TextBox CommandSearch => CommandBrowser.CommandSearch;
        private ListBox CommandPicker => CommandBrowser.CommandsList;
        private TextBlock CommandFilterCount => CommandBrowser.CommandCount;
        private Button RescanControllerButton => _devicesView.RescanControllerButton;
        private Button IdentifyControllerButton => _devicesView.IdentifyControllerButton;
        private Button CancelIdentificationButton => _devicesView.CancelIdentificationButton;
        private Button RenameControllerButton => _devicesView.RenameControllerButton;
        private Button AddControllerButton => _devicesView.AddControllerButton;
        private TextBox NativeControllerName => _devicesView.NativeControllerName;
        private Button LinkControllerButton => _devicesView.LinkControllerButton;
        private Button UnlinkControllerButton => _devicesView.UnlinkControllerButton;
        private Button ClearSlotButton => _devicesView.ClearSlotButton;
        private Button RemoveUnusedSlotButton => _devicesView.RemoveUnusedSlotButton;
        private Button ClearDrawingButton => _devicesView.ClearDrawingButton;
        private Expander ControllerSetup => _devicesView.ControllerSetup;
        private TextBlock LinkStatus => _devicesView.LinkStatus;
        private ComboBox ConnectedPicker => _devicesView.ConnectedPicker;
        private TextBlock IdentificationStatus => _devicesView.IdentificationStatus;
        private ComboBox DrawingPicker => _devicesView.DrawingPicker;
        private Button FitDrawingButton => _assignmentsView.FitDrawingButton;
        private Button ApplyButton => _assignmentsView.ApplyButton;
        private Button StartInputButton => _assignmentsView.StartInputButton;
        private Button StopInputButton => _assignmentsView.StopInputButton;
        private Button ShowCommandButton => _assignmentsView.ShowCommandButton;
        private TextBlock CommandHint => _assignmentsView.CommandHint;
        private ComboBox SectionPicker => _assignmentsView.SectionPicker;
        private ComboBox TypePicker => _assignmentsView.TypePicker;
        private TextBox Search => _assignmentsView.Search;
        private CheckBox ReviewCaptured => _assignmentsView.ReviewCaptured;
        private TextBlock CountText => _assignmentsView.CountText;
        private GameCommandBrowserView CommandBrowser => _assignmentsView.CommandBrowser;
        private Expander CapturedAssignments => _assignmentsView.CapturedAssignments;
        private DataGrid BindingsGrid => _assignmentsView.BindingsGrid;
        private MenuItem EditNativeRecordItem => _assignmentsView.EditNativeRecordItem;
        private MenuItem ClearNativeRecordItem => _assignmentsView.ClearNativeRecordItem;
        private MenuItem ApplyNativeRecordItem => _assignmentsView.ApplyNativeRecordItem;
        private DataGridTextColumn ContextColumn => _assignmentsView.ContextColumn;
        private DataGridTextColumn HardwareColumn => _assignmentsView.HardwareColumn;
        private DataGridTextColumn ControllerColumn => _assignmentsView.ControllerColumn;
        private DataGridTextColumn BindingColumn => _assignmentsView.BindingColumn;
        private TextBlock EmptyState => _assignmentsView.EmptyState;
        private Expander DrawingPanel => _assignmentsView.DrawingPanel;
        private Slider DrawingZoom => _assignmentsView.DrawingZoom;
        private TextBlock DrawingDropFeedback => _assignmentsView.DrawingDropFeedback;
        private ScrollViewer DrawingScroll => _assignmentsView.DrawingScroll;
        private Canvas DrawingCanvas => _assignmentsView.DrawingCanvas;
        private TextBox InputActivity => _assignmentsView.InputActivity;
        private TextBlock DrawingHint => _assignmentsView.DrawingHint;
        private Button ReverseAxisButton => _assignmentsView.ReverseAxisButton;
        private GameAssignmentEditorView SelectionEditor => _assignmentsView.SelectionEditor;
        private StackPanel AssignmentEditor => _assignmentsView.AssignmentEditor;
        private TextBlock AssignmentHint => _assignmentsView.AssignmentHint;
        private ComboBox TargetDevice => _assignmentsView.TargetDevice;
        private ComboBox InputPicker => _assignmentsView.InputPicker;
        #endregion END private helpers
    }
}
