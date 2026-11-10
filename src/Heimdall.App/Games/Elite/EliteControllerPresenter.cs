using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;
using Heimdall.EliteDangerous;

namespace Heimdall.App.Games.Elite
{
    /// <summary>Owns native ED controller identification, linking, discovery evidence and input-testing lifetime.</summary>
    internal sealed class EliteControllerPresenter(
        EliteWorkspaceContext context,
        Window owner,
        EliteDevicesView _devicesView,
        ListBox DevicePicker,
        HardwareDiagramPresenter drawing,
        Action<string> activity,
        Action<IReadOnlyDictionary<DetectedDcsDevice, string>> observations,
        Action clearIdentification,
        Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? inputFactory,
        Func<GameControllerChange, bool> _reviewControllerChange,
        Action refreshInputs,
        Func<bool> canStart,
        Action<Action> RunBusy,
        Action UpdateAvailability,
        Action<Action> Run,
        Action<string> Status) : IDisposable
    {
        private DetectedDcsDevice? _identifiedController;
        private GameControllerActions _controllerActions = new(false, false, false, false, false, false, false, false);
        private readonly Func<nint, IReadOnlyList<Guid>, IDeviceInputSource> _controllerInputs = inputFactory ?? ((handle, devices) => new WindowsDeviceInputSource(handle, devices));
        private readonly System.Windows.Threading.DispatcherTimer _readinessTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        private bool _lastReady;
        private IReadOnlyList<ControllerObservation> _inputEvidence = [];
        private GameProfileSession? _session => context.Session;
        private bool _refreshing { get => context.Refreshing; set => context.Refreshing = value; }
        private bool _busy => context.Busy;
        private GameWorkspaceServices _services => context.Services;
        private IReadOnlyList<WorkspaceHardware> _hardware => context.Hardware;
        private GameDeviceChoice? Device => context.Device;
        #region BEGIN Public
        internal void ClearIdentification()
        {
            _identifiedController = null;
            _devicesView.IdentificationStatus.Text = "";
            if (_identification is not null && _input is not null && !_refreshing) Run(UpdateControllerAvailability);
        }

        internal void IdentifyControllerClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            UpdateControllerAvailability(); if (!_controllerActions.Start) return;
            StopLiveInput();
            EditionAccess.Policy.RequirePremium();
            if (_session is null || Device is null || context.Feature != 0)
                throw new InvalidOperationException("Select a controller reference on Devices before identifying it.");
            _identification.Start(_services.Discover());
            UpdateControllerAvailability();
            _devicesView.IdentificationStatus.Text = "Listening to all connected controllers. Press a button, move a hat, or move an axis noticeably. Held switches are ignored until pressed again.";
            Status("Identify " + Device.Name + ": operate the physical controller you want to check.");
        });

        internal void ShowIdentification(IReadOnlyDictionary<DetectedDcsDevice, string> activity)
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
            var matches = context.Elite.DeviceReferences.Matches(identity, detected.Key);
            _devicesView.IdentificationStatus.Text = detected.Key.Name + " · " + detected.Value + Environment.NewLine + detected.Key.PhysicalGuid +
                Environment.NewLine + (matches ? "Matches selected ED reference: " : "Does not match selected ED reference: ") + Device.Name;
            if (!matches) return;
            _identifiedController = detected.Key;
            UpdateControllerAvailability();
            if (_session.ControllerReferences(detected.Key.PhysicalGuid).Count > 1)
                _devicesView.IdentificationStatus.Text += Environment.NewLine + "Identical products share this ID. Input identifies this physical unit, but cannot prove its ED index. Verify the selected indexed reference in Elite before confirming.";
        }

        internal void ConfirmIdentificationClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            UpdateControllerAvailability(); if (!_controllerActions.Confirm) return;
            EditionAccess.Policy.RequirePremium();
            if (_session is null || Device is null || _identifiedController is null || context.Feature != 0)
                throw new InvalidOperationException("Identify a matching controller before confirming its link.");
            var physical = _identifiedController.PhysicalGuid;
            var id = Device.Id;
            StopLiveInput();
            var connected = _services.Discover().Single(x => x.Connected && x.PhysicalGuid == physical);
            ConnectController(connected, id);
        });

        internal void CancelIdentificationClicked(object sender, RoutedEventArgs e)
        { _identification.Stop(); Status("Controller identification stopped."); }

        internal void UpdateControllerAvailability()
        {
            var document = _session?.Snapshot;
            var currentPin = Device is null ? null : document?.Hardware.GetValueOrDefault(Device.Id);
            _lastReady = EditionAccess.Policy.HasPremium && currentPin is not null && GameInputReadiness.Ready(currentPin, _services.Discover());
            var reference = document?.Profile.Devices.FirstOrDefault(x => x.LogicalId == Device?.Id);
            var physical = reference is not null && !GameWorkspaceAccess.IsSystemDevice(reference.Identity!.SimSpecificIds["ed.device"]);
            var observed = _services.Discover();
            var eligible = observed.Where(x => x.Connected && EditionAccess.Policy.AllowsDevice(x)).ToArray();
            var pin = reference is null ? null : document!.Hardware.GetValueOrDefault(reference.LogicalId);
            var refreshing = _refreshing;
            _refreshing = true;
            try { ControllerLinkPresentation.ShowPhysical(_devicesView.ConnectedPicker, pin?.Device, eligible); }
            finally { _refreshing = refreshing; }
            var selected = _devicesView.ConnectedPicker.SelectedItem as DetectedDcsDevice;
            var connected = selected is not null && eligible.Count(x => x.PhysicalGuid == selected.PhysicalGuid && x.ProductGuid == selected.ProductGuid) == 1;
            var alreadyLinked = connected && document is not null && document.Hardware.Values.Any(x => x.Device.PhysicalGuid == selected!.PhysicalGuid);
            var candidate = _identifiedController;
            var confirm = pin is null && document is not null && reference is not null && physical && EditionAccess.Policy.HasPremium && candidate is not null && eligible.Count(x => x.PhysicalGuid == candidate.PhysicalGuid && x.ProductGuid == candidate.ProductGuid) == 1
                && context.Elite.DeviceReferences.Matches(reference!.Identity!, candidate)
                && !document!.Hardware.Any(x => x.Key != reference.LogicalId && x.Value.Device.PhysicalGuid == candidate.PhysicalGuid)
                && (pin is null || pin.Device.PhysicalGuid == candidate.PhysicalGuid || !GameInputReadiness.Ready(pin, observed))
                && (eligible.Count(x => x.ProductGuid == candidate.ProductGuid) == 1 || reference.Identity!.SimSpecificIds.ContainsKey("ed.deviceIndex"));
            var indexedChoice = _devicesView.ReplacementReferencePicker.Items.Count <= 1 || _devicesView.ReplacementReferencePicker.SelectedItem is GameDeviceChoice;
            _controllerActions = GameControllerActions.For(pin is null && physical && EditionAccess.Policy.HasPremium && eligible.Length > 0,
                confirm, pin is not null || document is not null && connected && !alreadyLinked,
                pin is null && physical && EditionAccess.Policy.HasPremium && connected && pin?.Device.PhysicalGuid != selected!.PhysicalGuid && indexedChoice,
                physical, physical && pin is not null, context.Identifying,
                context.Testing, _busy, context.Feature == 0);
            _devicesView.IdentifyControllerButton.IsEnabled = _controllerActions.Start;
            _devicesView.CancelIdentificationButton.IsEnabled = _controllerActions.Stop;
            _devicesView.ConfirmIdentificationButton.IsEnabled = _controllerActions.Confirm;
            FeatureActionBar.ShowAlternate(_devicesView.IdentifyControllerButton, _devicesView.CancelIdentificationButton,
                context.Identifying);
            _devicesView.AddControllerButton.IsEnabled = _controllerActions.Add;
            _devicesView.ReplaceControllerButton.IsEnabled = _controllerActions.Replace;
            _devicesView.RemoveControllerButton.IsEnabled = _controllerActions.Remove;
            _devicesView.RescanControllerButton.IsEnabled = _controllerActions.Rescan;
            var drawingId = Device is null ? null : _session?.DrawingId(Device.Id);
            var links = ControllerLinkState.For(pin is not null, drawingId is not null, document is not null && (reference is null || physical) && _session?.AccessMessage.Length == 0,
                context.Identifying || context.Testing || _busy || context.Feature != 0);
            _devicesView.ConnectedPicker.IsEnabled = links.SelectPhysical;
            _devicesView.UnlinkControllerButton.IsEnabled = links.UnlinkPhysical;
            _devicesView.ChangeDrawingButton.IsEnabled = links.SelectDrawing;
            _devicesView.UseDrawingButton.IsEnabled = links.SelectDrawing;
            _devicesView.NoDrawingButton.IsEnabled = links.UnlinkDrawing;
            FeatureActionBar.ShowAlternate(_devicesView.ChangeDrawingButton, _devicesView.NoDrawingButton, drawingId is not null);
            _devicesView.DrawingTools.Visibility = !physical || drawingId is not null ? Visibility.Collapsed : _devicesView.DrawingTools.Visibility;
            _devicesView.HardwarePicker.IsEnabled = links.SelectDrawing;
            _refreshing = true;
            try { if (drawingId is not null) _devicesView.HardwarePicker.SelectedItem = _hardware.FirstOrDefault(x => x.DeviceId == drawingId); }
            finally { _refreshing = refreshing; }
            _devicesView.LinkReferenceButton.IsEnabled = links.SelectPhysical && connected;
        }

        internal void Rescan() => Run(() =>
        {
            StopLiveInput();
            _devicesView.ConnectedPicker.ItemsSource = _services.Discover()
                .Where(x => x.Connected && EditionAccess.Policy.AllowsDevice(x)).ToArray();
            _devicesView.ConnectedPicker.SelectedIndex = -1;
            _ = RefreshInputEvidenceAsync();
        });

        internal void RescanClicked(object sender, RoutedEventArgs e) => Run(() =>
        { UpdateControllerAvailability(); if (_controllerActions.Rescan) { Rescan(); UpdateControllerAvailability(); } });

        internal void AddControllerClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            UpdateControllerAvailability(); if (!_controllerActions.Add) return;
            if (Device is not null && _session?.HardwarePin(Device.Id) is not null)
            {
                DevicePicker.SelectedIndex = -1;
                _devicesView.ConnectedPicker.SelectedItem = null;
                UpdateControllerAvailability();
                Status("Choose a connected controller, then Add controller. Existing links are retained.");
                return;
            }
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
            _devicesView.ReferencePicker.ItemsSource = references.Select(id => new GameDeviceChoice(id, EliteWorkspaceContext.ControllerName(document, id))).ToArray();
            _devicesView.ReferencePicker.SelectedIndex = -1; _devicesView.ReferenceChoiceTools.Visibility = Visibility.Visible;
            Status("Choose which captured ED reference belongs to this controller. Existing links are retained until an explicit choice succeeds.");
        });

        internal void ConnectedChanged(object sender, SelectionChangedEventArgs e)
        { if (!_refreshing && _devicesView.ReferenceChoiceTools is not null) { _devicesView.ReferenceChoiceTools.Visibility = Visibility.Collapsed; Run(UpdateReplacementReferences); UpdateAvailability(); } }

        internal void UpdateReplacementReferences()
        {
            if (_devicesView.ReplacementReferencePicker is null) return;
            var selected = (_devicesView.ReplacementReferencePicker.SelectedItem as GameDeviceChoice)?.Id;
            var ids = _session is not null && _devicesView.ConnectedPicker.SelectedItem is DetectedDcsDevice connected
                ? _session.ControllerReferences(connected.PhysicalGuid) : [];
            var document = _session?.Snapshot;
            var choices = ids.Select(id => new GameDeviceChoice(id, EliteWorkspaceContext.ControllerName(document!, id))).ToArray();
            _devicesView.ReplacementReferencePicker.ItemsSource = choices;
            _devicesView.ReplacementReferencePicker.SelectedItem = choices.FirstOrDefault(x => x.Id == selected);
            _devicesView.ReplacementReferenceTools.Visibility = choices.Length > 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        internal void RemoveControllerClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            UpdateControllerAvailability(); if (!_controllerActions.Remove) return;
            StopLiveInput();
            if (_session is null || Device is null) throw new InvalidOperationException("Select the profile controller to remove.");
            ReviewControllerChange(_session.PreviewRemoveController(Device.Id));
        });

        internal void ReplaceControllerClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            UpdateControllerAvailability(); if (!_controllerActions.Replace) return;
            StopLiveInput();
            if (_session is null || Device is null || _devicesView.ConnectedPicker.SelectedItem is not DetectedDcsDevice connected)
                throw new InvalidOperationException("Select the old profile controller and its connected replacement.");
            var target = (_devicesView.ReplacementReferencePicker.SelectedItem as GameDeviceChoice)?.Id;
            var drawing = GameControllerDrawings.Suggest(_hardware.Where(x => EditionAccess.Policy.AllowsHardware(x.DeviceId)), connected);
            var change = _session.PreviewReplaceController(Device.Id, connected.PhysicalGuid,
                () => ReadReplacementInputs(connected.PhysicalGuid), target, drawing);
            ReviewControllerChange(change);
        });

        internal string[] ReadReplacementInputs(Guid physicalGuid)
        {
            EditionAccess.Policy.RequirePremium();
            using var source = _controllerInputs(new WindowInteropHelper(owner).EnsureHandle(), [physicalGuid]);
            if (!source.SupportedInputs.TryGetValue(physicalGuid, out var inputs))
                throw new InvalidOperationException("Replacement inputs are unavailable. Rescan and review again.");
            return inputs.Select(EliteInputKeys.FromShared).Where(x => x is not null).Select(x => x!).Distinct(StringComparer.Ordinal).ToArray();
        }

        internal void ReviewControllerChange(GameControllerChange change)
        {
            var confirmed = false;
            if (_busy) confirmed = _reviewControllerChange(change);
            else RunBusy(() => confirmed = _reviewControllerChange(change));
            if (!confirmed) { Status("Controller change cancelled; profile unchanged."); return; }
            _session!.ApplyControllerChange(change);
            if (change.ResultDeviceId is { } id) DevicePicker.SelectedItem = DevicePicker.Items.Cast<GameDeviceChoice>().Single(x => x.Id == id);
            _devicesView.ReferenceChoiceTools.Visibility = _devicesView.DrawingTools.Visibility = Visibility.Collapsed;
            Status("Reviewed controller change staged. Save to persist; Preview / Export to review the native ED bindings.");
        }

        internal void LinkReferenceClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            UpdateControllerAvailability(); if (!_devicesView.LinkReferenceButton.IsEnabled) return;
            if (_devicesView.ConnectedPicker.SelectedItem is not DetectedDcsDevice connected || _devicesView.ReferencePicker.SelectedItem is not GameDeviceChoice reference)
                throw new InvalidOperationException("Choose the connected controller and its captured ED reference.");
            ConnectController(connected, reference.Id);
        });

        internal void UnlinkControllerClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            UpdateControllerAvailability();
            if (_devicesView.UnlinkControllerButton.IsEnabled && _session is not null && Device is not null)
                ReviewControllerChange(_session.PreviewUnlinkController(Device.Id));
        });

        internal void ChangeDrawingClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            UpdateControllerAvailability(); if (!_devicesView.ChangeDrawingButton.IsEnabled) return;
            if (_session is null || Device is null) return;
            _devicesView.DrawingTools.Visibility = Visibility.Visible;
        });

        internal void UseDrawingClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (!_devicesView.UseDrawingButton.IsEnabled) return;
            if (_session is null || Device is null || _devicesView.HardwarePicker.SelectedItem is not WorkspaceHardware drawing)
                throw new InvalidOperationException("Choose an optional drawing first.");
            _session.ChangeDrawing(Device.Id, drawing.DeviceId); _devicesView.DrawingTools.Visibility = Visibility.Collapsed;
        });

        internal void NoDrawingClicked(object sender, RoutedEventArgs e) => Run(() =>
        { UpdateControllerAvailability(); if (_devicesView.NoDrawingButton.IsEnabled && _session is not null && Device is not null) { _session.ChangeDrawing(Device.Id, null); _devicesView.DrawingTools.Visibility = Visibility.Collapsed; } });

        internal void StartInputClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            UpdateAvailability();
            if (!canStart()) throw new InvalidOperationException("Choose one linked connected controller and stop any current test before starting.");
            _identification.Stop();
            if (_session is null || Device is null || _session.HardwarePin(Device.Id) is not { } pin)
                throw new InvalidOperationException("On Devices, select this reference and use Identify Controller to discover and confirm its physical controller first.");
            _input.Start(pin, _services.Discover());
        });

        internal void StopInputClicked(object sender, RoutedEventArgs e) { StopLiveInput(); Status("Input test stopped."); }
        internal HardwareInputFeedback Input { get; } = new(owner, drawing, Status, activity, inputFactory, context);
        internal bool Identifying => _identification.Running;
        internal IReadOnlyList<ControllerObservation> Evidence => _inputEvidence;
        internal void SampleIdentification() => _identification.Sample();
        internal void Start()
        {
            Input.StateChanged += InputStateChanged;
            _readinessTimer.Tick += HardwareReadinessChanged; _readinessTimer.Start();
        }
        /// <summary>Releases native discovery timers and input handles.</summary>
        public void Dispose()
        {
            context.StopActivity(); _readinessTimer.Stop(); _readinessTimer.Tick -= HardwareReadinessChanged;
            Input.StateChanged -= InputStateChanged; _identification.Dispose(); Input.Dispose();
        }
        #endregion END Public
        #region BEGIN private helpers
        private void HardwareReadinessChanged(object? sender, EventArgs e)
        {
            if (_session is null || !owner.IsActive || _busy) return;
            Run(() =>
            {
                var pin = Device is null ? null : _session.HardwarePin(Device.Id);
                var ready = EditionAccess.Policy.HasPremium && pin is not null && GameInputReadiness.Ready(pin, _services.Discover());
                if (ready != _lastReady) UpdateAvailability();
                else if (context.Feature == 0) UpdateControllerAvailability();
            });
        }

        private void InputStateChanged(object? sender, EventArgs e) { if (!_refreshing) UpdateAvailability(); }

        internal void StopLiveInput() { _input.Stop(); _identification.Stop(); }

        private async Task RefreshInputEvidenceAsync()
        {
            if (!context.TryStartActivity(WorkspaceActivityKind.Scan, out var lease).Enabled) return;
            using var scan = lease!;
            _inputEvidence = [];
            try
            {
                var observations = await _services.DiscoverInputsAsync();
                if (scan.Cancellation.IsCancellationRequested || scan.Identity != context.State.Identity) return;
                _inputEvidence = observations;
                refreshInputs();
            }
            catch (Exception error)
            {
                if (!scan.Cancellation.IsCancellationRequested) Status("Controller input scan failed: " + error.Message);
            }
        }

        private void ConnectController(DetectedDcsDevice connected, string? logicalId)
        {
            StopLiveInput();
            if (_session is null) return;
            var current = logicalId is null ? null : _session.HardwarePin(logicalId);
            var drawing = current is null ? _services.Inventory is not null ? _services.PreferredDrawing(connected.PhysicalGuid) :
                GameControllerDrawings.Suggest(_hardware.Where(x => EditionAccess.Policy.AllowsHardware(x.DeviceId)), connected) : current.HardwareId;
            if (drawing is not null && !_hardware.Any(x => x.DeviceId == drawing)) drawing = null;
            var id = _session.ConnectController(connected.PhysicalGuid, drawing, logicalId);
            _devicesView.ReferenceChoiceTools.Visibility = _devicesView.DrawingTools.Visibility = Visibility.Collapsed;
            DevicePicker.SelectedItem = DevicePicker.Items.Cast<GameDeviceChoice>().Single(x => x.Id == id);
            UpdateAvailability();
            Status("Controller linked. Unlink physical controller or drawing before changing its selection." + (_session.AccessMessage.Length > 0 ? " " + _session.AccessMessage : ""));
        }

        private HardwareInputFeedback _input => Input;
        private readonly GameControllerIdentification _identification = new(owner, observations, clearIdentification, Status, inputFactory, context);
        #endregion END private helpers
    }
}
