using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;
using Heimdall.App;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Heimdall.App.Features.Devices
{
    /// <summary>Manages physical device profiles and their saved hardware drawings.</summary>
    public partial class DevicesView : UserControl
    {
        private IModuleWorkspaceContext? _context;
        private AssignmentLayer _layer = AssignmentLayer.Module;

        #region BEGIN Public
        public DevicesView()
        {
            InitializeComponent();
            ManagedDevices.SelectionChanged += ManagedDeviceSelected;
            AppearanceManager.ObserveArtwork(this, Refresh);
        }

        /// <summary>Connects this view to the shared workspace and refreshes its displayed state.</summary>
        public void Initialize(IModuleWorkspaceContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
            _context.StateChanged += (_, _) => Refresh();
            _context.StatusChanged += (_, msg) => StatusMessage.Text = msg;
            DevicesScope.SelectedIndex = 0;
            Refresh();
            _ = Dispatcher.InvokeAsync(async () =>
            {
                await RefreshDetectedDevicesAsync();
                await LoadInventoryChoicesAsync();
                await LoadUserMappingsAsync();
            });
        }
        #endregion END Public

        #region BEGIN private helpers
        private void ScopeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_context is null || DevicesScope.SelectedIndex < 0) return;
            _layer = DevicesScope.SelectedIndex == 1 ? AssignmentLayer.UiLayer : AssignmentLayer.Module;
            KneeboardTitleFields.Visibility = _layer == AssignmentLayer.Module ? Visibility.Visible : Visibility.Collapsed;
            UpdateMfdVisibility(ManagedDevices.SelectedItem as WorkspaceDevice);
            Refresh();
        }

        private void Refresh()
        {
            if (_context?.Working is null)
            {
                StatusText.Text = "No working snapshot";
                ManagedDevices.ItemsSource = null;
                return;
            }

            var selectedId = (ManagedDevices.SelectedItem as WorkspaceDevice)?.ProfileId;
            var state = WorkspaceStructureEditor.Describe(_context.Working);
            var devices = state.Devices.Where(x => x.Layer == _layer).ToArray();
            ManagedDevices.ItemsSource = devices;
            ManagedDevices.SelectedItem = devices.FirstOrDefault(x => x.ProfileId == selectedId);
            StatusText.Text = $"{devices.Length} devices in {_layer}";
            ManagedHardware.ItemsSource = _context.Working is null ? null : EditionAccess.Hardware(WorkspaceStructureEditor.Hardware(_context.Working), EditionAccess.Training(_context)).ToArray();
        }

        private void ManagedDeviceSelected(object sender, SelectionChangedEventArgs e)
        {
            if (_context?.Working is null || ManagedDevices.SelectedItem is not WorkspaceDevice device)
            {
                ControllerPreview.Clear();
                ManagedFileName.Text = "";
                ManagedInstance.Text = "";
                ManagedRole.Text = "";
                ManagedHardware.SelectedItem = null;
                DetectedDevice.SelectedItem = null;
                return;
            }

            var profile = _context.Working.Configuration.Profiles.Single(x => x.Id == device.ProfileId);
            ManagedHardware.SelectedItem = (ManagedHardware.ItemsSource as IEnumerable<WorkspaceHardware>)?
                .FirstOrDefault(x => x.DeviceId == profile.DeviceId);

            ManagedFileName.Text = device.FileName;
            ManagedInstance.Text = device.Instance ?? "";
            ManagedRole.Text = device.Role ?? "";

            var diagram = _context.Working.Diagrams.Single(x => x.ProfileId == device.ProfileId);
            var page = KneeboardDraftProjection.CreateCombined(
                _context.Working, diagram, _context.Draft?.Snapshot ?? _context.Working.Assignments, new Dictionary<string, string>());
            ControllerPreview.Show(diagram, page, AppearanceManager.ArtworkOpacityFor(this));
            var labels = page.CategoryLabels;
            MfdLeft.Text = labels.GetValueOrDefault("mfd-category-left") ?? "";
            MfdRight.Text = labels.GetValueOrDefault("mfd-category-right") ?? "";
            MfdTop.Text = labels.GetValueOrDefault("mfd-category-top") ?? "";
            MfdBottom.Text = labels.GetValueOrDefault("mfd-category-bottom") ?? "";
            KneeboardTitleFields.Visibility = _layer == AssignmentLayer.Module ? Visibility.Visible : Visibility.Collapsed;
            ManagedKneeboardTitle.Text = page.Title;
            ManagedKneeboardSubtitle.Text = page.Kicker;

            var guid = DcsPhysicalIdentity.FromFileName(device.FileName);
            DetectedDevice.SelectedItem = (DetectedDevice.ItemsSource as IEnumerable<DetectedDcsDevice>)?
                .FirstOrDefault(x => x.PhysicalGuid == guid);
            UpdateMfdVisibility(ManagedDevices.SelectedItem as WorkspaceDevice);
        }

        private void UpdateDeviceClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null || ManagedDevices.SelectedItem is not WorkspaceDevice device)
            {
                StatusMessage.Text = "Select a device to update.";
                return;
            }

            _context.RunStructureEdit(workspace =>
                WorkspaceStructureEditor.UpdateDevice(
                    workspace,
                    device,
                    ManagedFileName.Text,
                    ManagedInstance.Text,
                    ManagedRole.Text,
                    new Dictionary<string, string>
                    {
                        ["left"] = MfdLeft.Text,
                        ["right"] = MfdRight.Text,
                        ["top"] = MfdTop.Text,
                        ["bottom"] = MfdBottom.Text
                    },
                    null,
                    _layer == AssignmentLayer.Module ? ManagedKneeboardTitle.Text : null,
                    _layer == AssignmentLayer.Module ? ManagedKneeboardSubtitle.Text : null));

            StatusMessage.Text = "Change staged. Save to database to keep it, or Discard draft.";
        }

        private void RemoveDeviceClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null || ManagedDevices.SelectedItem is not WorkspaceDevice device)
            {
                StatusMessage.Text = "Select a device to remove.";
                return;
            }

            var before = ManagedDevices.Items.Count;
            _context.RunStructureEdit(workspace =>
                WorkspaceStructureEditor.RemoveDevice(workspace, device));

            if (ManagedDevices.Items.Count < before)
                StatusMessage.Text = "Device removal staged. Save to database to keep it, or Discard draft.";
            // otherwise the StatusChanged handler already wrote the error message
        }

        private void ClearDeviceAssignmentsClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null || ManagedDevices.SelectedItem is not WorkspaceDevice device)
            {
                StatusMessage.Text = "Select a device to clear.";
                return;
            }

            _context.RunStructureEdit(workspace =>
                WorkspaceStructureEditor.ClearDeviceAssignments(workspace, device));

            StatusMessage.Text = "Assignments cleared and staged. Save to database to keep it, or Discard draft.";
        }

        private void DiscardClicked(object sender, RoutedEventArgs e)
        {
            _context?.Discard();
            Refresh();
            StatusMessage.Text = "Pending edits discarded.";
        }

        private async void SaveClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null) return;
            try
            {
                await _context.SaveAsync();
                Refresh();
                StatusMessage.Text = "Saved to database.";
            }
            catch (Exception ex) { StatusMessage.Text = "Database save failed: " + ex.Message; }
        }

        private void NewDeviceClicked(object sender, RoutedEventArgs e)
        {
            ManagedDevices.SelectedItem = null;
            ManagedFileName.Text = "";
            ManagedInstance.Text = "";
            ManagedRole.Text = "";
            ManagedHardware.SelectedIndex = -1;
            StatusMessage.Text = "Enter filename, instance, role and select hardware, then Add.";
        }

        private void AddDeviceClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null || ManagedHardware.SelectedItem is not WorkspaceHardware hardware)
            {
                StatusMessage.Text = "Select shared hardware before adding.";
                return;
            }

            _context.RunStructureEdit(workspace =>
                WorkspaceStructureEditor.AddDevice(
                    workspace,
                    hardware,
                    _layer,
                    ManagedFileName.Text,
                    ManagedInstance.Text,
                    ManagedRole.Text,
                    _layer == AssignmentLayer.Module ? ManagedKneeboardTitle.Text : null,
                    _layer == AssignmentLayer.Module ? ManagedKneeboardSubtitle.Text : null));

            StatusMessage.Text = "Device added and staged. Save to database to keep it, or Discard draft.";
        }

        private async void LoadHardwareClicked(object sender, RoutedEventArgs e)
        {
            if (_context?.Working is null) return;
            try
            {
                StatusMessage.Text = "Loading committed shared hardware…";
                var hardware = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
                _context.RunStructureEdit(workspace =>
                {
                    var sources = new Dictionary<string, string>(workspace.Sources ?? new Dictionary<string, string>(), StringComparer.Ordinal);
                    foreach (var pair in hardware)
                        if (pair.Key is "config/ipia-hardware.json" or "config/ipia-device-map.json" or "config/ipia-hardware-diagnostics.json" || !sources.ContainsKey(pair.Key))
                            sources[pair.Key] = pair.Value;
                    var generic = WorkspaceStructureEditor.Hardware(workspace).Where(x => x.Diagram.GenericCapabilities is not null);
                    var bundled = JsonSerializer.Deserialize<WorkspaceHardware[]>(sources["config/ipia-hardware.json"]) ?? [];
                    sources["config/ipia-hardware.json"] = JsonSerializer.Serialize(bundled.Concat(generic).DistinctBy(x => x.DeviceId).ToArray());
                    return workspace with { Sources = sources };
                });
                StatusMessage.Text = "Shared hardware loaded. Save to database to keep it.";
            }
            catch (Exception ex)
            {
                StatusMessage.Text = "Shared hardware could not be loaded: " + ex.Message;
            }
        }

        private async void RefreshDetectedDevicesClicked(object sender, RoutedEventArgs e) => await RefreshDetectedDevicesAsync();

        private async Task RefreshDetectedDevicesAsync()
        {
            if (_context is null) return;
            try
            {
                StatusMessage.Text = "Reading connected controllers…";
                var connected = await Task.Run(() => _context.DeviceDiscovery.Discover());
                DetectedDevice.ItemsSource = connected.Where(device => EditionAccess.Policy.AllowsDevice(device)).ToArray();
                StatusMessage.Text = $"{connected.Count} connected device(s).";
            }
            catch (Exception ex)
            {
                StatusMessage.Text = "Device discovery failed: " + ex.Message;
            }
        }

        private void DetectedDeviceChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DetectedDevice.SelectedItem is DetectedDcsDevice device)
                ManagedFileName.Text = device.FileName;
        }

        private void RebindDeviceClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null || ManagedDevices.SelectedItem is not WorkspaceDevice device)
            {
                StatusMessage.Text = "Select a device to rebind.";
                return;
            }

            var result = ThemeMessageBox.Show(
                Window.GetWindow(this),
                "Rebind this profile in both aircraft and UI Layer scopes?\n\n" + device.FileName +
                "\n→ " + ManagedFileName.Text + "\n\nAssignments, labels, filters and modifier references will be retained. Save to keep the staged change.",
                "Rebind physical unit",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            _context.RunStructureEdit(workspace =>
                WorkspaceStructureEditor.RebindDevice(
                    workspace,
                    device,
                    ManagedFileName.Text,
                    ManagedInstance.Text,
                    ManagedRole.Text));

            StatusMessage.Text = "Rebind staged. Save to database to keep it, or Discard draft.";
        }

        private void UpdateMfdVisibility(WorkspaceDevice? device)
        {
            var isMfd = device is not null &&
                        _context?.Working?.Configuration.Profiles.FirstOrDefault(x => x.Id == device.ProfileId)?.DeviceId == "tm-mfd";
            MfdLabelFields.Visibility = _layer == AssignmentLayer.Module && isMfd
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void ImportAxisDefaultsClicked(object sender, RoutedEventArgs e)
        {
            if (_context?.Working is null || ManagedDevices.SelectedItem is not WorkspaceDevice device)
            {
                StatusMessage.Text = "Select a device before importing its DCS axis defaults.";
                return;
            }

            var picker = new ThemeBrowseDialog
            {
                Title = "Select the DCS profile saved after clearing all axes",
                Filter = "DCS diff profile|*.diff.lua"
            };
            var path = _context.AxisDefaultsFilePicker is null
                ? (picker.ShowDialog(Window.GetWindow(this)) == true ? picker.FileName : null)
                : _context.AxisDefaultsFilePicker();
            if (path is null) return;

            try
            {
                var source = File.ReadAllText(path);
                var workspace = _context.Working;
                var removals = ModuleAxisDefaults.Review(workspace, device.ProfileId, device.Layer, source);
                var review = new AxisDefaultsReviewWindow(workspace, device, removals) { Owner = Window.GetWindow(this) };
                if (review.ShowDialog() != true) return;
                _context.RunStructureEdit(current => ModuleAxisDefaults.Import(current, device.ProfileId, device.Layer, source));
                StatusMessage.Text = "Axis defaults imported and staged. Save to database to keep them.";
            }
            catch (Exception ex)
            {
                StatusMessage.Text = "Axis default import failed: " + ex.Message;
            }
        }

        private async Task LoadInventoryChoicesAsync()
        {
            if (_context?.Inventory is null) return;
            try
            {
                var choices = InventoryHardwareAuthoring.Choices(await _context.Inventory.ReadSnapshotAsync());
                InventoryUnits.ItemsSource = EditionAccess.Policy.HasPremium ? choices : [];
            }
            catch (Exception ex)
            {
                StatusMessage.Text = "Unable to read hardware inventory: " + ex.Message;
            }
        }

        private void AddInventoryClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null || InventoryUnits.SelectedItem is not InventoryHardwareChoice choice)
            {
                StatusMessage.Text = "Select an adopted inventory unit. Discover and save it from the Hardware menu first.";
                return;
            }

            _context.RunStructureEdit(workspace =>
                InventoryHardwareAuthoring.Add(workspace, choice, _layer));

            StatusMessage.Text = "Inventory unit added and staged. Save to database to keep it, or Discard draft.";
        }

        private async Task LoadUserMappingsAsync()
        {
            if (_context?.UserMappings is null || _context.Working is null) return;
            try
            {
                var pins = _context.Working.Diagrams.SelectMany(x =>
                    (x.MappingHistory ?? []).Concat(x.CustomMapping is { } pin ? [pin] : []));
                CustomMappingRevisions.ItemsSource = (await _context.UserMappings.ListAsync())
                    .Concat(pins)
                    .DistinctBy(x => x.Id)
                    .OrderBy(x => x.Title, StringComparer.Ordinal)
                    .ToArray();
            }
            catch (Exception ex)
            {
                StatusMessage.Text = "Unable to read user mappings: " + ex.Message;
            }
        }

        private async void RefreshUserMappingsClicked(object sender, RoutedEventArgs e) => await LoadUserMappingsAsync();

        private void AdoptCustomMappingClicked(object sender, RoutedEventArgs e)
        {
            if (_context?.Working is null ||
                ManagedDevices.SelectedItem is not WorkspaceDevice device ||
                CustomMappingRevisions.SelectedItem is not UserMappingRevision revision)
            {
                StatusMessage.Text = "Select a physical profile and a saved drawing revision.";
                return;
            }

            try
            {
                var review = MappingRevisionReview.Create(
                    _context.Working.Diagrams.Single(x => x.ProfileId == device.ProfileId), revision);
                if (review.Conflicts.Count > 0)
                {
                    StatusMessage.Text = string.Join("\n", review.Conflicts);
                    return;
                }

                var confirmed = _context.MappingReviewConfirmation?.Invoke(review) ??
                    ThemeMessageBox.Show(Window.GetWindow(this),
                        string.Join("\n", review.Changes) + "\n\nStage this drawing on the selected device? Save/Discard controls the final change.",
                        "Review drawing change",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question,
                        MessageBoxResult.No) == MessageBoxResult.Yes;

                if (!confirmed) return;

                _context.RunStructureEdit(workspace =>
                    UserMappingAdoption.Apply(workspace, device.ProfileId, revision));
                StatusMessage.Text = "Drawing applied and staged. Save to database to keep it, or Discard draft.";
            }
            catch (Exception ex)
            {
                StatusMessage.Text = "Unable to review drawing: " + ex.Message;
            }
        }
        #endregion END private helpers
    }

}
