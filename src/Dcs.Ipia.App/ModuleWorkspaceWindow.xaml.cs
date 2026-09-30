using System.IO;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Rendering;
using Microsoft.Win32;
using System.Text.Json;

namespace DcsIpia.Presentation
{
    /// <summary>Edits locally persisted module controls on canonical draw.io generated SVGs.</summary>
    public partial class ModuleWorkspaceWindow : Window
    {
        private readonly ModuleWorkspaceViewModel _viewModel;
        private readonly IAppSettingsService? _settings;
        private readonly IEditorWorkspaceRepository? _repository;
        private EditorWorkspaceSnapshot? _saved;
        private DraftAssignmentService? _draft;
        private EditorWorkspaceSnapshot? _working;
        private readonly IConnectedDeviceDiscovery _deviceDiscovery;
        private string _savedGamesRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games");
        private bool _discoveryBusy;
        private bool _structureDirty;
        private bool _managementRefreshing;
        private readonly Dictionary<string, string> _labels = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TextBlock> _calloutText = new(StringComparer.Ordinal);
        private HardwareGraphicControl? _selected;
        private Point? _dragStart;
        private bool _updatingInspector;
        private bool _updatingFilters;
        private bool _building;
        private AssignmentLayer? _categoryScope;
        /// <summary>Opens a saved module editor with a SQLite persistence boundary.</summary>
        /// <param name="viewModel">Persisted editor data or a source preview.</param>
        /// <param name="settings">Occupied destination preference.</param>
        /// <param name="repository">Local editor database.</param>
        /// <param name="deviceDiscovery">Optional native device discovery adapter.</param>
        public ModuleWorkspaceWindow(ModuleWorkspaceViewModel viewModel, IAppSettingsService? settings = null,
                                     IEditorWorkspaceRepository? repository = null, IConnectedDeviceDiscovery? deviceDiscovery = null)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _settings = settings;
            _repository = repository;
            _deviceDiscovery = deviceDiscovery ?? new WindowsConnectedDeviceDiscovery();
            _saved = viewModel.Workspace;
            _working = _saved;
            _draft = viewModel.Draft;
            foreach (var pair in viewModel.Labels)
                _labels[pair.Key] = pair.Value;
            InitializeComponent();
            DataContext = _viewModel;
            WorkspaceSettingsButton.IsEnabled = _settings is not null;
            KneeboardList.SelectedIndex = 0;
            DeviceChoice.ItemsSource = _viewModel.UiDiagrams;
            Layers.ItemsSource = _viewModel.ModuleModifierChoices;
            Layers.SelectedIndex = 0;
            Scope.SelectedIndex = 0;
            CommandType.SelectedIndex = 0;
            BindingState.SelectedIndex = 0;
            DeviceChoice.SelectedIndex = _viewModel.UiDiagrams.Count > 0 ? 0 : -1;
            if (_viewModel.HasErrors)
                ImportedTabs.SelectedItem = DiagnosticsTab;
            DevicesScope.SelectedIndex = 0;
            ModifiersScope.SelectedIndex = 0;
            ModifierModeChoice.ItemsSource = Enum.GetValues<ModifierMode>();
            ModifierModeChoice.SelectedIndex = 0;
            RefreshManagement();
            RefreshDeviceChoices();
            Loaded += async (_, _) => { if (_working is not null) await RefreshDetectedDevicesAsync(); };
            Closing += ConfirmClose;
            RefreshEditor();
            UpdateBuildAvailability();
        }
        #region BEGIN Public
        #endregion END Public
        #region BEGIN private helpers
        private AssignmentLayer DeviceManagementLayer => DevicesScope.SelectedIndex == 1 ? AssignmentLayer.UiLayer : AssignmentLayer.Module;
        private AssignmentLayer ModifierManagementLayer => ModifiersScope.SelectedIndex == 1 ? AssignmentLayer.UiLayer : AssignmentLayer.Module;
        private EditorWorkspaceSnapshot EditSnapshot()
        {
            var workspace = _working ?? _saved ?? throw new InvalidOperationException("Open a saved module to manage devices and modifiers.");
            return workspace with { Assignments = _draft?.Snapshot ?? workspace.Assignments,
                                    Labels = new Dictionary<string, string>(_labels, StringComparer.Ordinal) };
        }
        private void RebuildDraft(EditorWorkspaceSnapshot workspace)
        {
            var commands = workspace.ModuleCommands.Select(x => new DraftCommand(x.Id, x.Name, x.IsAxis, AssignmentLayer.Module))
                .Concat(workspace.UiCommands.Select(x => new DraftCommand(x.DcsCommandId, x.Name, x.IsAxis, AssignmentLayer.UiLayer)));
            _draft = new DraftAssignmentService(workspace.Configuration, commands, workspace.Assignments,
                WorkspaceStructureEditor.UiModifierNames(workspace));
        }
        private string[] ModifierChoicesFor(AssignmentLayer layer)
        {
            if (_working is null)
                return (layer == AssignmentLayer.Module ? _viewModel.ModuleModifierChoices : _viewModel.UiModifierChoices).ToArray();
            return Enumerable.Repeat("Base", 1).Concat(WorkspaceStructureEditor.Describe(_working).Modifiers.Where(x => x.Layer == layer).Select(x => x.Name))
                .Concat(layer == AssignmentLayer.UiLayer ? WorkspaceStructureEditor.UiModifierNames(_working) : [])
                .Concat((_draft?.Snapshot ?? []).Where(x => x.Slot.Layer == layer && x.Slot.ModifierChord.Length > 0)
                    .Select(x => x.Slot.ModifierChord.Replace("+", " + ", StringComparison.Ordinal)))
                .Distinct(StringComparer.Ordinal).ToArray();
        }
        private void RefreshDeviceChoices()
        {
            if (DeviceChoice is null || Scope is null) return;
            var selected = CurrentDiagram?.ProfileId;
            var devices = _working is null ? _viewModel.UiDiagrams : _working.Diagrams.Where(x =>
                WorkspaceStructureEditor.Describe(_working).Devices.Any(d => d.ProfileId == x.ProfileId && d.Layer == CurrentLayer)).ToArray();
            DeviceChoice.ItemsSource = devices;
            DeviceChoice.SelectedItem = devices.FirstOrDefault(x => x.ProfileId == selected) ?? (devices.Count > 0 ? devices[0] : null);
            var layer = Layers.SelectedItem as string ?? "Base";
            var choices = ModifierChoicesFor(CurrentLayer);
            Layers.ItemsSource = choices;
            Layers.SelectedItem = choices.Contains(layer) ? layer : "Base";
        }
        private void RefreshManagement()
        {
            if (ManagedDevices is null || ManagedModifiers is null || _managementRefreshing) return;
            _managementRefreshing = true;
            try
            {
                var workspace = _working;
                var state = workspace is null ? null : WorkspaceStructureEditor.Describe(workspace);
                ManagedDevices.ItemsSource = state?.Devices.Where(x => x.Layer == DeviceManagementLayer).ToArray();
                ModifierRemoveButton.IsEnabled = false;
                ManagedModifiers.ItemsSource = state?.Modifiers.Where(x => x.Layer == ModifierManagementLayer).ToArray();
                ModifierDevice.ItemsSource = state?.Devices.Where(x => x.Layer == ModifierManagementLayer).ToArray();
                ManagedHardware.ItemsSource = workspace is null ? null : WorkspaceStructureEditor.Hardware(workspace);
                if (workspace is not null && workspace.Sources?.ContainsKey("config/ipia-hardware.json") != true)
                    DevicesMessage.Text = "Shared hardware catalog is missing. Click Load built-in hardware, then Save to database. Existing assignments are preserved.";
                if (workspace?.Sources?.TryGetValue("config/ipia-hardware-diagnostics.json", out var diagnostics) == true)
                {
                    var errors = JsonSerializer.Deserialize<string[]>(diagnostics) ?? [];
                    if (errors.Length > 0) DevicesMessage.Text = "Unavailable shared hardware: " + string.Join("; ", errors);
                }

            }
            finally { _managementRefreshing = false; }
        }
        private void ManagementScopeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_managementRefreshing) return;
            RefreshManagement();
            if (ManagedFileName is not null) NewDeviceClicked(sender, new RoutedEventArgs());
            if (ModifierName is not null) NewModifierClicked(sender, new RoutedEventArgs());
        }
        private void SetManagementMessage(string message)
        {
            EditorMessage.Text = message;
            DevicesMessage.Text = message;
            ModifiersMessage.Text = message;
        }
        private void RunStructureEdit(Func<EditorWorkspaceSnapshot, EditorWorkspaceSnapshot> edit)
        {
            try
            {
                var updated = edit(EditSnapshot());
                RebuildDraft(updated);
                _working = updated;
                _structureDirty = true;
                _labels.Clear();
                foreach (var pair in updated.Labels) _labels[pair.Key] = pair.Value;
                RefreshManagement();
                RefreshDeviceChoices();
                RefreshEditor();
                SetManagementMessage("Change staged. Save to database to keep it, or Discard draft.");
                UpdateBuildAvailability();
            }
            catch (Exception ex)
            {
                SetManagementMessage(ex.Message);
            }
        }
        private async void LoadHardwareClicked(object sender, RoutedEventArgs e)
        {
            if (_working is null) return;
            try
            {
                SetManagementMessage("Loading committed shared hardware…");
                var hardware = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
                RunStructureEdit(workspace => {
                    var sources = new Dictionary<string, string>(workspace.Sources ?? new Dictionary<string, string>(), StringComparer.Ordinal);
                    foreach (var pair in hardware)
                        if (pair.Key is "config/ipia-hardware.json" or "config/ipia-device-map.json" or "config/ipia-hardware-diagnostics.json" || !sources.ContainsKey(pair.Key)) sources[pair.Key] = pair.Value;
                    return workspace with { Sources = sources };
                });
                MatchSelectedDetectedDevice();
            }
            catch (Exception ex) { SetManagementMessage("Shared hardware could not be loaded: " + ex.Message); }
        }
        private async void RefreshDetectedDevicesClicked(object sender, RoutedEventArgs e) => await RefreshDetectedDevicesAsync();
        private async void ChooseSavedGamesClicked(object sender, RoutedEventArgs e)
        {
            var picker = new OpenFolderDialog { Title = "Select Saved Games or your DCS folder" };
            if (picker.ShowDialog(this) != true) return;
            _savedGamesRoot = picker.FolderName;
            await RefreshDetectedDevicesAsync();
        }
        private async Task RefreshDetectedDevicesAsync()
        {
            if (_discoveryBusy) return;
            _discoveryBusy = true;
            RefreshDetectedDevicesButton.IsEnabled = false;
            var selected = (DetectedDevice.SelectedItem as DetectedDcsDevice)?.PhysicalGuid;
            try
            {
                DetectedDevicesMessage.Text = "Reading connected controllers and Saved Games profiles…";
                var errors = new List<string>();
                var choices = await Task.Run(() => {
                    IReadOnlyList<DetectedDcsDevice> connected = [];
                    IReadOnlyList<DetectedDcsDevice> profiles = [];
                    try { connected = _deviceDiscovery.Discover(); }
                    catch (Exception ex) { errors.Add("Connected controllers: " + ex.Message); }
                    try { profiles = SavedGamesDeviceDiscovery.Discover(_savedGamesRoot); }
                    catch (Exception ex) { errors.Add("Saved Games: " + ex.Message + " Choose the correct folder above."); }
                    return DcsPhysicalIdentity.Merge(connected, profiles);
                });
                DetectedDevice.ItemsSource = choices;
                DetectedDevice.SelectedItem = choices.FirstOrDefault(x => x.PhysicalGuid == selected);
                DetectedDevicesMessage.Text = choices.Count + " physical device(s). Select a device, then verify shared hardware. " + string.Join(" ", errors);
            }
            finally { _discoveryBusy = false; RefreshDetectedDevicesButton.IsEnabled = true; }
        }
        private void DetectedDeviceChanged(object sender, SelectionChangedEventArgs e) => MatchSelectedDetectedDevice();
        private void MatchSelectedDetectedDevice()
        {
            if (_managementRefreshing || DetectedDevice.SelectedItem is not DetectedDcsDevice device || _working is null) return;
            if (ManagedDevices.SelectedItem is WorkspaceDevice) return;
            var match = DetectedDeviceHardwareMatcher.Match(_working, device);
            ManagedHardware.SelectedItem = match is null ? null :
                (ManagedHardware.ItemsSource as IEnumerable<WorkspaceHardware>)?.SingleOrDefault(x => x.DeviceId == match.DeviceId);
            ManagedFileName.Text = device.FileName;
            DetectedDevicesMessage.Text = match is null
                ? _working.Sources?.ContainsKey("config/ipia-hardware.json") != true
                    ? "Physical GUID found, but the shared hardware catalog is missing. Click Load built-in hardware."
                    : "Physical GUID found. Select the matching shared hardware; the installed grip may require your choice."
                : "Physical GUID found. Shared hardware recognized; filename remains editable.";
        }
        private string NewDeviceFileName()
        {
            var guid = DcsPhysicalIdentity.FromFileName(ManagedFileName.Text.Trim());
            if (guid is null) throw new InvalidOperationException("Select a detected device, or enter its GUID-qualified DCS filename.");
            if (DetectedDevice.SelectedItem is DetectedDcsDevice selected && guid != selected.PhysicalGuid)
                throw new InvalidOperationException("Keep the selected physical device GUID in the filename.");
            return ManagedFileName.Text;
        }
        private void NewDeviceClicked(object sender, RoutedEventArgs e)
        {
            ManagedDevices.SelectedItem = null;
            DetectedDevice.IsEnabled = true;
            DetectedDevice.SelectedItem = null;
            ManagedHardware.IsEnabled = true;
            ManagedInstance.Text = "";
            ManagedRole.Text = "";
            ManagedKneeboardTitle.Text = "";
            ManagedKneeboardSubtitle.Text = "";
            KneeboardTitleFields.IsEnabled = DeviceManagementLayer == AssignmentLayer.Module;
            ManagedFileName.Text = (DetectedDevice.SelectedItem as DetectedDcsDevice)?.FileName ?? "";
            MfdLeft.Text = MfdRight.Text = MfdTop.Text = MfdMiddle.Text = "";
            MfdLabelFields.IsEnabled = DeviceManagementLayer == AssignmentLayer.Module && (ManagedHardware.SelectedItem as WorkspaceHardware)?.DeviceId == "tm-mfd";
        }
        private void ManagedHardwareChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_managementRefreshing || ManagedFileName is null) return;
            if (ManagedDevices.SelectedItem is not WorkspaceDevice selected || _working?.Configuration.Profiles.FirstOrDefault(x => x.Id == selected.ProfileId)?.DeviceId != (ManagedHardware.SelectedItem as WorkspaceHardware)?.DeviceId)
                ManagedFileName.Text = (DetectedDevice.SelectedItem as DetectedDcsDevice)?.FileName ?? "";
            MfdLabelFields.IsEnabled = DeviceManagementLayer == AssignmentLayer.Module && (ManagedHardware.SelectedItem as WorkspaceHardware)?.DeviceId == "tm-mfd";
        }
        private void ManagedDeviceSelected(object sender, SelectionChangedEventArgs e)
        {
            if (_managementRefreshing || ManagedDevices.SelectedItem is not WorkspaceDevice device || _working is null) return;
            DetectedDevice.IsEnabled = false;
            var profile = _working.Configuration.Profiles.Single(x => x.Id == device.ProfileId);
            ManagedHardware.SelectedItem = (ManagedHardware.ItemsSource as IEnumerable<WorkspaceHardware>)?.FirstOrDefault(x => x.DeviceId == profile.DeviceId);
            ManagedHardware.IsEnabled = true;
            ManagedFileName.Text = device.FileName;
            ManagedInstance.Text = device.Instance ?? "";
            ManagedRole.Text = device.Role ?? "";
            var diagram = _working.Diagrams.Single(x => x.ProfileId == device.ProfileId);
            var page = KneeboardDraftProjection.CreateCombined(_working, diagram, _draft?.Snapshot ?? _working.Assignments, _labels);
            ManagedKneeboardTitle.Text = page.Title;
            ManagedKneeboardSubtitle.Text = page.Kicker;
            KneeboardTitleFields.IsEnabled = DeviceManagementLayer == AssignmentLayer.Module;
            var labels = page.CategoryLabels;
            MfdLeft.Text = labels.GetValueOrDefault("mfd-category-left") ?? "";
            MfdRight.Text = labels.GetValueOrDefault("mfd-category-right") ?? "";
            MfdTop.Text = labels.GetValueOrDefault("mfd-category-top") ?? "";
            MfdMiddle.Text = labels.GetValueOrDefault("mfd-category-middle") ?? "";
            MfdLabelFields.IsEnabled = DeviceManagementLayer == AssignmentLayer.Module && profile.DeviceId == "tm-mfd";
        }
        private void AddDeviceClicked(object sender, RoutedEventArgs e) => RunStructureEdit(workspace =>
            WorkspaceStructureEditor.AddDevice(workspace, ManagedHardware.SelectedItem as WorkspaceHardware ??
                throw new InvalidOperationException("Select shared hardware."), DeviceManagementLayer,
                NewDeviceFileName(), ManagedInstance.Text, ManagedRole.Text,
                DeviceManagementLayer == AssignmentLayer.Module && !string.IsNullOrWhiteSpace(ManagedKneeboardTitle.Text) ? ManagedKneeboardTitle.Text : null,
                DeviceManagementLayer == AssignmentLayer.Module && !string.IsNullOrWhiteSpace(ManagedKneeboardSubtitle.Text) ? ManagedKneeboardSubtitle.Text : null));
        private void UpdateDeviceClicked(object sender, RoutedEventArgs e) => RunStructureEdit(workspace =>
            WorkspaceStructureEditor.UpdateDevice(workspace, ManagedDevices.SelectedItem as WorkspaceDevice ??
                throw new InvalidOperationException("Select a device to update."), ManagedFileName.Text, ManagedInstance.Text, ManagedRole.Text,
                new Dictionary<string, string> { ["left"] = MfdLeft.Text, ["right"] = MfdRight.Text, ["top"] = MfdTop.Text, ["middle"] = MfdMiddle.Text }, ManagedHardware.SelectedItem as WorkspaceHardware,
                DeviceManagementLayer == AssignmentLayer.Module ? ManagedKneeboardTitle.Text : null,
                DeviceManagementLayer == AssignmentLayer.Module ? ManagedKneeboardSubtitle.Text : null));
        private void RemoveDeviceClicked(object sender, RoutedEventArgs e) => RunStructureEdit(workspace =>
            WorkspaceStructureEditor.RemoveDevice(workspace, ManagedDevices.SelectedItem as WorkspaceDevice ??
                throw new InvalidOperationException("Select a device to remove.")));
        private void ClearDeviceAssignmentsClicked(object sender, RoutedEventArgs e) => RunStructureEdit(workspace =>
            WorkspaceStructureEditor.ClearDeviceAssignments(workspace, ManagedDevices.SelectedItem as WorkspaceDevice ??
                throw new InvalidOperationException("Select a device to clear.")));
        private void ModifierDeviceChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModifierControl is null || _managementRefreshing) return;
            var device = ModifierDevice.SelectedItem as WorkspaceDevice;
            ModifierControl.ItemsSource = _working?.Diagrams.FirstOrDefault(x => x.ProfileId == device?.ProfileId)?.Controls
                .Where(x => !x.IsAxis && !x.Control.EndsWith("-shifted", StringComparison.Ordinal)).DistinctBy(x => x.InputKey).ToArray();
            ModifierControl.SelectedIndex = 0;
        }
        private void NewModifierClicked(object sender, RoutedEventArgs e)
        {
            ManagedModifiers.SelectedItem = null;
            ModifierName.Text = "";
            ModifierSemantic.Text = "";
            ModifierModeChoice.SelectedItem = ModifierMode.Hold;
            ModifierDevice.SelectedIndex = 0;
        }
        private void ManagedModifierSelected(object sender, SelectionChangedEventArgs e)
        {
            if (ModifierRemoveButton is null || _managementRefreshing) return;
            ModifierRemoveButton.IsEnabled = ManagedModifiers.SelectedItem is WorkspaceModifier selected && !WorkspaceStructureEditor.IsDefaultKeyboardModifier(selected);
            if (ManagedModifiers.SelectedItem is not WorkspaceModifier modifier || _working is null) return;
            ModifierName.Text = modifier.Name;
            ModifierSemantic.Text = modifier.SemanticLayer;
            ModifierModeChoice.SelectedItem = modifier.Mode;
            ModifierDevice.SelectedItem = WorkspaceStructureEditor.Describe(_working).Devices.FirstOrDefault(x => x.ProfileId == modifier.ProfileId && x.Layer == modifier.Layer);
            ModifierControl.SelectedItem = (ModifierControl.ItemsSource as IEnumerable<HardwareGraphicControl>)?.FirstOrDefault(x => DcsInputKey.SamePhysicalInput(x.InputKey, modifier.Key));
        }
        private WorkspaceModifier ModifierCandidate()
        {
            var device = ModifierDevice.SelectedItem as WorkspaceDevice ?? throw new InvalidOperationException("Select a device.");
            var control = ModifierControl.SelectedItem as HardwareGraphicControl ?? throw new InvalidOperationException("Select a shared control.");
            var mode = ModifierModeChoice.SelectedItem is ModifierMode value ? value : throw new InvalidOperationException("Select Hold or Toggle.");
            return new(ModifierManagementLayer, device.ProfileId, ModifierName.Text, control.InputKey, ModifierSemantic.Text, mode, "");
        }
        private void AddModifierClicked(object sender, RoutedEventArgs e) => RunStructureEdit(workspace =>
            WorkspaceStructureEditor.SetModifier(workspace, null, ModifierCandidate()));
        private void UpdateModifierClicked(object sender, RoutedEventArgs e) => RunStructureEdit(workspace =>
            WorkspaceStructureEditor.SetModifier(workspace, ManagedModifiers.SelectedItem as WorkspaceModifier ??
                throw new InvalidOperationException("Select a modifier to update."), ModifierCandidate()));
        private void RemoveModifierClicked(object sender, RoutedEventArgs e) => RunStructureEdit(workspace =>
            WorkspaceStructureEditor.RemoveModifier(workspace, ManagedModifiers.SelectedItem as WorkspaceModifier ??
                throw new InvalidOperationException("Select a modifier to remove.")));

        private void SettingsClicked(object sender, RoutedEventArgs e)
        {
            if (_settings is null)
                return;
            var dialog = new SettingsWindow(_settings) { Owner = this };
            dialog.ShowDialog();
            ThemeManager.Apply(_settings.Current.Theme.ToString());
        }

        private UiLayerDiagram? CurrentDiagram => DeviceChoice.SelectedItem as UiLayerDiagram;
        private AssignmentLayer CurrentLayer =>
            Scope.SelectedIndex == 1 ? AssignmentLayer.UiLayer : AssignmentLayer.Module;
        private DraftAssignmentSlot? Slot(HardwareGraphicControl control)
        {
            var diagram = CurrentDiagram;
            if (diagram is null || _saved is null || _draft is null)
                return null;
            var layer = Layers.SelectedItem as string ?? "Base";
            var chord = layer == "Base" ? "" : layer.Replace(" + ", "+", StringComparison.Ordinal);
            return KneeboardDraftProjection.SlotForControl(_working ?? _saved, diagram, control, CurrentLayer, chord);
        }
        private DraftAssignment? Assignment(DraftAssignmentSlot slot) =>
            _draft?.Snapshot.FirstOrDefault(x => x.Slot.Key == slot.Key);
        private void DeviceChanged(object sender, SelectionChangedEventArgs e) => RefreshEditor();
        private void LayerChanged(object sender, SelectionChangedEventArgs e) => RefreshEditor();
        private void ScopeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Layers is null)
                return;
            var previous = Layers.SelectedItem as string ?? "Base";
            var choices = ModifierChoicesFor(CurrentLayer);
            Layers.ItemsSource = choices;
            Layers.SelectedItem = choices.Contains(previous, StringComparer.Ordinal) ? previous
                : previous != "Base" && choices.Length == 2 ? choices[1] : "Base";
            RefreshDeviceChoices();
            RefreshEditor();
        }
        private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => RefreshEditor();
        private void FiltersChanged(object sender, EventArgs e)
        {
            if (_updatingFilters)
                return;
            FilterCommands();
        }
        private void RefreshEditor()
        {
            UpdateBuildAvailability();
            if (Diagram is null || Commands is null || DeviceTitle is null)
                return;
            var diagram = CurrentDiagram;
            Diagram.Children.Clear();
            _calloutText.Clear();
            _selected = null;
            DeviceTitle.Text =
                diagram is null
                    ? "Select a stored device"
                    : diagram.Title + " — " +
                          (CurrentLayer == AssignmentLayer.Module ? "Module controls" : "Module UI Layer controls");
            EditorMessage.Text =
                _saved is null ? "Import a module to SQLite or open a saved module to edit its controls." : "";
            if (_categoryScope != CurrentLayer)
            {
                _updatingFilters = true;
                var available =
                    CurrentLayer == AssignmentLayer.Module ? _viewModel.ModuleCommands : _viewModel.UiCommands;
                CommandCategory.ItemsSource = Enumerable.Repeat("All", 1)
                                                  .Concat(available.Select(x => x.Category)
                                                              .Distinct(StringComparer.Ordinal)
                                                              .Order(StringComparer.Ordinal))
                                                  .ToArray();
                CommandCategory.SelectedIndex = 0;
                _categoryScope = CurrentLayer;
                _updatingFilters = false;
            }
            FilterCommands();
            if (diagram is null || _saved is null || _draft is null || string.IsNullOrWhiteSpace(diagram.Svg))
            {
                RefreshInspector();
                return;
            }
            try
            {
                var png = CanonicalSvgRenderer.Render(diagram.Svg, diagram.Width, diagram.Height);
                using var stream = new MemoryStream(png);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                Diagram.Width = diagram.Width * Zoom.Value;
                Diagram.Height = diagram.Height * Zoom.Value;
                var surface = new Canvas { Width = diagram.Width, Height = diagram.Height };
                surface.Children.Add(new Image { Source = bitmap, Width = diagram.Width, Height = diagram.Height });
                CoverAuthoredLabels(surface, diagram.Svg);
                foreach (var control in diagram.Controls)
                {
                    var slot = Slot(control);
                    if (slot is null)
                        continue;
                    var assignment = Assignment(slot);
                    var caption = assignment is null ? "Unassigned"
                                                     : _labels.GetValueOrDefault(slot.Key) ?? assignment.Command.Name;
                    var text = new TextBlock { Text = caption,
                                               TextAlignment = TextAlignment.Center,
                                               TextWrapping = TextWrapping.Wrap,
                                               VerticalAlignment = VerticalAlignment.Center,
                                               FontSize = 11,
                                               Foreground = Brushes.Black };
                    _calloutText[slot.Key] = text;
                    var original = _saved.Assignments.FirstOrDefault(x => x.Slot.Key == slot.Key);
                    var stateBrush = assignment is null                       ? Brushes.SlateGray
                                     : original is null                       ? Brushes.ForestGreen
                                     : original.Command == assignment.Command ? Brushes.White
                                                                              : Brushes.Goldenrod;
                    var target = new Border { Width = control.Width,
                                              Height = control.Height,
                                              Background = Brushes.White,
                                              BorderBrush = stateBrush,
                                              BorderThickness = new Thickness(1),
                                              Child = text,
                                              AllowDrop = true,
                                              Cursor = Cursors.Hand,
                                              Tag = control };
                    Canvas.SetLeft(target, control.X);
                    Canvas.SetTop(target, control.Y);
                    target.PreviewMouseLeftButtonDown += (_, args) =>
                    {
                        _dragStart = args.GetPosition(this);
                        _selected = control;
                        RefreshInspector();
                    };
                    target.PreviewMouseMove += (_, args) => ControlDrag(target, control, args);
                    target.DragOver += (_, args) => DragOverControl(control, args);
                    target.Drop += (_, args) => DropOnControl(control, args);
                    var menu = new ContextMenu();
                    var assign = new MenuItem { Header = "Assign selected command" };
                    assign.Click += (_, _) =>
                    {
                        _selected = control;
                        AssignClicked(target, new RoutedEventArgs());
                    };
                    menu.Items.Add(assign);
                    var clear = new MenuItem { Header = "Clear assignment" };
                    clear.Click += (_, _) =>
                    {
                        _selected = control;
                        ClearClicked(target, new RoutedEventArgs());
                    };
                    menu.Items.Add(clear);
                    target.ContextMenu = menu;
                    surface.Children.Add(target);
                }
                surface.LayoutTransform = new ScaleTransform(Zoom.Value, Zoom.Value);
                Diagram.Children.Add(surface);
            }
            catch (Exception ex)
            {
                EditorMessage.Text = "Unable to render saved SVG: " + ex.Message;
            }
            RefreshInspector();
        }
        private static void CoverAuthoredLabels(Canvas surface, string svg)
        {
            using var reader = XmlReader.Create(new StringReader(svg), new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4_000_000
            });
            var root = XDocument.Load(reader, LoadOptions.PreserveWhitespace).Root;
            if (root is null)
                return;
            foreach (var (control, box) in SvgCalloutLocator.Find(root))
            {
                var x = double.Parse(box.Attribute("x")?.Value ?? "0", CultureInfo.InvariantCulture);
                var y = double.Parse(box.Attribute("y")?.Value ?? "0", CultureInfo.InvariantCulture);
                var width = double.Parse(box.Attribute("width")?.Value ?? "0", CultureInfo.InvariantCulture);
                var height = double.Parse(box.Attribute("height")?.Value ?? "0", CultureInfo.InvariantCulture);
                if (width <= 0 || height <= 0)
                    continue;
                var mask =
                    new Border { Width = width,
                                 Height = height,
                                 Background = Brushes.White,
                                 BorderBrush = Brushes.SlateGray,
                                 BorderThickness = new Thickness(1),
                                 Tag = control,
                                 Child = new TextBlock { Text = "Unassigned", TextAlignment = TextAlignment.Center,
                                                         TextWrapping = TextWrapping.Wrap,
                                                         VerticalAlignment = VerticalAlignment.Center, FontSize = 11,
                                                         Foreground = Brushes.Black } };
                Canvas.SetLeft(mask, x);
                Canvas.SetTop(mask, y);
                surface.Children.Add(mask);
            }
        }
        private void FilterCommands()
        {
            if (Commands is null || CommandCategory is null || CommandCount is null)
                return;
            var list = CurrentLayer == AssignmentLayer.Module ? _viewModel.ModuleCommands : _viewModel.UiCommands;
            var category = CommandCategory.SelectedItem as string;
            var filtered =
                CommandPalette.Filter(list, CommandSearch.Text, category == "All" ? null : category)
                    .Where(x => CommandType.SelectedIndex == 0 || (CommandType.SelectedIndex == 2) == x.IsAxis)
                    .ToArray();
            if (_draft is not null && BindingState.SelectedIndex > 0)
            {
                var layer = Layers.SelectedItem as string ?? "Base";
                var chord = layer == "Base" ? "" : layer.Replace(" + ", "+", StringComparison.Ordinal);
                var eligible =
                    _viewModel.UiDiagrams.Where(x => CurrentLayer == AssignmentLayer.Module || x.SupportsUiLayer)
                        .Select(x => x.ProfileId);
                filtered = CommandBindingStateFilter
                               .Filter(filtered, _draft.Snapshot, CurrentLayer, eligible, CurrentDiagram?.ProfileId,
                                       chord, (CommandBindingState)BindingState.SelectedIndex)
                               .ToArray();
            }
            Commands.ItemsSource = filtered;
            CommandCount.Text = $"Showing {filtered.Length} of {list.Count}";
        }
        private void DragStart(object sender, MouseButtonEventArgs e) => _dragStart = e.GetPosition(this);
        private bool Dragging(MouseEventArgs e) =>
            _dragStart is Point start && e.LeftButton == MouseButtonState.Pressed &&
            (Math.Abs(e.GetPosition(this).X - start.X) > SystemParameters.MinimumHorizontalDragDistance ||
             Math.Abs(e.GetPosition(this).Y - start.Y) > SystemParameters.MinimumVerticalDragDistance);
        private void CommandDrag(object sender, MouseEventArgs e)
        {
            if (!Dragging(e) || Commands.SelectedItem is not CommandPaletteEntry command || _draft is null)
                return;
            _dragStart = null;
            DragDrop.DoDragDrop(Commands, new DataObject(typeof(CommandPaletteEntry), command), DragDropEffects.Copy);
        }
        private void ControlDrag(Border source, HardwareGraphicControl control, MouseEventArgs e)
        {
            if (!Dragging(e) || _draft is null)
                return;
            var slot = Slot(control);
            if (slot is null || Assignment(slot) is null)
                return;
            _dragStart = null;
            DragDrop.DoDragDrop(source, new DataObject(typeof(EditorControlDrag), new EditorControlDrag(slot)),
                                DragDropEffects.Move | DragDropEffects.Copy);
        }
        private void DragOverControl(HardwareGraphicControl target, DragEventArgs e)
        {
            var destination = Slot(target);
            var source = e.Data.GetData(typeof(EditorControlDrag)) as EditorControlDrag;
            var catalog = e.Data.GetData(typeof(CommandPaletteEntry)) as CommandPaletteEntry;
            var compatible = destination is not null &&
                             ((catalog is not null && catalog.IsAxis == target.IsAxis) ||
                              (source is not null && source.Slot.ProfileId == destination.ProfileId &&
                               source.Slot.Layer == destination.Layer && source.Slot.Section == destination.Section &&
                               source.Slot.Key != destination.Key));
            e.Effects = !compatible                                        ? DragDropEffects.None
                        : source is null                                   ? DragDropEffects.Copy
                        : (Keyboard.Modifiers & ModifierKeys.Control) != 0 ? DragDropEffects.Copy
                                                                           : DragDropEffects.Move;
            e.Handled = true;
        }
        private void DropOnControl(HardwareGraphicControl target, DragEventArgs e)
        {
            var destination = Slot(target);
            if (destination is null || _draft is null)
                return;
            var source = e.Data.GetData(typeof(EditorControlDrag)) as EditorControlDrag;
            var command = e.Data.GetData(typeof(CommandPaletteEntry)) as CommandPaletteEntry;
            if (source is not null)
                Apply((choice) => (Keyboard.Modifiers & ModifierKeys.Control) != 0
                                      ? _draft.Copy(source.Slot, destination, Behavior, choice)
                                      : _draft.Move(source.Slot, destination, Behavior, choice),
                      source.Slot);
            else if (command is not null)
                Apply(choice => _draft.Assign(destination,
                                              new DraftCommand(command.Id, command.Name, command.IsAxis, CurrentLayer),
                                              Behavior, choice));
            e.Handled = true;
        }
        private OccupiedDestinationBehavior Behavior =>
            _settings?.Current.OccupiedDestinationBehavior ?? OccupiedDestinationBehavior.Ask;
        private void Apply(Func<DraftConflictChoice?, DraftResult> operation, DraftAssignmentSlot? source = null)
        {
            var result = operation(null);
            if (result.Outcome == DraftOutcome.ConfirmationRequired)
            {
                var decision = MessageBox.Show(
                    this, "Destination is occupied. Yes: replace; No: swap assigned controls; Cancel: keep both.",
                    "Occupied control", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                var choice = decision == MessageBoxResult.Yes                        ? DraftConflictChoice.Replace
                             : decision == MessageBoxResult.No && source is not null ? DraftConflictChoice.Swap
                                                                                     : DraftConflictChoice.Cancel;
                result = operation(choice);
            }
            EditorMessage.Text = result.Message;
            if (result.Outcome == DraftOutcome.Applied)
                RefreshEditor();
        }
        private void AssignClicked(object sender, RoutedEventArgs e)
        {
            if (_selected is null || _draft is null || Commands.SelectedItem is not CommandPaletteEntry command)
                return;
            var slot = Slot(_selected);
            if (slot is null)
                return;
            Apply(choice =>
                      _draft.Assign(slot, new DraftCommand(command.Id, command.Name, command.IsAxis, CurrentLayer),
                                    Behavior, choice));
        }
        private void ClearClicked(object sender, RoutedEventArgs e)
        {
            if (_selected is null || _draft is null)
                return;
            var slot = Slot(_selected);
            if (slot is not null)
                Apply(
                    _ => _draft.Remove(slot));
        }
        private void UndoClicked(object sender, RoutedEventArgs e)
        {
            if (_draft is null)
                return;
            EditorMessage.Text = _draft.Undo().Message;
            RefreshEditor();
        }
        private void ResetClicked(object sender, RoutedEventArgs e)
        {
            if (_selected is null)
                return;
            var slot = Slot(_selected);
            if (slot is null)
                return;
            _labels.Remove(slot.Key);
            RefreshEditor();
        }
        private void LabelChanged(object sender, TextChangedEventArgs e)
        {
            if (_updatingInspector || _selected is null || _saved is null)
                return;
            var slot = Slot(_selected);
            if (slot is null)
                return;
            if (string.IsNullOrWhiteSpace(LabelEditor.Text))
                _labels.Remove(slot.Key);
            else
                _labels[slot.Key] = LabelEditor.Text;
            if (_calloutText.TryGetValue(slot.Key, out var label))
                label.Text = LabelEditor.Text;
            PendingSummary.Text = "Pending label or assignment changes";
            UpdateBuildAvailability();
        }
        private void RefreshInspector()
        {
            if (PhysicalInput is null)
                return;
            var slot = _selected is null ? null : Slot(_selected);
            var assignment = slot is null ? null : Assignment(slot);
            PhysicalInput.Text = _selected is null ? "Select a callout" : _selected.Label + " · " + _selected.InputKey;
            Chord.Text = slot is null ? "" : slot.ModifierChord.Length == 0 ? "Base" : slot.ModifierChord;
            CurrentCommand.Text =
                slot is null
                    ? ""
                    : _saved?.Assignments.FirstOrDefault(x => x.Slot.Key == slot.Key)?.Command.Name ?? "Unassigned";
            PendingCommand.Text = assignment?.Command.Name ?? "Unassigned";
            DefaultLabel.Text = _selected?.Label ?? "";
            _updatingInspector = true;
            LabelEditor.IsEnabled = assignment is not null;
            LabelEditor.Text = slot is null         ? ""
                               : assignment is null ? "Unassigned"
                                                    : _labels.GetValueOrDefault(slot.Key) ?? assignment.Command.Name;
            _updatingInspector = false;
            TuneAxisButton.IsEnabled = slot?.Section == BindingSection.Axis && assignment is not null;
            AxisTuningSummary.Text = slot?.Section != BindingSection.Axis ? "Select an axis control to tune."
                                     : assignment is null                 ? "Assign a command before tuning."
                                     : assignment.Filter is null          ? "No axis filter set."
                                                                          : "Axis filter staged.";
            PendingSummary.Text = _structureDirty ? "Pending device or modifier changes" : _draft?.HasChanges == true ? "Pending assignments: " + _draft.History.Count
                                                             : "No pending assignment changes";
        }
        private async void SaveClicked(object sender, RoutedEventArgs e)
        {
            if (_repository is null || _saved is null || _draft is null)
            {
                EditorMessage.Text = "Open a saved database workspace to edit.";
                return;
            }
            try
            {
                var updated = (_working ?? _saved) with { Assignments = _draft.Snapshot,
                                            Labels = new Dictionary<string, string>(_labels, StringComparer.Ordinal) };
                await _repository.SaveAsync(updated);
                _saved = updated;
                _working = updated;
                _structureDirty = false;
                var allowed =
                    updated.ModuleCommands.Select(x => new DraftCommand(x.Id, x.Name, x.IsAxis, AssignmentLayer.Module))
                        .Concat(updated.UiCommands.Select(
                            x => new DraftCommand(x.DcsCommandId, x.Name, x.IsAxis, AssignmentLayer.UiLayer)));
                _draft = new DraftAssignmentService(
                    updated.Configuration, allowed, updated.Assignments,
                    WorkspaceStructureEditor.UiModifierNames(updated));
                RefreshEditor();
                RefreshKneeboardPreview();
                RefreshManagement();
                RefreshDeviceChoices();
                KneeboardList.ItemsSource = updated.Pages.Select(x => $"{x.Order:00}  ·  {x.Title}").ToArray();
                SetManagementMessage("Devices, modifiers, assignments, and labels saved to database.");
                UpdateBuildAvailability();
            }
            catch (Exception ex)
            {
                EditorMessage.Text = "Database save failed: " + ex.Message;
                UpdateBuildAvailability();
            }
        }
        private void LoadAuthoredLabelsClicked(object sender, RoutedEventArgs e)
        {
            if (_saved is null || _draft is null)
                return;
            var picker = new OpenFileDialog { Title = "Select a module config/kneeboard.json file",
                                              Filter = "kneeboard.json|kneeboard.json|JSON files|*.json" };
            if (picker.ShowDialog(this) != true)
                return;
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(picker.FileName));
                var metadata = new Dictionary<string, JsonElement> { ["config/kneeboard.json"] = document.RootElement };
                var imported = KneeboardLabelImporter.Import(metadata, _saved.Configuration);
                if (imported.Count == 0)
                {
                    EditorMessage.Text = "No authored labels matched this saved module.";
                    return;
                }
                foreach (var pair in imported)
                    _labels[pair.Key] = pair.Value;
                RefreshEditor();
                EditorMessage.Text =
                    $"{imported.Count} authored Module labels staged. Save to database to keep them, or Discard draft.";
                PendingSummary.Text = "Pending label changes";
                UpdateBuildAvailability();
            }
            catch (Exception ex)
                when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            {
                EditorMessage.Text = "Unable to load authored labels: " + ex.Message;
            }
        }
        private void DiscardClicked(object sender, RoutedEventArgs e)
        {
            _working = _saved;
            _structureDirty = false;
            if (_saved is not null) RebuildDraft(_saved);
            else _draft?.Discard();
            _labels.Clear();
            if (_saved is not null)
                foreach (var pair in _saved.Labels)
                    _labels[pair.Key] = pair.Value;
            RefreshEditor();
            RefreshManagement();
            RefreshDeviceChoices();
            SetManagementMessage("Pending edits discarded.");
            UpdateBuildAvailability();
        }
        private void RenderClicked(object sender, RoutedEventArgs e)
        {
            var diagram = CurrentDiagram;
            if (diagram is null || _saved is null || _draft is null || string.IsNullOrWhiteSpace(diagram.Svg))
            {
                EditorMessage.Text = "Select a stored device with an authored graphic to render its current kneeboard.";
                return;
            }
            try
            {
                var page = KneeboardDraftProjection.CreateCombined(_working ?? _saved, diagram, _draft.Snapshot, _labels);
                var png = CompositeKneeboardRenderer.Render(diagram, page);
                using var stream = new MemoryStream(png);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                var view = new Window { Title = "Current kneeboard · " + diagram.Title,
                                        Owner = this,
                                        Width = 900,
                                        Height = 900,
                                        MinWidth = 500,
                                        MinHeight = 500,
                                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                                        Background = (Brush)FindResource("WindowBackgroundBrush"),
                                        Foreground = (Brush)FindResource("PrimaryTextBrush") };
                view.Content = new ScrollViewer { Content = new Image { Source = bitmap, Stretch = Stretch.Uniform },
                                                  HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                                                  VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                view.ShowDialog();
            }
            catch (Exception ex)
            {
                EditorMessage.Text = "Unable to render current kneeboard: " + ex.Message;
            }
        }
        private void TuneAxisClicked(object sender, RoutedEventArgs e)
        {
            if (_selected is null || _draft is null)
                return;
            var slot = Slot(_selected);
            if (slot?.Section != BindingSection.Axis)
                return;
            var assignment = Assignment(slot);
            if (assignment is null)
                return;
            var dialog = new AxisTuningWindow(assignment.Filter) { Owner = this };
            if (dialog.ShowDialog() != true)
                return;
            var result = _draft.TuneAxis(slot, dialog.Filter);
            EditorMessage.Text = result.Message;
            if (result.Outcome == DraftOutcome.Applied)
                RefreshEditor();
        }
        private void KneeboardList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshKneeboardPreview();
        }

        private void RefreshKneeboardPreview()
        {
            if (KneeboardPreview is null || KneeboardCaption is null)
                return;
            KneeboardPreview.Source = null;
            var index = KneeboardList.SelectedIndex;
            var pageKeys = _saved?.Pages.Select(x => x.StableKey).ToArray() ?? _viewModel.PageKeys.ToArray();
            if (index < 0 || index >= pageKeys.Length)
            {
                KneeboardCaption.Text = "No rendered kneeboard image is available.";
                return;
            }
            try
            {
                var key = pageKeys[index];
                byte[] bytes;
                string caption;
                if (_saved is null)
                {
                    var imported = _viewModel.Images.FirstOrDefault(x => x.PageKey == key) ??
                                   throw new InvalidDataException("No imported PNG for this page.");
                    bytes = imported.Bytes;
                    caption = "Imported kneeboard: " + imported.Path;
                }
                else
                {
                    var page = _saved.Pages.Single(x => x.StableKey == key);
                    var path = $"KNEEBOARD/{_saved.Configuration.Module.DcsId}/{key}.png";
                    if (page.Kind == KneeboardPageKind.Device)
                    {
                        var diagram = _saved.Diagrams.Single(x =>
                            KneeboardPageLocator.PageKeyForProfile(_saved, x.ProfileId) == key);
                        var combined = KneeboardDraftProjection.CreateCombined(
                            _saved, diagram, _saved.Assignments, _saved.Labels);
                        bytes = CompositeKneeboardRenderer.Render(diagram, combined, "");
                    }
                    else if (page.Kind == KneeboardPageKind.Summary)
                    {
                        bytes = SnapshotSummaryKneeboardRenderer.Generate(_saved)[path];
                    }
                    else
                    {
                        throw new InvalidDataException("Unsupported saved kneeboard page: " + key);
                    }
                    caption = "Generated from saved assignments: " + path;
                }
                using var stream = new MemoryStream(bytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                KneeboardPreview.Source = bitmap;
                KneeboardCaption.Text = caption;
            }
            catch (Exception ex)
            {
                KneeboardCaption.Text = "Unable to generate this kneeboard: " + ex.Message;
            }
        }
        private bool HasPendingChanges() =>
            _structureDirty || _draft?.HasChanges == true ||
            _saved is not null &&
            (_labels.Count != _saved.Labels.Count ||
             _labels.Any(x => !_saved.Labels.TryGetValue(x.Key, out var original) || original != x.Value));

        private void BuildVersionChanged(object sender, TextChangedEventArgs e) => UpdateBuildAvailability();

        private void UpdateBuildAvailability()
        {
            if (BuildButton is null || PackageVersion is null)
                return;
            BuildButton.IsEnabled = !_building && _repository is not null && _saved is not null &&
                                    !_viewModel.HasErrors && !HasPendingChanges() &&
                                    !string.IsNullOrWhiteSpace(PackageVersion.Text);
        }

        private async void BuildClicked(object sender, RoutedEventArgs e)
        {
            if (_saved is null || !BuildButton.IsEnabled)
                return;
            _building = true;
            UpdateBuildAvailability();
            BuildStatus.Text = "Validating and composing the saved OvGME package…";
            try
            {
                var snapshot = _saved;
                var version = PackageVersion.Text.Trim();
                var package = await Task.Run(() => SnapshotOvGmeComposer.Build(snapshot, version));
                var picker = new OpenFolderDialog { Title = "Select output folder for the OvGME ZIP and SHA256SUMS.txt" };
                if (picker.ShowDialog(this) != true)
                {
                    BuildStatus.Text = "Package build cancelled; no files were written.";
                    return;
                }
                var written = OvGmeArtifactWriter.Write(package, picker.FolderName);
                BuildStatus.Text = $"Wrote {written.ArchivePath} and {written.ChecksumPath} · SHA-256 {written.Sha256}";
            }
            catch (Exception ex)
            {
                BuildStatus.Text = "OvGME build failed: " + ex.Message;
            }
            finally
            {
                _building = false;
                UpdateBuildAvailability();
            }
        }

        private void ConfirmClose(object? sender, CancelEventArgs e)
        {
            if (HasPendingChanges() &&
                MessageBox.Show(this, "Discard unsaved device, modifier, assignment, and label changes?", "Pending changes",
                                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                e.Cancel = true;
        }
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        #endregion END private helpers
    }
}
