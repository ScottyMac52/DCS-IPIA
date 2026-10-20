using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using BindingMode = Dcs.Ipia.Domain.MultiSim.BindingMode;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Games;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Domain.MultiSim;
using Dcs.Ipia.EliteDangerous;
using Dcs.Ipia.Infrastructure;
using Dcs.Ipia.Rendering;
using Microsoft.Win32;

namespace DcsIpia.Presentation.Games
{
    /// <summary>Hosts game-owned profile selection, native assignments and shared hardware artwork.</summary>
    public sealed partial class EliteWorkspaceWindow : Window, IDisposable
    {
        private readonly GameWorkspaceServices _services;
        private readonly IReadOnlyList<WorkspaceHardware> _hardware;
        private readonly EliteDiagramPresenter _diagram;
        private readonly DiagramViewport _viewport;
        private readonly EliteInputFeedback _input;
        private readonly Func<string?>? _importPicker;
        private readonly Func<string?>? _exportPicker;
        private readonly Func<MessageBoxResult>? _pendingChoice;
        private readonly List<EliteModifierChoice> _modifiers = [];
        private GameProfileSession? _session;
        private string? _revision;
        private bool _refreshing;
        private bool _allowClose;
        private IReadOnlyList<EliteAssignmentRow> _rows = [];
        private IReadOnlyList<SimCommand> _commands = [];
        /// <summary>Creates an isolated editor over the existing adapter and profile-owned services.</summary>
        public EliteWorkspaceWindow(GameWorkspaceServices services, IReadOnlyList<WorkspaceHardware> hardware,
            Guid? initial = null, Func<string?>? importPicker = null, Func<string?>? exportPicker = null,
            Func<MessageBoxResult>? pendingChoice = null, Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? inputFactory = null)
        {
            _services = services; _hardware = hardware; _importPicker = importPicker; _exportPicker = exportPicker; _pendingChoice = pendingChoice;
            InitializeComponent();
            _diagram = new(DiagramCanvas);
            _viewport = new(DiagramScroll, DiagramCanvas, DiagramZoom, "elite.diagram");
            _input = new(this, _diagram, Status, inputFactory);
            _refreshing = true;
            GamePicker.SelectedIndex = 1; SectionPicker.SelectedIndex = 0; SlotPicker.SelectedIndex = 0; ModePicker.SelectedIndex = 0;
            CommandCategoryPicker.ItemsSource = new[] { "All categories" }; CommandCategoryPicker.SelectedIndex = 0; CommandTypePicker.SelectedIndex = 0;
            HardwarePicker.ItemsSource = hardware.Where(x => EditionAccess.Policy.AllowsHardware(x.DeviceId)).ToArray();
            _refreshing = false;
            ReloadChoices(initial);
            FeaturePicker.SelectedIndex = 0;
            CommandsList.PreviewMouseMove += DragCommand;
            Closing += ConfirmClose; Closed += (_, _) => Dispose();
            AppearanceManager.ObserveArtwork(this, () => Run(RenderDevice));
            Rescan(); UpdateAvailability();
        }
        #region BEGIN Public
        /// <summary>Releases native input handles and draft event subscriptions.</summary>
        public void Dispose()
        {
            _input.Dispose(); if (_session is not null) _session.Changed -= SessionChanged; GC.SuppressFinalize(this);
        }
        /// <summary>Gets an explicit request to return to the DCS landing surface.</summary>
        public bool DcsRequested { get; private set; }
        /// <summary>Gets the current staged session for deterministic Windows interaction checks.</summary>
        internal GameProfileSession? Session => _session;
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
                Status("Saved in your IPIA data profile. Active Elite bindings are unchanged." + (_services.SelectionError is { } error ? " Recent selection was not saved: " + error : "")); return true;
            }
            catch (Exception error) { Status("Save failed; pending edits are retained: " + error.Message); return false; }
        }
        /// <summary>Exports the same authored shared-drawing projection after edition validation.</summary>
        internal void ExportReference(string path)
        {
            if (_session is null || Device is null) throw new InvalidOperationException("Select a profile and associated device first.");
            _session.Export();
            var document = _session.Snapshot;
            if (!document.Hardware.TryGetValue(Device.Id, out var pin) || pin.HardwareId is null) throw new InvalidOperationException("Choose an optional drawing before exporting an illustrated reference sheet.");
            var hardware = _hardware.Single(x => x.DeviceId == pin.HardwareId);
            var page = EliteWorkspaceProjection.Diagram(document.Name, hardware.Diagram, _rows.Where(x => x.DeviceId == Device.Id).ToArray(), Section);
            File.WriteAllBytes(path, CompositeKneeboardRenderer.Render(hardware.Diagram, page, "Elite Dangerous · " + document.Name, artworkOpacity: AppearanceManager.ArtworkOpacityFor(this)));
        }
        #endregion END Public
        #region BEGIN private helpers
        private string Section => (SectionPicker.SelectedItem as ListBoxItem)?.Content.ToString() ?? "All";
        private string? CommandCategory => CommandCategoryPicker.SelectedItem is string category && category != "All categories" ? category : null;
        private CommandType? InputType => CommandTypePicker.SelectedIndex switch { 1 => CommandType.Button, 2 => CommandType.Axis, _ => null };
        private GameDeviceChoice? Device => DevicePicker.SelectedItem as GameDeviceChoice;
        private SimCommand? Command => CommandsList.SelectedItem as SimCommand;
        private string Slot => Command?.Type == CommandType.Axis ? "Binding" : SlotPicker.SelectedIndex == 1 ? "Secondary" : "Primary";
        private bool Pending => _session is not null && (_revision is null || _session.HasChanges || ProfileName.Text.Trim() != _session.Snapshot.Name);
        private void Status(string message) => WorkspaceStatus.Text = message;
        private void Run(Action action) { try { action(); } catch (Exception error) { Status(error.Message); } }
        private void OpenSession(GameWorkspace workspace, string? revision)
        {
            _input.Stop();
            ReferenceChoiceTools.Visibility = DrawingTools.Visibility = Visibility.Collapsed;
            var opened = new GameProfileSession(_services.Backend, workspace, EditionAccess.Policy, _hardware.Select(x => x.DeviceId).ToArray(), _services.Discover, _services.DeviceReferences);
            if (_session is not null) _session.Changed -= SessionChanged;
            _session = opened; _revision = revision; _session.Changed += SessionChanged;
            _refreshing = true; ProfileName.Text = workspace.Name; _refreshing = false;
            Refresh();
        }
        private void SessionChanged(object? sender, EventArgs e) { _input.Stop(); Refresh(); }
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
            var devices = document.Profile.Devices.Select(x => new GameDeviceChoice(x.LogicalId, ControllerName(document, x.LogicalId))).ToArray();
            DevicePicker.ItemsSource = devices; DevicePicker.SelectedItem = devices.FirstOrDefault(x => x.Id == previousDevice) ?? devices.FirstOrDefault();
            ModifierDevicePicker.ItemsSource = devices; ModifierDevicePicker.SelectedIndex = 0;
            _refreshing = false;
            FilterCommands(previousCommand); RenderDevice(); UpdateAvailability();
        }
        private void FilterCommands(string? selected = null)
        {
            if (_session is null) return;
            var previousCategory = CommandCategory;
            var categories = EliteCommandBrowser.Categories(_commands, Section).Prepend("All categories").ToArray();
            _refreshing = true;
            CommandCategoryPicker.ItemsSource = categories;
            CommandCategoryPicker.SelectedItem = categories.Contains(previousCategory) ? previousCategory : categories[0];
            var commands = EliteCommandBrowser.Filter(_commands, Section, CommandCategory, InputType, CommandSearch.Text);
            var view = new ListCollectionView(commands.ToList());
            view.GroupDescriptions.Add(new PropertyGroupDescription("Metadata[ed.category]"));
            CommandsList.ItemsSource = view;
            CommandsList.SelectedItem = commands.FirstOrDefault(x => x.Id == selected) ?? (commands.Count > 0 ? commands[0] : null); _refreshing = false;
            CommandCount.Text = commands.Count + " of " + _commands.Count + " commands";
            CommandEmptyHint.Visibility = commands.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            AssignmentsGrid.ItemsSource = _rows.Where(x => x.DeviceId == Device?.Id && (Section == "All" || x.Section == Section)).ToArray();
            LoadAssignment(); UpdateAvailability();
        }
        private void RenderDevice()
        {
            _input.Stop(); DiagramCanvas.Children.Clear(); _diagram.Regions.Clear(); DiagramHint.Visibility = Visibility.Visible;
            if (_session is null || Device is null) return;
            var document = _session.Snapshot;
            var hardware = document.Hardware.TryGetValue(Device.Id, out var pin) ? _hardware.FirstOrDefault(x => x.DeviceId == pin.HardwareId) : null;
            if (hardware is null) { DiagramHint.Text = "No drawing selected. Assign commands using native input keys and the assignments table. You can choose an optional drawing on Devices."; return; }
            var rows = _rows.Where(x => x.DeviceId == Device.Id).ToArray();
            var page = EliteWorkspaceProjection.Diagram(document.Name, hardware.Diagram, rows, Section);
            _diagram.Show(hardware.Diagram, page, SelectControl, DropCommand, () => ClearClicked(this, new()), AppearanceManager.ArtworkOpacityFor(this));
            DiagramHint.Visibility = Visibility.Collapsed;
        }
        private void SelectControl(HardwareGraphicControl control)
        {
            var key = EliteInputKeys.FromShared(control.InputKey);
            if (key is null) { Status("This drawing input has no verified Elite key. Choose an explicit native input key instead."); return; }
            var row = _rows.FirstOrDefault(x => x.DeviceId == Device?.Id && x.Input == key && (Section == "All" || x.Section == Section));
            if (row is not null) SelectRow(row);
            InputKey.Text = key; InputKey.Focus();
        }
        private void DropCommand(HardwareGraphicControl control, string id) => Run(() =>
        {
            var command = _commands.Single(x => x.Id == id);
            if (control.IsAxis != (command.Type == CommandType.Axis)) throw new InvalidOperationException("Choose an axis command for an axis, or a button command for a button.");
            CommandsList.SelectedItem = CommandsList.Items.Cast<SimCommand>().First(x => x.Id == id);
            InputKey.Text = EliteInputKeys.FromShared(control.InputKey) ?? throw new InvalidOperationException("Unsupported drawing input; choose an explicit native key.");
            ApplyClicked(this, new());
        });
        private void DragCommand(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && Command is { } command && _session?.AccessMessage.Length == 0)
                DragDrop.DoDragDrop(CommandsList, new DataObject("ipia.elite.command", command.Id), DragDropEffects.Copy);
        }
        private void LoadAssignment()
        {
            if (_refreshing || _session is null) return;
            var axis = Command?.Type == CommandType.Axis;
            AxisTools.Visibility = axis ? Visibility.Visible : Visibility.Collapsed;
            ModifierTools.Visibility = axis ? Visibility.Collapsed : Visibility.Visible;
            SlotPicker.IsEnabled = ModePicker.IsEnabled = !axis;
            var row = _rows.FirstOrDefault(x => x.DeviceId == Device?.Id && x.CommandId == Command?.Id && x.Slot == Slot);
            InputKey.Text = row?.Input ?? ""; InvertAxis.IsChecked = row?.Invert ?? false;
            AxisDeadzone.Text = (row?.Deadzone ?? 0).ToString(CultureInfo.CurrentCulture);
            ModePicker.SelectedIndex = row?.Mode == BindingMode.Hold ? 1 : 0;
            _modifiers.Clear();
            if (row is not null) foreach (var modifier in row.Modifiers) _modifiers.Add(new(modifier,
                (ModifierDevicePicker.Items.Cast<GameDeviceChoice>().FirstOrDefault(x => x.Id == modifier.Source)?.Name ?? modifier.Source) + ": " + modifier.Value));
            ModifiersList.ItemsSource = null; ModifiersList.ItemsSource = _modifiers;
            var key = "ed.toggle:" + Command?.Id;
            var metadata = _session.Snapshot.Profile.Metadata;
            CommandToggle.Visibility = metadata.ContainsKey(key) ? Visibility.Visible : Visibility.Collapsed;
            CommandToggle.IsChecked = metadata.GetValueOrDefault(key) == "1";
        }
        private void UpdateAvailability()
        {
            var opened = _session is not null;
            AccessStatus.Text = _session?.AccessMessage ?? "Import or select a saved Elite controls profile.";
            AssignmentEditor.IsEnabled = Command is not null && AccessStatus.Text.Length == 0;
            SaveProfileButton.IsEnabled = opened && Pending; DiscardProfileButton.IsEnabled = opened && Pending;
            ExportPresetButton.IsEnabled = opened && !Pending && AccessStatus.Text.Length == 0;
            SaveState.Text = !opened ? "No profile" : Pending ? "Unsaved changes" : "Saved";
            ProfileName.IsEnabled = opened; AddControllerButton.IsEnabled = opened && ConnectedPicker.SelectedItem is DetectedDcsDevice;
            var document = _session?.Snapshot;
            var pin = Device is not null ? document?.Hardware.GetValueOrDefault(Device.Id) : null;
            var drawing = _hardware.FirstOrDefault(x => x.DeviceId == pin?.HardwareId);
            ChangeDrawingButton.IsEnabled = pin is not null;
            DrawingStatus.Text = pin is null ? "Select a controller in this profile to view its drawing." : drawing is null ? "No drawing · assignments table available" : "Drawing: " + drawing.Label;
            var feature = FeaturePicker.SelectedIndex;
            DeviceTools.Visibility = feature == 0 ? Visibility.Visible : Visibility.Collapsed;
            StartInputButton.Visibility = StopInputButton.Visibility = feature == 2 ? Visibility.Visible : Visibility.Collapsed;
            StartInputButton.IsEnabled = opened && EditionAccess.Policy.IsOwner;
            ExportSheetButton.Visibility = feature == 3 ? Visibility.Visible : Visibility.Collapsed;
            ExportSheetButton.IsEnabled = opened && drawing is not null && AccessStatus.Text.Length == 0;
        }
        private void ApplyClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (_session is null || Device is null || Command is null) return;
            var command = Command; var device = Device; var slot = Slot; var toggle = CommandToggle.IsChecked == true;
            var hasToggle = CommandToggle.Visibility == Visibility.Visible;
            if (command.Type == CommandType.Axis)
                _session.AssignAxis(device.Id, command.Id, InputKey.Text.Trim(), InvertAxis.IsChecked == true,
                    float.Parse(AxisDeadzone.Text, CultureInfo.CurrentCulture));
            else _session.AssignButton(device.Id, command.Id, slot, InputKey.Text.Trim(), ModePicker.SelectedIndex == 1 ? BindingMode.Hold : BindingMode.Press,
                _modifiers.Select(x => x.Modifier).ToArray());
            if (hasToggle) _session.SetToggle(command.Id, toggle);
            Status("Assignment staged. Save before exporting to a review folder.");
        });
        private void ClearClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (_session is not null && _rows.Any(x => x.DeviceId == Device?.Id && x.CommandId == Command?.Id && x.Slot == Slot && x.Input == InputKey.Text.Trim()))
                _session.Clear(Command!.Id, Slot);
        });
        private void AddModifierClicked(object sender, RoutedEventArgs e)
        {
            if (ModifierDevicePicker.SelectedItem is not GameDeviceChoice device || string.IsNullOrWhiteSpace(ModifierKey.Text)) return;
            _modifiers.Add(new(new() { Source = device.Id, Value = ModifierKey.Text.Trim() }, device.Name + ": " + ModifierKey.Text.Trim()));
            ModifiersList.Items.Refresh(); ModifierKey.Clear();
        }
        private void RemoveModifierClicked(object sender, RoutedEventArgs e)
        { if (ModifiersList.SelectedItem is EliteModifierChoice modifier) { _modifiers.Remove(modifier); ModifiersList.Items.Refresh(); } }
        private void Rescan() => Run(() => { ConnectedPicker.ItemsSource = _services.Discover().Where(x => x.Connected && EditionAccess.Policy.AllowsDevice(x)).ToArray(); ConnectedPicker.SelectedIndex = -1; });
        private void RescanClicked(object sender, RoutedEventArgs e) => Rescan();
        private void AddControllerClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (_session is null || ConnectedPicker.SelectedItem is not DetectedDcsDevice connected)
                throw new InvalidOperationException("Choose the connected controller you want to add.");
            var document = _session.Snapshot;
            var already = document.Hardware.FirstOrDefault(x => x.Value.Device.PhysicalGuid == connected.PhysicalGuid).Key;
            if (already is not null)
            {
                ReferenceChoiceTools.Visibility = Visibility.Collapsed;
                DevicePicker.SelectedItem = DevicePicker.Items.Cast<GameDeviceChoice>().Single(x => x.Id == already);
                Status("This controller is already in the profile. Its existing link and drawing choice are retained."); return;
            }
            var references = _session.ControllerReferences(connected.PhysicalGuid);
            var available = references.Where(x => !document.Hardware.ContainsKey(x)).ToArray();
            if (references.Count == 0 || available.Length == 1) { ConnectController(connected, available.SingleOrDefault()); return; }
            ReferencePicker.ItemsSource = references.Select(id => new GameDeviceChoice(id, ControllerName(document, id))).ToArray();
            ReferencePicker.SelectedIndex = -1; ReferenceChoiceTools.Visibility = Visibility.Visible;
            Status("Choose which captured ED reference belongs to this controller. Existing links are retained until an explicit choice succeeds.");
        });
        private void ConnectController(DetectedDcsDevice connected, string? logicalId)
        {
            if (_session is null) return;
            var current = logicalId is null ? null : _session.Snapshot.Hardware.GetValueOrDefault(logicalId);
            var drawing = current is null ? GameControllerDrawings.Suggest(_hardware.Where(x => EditionAccess.Policy.AllowsHardware(x.DeviceId)), connected) : current.HardwareId;
            var id = _session.ConnectController(connected.PhysicalGuid, drawing, logicalId);
            ReferenceChoiceTools.Visibility = DrawingTools.Visibility = Visibility.Collapsed;
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
        { if (!_refreshing && ReferenceChoiceTools is not null) { ReferenceChoiceTools.Visibility = Visibility.Collapsed; UpdateAvailability(); } }
        private void LinkReferenceClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (ConnectedPicker.SelectedItem is not DetectedDcsDevice connected || ReferencePicker.SelectedItem is not GameDeviceChoice reference)
                throw new InvalidOperationException("Choose the connected controller and its captured ED reference.");
            ConnectController(connected, reference.Id);
        });
        private void ChangeDrawingClicked(object sender, RoutedEventArgs e)
        {
            if (_session is null || Device is null || !_session.Snapshot.Hardware.TryGetValue(Device.Id, out var pin)) return;
            HardwarePicker.SelectedItem = _hardware.FirstOrDefault(x => x.DeviceId == pin.HardwareId);
            DrawingTools.Visibility = Visibility.Visible;
        }
        private void UseDrawingClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (_session is null || Device is null || HardwarePicker.SelectedItem is not WorkspaceHardware drawing)
                throw new InvalidOperationException("Choose an optional drawing first.");
            _session.ChangeDrawing(Device.Id, drawing.DeviceId); DrawingTools.Visibility = Visibility.Collapsed;
        });
        private void NoDrawingClicked(object sender, RoutedEventArgs e) => Run(() =>
        { if (_session is not null && Device is not null) { _session.ChangeDrawing(Device.Id, null); DrawingTools.Visibility = Visibility.Collapsed; } });
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
            var plan = _session.Export();
            var parent = _exportPicker is null ? PickExportFolder() : _exportPicker();
            if (parent is null) return;
            var active = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Frontier Developments", "Elite Dangerous", "Options", "Bindings");
            var output = GameReviewExport.Write(parent, plan.Files, [active]);
            Status("Exported for review: " + output + ". Verify in Elite after backing up your Bindings folder.");
        });
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
            _input.Stop(); if (!Pending) return true;
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
        private void ProfileNameChanged(object sender, TextChangedEventArgs e) { if (!_refreshing && SaveState is not null) UpdateAvailability(); }
        private void SectionChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing && _session is not null) Run(() => { FilterCommands(); RenderDevice(); }); }
        private void DeviceChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing && _session is not null) Run(() => { DrawingTools.Visibility = Visibility.Collapsed; FilterCommands(Command?.Id); RenderDevice(); }); }
        private void FeatureChanged(object sender, SelectionChangedEventArgs e) { if (_input is null) return; _input.Stop(); UpdateAvailability(); }
        private void SearchChanged(object sender, TextChangedEventArgs e) { if (!_refreshing) FilterCommands(Command?.Id); }
        private void CommandFilterChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing) FilterCommands(Command?.Id); }
        private void ClearCommandFiltersClicked(object sender, RoutedEventArgs e) => ClearCommandFilters(Command?.Id);
        private void ClearCommandFilters(string? selected)
        {
            _refreshing = true; CommandCategoryPicker.SelectedIndex = 0; CommandTypePicker.SelectedIndex = 0; CommandSearch.Clear(); _refreshing = false;
            FilterCommands(selected);
        }
        private void CommandChanged(object sender, SelectionChangedEventArgs e) => LoadAssignment();
        private void SlotChanged(object sender, SelectionChangedEventArgs e) => LoadAssignment();
        private void AssignmentSelected(object sender, SelectionChangedEventArgs e) { if (!_refreshing && AssignmentsGrid.SelectedItem is EliteAssignmentRow row) SelectRow(row); }
        private void SelectRow(EliteAssignmentRow row)
        {
            if (!CommandsList.Items.Cast<SimCommand>().Any(x => x.Id == row.CommandId))
            {
                ClearCommandFilters(row.CommandId);
                Status("Command filters cleared to show the selected assignment.");
            }
            _refreshing = true; CommandsList.SelectedItem = CommandsList.Items.Cast<SimCommand>().FirstOrDefault(x => x.Id == row.CommandId); SlotPicker.SelectedIndex = row.Slot == "Secondary" ? 1 : 0; _refreshing = false;
            LoadAssignment();
        }
        private void StartInputClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (_session is null || Device is null || !_session.Snapshot.Hardware.TryGetValue(Device.Id, out var pin))
                throw new InvalidOperationException("Associate this native reference with an exact connected controller first.");
            _input.Start(pin, _services.Discover());
        });
        private void FitDiagramClicked(object sender, RoutedEventArgs e) => _viewport.Fit();
        private void StopInputClicked(object sender, RoutedEventArgs e) { _input.Stop(); Status("Input test stopped."); }
        #endregion END private helpers
    }
}

