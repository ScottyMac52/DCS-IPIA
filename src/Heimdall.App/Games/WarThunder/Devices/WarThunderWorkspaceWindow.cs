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
                _input.Stop(); _selectedPhysicalInput = null; RefreshDevices(); RefreshCommands(); ShowDrawing();
            };
            _devices.Rescan.Click += async (_, _) => { await RefreshHardwareAsync(); RefreshDevices(); ShowDrawing(); };
            _devices.Link.Click += (_, _) => Run(() =>
            {
                if (_session is not null && SelectedDevice is { } id && _devices.Physical.SelectedItem is DetectedDcsDevice physical)
                    Review(_session.PreviewCapturedDeviceLink(id, physical.PhysicalGuid));
            });
            _devices.Unlink.Click += (_, _) => Run(() => { if (_session is not null && SelectedDevice is { } id) Review(_session.PreviewUnlinkController(id)); });
            _devices.UnlinkDrawing.Click += (_, _) => Run(() => { _input.Stop(); if (_session is not null && SelectedDevice is { } id) _session.ChangeDrawing(id, null); });
            _devices.Drawing.SelectionChanged += (_, _) =>
            {
                if (_refreshing || _session is null || SelectedDevice is not { } id || _devices.Drawing.SelectedItem is not WorkspaceHardware hardware) return;
                Run(() => { _input.Stop(); _session.ChangeDrawing(id, hardware.DeviceId); });
            };
            _devices.Physical.SelectionChanged += (_, _) => { if (!_refreshing) _devices.Link.IsEnabled = _devices.Physical.IsEnabled && _devices.Physical.SelectedItem is DetectedDcsDevice; };
        }
        private void Review(GameControllerChange change)
        {
            _input.Stop();
            if (_review?.Invoke(change) ?? ThemeMessageBox.Show(this, change.Summary, "Review controller link", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
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
            _devices.Physical.IsEnabled = state.SelectPhysical;
            _devices.Link.IsEnabled = state.SelectPhysical && _devices.Physical.SelectedItem is DetectedDcsDevice;
            _devices.Unlink.IsEnabled = state.UnlinkPhysical;
            _devices.Drawing.IsEnabled = state.SelectDrawing;
            _devices.UnlinkDrawing.IsEnabled = state.UnlinkDrawing;
            _devices.NativeDetails.Text = native is null ? "Select one saved mapping to review its controller setup."
                : string.Create(CultureInfo.InvariantCulture, $"{native.Name} · {native.Id}\nNative product: {native.Product}\nAxes: offset {native.AxesOffset}, count {native.AxesCount}. Buttons: offset {native.ButtonsOffset}, count {native.ButtonsCount}.\n")
                    + (pin is null ? "Physical controller unlinked." : "Linked: " + pin.Device.Name + " · " + pin.Device.PhysicalGuid);
            RefreshPhysicalMap();
            if (native is not null) _devices.NativeDetails.Text += "\n" + _physicalMap.Message;
            _refreshing = false;
        }
        private void ShowDrawing()
        {
            _assignments.DrawingCanvas.Children.Clear(); _presenter.Regions.Clear();
            var drawing = SelectedDevice is { } id ? _session?.DrawingId(id) : null;
            var hardware = _drawings.FirstOrDefault(item => item.DeviceId == drawing);
            RefreshPhysicalMap();
            if (hardware is not null)
            {
                var page = WarThunderDrawingProjection.Diagram(hardware.Diagram, _session!.Snapshot.Profile, SelectedDevice!, _physicalMap);
                _presenter.Show(hardware.Diagram, page, new HardwareDiagramInteractions(
                    SelectPhysicalControl, _physicalMap.Inputs.Count > 0 ? DropPhysicalCommand : null,
                    CanClear: CanClearPhysicalControl, Clear: ClearPhysicalControl,
                    CanTune: CanTunePhysicalControl, Tune: TunePhysicalControl,
                    DragFormat: "heimdall.war-thunder.command", Inspect: InspectPhysicalControl, CanDrop: CanDropPhysicalCommand), AppearanceManager.ArtworkOpacityFor(this));
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
