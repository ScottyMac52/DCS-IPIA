using System.ComponentModel;
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

namespace Heimdall.App.Games
{
    /// <summary>Hosts captured Squadrons profiles with guarded native editing, shared persistence and review-only export.</summary>
    public partial class SquadronsWorkspaceWindow : Window, IDisposable
    {
        private readonly SquadronsDevicesView _devicesView = new();
        private readonly SquadronsAssignmentsView _assignmentsView = new();
        private readonly GameWorkspaceFeatureHost _featureHost;
        private readonly HardwareInputFeedback _input;
        private readonly GameWorkspaceServices _services;
        private readonly SquadronsProfileBackend _backend = new();
        private readonly Func<MessageBoxResult>? _pendingChoice;
        private readonly Func<GameControllerChange, bool>? _reviewChange;
        private readonly GameControllerIdentification _identification;
        private readonly GameAssignmentSelection _selection = new();
        private readonly GameControllerReordering _reordering;
        private readonly HardwareDiagramPresenter _drawingPresenter;
        private readonly DiagramViewport _drawingViewport;
        private Point? _commandDragStart;
        private IReadOnlyList<WorkspaceHardware> _drawings = [];
        private GameProfileSession? _session;
        private string? _revision;
        private string? _sourceDirectory;
        private IReadOnlyList<SquadronsWorkspaceRow> _rows = [];
        private string? _focusedConcept;
        private HardwareGraphicControl? _inspectedControl;
        private IReadOnlyList<ControllerObservation> _controllerInputs = [];
        private int _inputScan;
        private bool _scanningInputs;
        private bool _disposed;
        private bool _refreshing = true;
        private bool _allowClose;
        /// <summary>Opens an explicitly chosen Squadrons workspace without importing active settings automatically.</summary>
        public SquadronsWorkspaceWindow(GameWorkspaceServices services, Guid? initial = null, Func<MessageBoxResult>? pendingChoice = null,
            Func<GameControllerChange, bool>? reviewChange = null, Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? identificationFactory = null,
            IReadOnlyList<WorkspaceHardware>? drawings = null,
            Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? inputFactory = null)
        {
            EditionAccess.Policy.RequireSimulator("star-wars-squadrons");
            _services = services; _pendingChoice = pendingChoice; _reviewChange = reviewChange;
            _drawings = drawings ?? [];
            InitializeComponent(); AppearanceManager.Attach(this);
            RegisterName("RescanControllerButton", RescanControllerButton);
            RegisterName("IdentifyControllerButton", IdentifyControllerButton);
            RegisterName("CancelIdentificationButton", CancelIdentificationButton);
            RegisterName("LinkControllerButton", LinkControllerButton);
            RegisterName("UnlinkControllerButton", UnlinkControllerButton);
            RegisterName("ClearSlotButton", ClearSlotButton);
            RegisterName("RemoveUnusedSlotButton", RemoveUnusedSlotButton);
            RegisterName("ClearDrawingButton", ClearDrawingButton);
            RegisterName("ControllerSetup", ControllerSetup);
            RegisterName("LinkStatus", LinkStatus);
            RegisterName("ConnectedPicker", ConnectedPicker);
            RegisterName("IdentificationStatus", IdentificationStatus);
            RegisterName("DrawingPicker", DrawingPicker);
            RegisterName("FitDrawingButton", FitDrawingButton);
            RegisterName("ApplyButton", ApplyButton);
            RegisterName("StartInputButton", StartInputButton);
            RegisterName("StopInputButton", StopInputButton);
            RegisterName("ShowCommandButton", ShowCommandButton);
            RegisterName("CommandHint", CommandHint);
            RegisterName("SectionPicker", SectionPicker);
            RegisterName("TypePicker", TypePicker);
            RegisterName("Search", Search);
            RegisterName("ReviewCaptured", ReviewCaptured);
            RegisterName("CountText", CountText);
            RegisterName("CommandBrowser", CommandBrowser);
            RegisterName("CapturedAssignments", CapturedAssignments);
            RegisterName("BindingsGrid", BindingsGrid);
            RegisterName("EditNativeRecordItem", EditNativeRecordItem);
            RegisterName("ClearNativeRecordItem", ClearNativeRecordItem);
            RegisterName("ContextColumn", ContextColumn);
            RegisterName("HardwareColumn", HardwareColumn);
            RegisterName("ControllerColumn", ControllerColumn);
            RegisterName("BindingColumn", BindingColumn);
            RegisterName("EmptyState", EmptyState);
            RegisterName("DrawingPanel", DrawingPanel);
            RegisterName("DrawingZoom", DrawingZoom);
            RegisterName("DrawingDropFeedback", DrawingDropFeedback);
            RegisterName("DrawingScroll", DrawingScroll);
            RegisterName("DrawingCanvas", DrawingCanvas);
            RegisterName("InputActivity", InputActivity);
            RegisterName("DrawingHint", DrawingHint);
            RegisterName("SelectionEditor", SelectionEditor);
            RegisterName("AssignmentEditor", AssignmentEditor);
            RegisterName("AssignmentHint", AssignmentHint);
            RegisterName("TargetDevice", TargetDevice);
            RegisterName("InputPicker", InputPicker);
            RescanControllerButton.Click += RescanClicked;
            IdentifyControllerButton.Click += IdentifyClicked;
            CancelIdentificationButton.Click += StopIdentificationClicked;
            LinkControllerButton.Click += LinkClicked;
            UnlinkControllerButton.Click += UnlinkClicked;
            ClearSlotButton.Click += ClearSlotClicked;
            RemoveUnusedSlotButton.Click += RemoveUnusedSlotClicked;
            ClearDrawingButton.Click += ClearDrawingClicked;
            ControllerSetup.Collapsed += SetupCollapsed;
            ConnectedPicker.SelectionChanged += ConnectedChanged;
            DrawingPicker.SelectionChanged += DrawingChanged;
            FitDrawingButton.Click += FitDrawingClicked;
            ApplyButton.Click += ApplyClicked;
            ShowCommandButton.Click += ShowCommandClicked;
            SectionPicker.SelectionChanged += FilterChanged;
            TypePicker.SelectionChanged += FilterChanged;
            Search.TextChanged += SearchChanged;
            ReviewCaptured.Checked += CaptureReviewChanged;
            ReviewCaptured.Unchecked += CaptureReviewChanged;
            BindingsGrid.SelectionChanged += BindingChanged;
            BindingsGrid.PreviewMouseRightButtonDown += RecordRightClicked;
            BindingsGrid.ContextMenuOpening += RecordContextOpening;
            EditNativeRecordItem.Click += EditSelectedClicked;
            ClearNativeRecordItem.Click += ClearSelectedClicked;
            DrawingPanel.Expanded += DrawingExpanded;
            DrawingPanel.Collapsed += DrawingCollapsed;
            DrawingPanel.SizeChanged += DrawingPanelSizeChanged;
            TargetDevice.SelectionChanged += TargetChanged;
            InputPicker.SelectionChanged += InputChanged;
            _featureHost = new(FeatureContent, FeatureActions, StopLiveInput);
            _drawingPresenter = new(DrawingCanvas);
            _drawingViewport = new(DrawingScroll, DrawingCanvas, DrawingZoom, "squadrons.diagram");
            _drawingViewport.EnablePanning();
            // Any drawing-area drop outside a verified callout must be explained.
            DrawingCanvas.AllowDrop = true;
            DrawingCanvas.DragOver += (_, e) =>
            {
                if (!e.Handled && e.Data.GetDataPresent(SquadronsCommandDragFormat))
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                }
            };
            DrawingCanvas.Drop += (_, e) =>
            {
                if (!e.Handled && e.Data.GetDataPresent(SquadronsCommandDragFormat))
                {
                    DrawingDropFeedback.Text = "Drop onto an outlined control callout, not the drawing background.";
                    StatusText.Text = DrawingDropFeedback.Text;
                    e.Handled = true;
                }
            };
            _reordering = new(DevicePicker, (source, target, after) =>
            {
                if (!source.StartsWith("squadrons.joystick.", StringComparison.Ordinal) ||
                    !target.StartsWith("squadrons.joystick.", StringComparison.Ordinal)) return;
                Run(() => ReviewNativeChange(_session!.PreviewMoveCapturedSlot(source, target, after)));
            }, id => id.StartsWith("squadrons.joystick.", StringComparison.Ordinal));
            _identification = new(this, ShowIdentification, ClearIdentification, message => StatusText.Text = message, identificationFactory);
            _input = new(this, _drawingPresenter, message => StatusText.Text = message, ShowInputActivity, inputFactory);
            _input.StateChanged += InputStateChanged;
            StartInputButton.Click += StartInputClicked;
            StopInputButton.Click += StopInputClicked;
            GamePicker.SelectedIndex = 2;
            SectionPicker.ItemsSource = new[] { "All", "Flight", "Menus & General", "Hangar & Briefing Room", "Other" }; SectionPicker.SelectedIndex = 1;
            TypePicker.ItemsSource = new[] { "All inputs", "Supported physical inputs", "Axes", "Buttons and hats", "Preserved inputs" }; TypePicker.SelectedIndex = 0;
            CommandBrowser.Configure(
                [new("All types", null), new("Axes", CommandType.Axis), new("Buttons and hats", CommandType.Button), new("Unknown / unbound", CommandType.Other)],
                [new("All", GameCommandAssignmentStatus.All), new("Assigned to selected controller", GameCommandAssignmentStatus.AssignedToController), new("Not assigned to selected controller", GameCommandAssignmentStatus.NotAssignedToController), new("Assigned anywhere", GameCommandAssignmentStatus.AssignedAnywhere), new("Unassigned everywhere", GameCommandAssignmentStatus.UnassignedEverywhere)]);
            CommandContextFilter.SelectionChanged += CommandFiltersChanged;
            CommandInputFilter.SelectionChanged += CommandFiltersChanged;
            CommandStatusFilter.SelectionChanged += CommandFiltersChanged;
            CommandSearch.TextChanged += CommandSearchChanged;
            CommandBrowser.ClearCommandFiltersButton.Click += ResetCommandFiltersClicked;
            CommandPicker.SelectionChanged += CommandChanged;
            CommandPicker.PreviewMouseLeftButtonDown += CommandListDragPressed;
            CommandPicker.PreviewMouseMove += DragCapturedCommand;
            CommandContextFilter.ItemsSource = new[] { "All", "Flight", "Menus & General", "Hangar & Briefing Room", "Other" };
            CommandContextFilter.SelectedIndex = CommandInputFilter.SelectedIndex = CommandStatusFilter.SelectedIndex = 0;
            Loaded += async (_, _) =>
            {
                await RefreshControllerInputsAsync();
                try
                {
                    _drawings = drawings ?? await GameWorkspaceServices.HardwareAsync();
                    if (!_disposed)
                    {
                        _session?.SetDrawingCatalog(_drawings.Select(x => x.DeviceId).ToArray());
                        Run(RenderDrawing);
                    }
                }
                catch (Exception error) { if (!_disposed) DrawingHint.Text = "Shared drawing catalog unavailable: " + error.Message; }
            };
            Closing += ConfirmClose;
            Closed += (_, _) => Dispose();
            _refreshing = false;
            ReloadChoices();
            if (initial is { } id) Run(() => LoadProfile(id));
            else Run(() => Refresh());
            Run(RescanControllers);
            FeaturePicker.SelectedIndex = 0;
        }
        #region BEGIN Public
        /// <summary>Releases identification handles and foreground lifecycle subscriptions.</summary>
        public void Dispose() { if (_disposed) return; _disposed = true; _inputScan++; _featureHost.Clear(); _input.StateChanged -= InputStateChanged; _input.Dispose(); _identification.Dispose(); _reordering.Dispose(); GC.SuppressFinalize(this); }
        /// <summary>Refreshes capability evidence asynchronously; failed, obsolete or closed-window scans cannot enable assignments.</summary>
        internal async Task RefreshControllerInputsAsync()
        {
            if (_disposed) return;
            StopLiveInput();
            var scan = ++_inputScan; _scanningInputs = true; _controllerInputs = [];
            try
            {
                Refresh(false); StatusText.Text = "Inspecting connected controller inputs…";
                var inputs = await _services.DiscoverInputsAsync();
                if (scan != _inputScan || _disposed) return;
                _controllerInputs = inputs;
                StatusText.Text = "Controller inputs inspected. Physical input support does not establish Squadrons slot identity.";
            }
            catch (Exception error) { if (scan == _inputScan && !_disposed) StatusText.Text = "Controller input verification unavailable: " + error.Message; }
            finally { if (scan == _inputScan && !_disposed) { _scanningInputs = false; Run(() => Refresh(false)); } }
        }
        /// <summary>Gets the game explicitly requested after the pending-change guard accepted leaving.</summary>
        public string? RequestedGame { get; private set; }
        /// <summary>Gets the isolated editing boundary for deterministic Windows smoke checks.</summary>
        internal GameProfileSession? Session => _session;
        /// <summary>Samples the shared identification boundary deterministically in Windows smoke tests.</summary>
        internal void SampleIdentification() => _identification.Sample();
        /// <summary>Samples shared foreground hardware testing in deterministic Windows smoke checks.</summary>
        internal void SampleInput() => _input.Sample();
        /// <summary>Exposes the same typed native commands installed on the shared presenter for deterministic smoke checks.</summary>
        internal HardwareDiagramInteractions DrawingInteractions() =>
            new(SelectDrawingInput, DropDrawingCommand, CanClearDrawingInput, ClearDrawingInput,
                DragFormat: SquadronsCommandDragFormat, ClearLabel: "Clear assignments…", Inspect: InspectDrawingInput);
        /// <summary>Imports one selected native file with bounded input and preserves the current session on failure.</summary>
        internal void ImportFile(string path)
        {
            if (!ConfirmLeave()) return;
            StopLiveInput();
            if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("Squadrons controls exceed the 4 MiB import limit.");
            var profile = _backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = File.ReadAllText(path) });
            var name = "Squadrons Controls"; var number = 2; var existing = _services.Store.List().Where(x => x.BackendId == _backend.SimId).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            while (existing.Contains(name)) name = "Squadrons Controls " + number++;
            var workspace = new GameWorkspace(1, Guid.NewGuid(), name, profile, new());
            var opened = CreateSession(workspace);
            _selection.Reset();
            _focusedConcept = null;
            _inspectedControl = null;
            CommandPicker.SelectedItem = null;
            SelectionEditor.AssignmentDraftStatus.Text = DrawingDropFeedback.Text = "";
            _session = opened; _revision = null; _sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
            _services.Remember(_services.Recent() with { BackendId = _backend.SimId, WorkspaceId = null, DisplayName = "" });
            ReloadChoices(); Refresh(); StatusText.Text = "Controls imported. Save this profile before exporting.";
        }
        /// <summary>Saves through the shared optimistic revision boundary; failures retain pending edits.</summary>
        internal bool SaveProfile()
        {
            try
            {
                if (_session is null) return false;
                _session.Rename(ProfileName.Text);
                _revision = _services.Store.Save(_session.Snapshot, _revision);
                _session.AcceptSaved(); Remember(); ReloadChoices(); Refresh();
                SelectionEditor.AssignmentDraftStatus.Text = DrawingDropFeedback.Text = "";
                StatusText.Text = "Squadrons profile saved."; return true;
            }
            catch (Exception error) { StatusText.Text = "Unable to save: " + error.Message; UpdateAvailability(); return false; }
        }
        /// <summary>Writes a new review folder after saving; no active game settings are replaced.</summary>
        internal string ExportReview(string parent)
        {
            if (_session is null || Pending) throw new InvalidOperationException("Save the Squadrons profile before exporting.");
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var roots = new List<string> { Path.Combine(documents, "STAR WARS Squadrons", "settings"), Path.Combine(documents, "STAR WARS Squadrons Steam", "settings") };
            if (_sourceDirectory is not null) roots.Add(_sourceDirectory);
            var directory = GameReviewExport.Write(parent, _session.Export().Files, roots, backendId: _backend.SimId);
            StatusText.Text = "Review copy exported to " + directory + ". Close Squadrons and back up its settings before manually installing this copy.";
            return directory;
        }
        #endregion END Public
        #region BEGIN private helpers
        private GameWorkspaceChromeState Chrome => GameWorkspaceChromeState.For(_session, _revision, ProfileName.Text);
        private bool Pending => Chrome.Pending;
        private GameProfileSession CreateSession(GameWorkspace workspace)
        {
            if (workspace.Profile.BackendId != _backend.SimId) throw new InvalidDataException("This is not a Squadrons profile.");
            return new(_backend, workspace, EditionAccess.Policy, _drawings.Select(x => x.DeviceId).ToArray(), _services.Discover, controllerInputs: () => _controllerInputs);
        }
        private void LoadProfile(Guid id)
        {
            StopLiveInput();
            var saved = _services.Store.Load(id); var opened = CreateSession(saved.Workspace);
            _selection.Reset();
            _focusedConcept = null;
            _inspectedControl = null;
            CommandPicker.SelectedItem = null;
            SelectionEditor.AssignmentDraftStatus.Text = DrawingDropFeedback.Text = "";
            _session = opened; _revision = saved.Revision; _sourceDirectory = null;
            Remember(); ReloadChoices(); Refresh();
        }
        private void Remember()
        {
            if (_session is not null) _services.Remember(_services.Recent() with { BackendId = _backend.SimId, WorkspaceId = _session.Snapshot.Id, DisplayName = _session.Snapshot.Name });
        }
        private void ReloadChoices()
        {
            _refreshing = true;
            try
            {
                ProfilePicker.ItemsSource = _services.Store.List().Where(x => x.BackendId == _backend.SimId).ToArray();
                ProfilePicker.SelectedItem = ProfilePicker.Items.Cast<GameWorkspaceEntry>().FirstOrDefault(x => x.Id == _session?.Snapshot.Id);
            }
            finally { _refreshing = false; }
        }
        private void Refresh(bool resetName = true)
        {
            var selected = (DevicePicker.SelectedItem as GameDeviceChoice)?.Id;
            _refreshing = true;
            var snapshot = _session?.Snapshot;
            if (resetName) ProfileName.Text = snapshot?.Name ?? "";
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
            RefreshCommandCatalog(snapshot);
            if (snapshot is null) { _focusedConcept = null; _inspectedControl = null; }
            TargetDevice.ItemsSource = devices;
            var verified = new Dictionary<string, IReadOnlyCollection<string>>();
            if (snapshot is not null)
                foreach (var (id, pin) in snapshot.Hardware)
                    if (GameControllerInputs.VerifiedKeys(pin, _controllerInputs) is { } keys) verified[id] = keys;
            _rows = snapshot is null ? [] : SquadronsWorkspaceProjection.ReviewInputs(SquadronsWorkspaceProjection.Rows(snapshot.Profile), verified);
            _refreshing = false; RefreshRows(); UpdateAvailability(); UpdateControllerAvailability(); RenderDrawing();
        }
        private void RefreshRows()
        {
            if (_refreshing) return;
            var selected = (BindingsGrid.SelectedItem as SquadronsWorkspaceRow)?.RecordId;
            var review = ReviewCaptured.IsChecked == true;
            var section = SectionPicker.SelectedItem as string ?? "Flight";
            var rows = SquadronsWorkspaceProjection.Filter(_rows, SectionPicker.SelectedItem as string ?? "All", TypePicker.SelectedItem as string ?? "All inputs", Search.Text,
                (DevicePicker.SelectedItem as GameDeviceChoice)?.Id ?? "all", includeUnverified: review);
            if (_focusedConcept is not null) rows = rows.Where(row => row.Native.Concept == _focusedConcept).ToArray();
            if (_inspectedControl is not null && SelectedController is { } inspectedSlot)
                rows = SquadronsDrawingProjection.ForControl(_inspectedControl, _rows, inspectedSlot, section);
            BindingsGrid.ItemsSource = rows;
            BindingsGrid.SelectedItem = rows.FirstOrDefault(x => x.RecordId == selected);
            ContextColumn.Visibility = section == "All" ? Visibility.Visible : Visibility.Collapsed;
            ControllerColumn.Visibility = review || (DevicePicker.SelectedItem is GameDeviceChoice { Id: "all" })
                ? Visibility.Visible : Visibility.Collapsed;
            HardwareColumn.Visibility = BindingColumn.Visibility = review ? Visibility.Visible : Visibility.Collapsed;
            CountText.Text = review ? rows.Count + " captured records · " + rows.Count(x => x.HardwareStatus == "Input absent on linked device") + " inputs absent on linked hardware" :
                rows.Count + " captured joystick assignments · " + section + ". Check hardware status in advanced review; saved slot ownership is not physical identity.";
            if (_focusedConcept is not null)
                CountText.Text = "Command: " + _focusedConcept + " · " + rows.Count + " matching captured bindings. " +
                    "Unassigned commands remain selectable in the command browser.";
            EmptyState.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyState.Text = _session is null ? "Import a Squadrons controls file to get started." : review ? "No matching captured records. Change the context, input filter or search." :
                _scanningInputs ? "Inspecting controller inputs…" : "No captured joystick assignments match these filters. Change the context or input filter, or enable advanced review for preserved native records.";
            if (_focusedConcept is not null && rows.Count == 0)
                EmptyState.Text = "This command has no assignments matching the current controller and input filters. The command is still available in the catalog.";
            if (_inspectedControl is not null)
            {
                CountText.Text = _inspectedControl.Label + " · " + rows.Count + " captured assignments · " + section + ".";
                if (rows.Count == 0) EmptyState.Text = "No captured assignments on this control in the current context.";
            }
            UpdateEditor();
            if (DrawingPanel.IsExpanded == true) Run(RenderDrawing);
        }
        private void UpdateEditor()
        {
            var row = BindingsGrid.SelectedItem as SquadronsWorkspaceRow;
            AssignmentEditor.Visibility = row is null ? Visibility.Collapsed : Visibility.Visible;
            var editable = row?.Editable == true && _session?.AccessMessage.Length == 0;
            TargetDevice.IsEnabled = editable && !_scanningInputs;
            TargetDevice.SelectedItem = TargetDevice.Items.Cast<GameDeviceChoice>().FirstOrDefault(x => x.Id == row?.DeviceId);
            AssignmentHint.Text = row is null ? "Select an assignment to edit." : editable
                ? row.CommandName + " · " + row.BindingLabel + ". Applying replaces only this record; other assignments remain intact."
                : "This native record is preserved. Editing its input type or combination is not supported yet.";
            UpdateInputChoices();
        }
        private void UpdateInputChoices()
        {
            var row = BindingsGrid.SelectedItem as SquadronsWorkspaceRow;
            var pin = TargetDevice.SelectedItem is GameDeviceChoice target ? _session?.Snapshot.Hardware.GetValueOrDefault(target.Id) : null;
            var keys = _scanningInputs ? null : GameControllerInputs.VerifiedKeys(pin, _controllerInputs);
            InputPicker.ItemsSource = keys is null ? [] : SquadronsInputCodec.Choices.Where(x => keys.Contains(x.PhysicalKey, StringComparer.Ordinal)).ToArray();
            InputPicker.SelectedItem = InputPicker.Items.Cast<SquadronsInput>().FirstOrDefault(x => x == row?.Native.Input);
            InputPicker.IsEnabled = row?.Editable == true && TargetDevice.IsEnabled && keys is not null;
            UpdateApplyAvailability();
            if (row?.Editable == true)
                AssignmentHint.Text = keys is null ? "Link the destination captured slot to a connected controller and Rescan to verify its real inputs. No generic controls can be assigned." :
                    row.CommandName + " · " + row.BindingLabel + ". Only enumerated controls on the destination device are offered." +
                    (row.HardwareStatus == "Input absent on linked device" ? " The captured input does not exist in the linked device's verified controls; its native record is preserved for review." : "");
        }
        private void UpdateApplyAvailability() => ApplyButton.IsEnabled = !(_input?.Running ?? false) && InputPicker.IsEnabled && InputPicker.SelectedItem is SquadronsInput && _session?.AccessMessage.Length == 0;
        private void TargetChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing) Run(UpdateInputChoices); }
        private void InputChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing) UpdateApplyAvailability(); }
        private void UpdateAvailability()
        {
            if (_refreshing || SaveState is null) return;
            UpdateInputAvailability();
            SaveButton.IsEnabled = Chrome.CanSave; DiscardButton.IsEnabled = Chrome.CanDiscard;
            ExportButton.IsEnabled = Chrome.CanExport;
            ProfileName.IsEnabled = _session is not null;
            SaveState.Text = Chrome.Status;
        }
        /// <summary>Only native joystick slots support physical linking and slot diagnostics.</summary>
        private string? SelectedController => DevicePicker.SelectedItem is GameDeviceChoice choice &&
            choice.Id.StartsWith("squadrons.joystick.", StringComparison.Ordinal) ? choice.Id : null;
        /// <summary>The catalog filters any selected native source, including read-only system devices.</summary>
        private string? SelectedAssignmentSource => (DevicePicker.SelectedItem as GameDeviceChoice)?.Id;
        private void RescanControllers()
        {
            StopLiveInput();
            var observed = _services.Discover();
            ConnectedPicker.ItemsSource = observed.Where(x => x.Connected && x.PhysicalGuid != Guid.Empty && x.ProductGuid != Guid.Empty && EditionAccess.Policy.AllowsDevice(x))
                .GroupBy(x => x.PhysicalGuid).Where(x => x.Count() == 1).Select(x => x.Single()).ToArray();
            ConnectedPicker.SelectedIndex = -1; UpdateControllerAvailability();
        }
        private void UpdateControllerAvailability()
        {
            var selected = SelectedController;
            var ready = selected is not null && !_scanningInputs && _session?.CanLinkCapturedDevices == true && _session.AccessMessage.Length == 0;
            var running = _identification.Running || (_input?.Running ?? false);
            var pin = selected is null ? null : _session?.Snapshot.Hardware.GetValueOrDefault(selected);
            RescanControllerButton.IsEnabled = !running && !_scanningInputs;
            ConnectedPicker.IsEnabled = ready && !running;
            IdentifyControllerButton.IsEnabled = ready && !running && ConnectedPicker.Items.Count > 0;
            CancelIdentificationButton.IsEnabled = running;
            LinkControllerButton.IsEnabled = ready && !running && ConnectedPicker.SelectedItem is DetectedDcsDevice;
            UnlinkControllerButton.IsEnabled = ready && !running && pin is not null;
            RemoveUnusedSlotButton.IsEnabled = ready && !running && pin is null;
            ClearSlotButton.IsEnabled = ready && !running && _rows.Any(x => x.Native.Type == 3 && x.DeviceId == selected);
            LinkStatus.Text = selected is null ? "Select a captured slot to manage its physical link." : pin is null ? "Unlinked captured slot" :
                "Linked: " + pin.Device.Name + "\n" + pin.Device.PhysicalGuid + "\n" +
                (GameInputReadiness.Ready(pin, _services.Discover()) ? "Connected · exact instance and product" : "Unavailable · disconnected or identity changed. Rescan after reconnecting.");
            if (selected is not null && _session is not null)
                LinkStatus.Text += "\n" + SquadronsWorkspaceProjection.SlotDiagnostic(_session.Snapshot.Profile, _rows, selected);
            var systemSelection = DevicePicker.SelectedItem is GameDeviceChoice { Id: var systemId }
                && systemId.StartsWith("system.", StringComparison.Ordinal);
            ControllerSummary.Text = systemSelection ? "Native system assignments · read-only. Codes are shown exactly as captured." :
                selected is null ? "Select a controller." : pin is null ? "Not linked · open Controller setup." :
                pin.Device.Name + (GameInputReadiness.Ready(pin, _services.Discover()) ? " · Connected" : " · Unavailable");
            UpdateInputAvailability();
        }
        private void CaptureReviewChanged(object sender, RoutedEventArgs e)
        {
            if (_refreshing) return;
            if (ReviewCaptured.IsChecked != true) _inspectedControl = null;
            RefreshRows();
        }
        private void SetupCollapsed(object sender, RoutedEventArgs e) { if (!_refreshing && ReferenceEquals(e.OriginalSource, ControllerSetup)) StopLiveInput(); }
        private void ClearIdentification()
        {
            IdentificationStatus.Text = ""; ConnectedPicker.SelectedIndex = -1;
            if (!_refreshing) Run(UpdateControllerAvailability);
        }
        private void ShowIdentification(IReadOnlyDictionary<DetectedDcsDevice, string> activity)
        {
            ConnectedPicker.SelectedIndex = -1;
            if (activity.Count != 1) { IdentificationStatus.Text = "Several controllers moved. Operate just one physical unit."; return; }
            var detected = activity.Single();
            StopLiveInput();
            ConnectedPicker.SelectedItem = ConnectedPicker.Items.Cast<DetectedDcsDevice>().SingleOrDefault(x => x.PhysicalGuid == detected.Key.PhysicalGuid);
            IdentificationStatus.Text = detected.Key.Name + " · " + detected.Value + "\n" + detected.Key.PhysicalGuid + "\nPhysical unit identified. Verify its captured slot in Squadrons, then review the link.";
            UpdateControllerAvailability();
        }
        private void ReviewLink(GameControllerChange change)
        {
            StopSamplingForReview();
            if (!(_reviewChange?.Invoke(change) ?? new ControllerChangeReviewWindow(this, change).ShowDialog() == true)) return;
            _session!.ApplyControllerChange(change); Refresh(false); StatusText.Text = "Physical link staged. Native slots and bindings are unchanged. Save to keep this link.";
        }
        private void LinkClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (!LinkControllerButton.IsEnabled || SelectedController is not { } id || ConnectedPicker.SelectedItem is not DetectedDcsDevice device) return;
            ReviewLink(_session!.PreviewCapturedDeviceLink(id, device.PhysicalGuid));
        });
        private void UnlinkClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (UnlinkControllerButton.IsEnabled && SelectedController is { } id) ReviewLink(_session!.PreviewCapturedDeviceUnlink(id));
        });
        private void ReviewNativeChange(GameControllerChange change)
        {
            StopSamplingForReview();
            if (!(_reviewChange?.Invoke(change) ?? new ControllerChangeReviewWindow(this, change).ShowDialog() == true)) return;
            _session!.ApplyControllerChange(change);
            Refresh(false);
            if (change.ResultDeviceId is { } id)
                DevicePicker.SelectedItem = DevicePicker.Items.Cast<GameDeviceChoice>().FirstOrDefault(x => x.Id == id);
            StatusText.Text = "Native Squadrons assignments / slots updated in the draft. Save before exporting a review copy.";
        }
        private void ClearSlotClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (ClearSlotButton.IsEnabled && SelectedController is { } id)
                ReviewNativeChange(_session!.PreviewClearCapturedAssignments(id));
        });
        private void RemoveUnusedSlotClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (!RemoveUnusedSlotButton.IsEnabled || SelectedController is not { } id) return;
            var change = _session!.PreviewRemoveUnusedCapturedDevice(id);
            if (!(_reviewChange?.Invoke(change) ?? new ControllerChangeReviewWindow(this, change).ShowDialog() == true)) return;
            _session.ApplyControllerChange(change);
            Refresh(false);
            StatusText.Text = "Unused slot removed from staged profile. Save before exporting.";
        });
        private void IdentifyClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (!IdentifyControllerButton.IsEnabled) return;
            _identification.Start(_services.Discover()); UpdateControllerAvailability();
            IdentificationStatus.Text = "Press a button, move a hat or move an axis noticeably on one controller. Held switches are ignored until pressed again.";
        });
        private void StopIdentificationClicked(object sender, RoutedEventArgs e) => StopLiveInput();
        private async void RescanClicked(object sender, RoutedEventArgs e)
        {
            if (!RescanControllerButton.IsEnabled) return;
            Run(RescanControllers); await RefreshControllerInputsAsync();
        }
        private void ConnectedChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing) Run(UpdateControllerAvailability); }
        private void CommandChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing) return;
            var command = CommandPicker.SelectedItem as Heimdall.Domain.MultiSim.SimCommand;
            if (_selection.CommandId != command?.Id) SelectionEditor.AssignmentDraftStatus.Text = "";
            _selection.SelectCommand(command?.Id);
            SelectionEditor.ShowCommand(command);
            ShowCommandButton.IsEnabled = command is not null;
            if (command is null || _session is null)
            {
                CommandHint.Text = "Choose a command to inspect its saved binding records.";
                return;
            }
            var plan = SquadronsAssignmentPlanner.For(_backend, _session.Snapshot.Profile,
                command.Metadata["squadrons.concept"]);
            CommandHint.Text = plan.CapturedRecords + " captured records · " + plan.EditableRecords + " editable.";
        }
        private void ShowCommandClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (CommandPicker.SelectedItem is not Heimdall.Domain.MultiSim.SimCommand command) return;
            _inspectedControl = null;
            SectionPicker.SelectedItem = command.Category;
            ReviewCaptured.IsChecked = true;
            CapturedAssignments.IsExpanded = true;
            _focusedConcept = command.Metadata["squadrons.concept"];
            RefreshRows();
            var match = _rows.FirstOrDefault(row => row.Native.Concept == _focusedConcept
                && (SelectedController is null || row.DeviceId == SelectedController));
            if (match is not null)
                BindingsGrid.SelectedItem = BindingsGrid.Items.Cast<SquadronsWorkspaceRow>()
                    .FirstOrDefault(row => row.RecordId == match.RecordId);
            if (BindingsGrid.SelectedItem is not null) BindingsGrid.ScrollIntoView(BindingsGrid.SelectedItem);
            else CommandHint.Text = "No captured assignment matches this command and controller. No new native records were written.";
        });
        private void RecordContextOpening(object sender, ContextMenuEventArgs e)
        {
            var row = BindingsGrid.SelectedItem as SquadronsWorkspaceRow;
            EditNativeRecordItem.IsEnabled = row?.Editable == true && !_input.Running;
            ClearNativeRecordItem.Visibility = !_input.Running && !_scanningInputs && row?.Native.Type == 3 && _session?.AccessMessage.Length == 0
                ? Visibility.Visible : Visibility.Collapsed;
        }
        private void RecordRightClicked(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject element &&
                ItemsControl.ContainerFromElement(BindingsGrid, element) is DataGridRow row)
                BindingsGrid.SelectedItem = row.Item;
        }
        private void EditSelectedClicked(object sender, RoutedEventArgs e)
        {
            if (BindingsGrid.SelectedItem is SquadronsWorkspaceRow)
                UpdateEditor();
        }
        private void ClearSelectedClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (BindingsGrid.SelectedItem is not SquadronsWorkspaceRow { Native.Type: 3 } row)
                throw new InvalidOperationException("Choose an existing joystick assignment to clear.");
            ReviewNativeChange(_session!.PreviewClearCapturedRecord(row.RecordId));
        });
        private void ApplyClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (_input.Running || _scanningInputs || _identification.Running)
                throw new InvalidOperationException("Stop live input and finish scanning before applying an assignment.");
            if (BindingsGrid.SelectedItem is not SquadronsWorkspaceRow { Editable: true } row || TargetDevice.SelectedItem is not GameDeviceChoice device || InputPicker.SelectedItem is not SquadronsInput input)
                throw new InvalidOperationException("Choose an editable binding, captured controller and input.");
            _session!.RebindNative(row.RecordId, device.Id, input.Key, input.Type, input.Direction);
            Refresh(false);
            SelectionEditor.AssignmentDraftStatus.Text = StatusText.Text = "Assignment staged. Save before exporting.";
        });
        private void ImportClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            var dialog = new OpenFileDialog { Title = "Import Squadrons controls", Filter = "Squadrons controls|ProfileOptions_profile_synced|All files|*.*" };
            if (dialog.ShowDialog(this) == true) ImportFile(dialog.FileName);
        });
        private void ExportClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            var dialog = new OpenFolderDialog { Title = "Choose a folder for a separate Squadrons review copy" };
            if (dialog.ShowDialog(this) == true) ExportReview(dialog.FolderName);
        });
        private void SaveClicked(object sender, RoutedEventArgs e) => SaveProfile();
        private void DiscardClicked(object sender, RoutedEventArgs e)
        {
            StopLiveInput();
            _session?.Discard(); Refresh();
            StatusText.Text = "Draft changes discarded.";
            SelectionEditor.AssignmentDraftStatus.Text = DrawingDropFeedback.Text = "";
            if (_revision is null) { _session = null; Refresh(); }
        }
        private void ProfileChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing || ProfilePicker.SelectedItem is not GameWorkspaceEntry entry || entry.Id == _session?.Snapshot.Id) return;
            if (ConfirmLeave()) Run(() => LoadProfile(entry.Id));
            _refreshing = true; ProfilePicker.SelectedItem = ProfilePicker.Items.Cast<GameWorkspaceEntry>().FirstOrDefault(x => x.Id == _session?.Snapshot.Id); _refreshing = false;
        }
        private void GameChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing || GamePicker.SelectedItem is not ComboBoxItem { Tag: string game } || game == "star-wars-squadrons") return;
            if (!ConfirmLeave()) { _refreshing = true; GamePicker.SelectedIndex = 2; _refreshing = false; return; }
            RequestedGame = game; _allowClose = true; Close();
        }
        private bool ConfirmLeave()
        {
            return GameWorkspaceLeaveGuard.Confirm(Pending,
                () => (_pendingChoice?.Invoke() ?? ThemeMessageBox.Show(this, "Save changes to this Squadrons profile?", "Unsaved changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question)) switch
                {
                    MessageBoxResult.Yes => GameWorkspaceLeaveDecision.Save,
                    MessageBoxResult.No => GameWorkspaceLeaveDecision.Discard,
                    _ => GameWorkspaceLeaveDecision.Cancel
                },
                SaveProfile, () => _session?.Discard());
        }
        private void ConfirmClose(object? sender, CancelEventArgs e) { StopLiveInput(); if (!_allowClose) e.Cancel = !ConfirmLeave(); }
        private void FilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing) return;
            if (ReferenceEquals(sender, DevicePicker))
            {
                StopLiveInput();
                _inspectedControl = null;
                DrawingDropFeedback.Text = "";
                SelectionEditor.AssignmentDraftStatus.Text = "";
                BindingsGrid.SelectedItem = null;
                if (DevicePicker.SelectedItem is GameDeviceChoice { Id: var id } &&
                    id.StartsWith("system.", StringComparison.Ordinal))
                {
                    SectionPicker.SelectedItem = "All";
                    TypePicker.SelectedItem = "All inputs";
                    ControllerSetup.IsExpanded = false;
                }
                Run(UpdateControllerAvailability);
                RefreshCommandCatalog(_session?.Snapshot);
                Run(RenderDrawing);
            }
            if (ReferenceEquals(sender, SectionPicker) || ReferenceEquals(sender, TypePicker))
            {
                _focusedConcept = null;
                _inspectedControl = null;
            }
            RefreshRows();
        }
        private void SearchChanged(object sender, TextChangedEventArgs e) { if (!_refreshing) { _inspectedControl = null; RefreshRows(); } }
        private void BindingChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing) UpdateEditor(); }
        private void NameChanged(object sender, TextChangedEventArgs e) { if (!_refreshing) UpdateAvailability(); }
        private void Run(Action action) { try { action(); } catch (Exception error) { StatusText.Text = error.Message; } }
        /// <summary>Refreshes the command catalog independently of the captured-binding table.</summary>
        private void RefreshCommandCatalog(GameWorkspace? snapshot)
        {
            var selected = (CommandPicker.SelectedItem as Heimdall.Domain.MultiSim.SimCommand)?.Id;
            IReadOnlyList<Heimdall.Domain.MultiSim.SimCommand> all = snapshot is null ? [] : _backend.GetCatalog(snapshot.Profile).Commands;
            IReadOnlyList<Heimdall.Domain.MultiSim.SimCommand> filtered = snapshot is null ? [] : SquadronsCommandFilter.Apply(all,
                _backend.ReadBindings(snapshot.Profile),
                CommandContextFilter.SelectedItem as string ?? "All",
                (CommandInputFilter.SelectedItem as GameCommandTypeOption)?.Label ?? "All types",
                (CommandStatusFilter.SelectedItem as GameCommandStatusOption)?.Label ?? "All",
                CommandSearch.Text, SelectedAssignmentSource);
            var wasRefreshing = _refreshing;
            _refreshing = true;
            try
            {
                _selection.Refresh(SelectedAssignmentSource, filtered, selected, selectFirst: false);
                CommandPicker.ItemsSource = filtered;
                CommandPicker.SelectedItem = filtered.FirstOrDefault(x => x.Id == _selection.CommandId);
            }
            finally { _refreshing = wasRefreshing; }
            CommandFilterCount.Text = filtered.Count + " of " + all.Count +
                " known/imported commands · assignment status includes all native input records.";
            SelectionEditor.ShowCommand(CommandPicker.SelectedItem as Heimdall.Domain.MultiSim.SimCommand);
            if (_selection.CommandId is null) CommandHint.Text = "Choose a command, then click or drag onto a drawing callout.";
            CommandBrowser.CommandEmptyHint.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ShowCommandButton.IsEnabled = CommandPicker.SelectedItem is not null;
        }
        private void CommandFiltersChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_refreshing) RefreshCommandCatalog(_session?.Snapshot);
        }
        private void CommandSearchChanged(object sender, TextChangedEventArgs e)
        {
            if (!_refreshing) RefreshCommandCatalog(_session?.Snapshot);
        }
        private void ResetCommandFiltersClicked(object sender, RoutedEventArgs e)
        {
            _refreshing = true;
            try
            {
                CommandContextFilter.SelectedIndex = 0;
                CommandInputFilter.SelectedIndex = 0;
                CommandStatusFilter.SelectedIndex = 0;
                CommandSearch.Clear();
            }
            finally { _refreshing = false; }
            RefreshCommandCatalog(_session?.Snapshot);
        }
        private const string SquadronsCommandDragFormat = "heimdall.squadrons.command";

        /// <summary>Renders only the explicitly pinned shared drawing for a selected joystick slot.</summary>
        private void RenderDrawing()
        {
            _input?.Stop();
            DrawingCanvas.Children.Clear();
            _drawingPresenter.Regions.Clear();
            DrawingCanvas.Width = 600; DrawingCanvas.Height = 200;
            DrawingHint.Visibility = Visibility.Visible;
            InputActivity.Visibility = Visibility.Visible;
            DrawingScroll.Visibility = Visibility.Collapsed;
            var snapshot = _session?.Snapshot;
            var slot = SelectedController;
            var pin = slot is null ? null : snapshot?.Hardware.GetValueOrDefault(slot);
            // A native physical link does not imply an artwork catalog identity.
            var oldRefreshing = _refreshing;
            _refreshing = true;
            try
            {
                DrawingPicker.ItemsSource = _drawings.Where(x => !string.IsNullOrWhiteSpace(x.Diagram.Svg))
                    .OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase).ToArray();
                DrawingPicker.SelectedItem = pin?.DrawingHidden == true ? null : _drawings.FirstOrDefault(x => x.DeviceId == pin?.HardwareId);
            }
            finally { _refreshing = oldRefreshing; }
            DrawingPicker.IsEnabled = ClearDrawingButton.IsEnabled = pin is not null && _drawings.Count > 0;
            if (DrawingPanel.IsExpanded != true) return;
            if (slot is null || pin is null)
            {
                DrawingHint.Text = "Select a linked joystick controller before choosing its hardware drawing.";
                return;
            }
            if (pin.DrawingHidden)
            {
                DrawingHint.Text = "Drawing hidden. Select a hardware drawing to show it again.";
                return;
            }
            if (string.IsNullOrEmpty(pin.HardwareId))
            {
                DrawingHint.Text = "This controller is linked, but no hardware drawing is selected. Choose a matching shared drawing above.";
                return;
            }
            var hardware = _drawings.FirstOrDefault(x => x.DeviceId == pin.HardwareId);
            if (hardware is null || string.IsNullOrWhiteSpace(hardware.Diagram.Svg))
            {
                DrawingHint.Text = "The selected hardware drawing is not available in the current shared catalog.";
                return;
            }
            var diagram = hardware.Diagram;
            var section = SectionPicker.SelectedItem as string ?? "All";
            var page = SquadronsDrawingProjection.Diagram(snapshot!.Name, diagram, _rows, slot, section);
            // Native clearing reviews the complete pointed-control record set, including both axis directions.
            _drawingPresenter.Show(diagram, page,
                DrawingInteractions(),
                AppearanceManager.ArtworkOpacityFor(this),
                control => control.Label + " · " + control.InputKey + "\n" +
                    SquadronsDrawingProjection.AssignmentSummary(control, _rows, slot, section));
            DrawingHint.Visibility = InputActivity.Visibility = Visibility.Collapsed;
            DrawingScroll.Visibility = Visibility.Visible;
        }
        private void DrawingChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing || _session is null || SelectedController is not { } slot ||
                DrawingPicker.SelectedItem is not WorkspaceHardware drawing) return;
            Run(() =>
            {
                _session.ChangeDrawing(slot, drawing.DeviceId);
                Refresh(false);
                StatusText.Text = "Shared drawing selected. Save profile to retain this choice.";
            });
        }
        private void ClearDrawingClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (_session is null || SelectedController is not { } slot) return;
            _session.ChangeDrawing(slot, null);
            Refresh(false);
            StatusText.Text = "Drawing hidden. Save profile to retain this choice.";
        });
        private void FitDrawingClicked(object sender, RoutedEventArgs e) => _drawingViewport.Fit();
        // The Expander may be larger than the pane's arranged row. Clamp the scroll
        // host to the actual resizable row so WPF registers clipped drawing content
        // as scrollable extent instead of simply hiding it behind the window edge.
        private bool _drawingBoundsQueued;
        private void DrawingPanelSizeChanged(object sender, SizeChangedEventArgs e) => QueueDrawingBounds();

        private void QueueDrawingBounds()
        {
            if (_drawingBoundsQueued || DrawingPanel is null || DrawingScroll is null) return;
            _drawingBoundsQueued = true;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                _drawingBoundsQueued = false;
                if (!DrawingPanel.IsExpanded || DrawingPanel.ActualHeight <= 0) return;
                var top = DrawingScroll.TranslatePoint(new Point(0, 0), DrawingPanel).Y;
                var maximum = Math.Max(24, DrawingPanel.ActualHeight - top - 4);
                if (double.IsFinite(maximum) && Math.Abs(DrawingScroll.MaxHeight - maximum) > 0.5)
                    DrawingScroll.MaxHeight = maximum;
            }));
        }

        private void DrawingExpanded(object sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, DrawingPanel)) return;

            QueueDrawingBounds();
            if (!_refreshing && _drawingPresenter is not null) Run(RenderDrawing);
        }
        private void DrawingCollapsed(object sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, DrawingPanel)) return;

            DrawingScroll.MaxHeight = double.PositiveInfinity;
        }
        private void SelectDrawingInput(HardwareGraphicControl control)
        {
            if (CommandPicker.SelectedItem is Heimdall.Domain.MultiSim.SimCommand command)
            {
                DropDrawingCommand(control, command.Id);
                return;
            }
            InspectDrawingInput(control);
        }
        private void InspectDrawingInput(HardwareGraphicControl control)
        {
            if (SelectedController is not { } slot) return;
            _focusedConcept = null;
            _inspectedControl = control;
            ReviewCaptured.IsChecked = true;
            CapturedAssignments.IsExpanded = true;
            RefreshRows();
            var section = SectionPicker.SelectedItem as string ?? "All";
            var matches = SquadronsDrawingProjection.ForControl(control, _rows, slot, section);
            if (matches.Count > 0)
            {
                CapturedAssignments.IsExpanded = true;
                BindingsGrid.SelectedItem = BindingsGrid.Items.Cast<SquadronsWorkspaceRow>()
                    .FirstOrDefault(row => row.RecordId == matches[0].RecordId);
                if (BindingsGrid.SelectedItem is not null) BindingsGrid.ScrollIntoView(BindingsGrid.SelectedItem);
            }
            DrawingDropFeedback.Text = control.Label + ": " +
                SquadronsDrawingProjection.AssignmentSummary(control, _rows, slot, section)
                    .Replace("\n", " · ", StringComparison.Ordinal);
        }
        private void DropDrawingCommand(HardwareGraphicControl control, string commandId)
        {
            try
            {
                if (_session is null || SelectedController is not { } slot)
                    throw new InvalidOperationException("Select a linked joystick slot.");
                if (_scanningInputs || _identification.Running || _input.Running)
                    throw new InvalidOperationException("Finish controller scanning or identification before assigning.");
                _selection.SelectInput(control.InputKey);
                var plan = SquadronsDrawingAssignment.For(_backend, _session.Snapshot.Profile,
                    commandId, slot, control.InputKey, control.IsAxis);
                if (plan.NegativeRecordId is not null)
                    _session.RebindNativeAxisPair(plan.RecordId!, plan.NegativeRecordId, slot, plan.Input.Key);
                else if (plan.RecordId is not null)
                    _session.RebindNative(plan.RecordId, slot, plan.Input.Key, plan.Input.Type, plan.Input.Direction);
                else
                    _session.AddSquadronsNativeBinding(plan.Concept, slot, plan.Input.Key, plan.Input.Type, plan.Input.Direction);
                Refresh(false);
                StatusText.Text = plan.NegativeRecordId is null
                    ? "Squadrons assignment staged. Save before exporting."
                    : "Both directions of the Squadrons axis staged. Save before exporting.";
                SelectionEditor.AssignmentDraftStatus.Text = DrawingDropFeedback.Text = StatusText.Text;
            }
            catch (Exception error)
            {
                SelectionEditor.AssignmentDraftStatus.Text = DrawingDropFeedback.Text = "Assignment not applied: " + error.Message;
                StatusText.Text = DrawingDropFeedback.Text;
            }
        }
        private void CommandListDragPressed(object sender, MouseButtonEventArgs e)
        {
            _commandDragStart = e.GetPosition(CommandPicker);
        }
        private void DragCapturedCommand(object sender, MouseEventArgs e)
        {
            if (!ReferenceEquals(sender, CommandPicker) || _commandDragStart is not { } start ||
                e.LeftButton != MouseButtonState.Pressed ||
                CommandPicker.SelectedItem is not Heimdall.Domain.MultiSim.SimCommand command ||
                _session is null || _session.AccessMessage.Length != 0)
            {
                if (e.LeftButton != MouseButtonState.Pressed) _commandDragStart = null;
                return;
            }
            var at = e.GetPosition(CommandPicker);
            if (Math.Abs(at.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(at.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            _commandDragStart = null;
            DrawingDropFeedback.Text = "Drag " + command.DisplayName + " onto the outlined hardware control.";
            var effect = DragDrop.DoDragDrop(CommandPicker,
                new DataObject(SquadronsCommandDragFormat, command.Id), DragDropEffects.Copy);
            if (effect == DragDropEffects.None &&
                !DrawingDropFeedback.Text.StartsWith("Drop onto ", StringComparison.Ordinal))
                DrawingDropFeedback.Text = "Drop canceled or outside a supported callout. Aim for an outlined input on the drawing.";
        }
        private bool CanClearDrawingInput(HardwareGraphicControl control) =>
            !_scanningInputs && !_identification.Running && !_input.Running && _session?.AccessMessage.Length == 0 &&
            SelectedController is { } slot &&
            SquadronsDrawingProjection.ForControl(control, _rows, slot, SectionPicker.SelectedItem as string ?? "All").Count > 0;
        private void ClearDrawingInput(HardwareGraphicControl control) => Run(() =>
        {
            if (!CanClearDrawingInput(control) || SelectedController is not { } slot) return;
            var records = SquadronsDrawingProjection.ForControl(control, _rows, slot, SectionPicker.SelectedItem as string ?? "All");
            ReviewNativeChange(_session!.PreviewClearCapturedRecords(records.Select(row => row.RecordId).ToArray()));
        });
        private ComboBox CommandContextFilter => CommandBrowser.CommandCategoryPicker;
        private ComboBox CommandInputFilter => CommandBrowser.CommandTypePicker;
        private ComboBox CommandStatusFilter => CommandBrowser.AssignmentStatusPicker;
        private TextBox CommandSearch => CommandBrowser.CommandSearch;
        private ListBox CommandPicker => CommandBrowser.CommandsList;
        private TextBlock CommandFilterCount => CommandBrowser.CommandCount;
        private void FeatureChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_featureHost is null || _input is null) return;
            _featureHost.Show(FeaturePicker.SelectedIndex == 1 ? _assignmentsView : _devicesView);
            Run(() => { RenderDrawing(); UpdateAvailability(); });
        }
        private void StopSamplingForReview()
        {
            _input.Stop();
            if (_identification.Running) _identification.Stop();
        }
        private void StopLiveInput()
        {
            _input?.Stop();
            _identification?.Stop();
        }
        private void InputStateChanged(object? sender, EventArgs e)
        {
            UpdateInputAvailability(); UpdateApplyAvailability(); UpdateControllerAvailability();
        }
        private void UpdateInputAvailability()
        {
            if (_input is null) return;
            var pin = SelectedController is { } slot ? _session?.Snapshot.Hardware.GetValueOrDefault(slot) : null;
            StartInputButton.IsEnabled = FeaturePicker.SelectedIndex == 1 && !_input.Running && !_scanningInputs &&
                !_identification.Running && pin is not null && _session?.AccessMessage.Length == 0 &&
                GameInputReadiness.Ready(pin, _services.Discover());
            StopInputButton.IsEnabled = _input.Running;
        }
        private void StartInputClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            UpdateInputAvailability();
            if (!StartInputButton.IsEnabled || SelectedController is not { } slot ||
                _session?.Snapshot.Hardware.GetValueOrDefault(slot) is not { } pin) return;
            _identification.Stop();
            _input.Start(pin, _services.Discover());
        });
        private void StopInputClicked(object sender, RoutedEventArgs e) => StopLiveInput();
        private void ShowInputActivity(string text)
        {
            InputActivity.Text = text;
            InputActivity.ScrollToEnd();
        }

        private Button RescanControllerButton => _devicesView.RescanControllerButton;
        private Button IdentifyControllerButton => _devicesView.IdentifyControllerButton;
        private Button CancelIdentificationButton => _devicesView.CancelIdentificationButton;
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
        private GameAssignmentEditorView SelectionEditor => _assignmentsView.SelectionEditor;
        private StackPanel AssignmentEditor => _assignmentsView.AssignmentEditor;
        private TextBlock AssignmentHint => _assignmentsView.AssignmentHint;
        private ComboBox TargetDevice => _assignmentsView.TargetDevice;
        private ComboBox InputPicker => _assignmentsView.InputPicker;
        #endregion END private helpers
    }
}
