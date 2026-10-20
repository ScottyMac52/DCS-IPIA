using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
            _input = new(this, _diagram, Status, inputFactory);
            _refreshing = true;
            GamePicker.SelectedIndex = 1; SectionPicker.SelectedIndex = 0; SlotPicker.SelectedIndex = 0; ModePicker.SelectedIndex = 0;
            HardwarePicker.ItemsSource = hardware.Where(x => EditionAccess.Policy.AllowsHardware(x.DeviceId)).ToArray();
            _refreshing = false;
            ReloadChoices(initial);
            FeaturePicker.SelectedIndex = 0;
            CommandsList.PreviewMouseMove += DragCommand;
            Closing += ConfirmClose; Closed += (_, _) => Dispose();
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
        #endregion END Public
        #region BEGIN private helpers
        private string Section => (SectionPicker.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "All";
        private GameDeviceChoice? Device => DevicePicker.SelectedItem as GameDeviceChoice;
        private SimCommand? Command => CommandsList.SelectedItem as SimCommand;
        private string Slot => Command?.Type == CommandType.Axis ? "Binding" : SlotPicker.SelectedIndex == 1 ? "Secondary" : "Primary";
        private bool Pending => _session is not null && (_revision is null || _session.HasChanges || ProfileName.Text.Trim() != _session.Snapshot.Name);
        private void Status(string message) => WorkspaceStatus.Text = message;
        private void Run(Action action) { try { action(); } catch (Exception error) { Status(error.Message); } }
        private void OpenSession(GameWorkspace workspace, string? revision)
        {
            _input.Stop();
            var opened = new GameProfileSession(_services.Backend, workspace, EditionAccess.Policy, _hardware.Select(x => x.DeviceId).ToArray(), _services.Discover);
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
            var devices = document.Profile.Devices.Select(x => new GameDeviceChoice(x.LogicalId,
                document.Hardware.TryGetValue(x.LogicalId, out var pin) ? pin.Device.Name :
                x.Identity!.SimSpecificIds["ed.device"] + (x.Identity.SimSpecificIds.TryGetValue("ed.deviceIndex", out var index) ? " [" + index + "]" : ""))).ToArray();
            DevicePicker.ItemsSource = devices; DevicePicker.SelectedItem = devices.FirstOrDefault(x => x.Id == previousDevice) ?? devices.FirstOrDefault();
            ModifierDevicePicker.ItemsSource = devices; ModifierDevicePicker.SelectedIndex = 0;
            _refreshing = false;
            FilterCommands(previousCommand); RenderDevice(); UpdateAvailability();
        }
        private void FilterCommands(string? selected = null)
        {
            if (_session is null) return;
            var commands = _commands.Where(x => (Section == "All" || x.Category == Section)
                && x.DisplayName.Contains(CommandSearch.Text, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.DisplayName, StringComparer.Ordinal).ToArray();
            _refreshing = true; CommandsList.ItemsSource = commands;
            CommandsList.SelectedItem = commands.FirstOrDefault(x => x.Id == selected) ?? commands.FirstOrDefault(); _refreshing = false;
            AssignmentsGrid.ItemsSource = _rows.Where(x => x.DeviceId == Device?.Id && (Section == "All" || x.Section == Section)).ToArray();
            LoadAssignment();
        }
        private void RenderDevice()
        {
            _input.Stop(); DiagramCanvas.Children.Clear(); _diagram.Regions.Clear(); DiagramHint.Visibility = Visibility.Visible;
            if (_session is null || Device is null) return;
            var document = _session.Snapshot;
            var hardware = document.Hardware.TryGetValue(Device.Id, out var pin) ? _hardware.FirstOrDefault(x => x.DeviceId == pin.HardwareId) : null;
            if (hardware is null) { DiagramHint.Text = "Choose this device's shared hardware drawing on Devices. You can review its native assignments below."; return; }
            var rows = _rows.Where(x => x.DeviceId == Device.Id).ToArray();
            var page = EliteWorkspaceProjection.Diagram(document.Name, hardware.Diagram, rows, Section);
            _diagram.Show(hardware.Diagram, page, SelectControl, DropCommand, () => ClearClicked(this, new()));
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
            AssignmentEditor.IsEnabled = opened && AccessStatus.Text.Length == 0;
            SaveProfileButton.IsEnabled = opened && Pending; DiscardProfileButton.IsEnabled = opened && Pending;
            ExportPresetButton.IsEnabled = opened && !Pending && AccessStatus.Text.Length == 0;
            SaveState.Text = !opened ? "No profile" : Pending ? "Unsaved changes" : "Saved";
            ProfileName.IsEnabled = opened; AssociateButton.IsEnabled = opened;
            var feature = FeaturePicker.SelectedIndex;
            DeviceTools.Visibility = feature == 0 ? Visibility.Visible : Visibility.Collapsed;
            StartInputButton.Visibility = StopInputButton.Visibility = feature == 2 ? Visibility.Visible : Visibility.Collapsed;
            StartInputButton.IsEnabled = opened && EditionAccess.Policy.IsOwner;
            ExportSheetButton.Visibility = feature == 3 ? Visibility.Visible : Visibility.Collapsed;
            ExportSheetButton.IsEnabled = opened && AccessStatus.Text.Length == 0;
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
        private void Rescan() => Run(() => { ConnectedPicker.ItemsSource = _services.Discover().Where(x => x.Connected && EditionAccess.Policy.AllowsDevice(x)).ToArray(); ConnectedPicker.SelectedIndex = 0; });
        private void RescanClicked(object sender, RoutedEventArgs e) => Rescan();
        private void AssociateClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (_session is not null && Device is { } device && HardwarePicker.SelectedItem is WorkspaceHardware hardware && ConnectedPicker.SelectedItem is DetectedDcsDevice connected)
                _session.Associate(device.Id, hardware.DeviceId, connected.PhysicalGuid);
        });
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
            _session.Export();
            var document = _session.Snapshot;
            if (!document.Hardware.TryGetValue(Device.Id, out var pin)) throw new InvalidOperationException("Associate a shared drawing first.");
            var hardware = _hardware.Single(x => x.DeviceId == pin.HardwareId);
            var page = EliteWorkspaceProjection.Diagram(document.Name, hardware.Diagram, _rows.Where(x => x.DeviceId == Device.Id).ToArray(), Section);
            var dialog = new SaveFileDialog { Filter = "PNG reference sheet (*.png)|*.png", FileName = "Elite-controller-reference.png" };
            if (dialog.ShowDialog(this) == true) { File.WriteAllBytes(dialog.FileName, CompositeKneeboardRenderer.Render(hardware.Diagram, page, "Elite Dangerous · " + document.Name)); Status("Reference sheet exported."); }
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
            if (ConfirmLeave()) Run(() => LoadProfile(entry.Id));
            else { _refreshing = true; ProfilePicker.SelectedItem = ProfilePicker.Items.Cast<GameWorkspaceEntry>().FirstOrDefault(x => x.Id == _session?.Snapshot.Id); _refreshing = false; }
        }
        private void ProfileNameChanged(object sender, TextChangedEventArgs e) { if (!_refreshing && SaveState is not null) UpdateAvailability(); }
        private void SectionChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing && _session is not null) Run(() => { FilterCommands(); RenderDevice(); }); }
        private void DeviceChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing && _session is not null) Run(() => { FilterCommands(Command?.Id); RenderDevice(); }); }
        private void FeatureChanged(object sender, SelectionChangedEventArgs e) { if (_input is null) return; _input.Stop(); UpdateAvailability(); }
        private void SearchChanged(object sender, TextChangedEventArgs e) { if (!_refreshing) FilterCommands(Command?.Id); }
        private void CommandChanged(object sender, SelectionChangedEventArgs e) => LoadAssignment();
        private void SlotChanged(object sender, SelectionChangedEventArgs e) => LoadAssignment();
        private void AssignmentSelected(object sender, SelectionChangedEventArgs e) { if (!_refreshing && AssignmentsGrid.SelectedItem is EliteAssignmentRow row) SelectRow(row); }
        private void SelectRow(EliteAssignmentRow row)
        {
            _refreshing = true; CommandsList.SelectedItem = CommandsList.Items.Cast<SimCommand>().FirstOrDefault(x => x.Id == row.CommandId); SlotPicker.SelectedIndex = row.Slot == "Secondary" ? 1 : 0; _refreshing = false;
            LoadAssignment();
        }
        private void StartInputClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (_session is null || Device is null || !_session.Snapshot.Hardware.TryGetValue(Device.Id, out var pin))
                throw new InvalidOperationException("Associate this native reference with an exact connected controller first.");
            _input.Start(pin, _services.Discover());
        });
        private void StopInputClicked(object sender, RoutedEventArgs e) { _input.Stop(); Status("Input test stopped."); }
        #endregion END private helpers
    }
}
