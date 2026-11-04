using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.WarThunder;

namespace Heimdall.App.Games
{
    /// <summary>Coordinates the devices feature of the composed War Thunder workspace.</summary>
    public partial class WarThunderWorkspaceWindow
    {
        #region BEGIN Public
        #endregion END Public
        #region BEGIN private helpers
        private void WireDevices()
        {
            Controllers.SelectionChanged += (_, _) =>
            {
                if (_refreshing) return;
                _input.Stop(); _selectedPhysicalInput = null; _selection.SelectInput(null); RefreshDevices(); RefreshCommands(); ShowDrawing();
            };
            _devices.Rescan.Click += async (_, _) => { await RefreshHardwareAsync(); RefreshDevices(); ShowDrawing(); };
            _devices.Add.Click += (_, _) => Run(AddController);
            _devices.Link.Click += (_, _) => Run(() =>
            {
                if (_session is not null && SelectedDevice is { } id && _devices.Physical.SelectedItem is DetectedDcsDevice physical)
                    Review(_session.PreviewCapturedDeviceLink(id, physical.PhysicalGuid));
            });
            _devices.Remove.Click += (_, _) => Run(() => { if (_session is not null && SelectedDevice is { } id) Review(_session.PreviewRemoveController(id)); });
            _devices.Unlink.Click += (_, _) => Run(() => { if (_session is not null && SelectedDevice is { } id) Review(_session.PreviewUnlinkController(id)); });
            _devices.ChooseDrawing.Click += (_, _) => ControllerLinkPresentation.OpenDrawingPicker(_devices.Drawing);
            _devices.UnlinkDrawing.Click += (_, _) => Run(() => { _input.Stop(); if (_session is not null && SelectedDevice is { } id) _session.ChangeDrawing(id, null); });
            _devices.Drawing.SelectionChanged += (_, _) =>
            {
                if (_refreshing || _session is null || SelectedDevice is not { } id || _devices.Drawing.SelectedItem is not WorkspaceHardware hardware) return;
                Run(() => { _input.Stop(); _session.ChangeDrawing(id, hardware.DeviceId); });
            };
            _devices.Physical.SelectionChanged += (_, _) => { if (!_refreshing)
                {
                    _devices.Link.IsEnabled = SelectedDevice is not null && _devices.Physical.IsEnabled && _devices.Physical.SelectedItem is DetectedDcsDevice;
                    UpdateAddController();
                } };
        }
        private void AddController()
        {
            if (!_devices.Add.IsEnabled || _session is null) return;
            if (SelectedDevice is not null)
            {
                Controllers.SelectedIndex = 0;
                _devices.Physical.SelectedIndex = -1;
                _devices.Physical.Focus();
                _devices.Physical.IsDropDownOpen = true;
                return;
            }
            if (_devices.Physical.SelectedItem is not DetectedDcsDevice physical) return;
            var change = _session.PreviewAddNativeController(physical.PhysicalGuid, physical.Name);
            Review(change);
            Controllers.SelectedItem = Controllers.Items.Cast<ListBoxItem>()
                .FirstOrDefault(item => item.Tag as string == change.ResultDeviceId) ?? Controllers.SelectedItem;
        }

        private void UpdateAddController()
        {
            var workspace = _session?.Snapshot;
            var physical = _devices.Physical.SelectedItem as DetectedDcsDevice;
            var available = _session?.CanAddNativeControllers == true && _session.AccessMessage.Length == 0 && !_input.Running;
            _devices.Add.IsEnabled = available && (SelectedDevice is not null ||
                physical is { Connected: true } && workspace is not null &&
                !workspace.Hardware.Values.Any(pin => pin.Device.PhysicalGuid == physical.PhysicalGuid));
            _devices.Add.ToolTip = "Choose an unlinked connected controller. Review its native input ranges before adding it.";
        }

        private void Review(GameControllerChange change)
        {
            _input.Stop();
            if (_review?.Invoke(change) ?? ThemeMessageBox.Show(this, change.Summary, "Review controller change", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                _session!.ApplyControllerChange(change);
        }
        private void RefreshDevices()
        {
            _refreshing = true;
            var id = SelectedDevice;
            var workspace = _session?.Snapshot;
            var native = workspace is null ? null : WarThunderProfileBackend.ReadDevices(workspace.Profile).SingleOrDefault(device => device.Id == id);
            var pin = id is null ? null : workspace?.Hardware.GetValueOrDefault(id);
            var drawing = id is null ? null : _session?.DrawingId(id);
            ControllerLinkPresentation.ShowPhysical(_devices.Physical, pin?.Device, _observed);
            var drawingChoices = _drawings.ToList();
            if (drawing is not null && !drawingChoices.Any(item => item.DeviceId == drawing))
                drawingChoices.Add(new(drawing, "Unavailable saved drawing", "", new("Unavailable", null, "", 1, 1, [], [])));
            _devices.Drawing.ItemsSource = drawingChoices;
            _devices.Drawing.SelectedItem = drawingChoices.FirstOrDefault(item => item.DeviceId == drawing);
            var ready = native is not null && native.Valid && _session!.AccessMessage.Length == 0;
            var state = ControllerLinkState.For(pin is not null, drawing is not null, ready, _input.Running);
            _devices.Remove.IsEnabled = ready && !_input.Running;
            _devices.Physical.IsEnabled = state.SelectPhysical ||
                id is null && _session?.CanAddNativeControllers == true && _session.AccessMessage.Length == 0 && !_input.Running;
            _devices.Link.IsEnabled = state.SelectPhysical && _devices.Physical.SelectedItem is DetectedDcsDevice;
            _devices.Unlink.IsEnabled = state.UnlinkPhysical;
            _devices.Drawing.IsEnabled = state.SelectDrawing;
            _devices.UnlinkDrawing.IsEnabled = state.UnlinkDrawing;
            _devices.ChooseDrawing.IsEnabled = state.SelectDrawing && drawingChoices.Count > 0;
            FeatureActionBar.ShowAlternate(_devices.Link, _devices.Unlink, pin is not null);
            FeatureActionBar.ShowAlternate(_devices.ChooseDrawing, _devices.UnlinkDrawing, drawing is not null);
            _devices.NativeDetails.Text = native is null ? "Select one saved mapping to review its controller setup."
                : string.Create(CultureInfo.InvariantCulture, $"{native.Name} · {native.Id}\nNative product: {native.Product}\nAxes: offset {native.AxesOffset}, count {native.AxesCount}. Buttons: offset {native.ButtonsOffset}, count {native.ButtonsCount}.\n")
                    + (pin is null ? "Physical controller unlinked." : "Linked: " + pin.Device.Name + " · " + pin.Device.PhysicalGuid);
            RefreshPhysicalMap();
            if (native is not null) _devices.NativeDetails.Text += "\n" + _physicalMap.Message;
            _devices.LayoutTrace.Text = _physicalMap.Layout is { } layout
                ? string.Join(Environment.NewLine, layout.Trace.Select(item => string.Create(CultureInfo.InvariantCulture,
                    $"Object {item.EnumerationIndex} · {item.Kind} · Native slot {item.NativeIndex?.ToString(CultureInfo.InvariantCulture) ?? "unresolved/excluded"} · {item.Detail} · {item.InputId}")))
                : _physicalMap.Message;
            _refreshing = false;
            UpdateAddController();
        }
        private void ShowDrawing()
        {
            _assignments.DrawingCanvas.Children.Clear(); _presenter.Regions.Clear();
            var drawing = SelectedDevice is { } id ? _session?.DrawingId(id) : null;
            var hardware = _drawings.FirstOrDefault(item => item.DeviceId == drawing);
            RefreshPhysicalMap();
            _devices.ControllerPreview.Clear();
            if (hardware is not null)
            {
                var page = WarThunderDrawingProjection.Diagram(hardware.Diagram, _session!.Snapshot.Profile, SelectedDevice!, _physicalMap);
                _devices.ControllerPreview.Show(hardware.Diagram, page, AppearanceManager.ArtworkOpacityFor(this));
                _presenter.Show(hardware.Diagram, page, new HardwareDiagramInteractions(
                    SelectPhysicalControl, _physicalMap.Inputs.Count > 0 ? DropPhysicalCommand : null,
                    CanClear: CanClearPhysicalControl, Clear: ClearPhysicalControl,
                    CanTune: CanTunePhysicalControl, Tune: TunePhysicalControl,
                    DragFormat: "heimdall.war-thunder.command", Inspect: InspectPhysicalControl, CanDrop: CanDropPhysicalCommand,
                    ClearChoices: ClearPhysicalChoices, ClearUnavailableReason: ClearUnavailableReason, ActionState: ControlAction), AppearanceManager.ArtworkOpacityFor(this));
                _assignments.Artwork.Feedback.Text = _physicalMap.Message;
            }
            _assignments.Artwork.Viewport.Visibility = hardware is null ? Visibility.Collapsed : Visibility.Visible;
            _assignments.Artwork.Hint.Visibility = hardware is null ? Visibility.Visible : Visibility.Collapsed;
            _assignments.Activity.Visibility = hardware is null ? Visibility.Visible : Visibility.Collapsed;
            if (hardware is null) _assignments.Artwork.Hint.Text = _physicalMap.Message + " Choose a controller drawing on Devices.";
            RefreshInputState();
        }
        private void RefreshPhysicalMap()
        {
            _drawingAssignments.Clear();
            var observations = _session is not null && SelectedDevice is { } selected && _session.Snapshot.Hardware.TryGetValue(selected, out var pin)
                ? _controllerInputs.Where(observation => observation.Device.PhysicalGuid == pin.Device.PhysicalGuid && observation.Device.ProductGuid == pin.Device.ProductGuid).ToArray() : [];
            _physicalMap = observations.Length == 1 && _session is not null && SelectedDevice is { } slot
                ? WarThunderPhysicalMap.Create(_session.Snapshot.Profile, slot, observations[0]) : new([], "Link the controller and rescan its inputs on Devices.");
        }
        private async Task RefreshHardwareAsync()
        {
            var scan = ++_hardwareScan;
            _input.Stop();
            try
            {
                _observed = _services.Discover();
                var inputs = await _services.DiscoverInputsAsync();
                if (_disposed || scan != _hardwareScan) return;
                _controllerInputs = inputs;
            }
            catch (Exception error)
            {
                if (_disposed || scan != _hardwareScan) return;
                _controllerInputs = []; ReportError(error, "Controller input scan failed");
            }
        }
        #endregion END private helpers
    }
}
