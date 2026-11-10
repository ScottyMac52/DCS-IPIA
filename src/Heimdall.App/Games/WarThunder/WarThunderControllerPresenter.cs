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
    /// <summary>Owns native controller review, verified evidence and physical layout projection.</summary>
    internal sealed class WarThunderControllerPresenter(
        WarThunderWorkspaceContext context,
        Action<Exception, string> reportError,
        ListBox Controllers,
        WarThunderDevicesView _devices,
        Window owner,
        Func<GameControllerChange, bool>? _review,
        HardwareDiagramPresenter presenter,
        Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? inputFactory,
        TextBlock InputStatus,
        WarThunderAssignmentsView _assignments,
        Action refreshChrome) : IDisposable
    {
        private HardwareInputFeedback _input = null!;
        #region BEGIN Public
        internal HardwareInputFeedback Input => _input;
        internal void Start() => _input = new(owner, presenter, text => InputStatus.Text = text, text => _assignments.Activity.Text = text, inputFactory, context);
        /// <summary>Invalidates discovery scans and releases foreground input resources.</summary>
        public void Dispose() { context.StopActivity(); _input.Dispose(); }
        #endregion END Public
        #region BEGIN private helpers
        internal void AddController()
        {
            if (!_devices.Add.IsEnabled || context.Session is null) return;
            if (context.SelectedDevice is not null)
            {
                Controllers.SelectedIndex = 0;
                _devices.Physical.SelectedIndex = -1;
                _devices.Physical.Focus();
                _devices.Physical.IsDropDownOpen = true;
                return;
            }
            if (_devices.Physical.SelectedItem is not DetectedDcsDevice physical) return;
            var change = context.Session.PreviewAddNativeController(physical.PhysicalGuid, physical.Name);
            Review(change);
            Controllers.SelectedItem = Controllers.Items.Cast<ListBoxItem>()
                .FirstOrDefault(item => item.Tag as string == change.ResultDeviceId) ?? Controllers.SelectedItem;
        }

        internal void UpdateAddController()
        {
            var physical = _devices.Physical.SelectedItem as DetectedDcsDevice;
            var available = context.Session?.CanAddNativeControllers == true && context.Session.AccessMessage.Length == 0 && !context.Busy && !context.ScanningHardware && !_input.Running;
            _devices.Add.IsEnabled = available && (context.SelectedDevice is not null ||
                physical is { Connected: true } && context.Session is not null &&
                !context.Session.IsPhysicalPinned(physical.PhysicalGuid));
            _devices.Add.ToolTip = "Choose an unlinked connected controller. Review its native input ranges before adding it.";
        }

        internal void Review(GameControllerChange change)
        {
            _input.Stop();
            if (_review?.Invoke(change) ?? ThemeMessageBox.Show(owner, change.Summary, "Review controller change", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                context.Session!.ApplyControllerChange(change);
        }

        internal void RefreshDevices()
        {
            context.Refreshing = true;
            var id = context.SelectedDevice;
            var native = context.Session is null ? null : context.NativeDevices.SingleOrDefault(device => device.Id == id);
            var pin = id is null ? null : context.Session?.HardwarePin(id);
            var drawing = id is null ? null : context.Session?.DrawingId(id);
            ControllerLinkPresentation.ShowPhysical(_devices.Physical, pin?.Device, context.Observed);
            var drawingChoices = context.Drawings.ToList();
            if (drawing is not null && !drawingChoices.Any(item => item.DeviceId == drawing))
                drawingChoices.Add(new(drawing, "Unavailable saved drawing", "", new("Unavailable", null, "", 1, 1, [], [])));
            _devices.Drawing.ItemsSource = drawingChoices;
            _devices.Drawing.SelectedItem = drawingChoices.FirstOrDefault(item => item.DeviceId == drawing);
            var ready = !context.Busy && !context.ScanningHardware && native is not null && native.Valid && context.Session!.AccessMessage.Length == 0;
            var state = ControllerLinkState.For(pin is not null, drawing is not null, ready, _input.Running);
            _devices.Remove.IsEnabled = ready && !context.Busy && !context.ScanningHardware && !_input.Running;
            _devices.Physical.IsEnabled = state.SelectPhysical ||
                id is null && context.Session?.CanAddNativeControllers == true && context.Session.AccessMessage.Length == 0 && !context.Busy && !context.ScanningHardware && !_input.Running;
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
            if (native is not null) _devices.NativeDetails.Text += "\n" + context.PhysicalMap.Message;
            _devices.LayoutTrace.Text = context.PhysicalMap.Layout is { } layout
                ? string.Join(Environment.NewLine, layout.Trace.Select(item => string.Create(CultureInfo.InvariantCulture,
                    $"Object {item.EnumerationIndex} · {item.Kind} · Native slot {item.NativeIndex?.ToString(CultureInfo.InvariantCulture) ?? "unresolved/excluded"} · {item.Detail} · {item.InputId}")))
                : context.PhysicalMap.Message;
            context.Refreshing = false;
            UpdateAddController();
        }

        internal void RefreshPhysicalMap()
        {
            context.DrawingAssignments.Clear();
            var observations = context.Session is not null && context.SelectedDevice is { } selected && context.Session.HardwarePin(selected) is { } pin
                ? context.ControllerInputs.Where(observation => observation.Device.PhysicalGuid == pin.Device.PhysicalGuid && observation.Device.ProductGuid == pin.Device.ProductGuid).ToArray() : [];
            context.PhysicalMap = observations.Length == 1 && context.Session is not null && context.SelectedDevice is { } slot
                ? context.PhysicalMapFor(slot, observations[0]) : new([], "Link the controller and rescan its inputs on Devices.");
        }

        internal async Task RefreshHardwareAsync()
        {
            if (context.Disposed || context.Busy) return;
            _input.Stop();
            if (!context.TryStartActivity(WorkspaceActivityKind.Scan, out var lease).Enabled) return;
            using var scan = lease!;
            refreshChrome();
            _input.Stop();
            try
            {
                context.Observed = context.Services.Discover();
                var inputs = await context.Services.DiscoverInputsAsync();
                if (context.Disposed || scan.Cancellation.IsCancellationRequested || scan.Identity != context.State.Identity) return;
                context.ControllerInputs = inputs;
            }
            catch (Exception error)
            {
                if (context.Disposed || scan.Cancellation.IsCancellationRequested || scan.Identity != context.State.Identity) return;
                context.ControllerInputs = []; reportError(error, "Controller input scan failed");
            }
            finally
            {
                scan.Dispose(); if (!context.Disposed) refreshChrome();
            }
        }
        #endregion END private helpers
    }
}
