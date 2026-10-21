using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Data;
using BindingMode = Heimdall.Domain.MultiSim.BindingMode;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;
using Heimdall.Infrastructure;
using Heimdall.Rendering;
using Microsoft.Win32;

namespace Heimdall.App.Games
{
    /// <summary>Hosts game-owned profile selection, native assignments and shared hardware artwork.</summary>
    public sealed partial class EliteWorkspaceWindow : Window, IDisposable
    {
        private readonly EliteDevicesView _devicesView = new();
        private readonly EliteAssignmentsView _assignmentsView = new();
        private readonly EliteReferenceSheetsView _referenceView = new();
        private readonly EliteControllerReordering _reordering;
        private readonly GameWorkspaceServices _services;
        private readonly IReadOnlyList<WorkspaceHardware> _hardware;
        private readonly EliteDiagramPresenter _diagram;
        private readonly DiagramViewport _viewport;
        private readonly EliteInputFeedback _input;
        private readonly EliteControllerIdentification _identification;
        private DetectedDcsDevice? _identifiedController;
        private readonly Func<string?>? _importPicker;
        private readonly Func<string?>? _exportPicker;
        private readonly Func<MessageBoxResult>? _pendingChoice;
        private readonly List<EliteModifierChoice> _modifiers = [];
        private GameProfileSession? _session;
        private string? _revision;
        private readonly Func<nint, IReadOnlyList<Guid>, IDeviceInputSource> _controllerInputs;
        private readonly Func<GameControllerChange, bool> _reviewControllerChange;
        internal Func<IReadOnlyList<GameExportIssue>, GameExportResolution?>? ExportReview { get; set; }
        private bool _refreshing;
        private bool _allowClose;
        private IReadOnlyList<EliteAssignmentRow> _rows = [];
        private IReadOnlyList<SimCommand> _commands = [];
        private ReferenceSheetDocument? _referenceSheets;
        private bool _referenceDirty = true;
        private bool _renderingReference;
        private int _referencePage;
        /// <summary>Creates an isolated editor over the existing adapter and profile-owned services.</summary>
        public EliteWorkspaceWindow(GameWorkspaceServices services, IReadOnlyList<WorkspaceHardware> hardware,
            Guid? initial = null, Func<string?>? importPicker = null, Func<string?>? exportPicker = null,
            Func<MessageBoxResult>? pendingChoice = null, Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? inputFactory = null, Func<GameControllerChange, bool>? controllerReview = null)
        {
            _services = services; _hardware = hardware; _importPicker = importPicker; _exportPicker = exportPicker; _pendingChoice = pendingChoice;
            _controllerInputs = inputFactory ?? ((handle, devices) => new WindowsDeviceInputSource(handle, devices));
            _reviewControllerChange = controllerReview ?? (change => new ControllerChangeReviewWindow(this, change).ShowDialog() == true);
            InitializeComponent();
            RegisterFeatureNames(_devicesView); RegisterFeatureNames(_assignmentsView); RegisterFeatureNames(_referenceView);
            _devicesView.IdentifyControllerButton.Click += IdentifyControllerClicked;
            _devicesView.CancelIdentificationButton.Click += CancelIdentificationClicked;
            _devicesView.ConfirmIdentificationButton.Click += ConfirmIdentificationClicked;
            _devicesView.ConnectedPicker.SelectionChanged += ConnectedChanged;
            _devicesView.RescanControllerButton.Click += RescanClicked;
            _devicesView.AddControllerButton.Click += AddControllerClicked;
            _devicesView.ReplaceControllerButton.Click += ReplaceControllerClicked;
            _devicesView.RemoveControllerButton.Click += RemoveControllerClicked;
            _devicesView.LinkReferenceButton.Click += LinkReferenceClicked;
            _devicesView.ChangeDrawingButton.Click += ChangeDrawingClicked;
            _devicesView.UseDrawingButton.Click += UseDrawingClicked;
            _devicesView.NoDrawingButton.Click += NoDrawingClicked;
            _assignmentsView.StartInputButton.Click += StartInputClicked;
            _assignmentsView.StopInputButton.Click += StopInputClicked;
            _assignmentsView.ExportSheetButton.Click += ExportSheetClicked;
            _assignmentsView.FitDiagramButton.Click += FitDiagramClicked;
            _assignmentsView.AssignmentsGrid.SelectionChanged += AssignmentSelected;
            _assignmentsView.CommandCategoryPicker.SelectionChanged += CommandFilterChanged;
            _assignmentsView.CommandTypePicker.SelectionChanged += CommandFilterChanged;
            _assignmentsView.ClearCommandFiltersButton.Click += ClearCommandFiltersClicked;
            _assignmentsView.CommandSearch.TextChanged += SearchChanged;
            _assignmentsView.CommandsList.SelectionChanged += CommandChanged;
            _assignmentsView.SlotPicker.SelectionChanged += SlotChanged;
            _assignmentsView.AddCombinationButton.Click += AddModifierClicked;
            _assignmentsView.RemoveCombinationButton.Click += RemoveModifierClicked;
            _assignmentsView.ApplyAssignmentButton.Click += ApplyClicked;
            _assignmentsView.ClearAssignmentButton.Click += ClearClicked;
            _referenceView.ReferenceLayoutPicker.SelectionChanged += ReferenceOptionsChanged;
            _referenceView.ReferencePaperPicker.SelectionChanged += ReferenceOptionsChanged;
            _referenceView.ReferenceOrientationPicker.SelectionChanged += ReferenceOptionsChanged;
            _referenceView.ReferenceAssignedOnly.Click += ReferenceAssignedChanged;
            _referenceView.ReferencePreviousButton.Click += ReferencePreviousClicked;
            _referenceView.ReferenceNextButton.Click += ReferenceNextClicked;
            _referenceView.ReferencePdfButton.Click += ReferencePdfClicked;
            _referenceView.ReferencePngButton.Click += ReferencePngClicked;
            _reordering = new(DevicePicker, (source, target, after) => Run(() => _session?.MoveController(source, target, after)));

            _diagram = new(_assignmentsView.DiagramCanvas);
            _viewport = new(_assignmentsView.DiagramScroll, _assignmentsView.DiagramCanvas, _assignmentsView.DiagramZoom, "elite.diagram");
            _input = new(this, _diagram, Status, ShowInputActivity, inputFactory);
            _identification = new(this, ShowIdentification, ClearIdentification, Status, inputFactory);
            _refreshing = true;
            GamePicker.SelectedIndex = 1; SectionPicker.SelectedIndex = 0; _assignmentsView.SlotPicker.SelectedIndex = 0; _assignmentsView.ModePicker.SelectedIndex = 0;
            _assignmentsView.CommandCategoryPicker.ItemsSource = new[] { "All categories" }; _assignmentsView.CommandCategoryPicker.SelectedIndex = 0; _assignmentsView.CommandTypePicker.SelectedIndex = 0;
            _referenceView.ReferenceLayoutPicker.SelectedIndex = 0; _referenceView.ReferencePaperPicker.SelectedIndex = 0; _referenceView.ReferenceOrientationPicker.SelectedIndex = 0;
            _devicesView.HardwarePicker.ItemsSource = hardware.Where(x => EditionAccess.Policy.AllowsHardware(x.DeviceId)).ToArray();
            _refreshing = false;
            ReloadChoices(initial);
            FeaturePicker.SelectedIndex = 0;
            _assignmentsView.CommandsList.PreviewMouseMove += DragCommand;
            Closing += ConfirmClose; Closed += (_, _) => Dispose();
            AppearanceManager.ObserveArtwork(this, () => Run(() => { RenderDevice(); InvalidateReferenceSheets(); }));
            Rescan(); UpdateAvailability();
        }
        #region BEGIN Public
        /// <summary>Releases native input handles and draft event subscriptions.</summary>
        public void Dispose()
        {
            FeatureActions.ClearActions(); _reordering.Dispose(); _identification.Dispose(); _input.Dispose(); if (_session is not null) _session.Changed -= SessionChanged; GC.SuppressFinalize(this);
        }
        /// <summary>Gets an explicit request to return to the DCS landing surface.</summary>
        public bool DcsRequested { get; private set; }
        /// <summary>Gets the current staged session for deterministic Windows interaction checks.</summary>
        internal GameProfileSession? Session => _session;
        /// <summary>Builds the same window-scoped payload used by controller drags.</summary>
        internal DataObject ControllerDrag(string id) => _reordering.Payload(id);
        /// <summary>Exercises the controller drop boundary without a modal operating-system drag loop.</summary>
        internal bool DropController(IDataObject data, string target, bool after) => _reordering.DropAt(data, target, after);
        /// <summary>Processes a deterministic sample through the same input path as the foreground timer.</summary>
        internal void SampleInput() => _input.Sample();
        /// <summary>Processes discovery samples through the same path as the foreground identification timer.</summary>
        internal void SampleIdentification() => _identification.Sample();
        /// <summary>Imports an explicitly chosen captured file; no active game configuration is changed.</summary>
        internal void ImportFile(string path)
        {
            var profile = _services.Backend.Import("controls", new Dictionary<string, string> { [Path.GetFileName(path)] = File.ReadAllText(path) });
            var workspace = new GameWorkspace(1, Guid.NewGuid(), profile.Name + " Controls", profile, new());
            OpenSession(workspace, null); Status("Imported bindings. Associate your hardware drawing, then review and save this profile.");
        }
        /// <summary>Saves the current game workspace without deploying native files.</summary>
        internal bool SaveProfile()
        {
            if (_session is null) return true;
            try
            {
                var workspace = _session.Snapshot with { Name = ProfileName.Text.Trim() };
                GameWorkspaceContract.Validate(workspace);
                _revision = _services.Store.Save(workspace, _revision);
                OpenSession(workspace, _revision); ReloadChoices(workspace.Id); Remember(workspace);
                ExportState.Text = "Saved in HEIMDALL. Export for review to create an ED preset; installation into ED is manual.";
                Status("Saved in your HEIMDALL data profile. Active Elite bindings are unchanged." + (_services.SelectionError is { } error ? " Recent selection was not saved: " + error : "")); return true;
            }
            catch (Exception error) { Status("Save failed; pending edits are retained: " + error.Message); return false; }
        }
        /// <summary>Exports the same authored shared-drawing projection after edition validation.</summary>
        internal void ExportReference(string path)
        {
            if (_session is null || Device is null) throw new InvalidOperationException("Select a profile and associated device first.");
            _session.Export();
            var document = _session.Snapshot;
            if (!document.Hardware.TryGetValue(Device.Id, out var pin) || pin.HardwareId is null || pin.DrawingHidden) throw new InvalidOperationException("Choose an optional drawing before exporting an illustrated reference sheet.");
            var hardware = _hardware.Single(x => x.DeviceId == pin.HardwareId);
            var page = EliteWorkspaceProjection.Diagram(document.Name, hardware.Diagram, _rows.Where(x => x.DeviceId == Device.Id).ToArray(), Section);
            File.WriteAllBytes(path, CompositeKneeboardRenderer.Render(hardware.Diagram, page, "Elite Dangerous · " + document.Name, artworkOpacity: AppearanceManager.ArtworkOpacityFor(this)));
        }
        /// <summary>Gets the exact PDF backing the current reference preview after revalidating edition access.</summary>
        internal byte[] ReferencePdf()
        {
            _session?.Export(); RefreshReferenceSheets();
            return _referenceSheets?.Pdf ?? throw new InvalidOperationException("Select a valid controller profile and reference layout first.");
        }
        /// <summary>Gets the current preview page as a PNG, using the same document as PDF export.</summary>
        internal byte[] ReferencePng()
        {
            _session?.Export(); RefreshReferenceSheets();
            return _referenceSheets?.PngPages[_referencePage] ?? throw new InvalidOperationException("Select a valid controller profile and reference layout first.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private void RegisterFeatureNames(DependencyObject root)
        {
            if (root is FrameworkElement { Name.Length: > 0 } element) RegisterName(element.Name, element);
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) RegisterFeatureNames(child);
        }
        private void MoveControllerUpClicked(object sender, RoutedEventArgs e) => _reordering.MoveSelected(-1);
        private void MoveControllerDownClicked(object sender, RoutedEventArgs e) => _reordering.MoveSelected(1);
        private string Section => (SectionPicker.SelectedItem as ListBoxItem)?.Content.ToString() ?? "All";
        private string? CommandCategory => _assignmentsView.CommandCategoryPicker.SelectedItem is string category && category != "All categories" ? category : null;
        private CommandType? InputType => _assignmentsView.CommandTypePicker.SelectedIndex switch { 1 => CommandType.Button, 2 => CommandType.Axis, _ => null };
        private GameDeviceChoice? Device => DevicePicker.SelectedItem as GameDeviceChoice;
        private SimCommand? Command => _assignmentsView.CommandsList.SelectedItem as SimCommand;
        private string Slot => Command?.Type == CommandType.Axis ? "Binding" : _assignmentsView.SlotPicker.SelectedIndex == 1 ? "Secondary" : "Primary";
        private bool Pending => _session is not null && (_revision is null || _session.HasChanges || ProfileName.Text.Trim() != _session.Snapshot.Name);
        private void Status(string message) => WorkspaceStatus.Text = message;
        private void StopLiveInput() { _input.Stop(); _identification.Stop(); }
        private void ClearIdentification()
        {
            _identifiedController = null;
            _devicesView.ConfirmIdentificationButton.IsEnabled = false;
            _devicesView.IdentificationStatus.Text = "";
        }
        private void IdentifyControllerClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            StopLiveInput();
            EditionAccess.Policy.RequireOwner();
            if (_session is null || Device is null || FeaturePicker.SelectedIndex != 0)
                throw new InvalidOperationException("Select a controller reference on Devices before identifying it.");
            _identification.Start(_services.Discover());
            _devicesView.IdentificationStatus.Text = "Listening to all connected controllers. Press a button, move a hat, or move an axis noticeably. Held switches are ignored until pressed again.";
            Status("Identify " + Device.Name + ": operate the physical controller you want to check.");
        });
        private void ShowIdentification(IReadOnlyDictionary<DetectedDcsDevice, string> activity)
        {
            ClearIdentification();
            if (activity.Count != 1)
            {
                _devicesView.IdentificationStatus.Text = "Several controllers moved together. Operate just one controller to identify it.";
                return;
            }
            if (_session is null || Device is null) return;
            var detected = activity.Single();
            var identity = _session.Snapshot.Profile.Devices.Single(x => x.LogicalId == Device.Id).Identity!;
            var matches = _services.DeviceReferences.Matches(identity, detected.Key);
            _devicesView.IdentificationStatus.Text = detected.Key.Name + " · " + detected.Value + Environment.NewLine + detected.Key.PhysicalGuid +
                Environment.NewLine + (matches ? "Matches selected ED reference: " : "Does not match selected ED reference: ") + Device.Name;
            if (!matches) return;
            _identifiedController = detected.Key;
            _devicesView.ConfirmIdentificationButton.IsEnabled = true;
            if (_session.ControllerReferences(detected.Key.PhysicalGuid).Count > 1)
                _devicesView.IdentificationStatus.Text += Environment.NewLine + "Identical products share this ID. Input identifies this physical unit, but cannot prove its ED index. Verify the selected indexed reference in Elite before confirming.";
        }
        private void ConfirmIdentificationClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            EditionAccess.Policy.RequireOwner();
            if (_session is null || Device is null || _identifiedController is null || FeaturePicker.SelectedIndex != 0)
                throw new InvalidOperationException("Identify a matching controller before confirming its link.");
            var physical = _identifiedController.PhysicalGuid;
            var id = Device.Id;
            StopLiveInput();
            var connected = _services.Discover().Single(x => x.Connected && x.PhysicalGuid == physical);
            ConnectController(connected, id);
        });
        private void CancelIdentificationClicked(object sender, RoutedEventArgs e)
        { _identification.Stop(); Status("Controller identification stopped."); }
        private void Run(Action action) { try { action(); } catch (Exception error) { Status(error.Message); } }
        private void OpenSession(GameWorkspace workspace, string? revision)
        {
            StopLiveInput(); _referenceDirty = true;
            _devicesView.ReferenceChoiceTools.Visibility = _devicesView.DrawingTools.Visibility = Visibility.Collapsed;
            var opened = new GameProfileSession(_services.Backend, workspace, EditionAccess.Policy, _hardware.Select(x => x.DeviceId).ToArray(), _services.Discover, _services.DeviceReferences);
            if (_session is not null) _session.Changed -= SessionChanged;
            ExportState.Text = "Workspace open in HEIMDALL. Export for review creates a named ED preset; installation into ED is manual.";
            _session = opened; _revision = revision; _session.Changed += SessionChanged;
            _refreshing = true; ProfileName.Text = workspace.Name; _refreshing = false;
            Refresh();
        }
        private void SessionChanged(object? sender, EventArgs e) { StopLiveInput(); _referenceDirty = true; Refresh(); }
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
            _commands = _session.Catalog.Commands;
            _rows = EliteWorkspaceProjection.Rows(document.Profile, _session.Catalog);
            _refreshing = true;
            var devices = GameControllerOrder.For(document).Select(id => new GameDeviceChoice(id, ControllerName(document, id))).ToArray();
            DevicePicker.ItemsSource = devices; DevicePicker.SelectedItem = devices.FirstOrDefault(x => x.Id == previousDevice) ?? devices.FirstOrDefault();
            _assignmentsView.ModifierDevicePicker.ItemsSource = devices; _assignmentsView.ModifierDevicePicker.SelectedIndex = 0;
            _refreshing = false;
            FilterCommands(previousCommand); RenderDevice(); UpdateReplacementReferences(); UpdateAvailability();
        }
        private void FilterCommands(string? selected = null)
        {
            if (_session is null) return;
            var previousCategory = CommandCategory;
            var categories = EliteCommandBrowser.Categories(_commands, Section).Prepend("All categories").ToArray();
            _refreshing = true;
            _assignmentsView.CommandCategoryPicker.ItemsSource = categories;
            _assignmentsView.CommandCategoryPicker.SelectedItem = categories.Contains(previousCategory) ? previousCategory : categories[0];
            var commands = EliteCommandBrowser.Filter(_commands, Section, CommandCategory, InputType, _assignmentsView.CommandSearch.Text);
            var view = new ListCollectionView(commands.ToList());
            view.GroupDescriptions.Add(new PropertyGroupDescription("Metadata[ed.category]"));
            _assignmentsView.CommandsList.ItemsSource = view;
            _assignmentsView.CommandsList.SelectedItem = commands.FirstOrDefault(x => x.Id == selected) ?? (commands.Count > 0 ? commands[0] : null); _refreshing = false;
            _assignmentsView.CommandCount.Text = commands.Count + " of " + _commands.Count + " commands";
            _assignmentsView.CommandEmptyHint.Visibility = commands.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            _assignmentsView.AssignmentsGrid.ItemsSource = _rows.Where(x => x.DeviceId == Device?.Id && (Section == "All" || x.Section == Section)).ToArray();
            LoadAssignment(); UpdateAvailability();
        }
        private void RenderDevice()
        {
            StopLiveInput(); _assignmentsView.DiagramCanvas.Children.Clear(); _diagram.Regions.Clear(); _assignmentsView.DiagramHint.Visibility = Visibility.Visible;
            UpdateInputActivityVisibility();
            if (_session is null || Device is null) return;
            var document = _session.Snapshot;
            var hardware = document.Hardware.TryGetValue(Device.Id, out var pin) && !pin.DrawingHidden ? _hardware.FirstOrDefault(x => x.DeviceId == pin.HardwareId) : null;
            if (hardware is null) { _assignmentsView.DiagramHint.Text = "No drawing selected. Assign commands using native input keys and the assignments table. You can choose an optional drawing on Devices."; return; }
            var rows = _rows.Where(x => x.DeviceId == Device.Id).ToArray();
            var page = EliteWorkspaceProjection.Diagram(document.Name, hardware.Diagram, rows, Section);
            _diagram.Show(hardware.Diagram, page, SelectControl, DropCommand, () => ClearClicked(this, new()), AppearanceManager.ArtworkOpacityFor(this));
            _assignmentsView.DiagramHint.Visibility = Visibility.Collapsed;
            UpdateInputActivityVisibility();
        }
        private void SelectControl(HardwareGraphicControl control)
        {
            var key = EliteInputKeys.FromShared(control.InputKey);
            if (key is null) { Status("This drawing input has no verified Elite key. Choose an explicit native input key instead."); return; }
            var row = _rows.FirstOrDefault(x => x.DeviceId == Device?.Id && x.Input == key && (Section == "All" || x.Section == Section));
            if (row is not null) SelectRow(row);
            _assignmentsView.InputKey.Text = key; _assignmentsView.InputKey.Focus();
        }
        private void DropCommand(HardwareGraphicControl control, string id) => Run(() =>
        {
            var command = _commands.Single(x => x.Id == id);
            if (control.IsAxis != (command.Type == CommandType.Axis)) throw new InvalidOperationException("Choose an axis command for an axis, or a button command for a button.");
            _assignmentsView.CommandsList.SelectedItem = _assignmentsView.CommandsList.Items.Cast<SimCommand>().First(x => x.Id == id);
            _assignmentsView.InputKey.Text = EliteInputKeys.FromShared(control.InputKey) ?? throw new InvalidOperationException("Unsupported drawing input; choose an explicit native key.");
            ApplyClicked(this, new());
        });
        private void DragCommand(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && Command is { } command && _session?.AccessMessage.Length == 0)
                DragDrop.DoDragDrop(_assignmentsView.CommandsList, new DataObject("heimdall.elite.command", command.Id), DragDropEffects.Copy);
        }
        private void LoadAssignment()
        {
            if (_refreshing || _session is null) return;
            var axis = Command?.Type == CommandType.Axis;
            _assignmentsView.AxisTools.Visibility = axis ? Visibility.Visible : Visibility.Collapsed;
            _assignmentsView.ModifierTools.Visibility = axis ? Visibility.Collapsed : Visibility.Visible;
            _assignmentsView.SlotPicker.IsEnabled = _assignmentsView.ModePicker.IsEnabled = !axis;
            var row = _rows.FirstOrDefault(x => x.DeviceId == Device?.Id && x.CommandId == Command?.Id && x.Slot == Slot);
            _assignmentsView.InputKey.Text = row?.Input ?? ""; _assignmentsView.InvertAxis.IsChecked = row?.Invert ?? false;
            _assignmentsView.AxisDeadzone.Text = (row?.Deadzone ?? 0).ToString(CultureInfo.CurrentCulture);
            _assignmentsView.ModePicker.SelectedIndex = row?.Mode == BindingMode.Hold ? 1 : 0;
            _modifiers.Clear();
            if (row is not null) foreach (var modifier in row.Modifiers) _modifiers.Add(new(modifier,
                (_assignmentsView.ModifierDevicePicker.Items.Cast<GameDeviceChoice>().FirstOrDefault(x => x.Id == modifier.Source)?.Name ?? modifier.Source) + ": " + modifier.Value));
            _assignmentsView.ModifierTools.IsExpanded = _modifiers.Count > 0;
            _assignmentsView.ModifiersList.ItemsSource = null; _assignmentsView.ModifiersList.ItemsSource = _modifiers;
            var key = "ed.toggle:" + Command?.Id;
            var metadata = _session.Snapshot.Profile.Metadata;
            _assignmentsView.CommandToggle.Visibility = metadata.ContainsKey(key) ? Visibility.Visible : Visibility.Collapsed;
            _assignmentsView.CommandToggle.IsChecked = metadata.GetValueOrDefault(key) == "1";
        }
        private void UpdateAvailability()
        {
            var opened = _session is not null;
            AccessStatus.Text = _session?.AccessMessage ?? "Import or select a saved Elite controls profile.";
            _assignmentsView.AssignmentEditor.IsEnabled = Command is not null && AccessStatus.Text.Length == 0;
            SaveProfileButton.IsEnabled = opened && Pending; DiscardProfileButton.IsEnabled = opened && Pending;
            ExportPresetButton.IsEnabled = opened && !Pending && AccessStatus.Text.Length == 0;
            SaveState.Text = !opened ? "No profile" : Pending ? "Unsaved changes" : "Saved";
            var controller = Device is not null && _session is not null && !GameWorkspaceAccess.IsSystemDevice(
                _session.Snapshot.Profile.Devices.Single(x => x.LogicalId == Device.Id).Identity!.SimSpecificIds["ed.device"]);
            _devicesView.RemoveControllerButton.IsEnabled = opened && controller;
            _devicesView.ReplaceControllerButton.IsEnabled = opened && controller && EditionAccess.Policy.IsOwner && _devicesView.ConnectedPicker.SelectedItem is DetectedDcsDevice;
            ProfileName.IsEnabled = opened; _devicesView.AddControllerButton.IsEnabled = opened && _devicesView.ConnectedPicker.SelectedItem is DetectedDcsDevice;
            var document = _session?.Snapshot;
            var pin = Device is not null ? document?.Hardware.GetValueOrDefault(Device.Id) : null;
            var drawing = pin is not null && !pin.DrawingHidden ? _hardware.FirstOrDefault(x => x.DeviceId == pin.HardwareId) : null;
            _devicesView.ChangeDrawingButton.IsEnabled = pin is not null;
            _devicesView.IdentifyControllerButton.IsEnabled = opened && Device is not null && EditionAccess.Policy.IsOwner;
            _devicesView.DrawingStatus.Text = pin is null ? "Select a controller in this profile to view its drawing." : drawing is null ? "No drawing · assignments table available" : "Drawing: " + drawing.Label;
            var feature = FeaturePicker.SelectedIndex;
            MoveControllerUpButton.IsEnabled = DevicePicker.SelectedIndex > 0;
            MoveControllerDownButton.IsEnabled = DevicePicker.SelectedIndex >= 0 && DevicePicker.SelectedIndex + 1 < DevicePicker.Items.Count;
            _assignmentsView.ApplyAssignmentButton.IsEnabled = _assignmentsView.ClearAssignmentButton.IsEnabled = _assignmentsView.AssignmentEditor.IsEnabled && Device is not null;
            _assignmentsView.FitDiagramButton.IsEnabled = drawing is not null;
            _assignmentsView.StartInputButton.IsEnabled = opened && EditionAccess.Policy.IsOwner;
            _assignmentsView.ExportSheetButton.Visibility = Visibility.Collapsed;
            _assignmentsView.ExportSheetButton.IsEnabled = opened && drawing is not null && AccessStatus.Text.Length == 0;
            UpdateInputActivityVisibility();
            if (feature == 2) RefreshReferenceSheets();
        }
        private void ApplyClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (_session is null || Device is null || Command is null) return;
            var command = Command; var device = Device; var slot = Slot; var toggle = _assignmentsView.CommandToggle.IsChecked == true;
            var hasToggle = _assignmentsView.CommandToggle.Visibility == Visibility.Visible;
            if (command.Type == CommandType.Axis)
                _session.AssignAxis(device.Id, command.Id, _assignmentsView.InputKey.Text.Trim(), _assignmentsView.InvertAxis.IsChecked == true,
                    float.Parse(_assignmentsView.AxisDeadzone.Text, CultureInfo.CurrentCulture));
            else _session.AssignButton(device.Id, command.Id, slot, _assignmentsView.InputKey.Text.Trim(), _assignmentsView.ModePicker.SelectedIndex == 1 ? BindingMode.Hold : BindingMode.Press,
                _modifiers.Select(x => x.Modifier).ToArray());
            if (hasToggle) _session.SetToggle(command.Id, toggle);
            Status("Assignment staged. Save before exporting to a review folder.");
        });
        private void ClearClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (_session is not null && _rows.Any(x => x.DeviceId == Device?.Id && x.CommandId == Command?.Id && x.Slot == Slot && x.Input == _assignmentsView.InputKey.Text.Trim()))
                _session.Clear(Command!.Id, Slot);
        });
        private void AddModifierClicked(object sender, RoutedEventArgs e)
        {
            if (_assignmentsView.ModifierDevicePicker.SelectedItem is not GameDeviceChoice device || string.IsNullOrWhiteSpace(_assignmentsView.ModifierKey.Text)) return;
            _modifiers.Add(new(new() { Source = device.Id, Value = _assignmentsView.ModifierKey.Text.Trim() }, device.Name + ": " + _assignmentsView.ModifierKey.Text.Trim()));
            _assignmentsView.ModifiersList.Items.Refresh(); _assignmentsView.ModifierKey.Clear();
        }
        private void RemoveModifierClicked(object sender, RoutedEventArgs e)
        { if (_assignmentsView.ModifiersList.SelectedItem is EliteModifierChoice modifier) { _modifiers.Remove(modifier); _assignmentsView.ModifiersList.Items.Refresh(); } }
        private void Rescan() => Run(() => { StopLiveInput(); _devicesView.ConnectedPicker.ItemsSource = _services.Discover().Where(x => x.Connected && EditionAccess.Policy.AllowsDevice(x)).ToArray(); _devicesView.ConnectedPicker.SelectedIndex = -1; });
        private void RescanClicked(object sender, RoutedEventArgs e) => Rescan();
        private void AddControllerClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            StopLiveInput();
            if (_session is null || _devicesView.ConnectedPicker.SelectedItem is not DetectedDcsDevice connected)
                throw new InvalidOperationException("Choose the connected controller you want to add.");
            var document = _session.Snapshot;
            var already = document.Hardware.FirstOrDefault(x => x.Value.Device.PhysicalGuid == connected.PhysicalGuid).Key;
            if (already is not null)
            {
                _devicesView.ReferenceChoiceTools.Visibility = Visibility.Collapsed;
                DevicePicker.SelectedItem = DevicePicker.Items.Cast<GameDeviceChoice>().Single(x => x.Id == already);
                Status("This controller is already in the profile. Its existing link and drawing choice are retained."); return;
            }
            var references = _session.ControllerReferences(connected.PhysicalGuid);
            var available = references.Where(x => !document.Hardware.ContainsKey(x)).ToArray();
            if (references.Count == 0 || available.Length == 1) { ConnectController(connected, available.SingleOrDefault()); return; }
            _devicesView.ReferencePicker.ItemsSource = references.Select(id => new GameDeviceChoice(id, ControllerName(document, id))).ToArray();
            _devicesView.ReferencePicker.SelectedIndex = -1; _devicesView.ReferenceChoiceTools.Visibility = Visibility.Visible;
            Status("Choose which captured ED reference belongs to this controller. Existing links are retained until an explicit choice succeeds.");
        });
        private void ConnectController(DetectedDcsDevice connected, string? logicalId)
        {
            StopLiveInput();
            if (_session is null) return;
            var current = logicalId is null ? null : _session.Snapshot.Hardware.GetValueOrDefault(logicalId);
            var drawing = current is null ? GameControllerDrawings.Suggest(_hardware.Where(x => EditionAccess.Policy.AllowsHardware(x.DeviceId)), connected) : current.HardwareId;
            var id = _session.ConnectController(connected.PhysicalGuid, drawing, logicalId);
            _devicesView.ReferenceChoiceTools.Visibility = _devicesView.DrawingTools.Visibility = Visibility.Collapsed;
            DevicePicker.SelectedItem = DevicePicker.Items.Cast<GameDeviceChoice>().Single(x => x.Id == id);
            Status("Controller connected. Assign commands now; Change drawing is optional." + (_session.AccessMessage.Length > 0 ? " " + _session.AccessMessage : ""));
        }
        private static string ControllerName(GameWorkspace workspace, string id)
        {
            var identity = workspace.Profile.Devices.Single(x => x.LogicalId == id).Identity!;
            var token = identity.SimSpecificIds["ed.device"];
            var suffix = identity.SimSpecificIds.TryGetValue("ed.deviceIndex", out var index) ? " · ED index " + index : "";
            return workspace.Hardware.TryGetValue(id, out var pin) ? pin.Device.Name + " · " + token + suffix : token + suffix;
        }
        private void ConnectedChanged(object sender, SelectionChangedEventArgs e)
        { if (!_refreshing && _devicesView.ReferenceChoiceTools is not null) { _devicesView.ReferenceChoiceTools.Visibility = Visibility.Collapsed; Run(UpdateReplacementReferences); UpdateAvailability(); } }
        private void UpdateReplacementReferences()
        {
            if (_devicesView.ReplacementReferencePicker is null) return;
            var selected = (_devicesView.ReplacementReferencePicker.SelectedItem as GameDeviceChoice)?.Id;
            var ids = _session is not null && _devicesView.ConnectedPicker.SelectedItem is DetectedDcsDevice connected
                ? _session.ControllerReferences(connected.PhysicalGuid) : [];
            var document = _session?.Snapshot;
            var choices = ids.Select(id => new GameDeviceChoice(id, ControllerName(document!, id))).ToArray();
            _devicesView.ReplacementReferencePicker.ItemsSource = choices;
            _devicesView.ReplacementReferencePicker.SelectedItem = choices.FirstOrDefault(x => x.Id == selected);
            _devicesView.ReplacementReferenceTools.Visibility = choices.Length > 1 ? Visibility.Visible : Visibility.Collapsed;
        }
        private void RemoveControllerClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            StopLiveInput();
            if (_session is null || Device is null) throw new InvalidOperationException("Select the profile controller to remove.");
            ReviewControllerChange(_session.PreviewRemoveController(Device.Id));
        });
        private void ReplaceControllerClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            StopLiveInput();
            if (_session is null || Device is null || _devicesView.ConnectedPicker.SelectedItem is not DetectedDcsDevice connected)
                throw new InvalidOperationException("Select the old profile controller and its connected replacement.");
            var target = (_devicesView.ReplacementReferencePicker.SelectedItem as GameDeviceChoice)?.Id;
            var drawing = GameControllerDrawings.Suggest(_hardware.Where(x => EditionAccess.Policy.AllowsHardware(x.DeviceId)), connected);
            var change = _session.PreviewReplaceController(Device.Id, connected.PhysicalGuid,
                () => ReadReplacementInputs(connected.PhysicalGuid), target, drawing);
            ReviewControllerChange(change);
        });
        private string[] ReadReplacementInputs(Guid physicalGuid)
        {
            EditionAccess.Policy.RequireOwner();
            using var source = _controllerInputs(new WindowInteropHelper(this).EnsureHandle(), [physicalGuid]);
            if (!source.SupportedInputs.TryGetValue(physicalGuid, out var inputs))
                throw new InvalidOperationException("Replacement inputs are unavailable. Rescan and review again.");
            return inputs.Select(EliteInputKeys.FromShared).Where(x => x is not null).Select(x => x!).Distinct(StringComparer.Ordinal).ToArray();
        }
        private void ReviewControllerChange(GameControllerChange change)
        {
            if (!_reviewControllerChange(change)) { Status("Controller change cancelled; profile unchanged."); return; }
            _session!.ApplyControllerChange(change);
            if (change.ResultDeviceId is { } id) DevicePicker.SelectedItem = DevicePicker.Items.Cast<GameDeviceChoice>().Single(x => x.Id == id);
            _devicesView.ReferenceChoiceTools.Visibility = _devicesView.DrawingTools.Visibility = Visibility.Collapsed;
            Status("Reviewed controller change staged. Save to persist; Preview / Export to review the native ED bindings.");
        }
        private void LinkReferenceClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (_devicesView.ConnectedPicker.SelectedItem is not DetectedDcsDevice connected || _devicesView.ReferencePicker.SelectedItem is not GameDeviceChoice reference)
                throw new InvalidOperationException("Choose the connected controller and its captured ED reference.");
            ConnectController(connected, reference.Id);
        });
        private void ChangeDrawingClicked(object sender, RoutedEventArgs e)
        {
            if (_session is null || Device is null || !_session.Snapshot.Hardware.TryGetValue(Device.Id, out var pin)) return;
            _devicesView.HardwarePicker.SelectedItem = _hardware.FirstOrDefault(x => x.DeviceId == pin.HardwareId);
            _devicesView.DrawingTools.Visibility = Visibility.Visible;
        }
        private void UseDrawingClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (_session is null || Device is null || _devicesView.HardwarePicker.SelectedItem is not WorkspaceHardware drawing)
                throw new InvalidOperationException("Choose an optional drawing first.");
            _session.ChangeDrawing(Device.Id, drawing.DeviceId); _devicesView.DrawingTools.Visibility = Visibility.Collapsed;
        });
        private void NoDrawingClicked(object sender, RoutedEventArgs e) => Run(() =>
        { if (_session is not null && Device is not null) { _session.ChangeDrawing(Device.Id, null); _devicesView.DrawingTools.Visibility = Visibility.Collapsed; } });
        private void ImportClicked(object sender, RoutedEventArgs e)
        {
            if (!ConfirmLeave()) return;
            Run(() => { var path = _importPicker is null ? PickImport() : _importPicker(); if (path is not null) { ImportFile(path); ProfilePicker.SelectedIndex = -1; } });
        }
        private string? PickImport()
        {
            var dialog = new OpenFileDialog { Filter = "Elite bindings (*.binds)|*.binds", Title = "Import Elite controls preset",
                InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Frontier Developments", "Elite Dangerous", "Options", "Bindings") };
            return dialog.ShowDialog(this) == true ? dialog.FileName : null;
        }
        private void SaveClicked(object sender, RoutedEventArgs e) => SaveProfile();
        private void DiscardClicked(object sender, RoutedEventArgs e)
        {
            _session?.Discard();
            if (_session is not null) { _refreshing = true; ProfileName.Text = _session.Snapshot.Name; _refreshing = false; UpdateAvailability(); }
        }
        private void ExportClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (_session is null || Pending) return;
            StopLiveInput();
            var issues = _session.ReviewGameExport();
            if (issues.Count > 0) { ResolveExportIssue(issues); return; }
            _session.ExportForGame();
            var parent = _exportPicker is null ? PickExportFolder() : _exportPicker();
            if (parent is null) return;
            // Discovery and validation run again after the folder dialog, before any output directory is created.
            var plan = _session.ExportForGame();
            var active = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Frontier Developments", "Elite Dangerous", "Options", "Bindings");
            var output = GameReviewExport.Write(parent, plan.Files, [active]);
            ExportState.Text = "Exported for review: " + output + "\nInstallation into ED is manual. With ED closed, back up Bindings, copy the reviewed preset there, restart ED and select " + _session.Snapshot.Name + ".";
            Status("Exported for review; active ED bindings unchanged. " + string.Join(" ", plan.Diagnostics.Select(x => x.Message)));

        });
        private void ResolveExportIssue(IReadOnlyList<GameExportIssue> issues)
        {
            GameExportResolution? resolution;
            if (ExportReview is not null) resolution = ExportReview(issues);
            else
            {
                var connected = _services.Discover().Where(x => x.Connected && EditionAccess.Policy.AllowsDevice(x)).ToArray();
                var dialog = new EliteExportReviewWindow(this, issues, connected, guid => _session!.ControllerReferences(guid)
                    .Select(id => new GameDeviceChoice(id, ControllerName(_session.Snapshot, id))).ToArray(), EditionAccess.Policy.IsOwner);
                resolution = dialog.ShowDialog() == true ? dialog.Resolution : null;
            }
            if (resolution is null) { Status("Export cancelled; no files written."); return; }
            if (!issues.Any(x => x.DeviceId == resolution.DeviceId && x.CanRepair)) throw new InvalidOperationException("Choose an editable unresolved controller reference.");
            var change = resolution.Clear ? _session!.PreviewRemoveController(resolution.DeviceId)
                : _session!.PreviewReplaceController(resolution.DeviceId, resolution.PhysicalGuid,
                    () => ReadReplacementInputs(resolution.PhysicalGuid), resolution.TargetReference);
            ReviewControllerChange(change);
            if (_session!.HasChanges) Status("Reviewed repair staged; no files exported. Save, then export again to check remaining references.");
        }
        private string? PickExportFolder()
        {
            var picker = new OpenFolderDialog { Title = "Choose a parent folder for a new review export" };
            return picker.ShowDialog(this) == true ? picker.FolderName : null;
        }
        private void ExportSheetClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (_session is null || Device is null) return;
            var dialog = new SaveFileDialog { Filter = "PNG reference sheet (*.png)|*.png", FileName = "Elite-controller-reference.png" };
            if (dialog.ShowDialog(this) == true) { ExportReference(dialog.FileName); Status("Reference sheet exported."); }
        });

        private bool ConfirmLeave()
        {
            StopLiveInput(); if (!Pending) return true;
            var choice = _pendingChoice?.Invoke() ?? ThemeMessageBox.Show(this, "Save this profile before leaving? Yes saves, No discards pending edits, Cancel stays here.", "Unsaved game profile", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
            if (choice == MessageBoxResult.Yes) return SaveProfile();
            if (choice != MessageBoxResult.No) return false;
            _session?.Discard(); return true;
        }
        private void ConfirmClose(object? sender, CancelEventArgs e) { if (!_allowClose) e.Cancel = !ConfirmLeave(); }
        private void GameChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing || GamePicker.SelectedIndex != 0) return;
            if (!ConfirmLeave()) { _refreshing = true; GamePicker.SelectedIndex = 1; _refreshing = false; return; }
            DcsRequested = true; _allowClose = true; Close();
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
        private void ProfileNameChanged(object sender, TextChangedEventArgs e) { if (!_refreshing && SaveState is not null) { _referenceDirty = true; UpdateAvailability(); } }
        private void SectionChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing && _session is not null) Run(() => { _referenceDirty = true; FilterCommands(); RenderDevice(); }); }
        private void DeviceChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing && _session is not null) Run(() => { _referenceDirty = true; _devicesView.DrawingTools.Visibility = Visibility.Collapsed; FilterCommands(Command?.Id); RenderDevice(); }); }
        private void FeatureChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_input is null || _identification is null) return;
            StopLiveInput();
            UserControl view = FeaturePicker.SelectedIndex switch { 1 => _assignmentsView, 2 => _referenceView, _ => _devicesView };
            FeatureContent.Content = view; FeatureActions.ShowActions(view); UpdateAvailability();
        }
        private void SearchChanged(object sender, TextChangedEventArgs e) { if (!_refreshing) FilterCommands(Command?.Id); }
        private void CommandFilterChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing) FilterCommands(Command?.Id); }
        private void ClearCommandFiltersClicked(object sender, RoutedEventArgs e) => ClearCommandFilters(Command?.Id);
        private void ClearCommandFilters(string? selected)
        {
            _refreshing = true; _assignmentsView.CommandCategoryPicker.SelectedIndex = 0; _assignmentsView.CommandTypePicker.SelectedIndex = 0; _assignmentsView.CommandSearch.Clear(); _refreshing = false;
            FilterCommands(selected);
        }
        private void CommandChanged(object sender, SelectionChangedEventArgs e) => LoadAssignment();
        private void SlotChanged(object sender, SelectionChangedEventArgs e) => LoadAssignment();
        private void AssignmentSelected(object sender, SelectionChangedEventArgs e) { if (!_refreshing && _assignmentsView.AssignmentsGrid.SelectedItem is EliteAssignmentRow row) SelectRow(row); }
        private void SelectRow(EliteAssignmentRow row)
        {
            if (!_assignmentsView.CommandsList.Items.Cast<SimCommand>().Any(x => x.Id == row.CommandId))
            {
                ClearCommandFilters(row.CommandId);
                Status("Command filters cleared to show the selected assignment.");
            }
            _refreshing = true; _assignmentsView.CommandsList.SelectedItem = _assignmentsView.CommandsList.Items.Cast<SimCommand>().FirstOrDefault(x => x.Id == row.CommandId); _assignmentsView.SlotPicker.SelectedIndex = row.Slot == "Secondary" ? 1 : 0; _refreshing = false;
            LoadAssignment();
        }
        private void UpdateInputActivityVisibility()
        {
            var fallback = FeaturePicker.SelectedIndex == 1 && _assignmentsView.DiagramHint.Visibility == Visibility.Visible;
            _assignmentsView.InputActivity.Visibility = _assignmentsView.InputActivityHint.Visibility = fallback ? Visibility.Visible : Visibility.Collapsed;
            _assignmentsView.DiagramScroll.Visibility = fallback ? Visibility.Collapsed : Visibility.Visible;
        }
        private void ShowInputActivity(string text)
        {
            if (_assignmentsView.InputActivity.Text == text) return;
            _assignmentsView.InputActivity.Text = text; _assignmentsView.InputActivity.ScrollToEnd();
        }
        private void StartInputClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            _identification.Stop();
            if (_session is null || Device is null || !_session.Snapshot.Hardware.TryGetValue(Device.Id, out var pin))
                throw new InvalidOperationException("On Devices, select this reference and use Identify Controller to discover and confirm its physical controller first.");
            _input.Start(pin, _services.Discover());
        });
        private void InvalidateReferenceSheets()
        { _referenceDirty = true; if (FeaturePicker.SelectedIndex == 2) RefreshReferenceSheets(); }
        private void ReferenceOptionsChanged(object sender, SelectionChangedEventArgs e)
        { if (!_refreshing && !_renderingReference) InvalidateReferenceSheets(); }
        private void ReferenceAssignedChanged(object sender, RoutedEventArgs e) => InvalidateReferenceSheets();
        private void RefreshReferenceSheets()
        {
            if (_renderingReference || !_referenceDirty) return;
            _renderingReference = true; _referenceSheets = null; _referencePage = 0;
            try
            {
                if (_session is null || Device is null) throw new InvalidOperationException("Import a controls profile and choose a controller on the left.");
                _session.Export();
                var document = _session.Snapshot;
                var pin = document.Hardware.GetValueOrDefault(Device.Id);
                var hardware = pin is not null && !pin.DrawingHidden ? _hardware.FirstOrDefault(x => x.DeviceId == pin.HardwareId) : null;
                _referenceView.ReferenceArtworkOption.IsEnabled = hardware is not null;
                if (hardware is null) _referenceView.ReferenceLayoutPicker.SelectedIndex = 1;
                var content = EliteReferenceSheetBuilder.Build(document.Profile, _session.Catalog, Device.Id, Device.Name, Section, _referenceView.ReferenceAssignedOnly.IsChecked == true)
                    with { Title = "Elite Dangerous / " + (string.IsNullOrWhiteSpace(ProfileName.Text) ? document.Name : ProfileName.Text.Trim()) };
                var options = new ReferenceSheetOptions(_referenceView.ReferencePaperPicker.SelectedIndex == 1 ? "Letter" : "A4",
                    _referenceView.ReferenceOrientationPicker.SelectedIndex == 1, _referenceView.ReferenceLayoutPicker.SelectedIndex == 0);
                byte[]? artwork = null;
                if (options.Artwork && hardware is not null)
                {
                    var projection = EliteWorkspaceProjection.Diagram(document.Name, hardware.Diagram, _rows.Where(x => x.DeviceId == Device.Id).ToArray(), Section);
                    artwork = CompositeKneeboardRenderer.Render(hardware.Diagram, projection, "Elite Dangerous / " + document.Name, artworkOpacity: AppearanceManager.ArtworkOpacityFor(this));
                }
                _referenceSheets = ReferenceSheetRenderer.Render(content, options, artwork);
                _referenceView.ReferenceStatus.Text = content.Rows.Count + " entries · current profile preview" + (options.Artwork ? " · artwork followed by the complete assignment table" : " · assignment table");
            }
            catch (Exception error) { _referenceView.ReferenceStatus.Text = error.Message; }
            finally { _referenceDirty = false; _renderingReference = false; ShowReferencePage(); }
        }
        private void ShowReferencePage()
        {
            var ready = _referenceSheets is not null;
            _referenceView.ReferencePdfButton.IsEnabled = _referenceView.ReferencePngButton.IsEnabled = ready;
            _referenceView.ReferencePreviousButton.IsEnabled = ready && _referencePage > 0;
            _referenceView.ReferenceNextButton.IsEnabled = ready && _referencePage + 1 < _referenceSheets!.PageCount;
            _referenceView.ReferencePageLabel.Text = ready ? "Page " + (_referencePage + 1) + " of " + _referenceSheets!.PageCount : "No preview";
            _referenceView.ReferencePreview.Source = null;
            if (!ready) return;
            using var stream = new MemoryStream(_referenceSheets!.PngPages[_referencePage]);
            var image = new System.Windows.Media.Imaging.BitmapImage();
            image.BeginInit(); image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze();
            _referenceView.ReferencePreview.Source = image;
        }
        private void ReferencePreviousClicked(object sender, RoutedEventArgs e)
        { if (_referencePage > 0) { _referencePage--; ShowReferencePage(); } }
        private void ReferenceNextClicked(object sender, RoutedEventArgs e)
        { if (_referenceSheets is not null && _referencePage + 1 < _referenceSheets.PageCount) { _referencePage++; ShowReferencePage(); } }
        private void ReferencePdfClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            var data = ReferencePdf();
            var dialog = new SaveFileDialog { Filter = "PDF reference (*.pdf)|*.pdf", FileName = "Elite-controller-reference.pdf" };
            if (dialog.ShowDialog(this) == true) { File.WriteAllBytes(dialog.FileName, data); Status("Reference PDF exported."); }
        });
        private void ReferencePngClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            var data = ReferencePng();
            var dialog = new SaveFileDialog { Filter = "PNG reference page (*.png)|*.png", FileName = "Elite-reference-page-" + (_referencePage + 1) + ".png" };
            if (dialog.ShowDialog(this) == true) { File.WriteAllBytes(dialog.FileName, data); Status("Current reference page exported."); }
        });
        private void FitDiagramClicked(object sender, RoutedEventArgs e) => _viewport.Fit();
        private void StopInputClicked(object sender, RoutedEventArgs e) { StopLiveInput(); Status("Input test stopped."); }
        #endregion END private helpers
    }
}

