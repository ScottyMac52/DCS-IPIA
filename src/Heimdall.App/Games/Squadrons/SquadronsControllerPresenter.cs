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

namespace Heimdall.App.Games.Squadrons
{
    /// <summary>Owns native slot linking, identification and verified foreground input evidence.</summary>
    internal sealed class SquadronsControllerPresenter(
        SquadronsWorkspaceContext context,
        Func<SquadronsAssignmentEditing> editing,
        Action<bool> refresh,
        Action<Action> run,
        ListBox DevicePicker,
        TextBlock StatusText,
        TextBlock ControllerSummary,
        GameFeatureNavigation FeaturePicker,
        SquadronsDevicesView _devicesView,
        SquadronsAssignmentsView _assignmentsView,
        Window owner,
        Func<GameControllerChange, bool>? _reviewChange,
        Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? identificationFactory,
        Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? inputFactory,
        HardwareDiagramPresenter drawingPresenter) : IDisposable
    {
        private HardwareInputFeedback _input = null!;
        private GameControllerIdentification _identification = null!;
        #region BEGIN Public
        internal HardwareInputFeedback Input => _input;
        internal GameControllerIdentification Identification => _identification;
        internal void Start()
        {
            _identification = new(owner, ShowIdentification, ClearIdentification, message => StatusText.Text = message, identificationFactory, context);
            _input = new(owner, drawingPresenter, message => StatusText.Text = message, ShowInputActivity, inputFactory, context);
            _input.StateChanged += InputStateChanged;
        }
        /// <summary>Releases foreground input handles and invalidates outstanding discovery scans.</summary>
        public void Dispose()
        {
            context.StopActivity();
            _input.StateChanged -= InputStateChanged;
            _input.Dispose(); _identification.Dispose();
        }
        #endregion END Public
        #region BEGIN private helpers
        internal async Task RefreshControllerInputsAsync()
        {
            if (context.Disposed) return;
            StopLiveInput();
            if (!context.TryStartActivity(WorkspaceActivityKind.Scan, out var lease).Enabled) return;
            using var scan = lease!; context.ControllerInputs = [];
            try
            {
                refresh(false); StatusText.Text = "Inspecting connected controller inputs…";
                var inputs = await context.Services.DiscoverInputsAsync();
                if (scan.Cancellation.IsCancellationRequested || context.Disposed || scan.Identity != context.State.Identity) return;
                context.ControllerInputs = inputs;
                StatusText.Text = "Controller inputs inspected. Physical input support does not establish Squadrons slot identity.";
            }
            catch (Exception error) { if (!scan.Cancellation.IsCancellationRequested && !context.Disposed) StatusText.Text = "Controller input verification unavailable: " + error.Message; }
            finally { scan.Dispose(); if (!context.Disposed) run(() => refresh(false)); }
        }

        internal void RescanControllers()
        {
            StopLiveInput();
            var observed = context.Services.Discover();
            _devicesView.ConnectedPicker.ItemsSource = observed.Where(x => x.Connected && x.PhysicalGuid != Guid.Empty && x.ProductGuid != Guid.Empty && EditionAccess.Policy.AllowsDevice(x))
                .GroupBy(x => x.PhysicalGuid).Where(x => x.Count() == 1).Select(x => x.Single()).ToArray();
            _devicesView.ConnectedPicker.SelectedIndex = -1; UpdateControllerAvailability();
        }

        internal void UpdateControllerAvailability()
        {
            var selected = context.SelectedController;
            var workspaceReady = !context.Busy && !context.ScanningInputs && context.Session?.CanLinkCapturedDevices == true && context.Session.AccessMessage.Length == 0;
            var ready = selected is not null && workspaceReady;
            var running = _identification.Running || (_input?.Running ?? false);
            var pin = selected is null ? null : context.Session?.HardwarePin(selected);
            _devicesView.RescanControllerButton.IsEnabled = !context.Busy && !running && !context.ScanningInputs;
            var links = ControllerLinkState.For(pin is not null, selected is not null && context.Session?.DrawingId(selected) is not null, workspaceReady, running);
            var refreshing = context.Refreshing;
            context.Refreshing = true;
            try
            {
                ControllerLinkPresentation.ShowPhysical(_devicesView.ConnectedPicker, pin?.Device,
                context.Services.Discover().Where(x => x.Connected && EditionAccess.Policy.AllowsDevice(x)));
            }
            finally { context.Refreshing = refreshing; }
            _devicesView.ConnectedPicker.IsEnabled = links.SelectPhysical;
            _devicesView.ConnectedPicker.ToolTip = pin is null ? "Choose a physical controller to link." : "Unlink physical controller before choosing another unit.";
            _devicesView.NativeControllerName.IsEnabled = workspaceReady && !running;
            _devicesView.IdentifyControllerButton.IsEnabled = links.SelectPhysical && _devicesView.ConnectedPicker.Items.Count > 0;
            _devicesView.AddControllerButton.IsEnabled = workspaceReady && !running && context.Session?.CanAddNativeControllers == true &&
                (pin is not null || _devicesView.ConnectedPicker.SelectedItem is DetectedDcsDevice { Connected: true } candidate &&
                    !string.IsNullOrWhiteSpace(_devicesView.NativeControllerName.Text) &&
                    !context.Session.IsPhysicalPinned(candidate.PhysicalGuid));
            _devicesView.RenameControllerButton.IsEnabled = ready && !running && context.Session?.CanRenameNativeControllers == true &&
                !string.IsNullOrWhiteSpace(_devicesView.NativeControllerName.Text);
            _devicesView.CancelIdentificationButton.IsEnabled = running;
            _devicesView.LinkControllerButton.IsEnabled = ready && links.SelectPhysical && _devicesView.ConnectedPicker.SelectedItem is DetectedDcsDevice { Connected: true };
            _devicesView.UnlinkControllerButton.IsEnabled = ready && links.UnlinkPhysical;
            FeatureActionBar.ShowAlternate(_devicesView.LinkControllerButton, _devicesView.UnlinkControllerButton, pin is not null);
            _devicesView.RemoveUnusedSlotButton.IsEnabled = ready && !running && pin is null;
            FeatureActionBar.ShowAlternate(_devicesView.AddControllerButton, _devicesView.RemoveUnusedSlotButton, selected is not null && pin is null && !_devicesView.AddControllerButton.IsEnabled);
            FeatureActionBar.ShowAlternate(_devicesView.IdentifyControllerButton, _devicesView.CancelIdentificationButton, _identification.Running);
            _devicesView.ClearSlotButton.IsEnabled = ready && !running && context.Rows.Any(x => x.Native.Type == 3 && x.DeviceId == selected);
            _devicesView.LinkStatus.Text = selected is null ? "Select connected hardware to add a controller, or select an existing slot to manage its link." : pin is null ? "Unlinked captured slot" :
                "Linked: " + pin.Device.Name + "\n" + pin.Device.PhysicalGuid + "\n" +
                (GameInputReadiness.Ready(pin, context.Services.Discover()) ? "Connected · exact instance and product" : "Unavailable · disconnected or identity changed. Rescan after reconnecting.");
            if (selected is not null && context.Session is not null)
                _devicesView.LinkStatus.Text += "\n" + SquadronsWorkspaceProjection.SlotDiagnostic(context.Session.Snapshot.Profile, context.Rows, selected);
            var systemSelection = DevicePicker.SelectedItem is GameDeviceChoice { Id: var systemId }
                && systemId.StartsWith("system.", StringComparison.Ordinal);
            ControllerSummary.Text = systemSelection ? "Native system assignments · read-only. Codes are shown exactly as captured." :
                selected is null ? "Select a controller." : pin is null ? "Not linked · open Controller setup." :
                pin.Device.Name + (GameInputReadiness.Ready(pin, context.Services.Discover()) ? " · Connected" : " · Unavailable");
            UpdateInputAvailability();
        }

        internal void SetupCollapsed(object sender, RoutedEventArgs e) { if (!context.Refreshing && ReferenceEquals(e.OriginalSource, _devicesView.ControllerSetup)) StopLiveInput(); }

        private void ClearIdentification()
        {
            _devicesView.IdentificationStatus.Text = ""; _devicesView.ConnectedPicker.SelectedIndex = -1;
            if (!context.Refreshing) run(UpdateControllerAvailability);
        }

        private void ShowIdentification(IReadOnlyDictionary<DetectedDcsDevice, string> activity)
        {
            _devicesView.ConnectedPicker.SelectedIndex = -1;
            if (activity.Count != 1) { _devicesView.IdentificationStatus.Text = "Several controllers moved. Operate just one physical unit."; return; }
            var detected = activity.Single();
            StopLiveInput();
            _devicesView.ConnectedPicker.SelectedItem = _devicesView.ConnectedPicker.Items.Cast<DetectedDcsDevice>().SingleOrDefault(x => x.PhysicalGuid == detected.Key.PhysicalGuid);
            _devicesView.IdentificationStatus.Text = detected.Key.Name + " · " + detected.Value + "\n" + detected.Key.PhysicalGuid + "\nPhysical unit identified. Verify its captured slot in Squadrons, then review the link.";
            UpdateControllerAvailability();
        }

        private void ReviewLink(GameControllerChange change)
        {
            StopSamplingForReview();
            if (!(_reviewChange?.Invoke(change) ?? new ControllerChangeReviewWindow(owner, change).ShowDialog() == true)) return;
            context.Session!.ApplyControllerChange(change); refresh(false); StatusText.Text = "Physical link staged. Native slots and bindings are unchanged. Save to keep this link.";
        }

        internal void LinkClicked(object sender, RoutedEventArgs e) => run(() =>
        {
            if (!_devicesView.LinkControllerButton.IsEnabled || context.SelectedController is not { } id || _devicesView.ConnectedPicker.SelectedItem is not DetectedDcsDevice device) return;
            ReviewLink(context.Session!.PreviewCapturedDeviceLink(id, device.PhysicalGuid));
        });

        internal void UnlinkClicked(object sender, RoutedEventArgs e) => run(() =>
        {
            if (_devicesView.UnlinkControllerButton.IsEnabled && context.SelectedController is { } id) ReviewLink(context.Session!.PreviewCapturedDeviceUnlink(id));
        });

        internal void ReviewNativeChange(GameControllerChange change)
        {
            StopSamplingForReview();
            var session = context.Session!;
            var result = editing().ExecuteAssignment("", "", "", () =>
                (_reviewChange?.Invoke(change) ?? new ControllerChangeReviewWindow(owner, change).ShowDialog() == true)
                    ? new(true, true, "") : new(false, false, "Review canceled. No assignments changed."),
                () => session.ApplyControllerChange(change));
            if (result.Outcome != DraftOutcome.Applied) { editing().AssignmentFeedback(result); return; }
            refresh(false);
            if (change.ResultDeviceId is { } id)
                DevicePicker.SelectedItem = DevicePicker.Items.Cast<GameDeviceChoice>().FirstOrDefault(x => x.Id == id);
            editing().AssignmentFeedback(result);
        }

        internal void ClearSlotClicked(object sender, RoutedEventArgs e) => run(() =>
        {
            if (_devicesView.ClearSlotButton.IsEnabled && context.SelectedController is { } id)
                ReviewNativeChange(context.Session!.PreviewClearCapturedAssignments(id));
        });

        internal void RemoveUnusedSlotClicked(object sender, RoutedEventArgs e) => run(() =>
        {
            if (!_devicesView.RemoveUnusedSlotButton.IsEnabled || context.SelectedController is not { } id) return;
            var change = context.Session!.PreviewRemoveUnusedCapturedDevice(id);
            if (!(_reviewChange?.Invoke(change) ?? new ControllerChangeReviewWindow(owner, change).ShowDialog() == true)) return;
            context.Session.ApplyControllerChange(change);
            refresh(false);
            StatusText.Text = "Unused slot removed from staged profile. Save before exporting.";
        });

        internal void IdentifyClicked(object sender, RoutedEventArgs e) => run(() =>
        {
            if (!_devicesView.IdentifyControllerButton.IsEnabled) return;
            _identification.Start(context.Services.Discover()); UpdateControllerAvailability();
            _devicesView.IdentificationStatus.Text = "Press a button, move a hat or move an axis noticeably on one controller. Held switches are ignored until pressed again.";
        });

        internal void StopIdentificationClicked(object sender, RoutedEventArgs e) => StopLiveInput();

        internal async void RescanClicked(object sender, RoutedEventArgs e)
        {
            if (!_devicesView.RescanControllerButton.IsEnabled) return;
            run(RescanControllers); await RefreshControllerInputsAsync();
        }

        internal void ConnectedChanged(object sender, SelectionChangedEventArgs e)
        {
            if (context.Refreshing) return;
            run(() => { RefreshNativeControllerName(); UpdateControllerAvailability(); });
        }

        internal void RefreshNativeControllerName()
        {
            if (context.Session is null) { _devicesView.NativeControllerName.Text = ""; return; }
            var snapshot = context.Session.Snapshot;
            var slot = context.SelectedController;
            var current = snapshot.Profile.Devices.FirstOrDefault(x => x.LogicalId == slot)?.Identity?.WindowsName;
            var pin = slot is null ? null : snapshot.Hardware.GetValueOrDefault(slot);
            if (_devicesView.ConnectedPicker.SelectedItem is DetectedDcsDevice device)
            {
                var matching = pin?.Device.PhysicalGuid == device.PhysicalGuid || current == device.Name || current?.StartsWith(device.Name + " ", StringComparison.OrdinalIgnoreCase) == true;
                _devicesView.NativeControllerName.Text = context.Backend.SuggestNativeControllerName(snapshot.Profile, device.Name, matching ? slot : null);
            }
            else _devicesView.NativeControllerName.Text = pin is null ? current ?? "" : context.Backend.SuggestNativeControllerName(snapshot.Profile, pin.Device.Name, slot);
        }

        internal void RenameControllerClicked(object sender, RoutedEventArgs e) => run(() =>
        {
            if (_devicesView.RenameControllerButton.IsEnabled && context.SelectedController is { } slot)
                ReviewNativeChange(context.Session!.PreviewRenameNativeController(slot, _devicesView.NativeControllerName.Text));
        });

        internal void AddControllerClicked(object sender, RoutedEventArgs e) => run(() =>
        {
            if (!_devicesView.AddControllerButton.IsEnabled) return;
            if (context.SelectedController is { } slot && context.Session!.HardwarePin(slot) is not null)
            {
                DevicePicker.SelectedIndex = 0;
                _devicesView.ConnectedPicker.SelectedIndex = -1;
                _devicesView.ConnectedPicker.Focus();
                _devicesView.ConnectedPicker.IsDropDownOpen = true;
                return;
            }
            if (_devicesView.ConnectedPicker.SelectedItem is not DetectedDcsDevice device) return;
            ReviewNativeChange(context.Session!.PreviewAddNativeController(device.PhysicalGuid, _devicesView.NativeControllerName.Text));
        });

        private void StopSamplingForReview()
        {
            _input.Stop();
            if (_identification.Running) _identification.Stop();
        }

        internal void StopLiveInput()
        {
            _input?.Stop();
            _identification?.Stop();
        }

        private void InputStateChanged(object? sender, EventArgs e)
        {
            UpdateInputAvailability(); editing().UpdateApplyAvailability(); UpdateControllerAvailability();
        }

        private void UpdateInputAvailability()
        {
            if (_input is null) return;
            var pin = context.SelectedController is { } slot ? context.Session?.HardwarePin(slot) : null;
            AssignmentActionPresentation.Show(_assignmentsView.StartInputButton, editing().AssignmentAction(WorkspaceAssignmentAction.StartInput,
                FeaturePicker.SelectedIndex == 1 && pin is not null && GameInputReadiness.Ready(pin, context.Services.Discover()),
                "Select one linked, connected controller for input testing."));
            AssignmentActionPresentation.Show(_assignmentsView.StopInputButton, editing().AssignmentAction(WorkspaceAssignmentAction.StopInput, true));
            FeatureActionBar.ShowAlternate(_assignmentsView.StartInputButton, _assignmentsView.StopInputButton, _input.Running);
        }

        internal void StartInputClicked(object sender, RoutedEventArgs e) => run(() =>
        {
            UpdateInputAvailability();
            if (!_assignmentsView.StartInputButton.IsEnabled || context.SelectedController is not { } slot ||
                context.Session?.HardwarePin(slot) is not { } pin) return;
            _identification.Stop();
            _input.Start(pin, context.Services.Discover());
        });

        internal void StopInputClicked(object sender, RoutedEventArgs e) => StopLiveInput();

        private void ShowInputActivity(string text)
        {
            _assignmentsView.InputActivity.Text = text;
            _assignmentsView.InputActivity.ScrollToEnd();
        }
        #endregion END private helpers
    }
}
