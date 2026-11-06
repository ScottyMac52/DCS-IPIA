using System.IO;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;
using Heimdall.Infrastructure;
using Heimdall.WarThunder;
using Microsoft.Win32;

namespace Heimdall.App.Games.WarThunder
{
    /// <summary>Assembles native War Thunder components over shared workspace and assignment algorithms.</summary>
    public sealed partial class WarThunderWorkspaceWindow : GameWorkspaceWindow, IDisposable
    {
        private readonly WarThunderWorkspaceContext _context;
        private readonly WarThunderDevicesView _devices = new();
        private readonly WarThunderAssignmentsView _assignments = new();
        private readonly WarThunderReferenceView _references = new();
        private readonly WarThunderControllerPresenter _controllers;
        private readonly WarThunderAssignmentBrowser _browser;
        private readonly WarThunderAssignmentEditing _editing;
        private readonly WarThunderAssignmentDrawing _drawing;
        private readonly WarThunderProfileOperations _profiles;
        private readonly GameCommandDragSource _commandDrag;
        private readonly Func<MessageBoxResult>? _pendingChoice;
        private bool _allowClose;
        private bool _disposed;
        #region BEGIN Public
        /// <summary>Assembles a native workspace without reading or writing active game settings automatically.</summary>
        public WarThunderWorkspaceWindow(GameWorkspaceServices services, Guid? initial = null,
            Func<MessageBoxResult>? pendingChoice = null, Func<GameControllerChange, bool>? reviewChange = null,
            IReadOnlyList<WorkspaceHardware>? drawings = null, Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? inputFactory = null)
        {
            EditionAccess.Policy.RequireSimulator("war-thunder");
            _pendingChoice = pendingChoice;
            _context = new(services, () => SelectedDevice, () => ProfileName.Text, () => WorkspaceActionRunning);
            _context.Selection = _assignments.AssignmentSelection;
            InitializeComponent(); AppearanceManager.Attach(this);
            _profiles = new(_context, Refresh, ConfirmLeave, Status, ProfileName, Profiles, this, SessionChanged, () => _controllers!, RefreshChrome);
            _browser = new(_context, () => _editing!, _assignments);
            _editing = new(_context, RefreshChrome, Run, Status, _assignments, this, () => _controllers!, () => _drawing!);
            _drawing = new(_context, () => _controllers!, () => _editing!, _devices, _assignments, this);
            _controllers = new(_context, ReportError, Controllers, _devices, this, reviewChange, _drawing.Presenter, inputFactory, InputStatus, _assignments, RefreshChrome);
            _drawing.Start(); _controllers.Start();
            _context.Testing = () => _controllers.Input.Running;
            _assignments.Initialize(_context, _editing.RefreshEditor, _drawing.ShowDrawing,
                SaveWorkspaceAsync, () => DiscardClicked(this, new()), message => Status.Text = message);
            ConfigureWorkspace(FeatureContent, FeatureActions,
                [new("devices", () => _devices), new("assignments", () => _assignments), new("references", () => _references)]);
            ConfigureWorkspaceCommands(Save, Discard, SavedState);
            RegisterWorkspaceCleanup(Dispose);
            WireDevices(); WireAssignments();
            _commandDrag = new(_assignments.Browser.CommandsList, "heimdall.war-thunder.command",
                () => !WorkspaceActionRunning && !_context.ScanningHardware && _context.Session?.AccessMessage.Length == 0 && !_controllers.Input.Running && _context.PhysicalMap.Inputs.Count > 0);
            FeaturePicker.SelectionChanged += (_, _) =>
            {
                if (_context.Refreshing) return;
                var key = FeaturePicker.SelectedIndex switch { 1 => "assignments", 2 => "references", _ => "devices" };
                if (CurrentFeatureKey != key) _controllers.Input.Stop();
                NavigateToFeature(key);
                if (key == "assignments") _drawing.ShowDrawing();
            };
            Import.Click += (_, _) => Run(() => { var picker = new OpenFileDialog { Filter = "War Thunder controls (*.blk)|*.blk" }; if (picker.ShowDialog(this) == true) ImportFile(picker.FileName); });
            Save.Click += SaveClicked;
            Discard.Click += DiscardClicked;
            Export.Click += (_, _) => Run(() => { var picker = new OpenFolderDialog { Title = "Choose a folder for a separate War Thunder review copy" }; if (picker.ShowDialog(this) == true) Status.Text = "Review copy: " + ExportReview(picker.FolderName); });
            Duplicate.Click += (_, _) => Run(_profiles.DuplicateCurrent);
            ProfileName.TextChanged += (_, _) => { if (!_context.Refreshing) RefreshChrome(); };
            Profiles.SelectionChanged += (_, _) =>
            {
                if (_context.Refreshing) return;
                var selected = Profiles.SelectedItem as GameWorkspaceEntry;
                if (selected is not null) Run(() => { if (ConfirmLeave()) LoadWorkspace(selected.Id); else _profiles.RefreshEntries(); });
            };
            GamePicker.SelectedIndex = 3;
            GamePicker.SelectionChanged += (_, _) =>
            {
                if (_context.Refreshing || GamePicker.SelectedIndex == 3) return;
                if (ConfirmLeave()) { RequestedGame = (GamePicker.SelectedItem as ComboBoxItem)?.Tag as string; _allowClose = true; Close(); }
                else { _context.Refreshing = true; GamePicker.SelectedIndex = 3; _context.Refreshing = false; }
            };
            Loaded += async (_, _) =>
            {
                try
                {
                    _context.Drawings = drawings ?? await GameWorkspaceServices.HardwareAsync();
                    if (_disposed) return;
                    await _controllers.RefreshHardwareAsync();
                    if (_disposed) return;
                    if (_context.Session is null && initial is { } id) LoadWorkspace(id); else { _profiles.RefreshEntries(); Refresh(); }
                    FeaturePicker.SelectedIndex = 0; NavigateToFeature("devices");
                }
                catch (Exception error) { ReportError(error); }
            };
            _controllers.Input.StateChanged += InputStateChanged;
            _context.Refreshing = false;
            RefreshChrome();
        }
        /// <summary>Gets an explicitly requested cross-game navigation target.</summary>
        public string? RequestedGame { get; private set; }
        internal GameProfileSession? Session => _context.Session;
        internal WarThunderAssignmentsView AssignmentView => _assignments;
        internal WarThunderDevicesView DeviceView => _devices;
        internal WorkspaceAssignmentWorkflow AssignmentWorkflow => _assignments.AssignmentWorkflow;
        internal GameAssignmentSelection AssignmentSelection => _assignments.AssignmentSelection;
        internal Action<AxisTuningWindow>? AxisTuningOpened { get => _editing.AxisTuningOpened; set => _editing.AxisTuningOpened = value; }
        internal void SampleInput() => _controllers.Input.Sample();
        internal void DropPhysicalCommand(HardwareGraphicControl control, string commandId) => _editing.DropPhysicalCommand(control, commandId);
        internal void ImportFile(string path) => _profiles.ImportFile(path);
        internal void LoadWorkspace(Guid id) => _profiles.LoadWorkspace(id);
        internal string ExportReview(string parent) => _profiles.ExportReview(parent);
        internal bool SaveCurrent()
        {
            try { SaveWorkspaceAsync().GetAwaiter().GetResult(); return true; }
            catch (Exception error) { ReportWorkspaceError(error); return false; }
        }
        /// <summary>Releases shared mounted actions, native input handles and session subscriptions.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = _context.Disposed = true;
            _controllers.Input.StateChanged -= InputStateChanged;
            _commandDrag.Dispose(); _controllers.Dispose(); ClearWorkspaceFeatures();
            if (_context.Session is not null) _context.Session.Changed -= SessionChanged;
            GC.SuppressFinalize(this);
        }
        #endregion END Public
        #region BEGIN private helpers
        protected override bool HasWorkspaceChanges => _context.Chrome.Pending;
        protected override bool CanSaveWorkspace => _context.Session?.AccessMessage.Length == 0 && !_context.Testing() && !_context.ScanningHardware && _context.Chrome.CanSave;
        protected override string WorkspaceStateSummary => _context.Chrome.Status;
        protected override Task SaveWorkspaceCoreAsync() { _profiles.SaveCore(); return Task.CompletedTask; }
        protected override void DiscardWorkspaceCore()
        {
            _controllers.Input.Stop(); _context.Session?.Discard();
            if (_context.Session is not null) ProfileName.Text = _context.Session.Snapshot.Name;
            Refresh();
        }
        protected override void ReportWorkspaceError(Exception exception) => ReportError(exception, "Save failed");
        protected override bool CanCloseWorkspace() => !WorkspaceActionRunning && (_allowClose || ConfirmLeave());
        private bool ConfirmLeave()
        {
            _controllers.Input.Stop();
            return ConfirmWorkspaceLeaveAsync(() =>
                (_pendingChoice?.Invoke() ?? ThemeMessageBox.Show(this, "Save changes to this War Thunder profile?", "Unsaved changes", MessageBoxButton.YesNoCancel)) switch
                { MessageBoxResult.Yes => GameWorkspaceLeaveDecision.Save, MessageBoxResult.No => GameWorkspaceLeaveDecision.Discard, _ => GameWorkspaceLeaveDecision.Cancel }).GetAwaiter().GetResult();
        }
        private string? SelectedDevice => (Controllers.SelectedItem as ListBoxItem)?.Tag as string;
        private void SessionChanged(object? sender, EventArgs e) => Refresh();
        private void InputStateChanged(object? sender, EventArgs e) { _controllers.RefreshDevices(); _editing.RefreshInputState(); }
        private void RefreshChrome() => RefreshWorkspaceState();
        protected override void OnWorkspaceStateChanged()
        {
            if (_context.Refreshing || _controllers is null) return;
            var editable = _context.Session?.AccessMessage.Length == 0 && !WorkspaceActionRunning && !_context.Testing() && !_context.ScanningHardware;
            Export.IsEnabled = editable && _context.Chrome.CanExport; Duplicate.IsEnabled = editable; ProfileName.IsEnabled = editable;
            Import.IsEnabled = !WorkspaceActionRunning && !_context.ScanningHardware;
            _controllers.RefreshDevices(); _editing.RefreshEditor();
        }
        private void Run(Action action)
        {
            try { if (WorkspaceActionRunning || _context.ScanningHardware) throw new InvalidOperationException("Another workspace operation is still running. Finish controller scanning before editing."); action(); }
            catch (Exception error) { ReportError(error); }
        }
        private void WireDevices()
        {
            Controllers.SelectionChanged += (_, _) =>
            {
                if (_context.Refreshing) return;
                _controllers.Input.Stop(); _context.SelectedPhysicalInput = null; _context.Selection.SelectInput(null); _controllers.RefreshDevices(); _browser.RefreshCommands(); _drawing.ShowDrawing();
            };
            _devices.Rescan.Click += async (_, _) => { await _controllers.RefreshHardwareAsync(); _controllers.RefreshDevices(); _drawing.ShowDrawing(); };
            _devices.Add.Click += (_, _) => Run(_controllers.AddController);
            _devices.Link.Click += (_, _) => Run(() =>
            {
                if (_context.Session is not null && SelectedDevice is { } id && _devices.Physical.SelectedItem is DetectedDcsDevice physical)
                    _controllers.Review(_context.Session.PreviewCapturedDeviceLink(id, physical.PhysicalGuid));
            });
            _devices.Remove.Click += (_, _) => Run(() => { if (_context.Session is not null && SelectedDevice is { } id) _controllers.Review(_context.Session.PreviewRemoveController(id)); });
            _devices.Unlink.Click += (_, _) => Run(() => { if (_context.Session is not null && SelectedDevice is { } id) _controllers.Review(_context.Session.PreviewUnlinkController(id)); });
            _devices.ChooseDrawing.Click += (_, _) => ControllerLinkPresentation.OpenDrawingPicker(_devices.Drawing);
            _devices.UnlinkDrawing.Click += (_, _) => Run(() => { _controllers.Input.Stop(); if (_context.Session is not null && SelectedDevice is { } id) _context.Session.ChangeDrawing(id, null); });
            _devices.Drawing.SelectionChanged += (_, _) =>
            {
                if (_context.Refreshing || _context.Session is null || SelectedDevice is not { } id || _devices.Drawing.SelectedItem is not WorkspaceHardware hardware) return;
                Run(() => { _controllers.Input.Stop(); _context.Session.ChangeDrawing(id, hardware.DeviceId); });
            };
            _devices.Physical.SelectionChanged += (_, _) => { if (!_context.Refreshing)
                {
                    _devices.Link.IsEnabled = SelectedDevice is not null && _devices.Physical.IsEnabled && _devices.Physical.SelectedItem is DetectedDcsDevice;
                    _controllers.UpdateAddController();
                } };
        }

        private void WireAssignments()
        {
            _assignments.Editor.CaptureContext = () => _assignments.ActionContext;
            var browser = _assignments.Browser;
            _assignments.ShowAssignments.Click += (_, _) => { browser.CommandsList.SelectedItem = null; _assignments.CapturedAssignments.IsExpanded = true; _browser.RefreshRecords(); };
            browser.Configure([new("All", null), new("Buttons", CommandType.Button), new("Axes", CommandType.Axis)],
                [new("All", GameCommandAssignmentStatus.All), new("Assigned to selected mapping", GameCommandAssignmentStatus.AssignedToController),
                 new("Not assigned to selected mapping", GameCommandAssignmentStatus.NotAssignedToController),
                 new("Assigned anywhere", GameCommandAssignmentStatus.AssignedAnywhere), new("Unassigned everywhere", GameCommandAssignmentStatus.UnassignedEverywhere)]);
            browser.CommandCategoryPicker.ItemsSource = new[] { "All categories", "Native hotkeys", "Native axes" };
            browser.CommandCategoryPicker.SelectedIndex = 0; browser.CommandTypePicker.SelectedIndex = 0; browser.AssignmentStatusPicker.SelectedIndex = 0;
            browser.CommandSearch.TextChanged += (_, _) => { if (!_context.Refreshing) _browser.RefreshCommands(); };
            browser.CommandCategoryPicker.SelectionChanged += (_, _) => { if (!_context.Refreshing) _browser.RefreshCommands(); };
            browser.CommandTypePicker.SelectionChanged += (_, _) => { if (!_context.Refreshing) _browser.RefreshCommands(); };
            browser.AssignmentStatusPicker.SelectionChanged += (_, _) => { if (!_context.Refreshing) _browser.RefreshCommands(); };
            browser.ClearCommandFiltersButton.Click += (_, _) =>
            {
                _context.Refreshing = true; browser.CommandSearch.Clear(); browser.CommandCategoryPicker.SelectedIndex = 0;
                browser.CommandTypePicker.SelectedIndex = 0; browser.AssignmentStatusPicker.SelectedIndex = 0; _context.Refreshing = false; _browser.RefreshCommands();
            };
            browser.CommandsList.SelectionChanged += (_, _) => { if (!_context.Refreshing) { _context.SelectedPhysicalInput = null; _context.Selection.SelectCommand((browser.CommandsList.SelectedItem as SimCommand)?.Id); _browser.RefreshRecords(); } };
            _assignments.Records.SelectionChanged += (_, _) => { if (!_context.Refreshing) { _context.SelectedPhysicalInput = null; _context.Selection.SelectInput(null); _context.Selection.SelectAlternative((_assignments.Records.SelectedItem as WarThunderWorkspaceRow)?.RecordId); _editing.RefreshEditor(); } };
            _assignments.Editor.PhysicalApplyRequested += (_, _) => Run(() =>
            {
                var command = (_assignments.Browser.CommandsList.SelectedItem as SimCommand) ?? (_assignments.Records.SelectedItem as WarThunderWorkspaceRow)?.Command;
                if (command is not null && _context.SelectedPhysicalInput is not null) _editing.ApplyPhysicalAssignment(command.Id, _context.SelectedPhysicalInput);
            });
            _assignments.Editor.EditRequested += (_, edit) => Run(() => _editing.ApplyDrawingEdit(edit));
            _assignments.Start.Click += (_, _) => Run(() =>
            {
                var pin = SelectedDevice is { } id ? _context.Session?.Snapshot.Hardware.GetValueOrDefault(id) : null;
                if (pin is not null) _controllers.Input.Start(pin, _context.Observed);
            });
            _assignments.Stop.Click += (_, _) => _controllers.Input.Stop();
            _assignments.Fit.Click += (_, _) => _drawing.Viewport.Fit();
            var menu = new ContextMenu();
            var clear = new MenuItem { Header = "Clear assignment" };
            clear.Click += (_, _) => _assignments.Editor.Submit(WarThunderEditKind.Clear);
            _assignments.Editor.Tune.Click += (_, _) => _editing.TuneSelectedNativeAxis();
            var tune = new MenuItem { Header = "Tune axis…" };
            tune.Click += (_, _) => _editing.TuneSelectedNativeAxis();
            menu.Items.Add(clear); menu.Items.Add(tune); _assignments.Records.ContextMenu = menu;
            _assignments.Records.PreviewMouseRightButtonDown += (_, args) =>
            {
                var row = args.OriginalSource is DependencyObject origin ? ItemsControl.ContainerFromElement(_assignments.Records, origin) as DataGridRow : null;
                _context.SelectedPhysicalInput = null; _context.Selection.SelectInput(null);
                if (row is not null) row.IsSelected = true; else _assignments.Records.SelectedItem = null;
                _editing.RefreshEditor();
            };
            _assignments.Records.ContextMenuOpening += (_, args) =>
            {
                AssignmentActionPresentation.Show(clear, _assignments.Editor.ActionState(WorkspaceAssignmentAction.Clear));
                AssignmentActionPresentation.Show(tune, _assignments.Editor.ActionState(WorkspaceAssignmentAction.TuneAxis));
                if (clear.Visibility != Visibility.Visible && tune.Visibility != Visibility.Visible) args.Handled = true;
            };
        }

        private void Refresh()
        {
            _context.Refreshing = true;
            var previous = SelectedDevice;
            Controllers.Items.Clear(); Controllers.Items.Add(new ListBoxItem { Content = "All devices and native inputs" });
            if (_context.Session is not null)
                foreach (var device in WarThunderProfileBackend.ReadDevices(_context.Session.Snapshot.Profile))
                    Controllers.Items.Add(new ListBoxItem { Content = device.Name + " · " + device.Id, Tag = device.Id });
            Controllers.SelectedItem = Controllers.Items.Cast<ListBoxItem>().FirstOrDefault(item => item.Tag as string == previous) ?? Controllers.Items[0];
            _context.Rows = _context.Session is null ? [] : WarThunderWorkspaceProjection.Rows(_context.Session.Snapshot.Profile, _context.Session.Catalog.Commands);
            _references.Show(_context.Rows);
            _context.Refreshing = false; _controllers.RefreshDevices(); _browser.RefreshCommands(); RefreshChrome(); _drawing.ShowDrawing(); _context.NotifyChanged();
        }

        private void ReportError(Exception error, string operation = "Operation failed")
        {
            Status.Text = operation + ": " + error.Message;
            ErrorDetails.Text = operation + Environment.NewLine + error;
            ErrorPanel.Visibility = Visibility.Visible;
        }
        #endregion END private helpers
    }
}
