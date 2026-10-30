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
        private bool _showingSavedLink;
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
            ManagedHardware.ItemsSource = EditionAccess.Hardware(WorkspaceStructureEditor.Hardware(_context.Working), EditionAccess.Training(_context)).ToArray();
            ManagedDevices.ItemsSource = devices;
            ManagedDevices.SelectedItem = devices.FirstOrDefault(x => x.ProfileId == selectedId);
            StatusText.Text = $"{devices.Length} devices in {_layer}";
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
                ApplyLinkState(null);
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

            var revisions = (CustomMappingRevisions.ItemsSource as IEnumerable<UserMappingRevision> ?? []).ToList();
            if (diagram.CustomMapping is { } savedMapping && !revisions.Any(x => x.Id == savedMapping.Id)) revisions.Add(savedMapping);
            CustomMappingRevisions.ItemsSource = revisions;
            if (!diagram.DrawingUnlinked) CustomMappingRevisions.SelectedItem = revisions.FirstOrDefault(x => x.Id == diagram.CustomMapping?.Id);
            var guid = device.PhysicalGuid ?? DcsPhysicalIdentity.FromFileName(device.FileName);
            _showingSavedLink = true;
            try
            {
                var observed = (DetectedDevice.ItemsSource as IEnumerable<DetectedDcsDevice> ?? []).ToList();
                if (guid is { } saved && !observed.Any(x => x.PhysicalGuid == saved))
                    observed.Add(new(saved, DcsPhysicalIdentity.NameFromFileName(device.FileName), false, device.FileName));
                DetectedDevice.ItemsSource = observed;
                DetectedDevice.SelectedItem = observed.FirstOrDefault(x => x.PhysicalGuid == guid);
            }
            finally { _showingSavedLink = false; }
            ApplyLinkState(device);
            UpdateMfdVisibility(ManagedDevices.SelectedItem as WorkspaceDevice);
        }

        private void ApplyLinkState(WorkspaceDevice? device)
        {
            var diagram = device is null ? null : _context?.Working?.Diagrams.FirstOrDefault(x => x.ProfileId == device.ProfileId);
            var linked = device is not null && (device.PhysicalGuid ?? DcsPhysicalIdentity.FromFileName(device.FileName)) is not null;
            var state = ControllerLinkState.For(linked, diagram is { DrawingUnlinked: false }, _context?.Working is not null, false);
            DetectedDevice.IsEnabled = state.SelectPhysical;
            ManagedFileName.IsReadOnly = linked;
            ManagedHardware.IsEnabled = device is null;
            UnlinkPhysicalButton.IsEnabled = state.UnlinkPhysical;
            UnlinkDrawingButton.IsEnabled = state.UnlinkDrawing;
            CustomMappingRevisions.IsEnabled = diagram?.DrawingUnlinked == true;
            ApplyCustomDrawingButton.IsEnabled = diagram?.DrawingUnlinked == true;
            UseRetainedDrawingButton.IsEnabled = diagram?.DrawingUnlinked == true;
        }
        private void UnlinkPhysicalClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null || !UnlinkPhysicalButton.IsEnabled || ManagedDevices.SelectedItem is not WorkspaceDevice device) return;
            _context.RunStructureEdit(workspace => WorkspaceStructureEditor.UnlinkPhysicalDevice(workspace, device));
            StatusMessage.Text = "Physical controller unlinked in both scopes. Assignments and drawing retained. Link a unit before exporting to DCS.";
        }
        private void UnlinkDrawingClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null || !UnlinkDrawingButton.IsEnabled || ManagedDevices.SelectedItem is not WorkspaceDevice device) return;
            _context.RunStructureEdit(workspace => WorkspaceStructureEditor.SetDrawingLinked(workspace, device.ProfileId, false));
        }
        private void UseRetainedDrawingClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null || !UseRetainedDrawingButton.IsEnabled || ManagedDevices.SelectedItem is not WorkspaceDevice device) return;
            _context.RunStructureEdit(workspace => WorkspaceStructureEditor.SetDrawingLinked(workspace, device.ProfileId, true));
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
                ManagedDeviceSelected(this, new SelectionChangedEventArgs(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent, Array.Empty<object>(), Array.Empty<object>()));
                StatusMessage.Text = $"{connected.Count} connected device(s).";
            }
            catch (Exception ex)
            {
                StatusMessage.Text = "Device discovery failed: " + ex.Message;
            }
        }

        private void DetectedDeviceChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_showingSavedLink && DetectedDevice.IsEnabled && DetectedDevice.SelectedItem is DetectedDcsDevice device)
                ManagedFileName.Text = device.FileName;
        }

        private void RebindDeviceClicked(object sender, RoutedEventArgs e)
        {
            if (_context is null || ManagedDevices.SelectedItem is not WorkspaceDevice device)
            {
                StatusMessage.Text = "Select a device to rebind.";
                return;
            }

            if (!DetectedDevice.IsEnabled && ManagedFileName.Text != device.FileName)
            {
                StatusMessage.Text = "Unlink the physical controller before choosing another unit.";
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
                ManagedDeviceSelected(this, new SelectionChangedEventArgs(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent, Array.Empty<object>(), Array.Empty<object>()));
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

            if (!ApplyCustomDrawingButton.IsEnabled) { StatusMessage.Text = "Unlink drawing before choosing another drawing."; return; }
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
