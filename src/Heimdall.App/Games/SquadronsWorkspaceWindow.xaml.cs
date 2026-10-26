using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Microsoft.Win32;

namespace Heimdall.App.Games
{
    /// <summary>Hosts captured Squadrons profiles with guarded native editing, shared persistence and review-only export.</summary>
    public partial class SquadronsWorkspaceWindow : Window, IDisposable
    {
        private readonly GameWorkspaceServices _services;
        private readonly SquadronsProfileBackend _backend = new();
        private readonly Func<MessageBoxResult>? _pendingChoice;
        private readonly Func<GameControllerChange, bool>? _reviewChange;
        private readonly GameControllerIdentification _identification;
        private readonly GameControllerReordering _reordering;
        private GameProfileSession? _session;
        private string? _revision;
        private string? _sourceDirectory;
        private IReadOnlyList<SquadronsWorkspaceRow> _rows = [];
        private IReadOnlyList<ControllerObservation> _controllerInputs = [];
        private int _inputScan;
        private bool _scanningInputs;
        private bool _disposed;
        private bool _refreshing = true;
        private bool _allowClose;
        /// <summary>Opens an explicitly chosen Squadrons workspace without importing active settings automatically.</summary>
        public SquadronsWorkspaceWindow(GameWorkspaceServices services, Guid? initial = null, Func<MessageBoxResult>? pendingChoice = null,
            Func<GameControllerChange, bool>? reviewChange = null, Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? identificationFactory = null)
        {
            EditionAccess.Policy.RequireSimulator("star-wars-squadrons");
            _services = services; _pendingChoice = pendingChoice; _reviewChange = reviewChange;
            InitializeComponent(); AppearanceManager.Attach(this);
            _reordering = new(DevicePicker, (source, target, after) =>
            {
                if (source == "all" || target == "all") return;
                Run(() => ReviewNativeChange(_session!.PreviewMoveCapturedSlot(source, target, after)));
            }, id => id != "all");
            _identification = new(this, ShowIdentification, ClearIdentification, message => StatusText.Text = message, identificationFactory);
            GamePicker.SelectedIndex = 2;
            SectionPicker.ItemsSource = new[] { "All", "Flight", "Menus & General", "Hangar & Briefing Room", "Other" }; SectionPicker.SelectedIndex = 1;
            TypePicker.ItemsSource = new[] { "All inputs", "Supported physical inputs", "Axes", "Buttons and hats", "Preserved inputs" }; TypePicker.SelectedIndex = 0;
            Loaded += async (_, _) => await RefreshControllerInputsAsync();
            Closing += ConfirmClose;
            Closed += (_, _) => Dispose();
            _refreshing = false;
            ReloadChoices();
            if (initial is { } id) Run(() => LoadProfile(id));
            else Run(() => Refresh());
            Run(RescanControllers);
        }
        #region BEGIN Public
        /// <summary>Releases identification handles and foreground lifecycle subscriptions.</summary>
        public void Dispose() { if (_disposed) return; _disposed = true; _inputScan++; _identification.Dispose(); _reordering.Dispose(); GC.SuppressFinalize(this); }
        /// <summary>Refreshes capability evidence asynchronously; failed, obsolete or closed-window scans cannot enable assignments.</summary>
        internal async Task RefreshControllerInputsAsync()
        {
            if (_disposed) return;
            var scan = ++_inputScan; _scanningInputs = true; _controllerInputs = [];
            try
            {
                Refresh(false); StatusText.Text = "Inspecting connected controller inputs…";
                var inputs = await _services.DiscoverInputsAsync();
                if (scan != _inputScan || _disposed) return;
                _controllerInputs = inputs;
                StatusText.Text = "Controller inputs inspected. Physical input support does not establish Squadrons slot identity.";
            }
            catch (Exception error) { if (scan == _inputScan && !_disposed) StatusText.Text = "Controller input verification unavailable: " + error.Message; }
            finally { if (scan == _inputScan && !_disposed) { _scanningInputs = false; Run(() => Refresh(false)); } }
        }
        /// <summary>Gets the game explicitly requested after the pending-change guard accepted leaving.</summary>
        public string? RequestedGame { get; private set; }
        /// <summary>Gets the isolated editing boundary for deterministic Windows smoke checks.</summary>
        internal GameProfileSession? Session => _session;
        /// <summary>Samples the shared identification boundary deterministically in Windows smoke tests.</summary>
        internal void SampleIdentification() => _identification.Sample();
        /// <summary>Imports one selected native file with bounded input and preserves the current session on failure.</summary>
        internal void ImportFile(string path)
        {
            if (!ConfirmLeave()) return;
            _identification.Stop();
            if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("Squadrons controls exceed the 4 MiB import limit.");
            var profile = _backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = File.ReadAllText(path) });
            var name = "Squadrons Controls"; var number = 2; var existing = _services.Store.List().Where(x => x.BackendId == _backend.SimId).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            while (existing.Contains(name)) name = "Squadrons Controls " + number++;
            var workspace = new GameWorkspace(1, Guid.NewGuid(), name, profile, new());
            var opened = CreateSession(workspace);
            _session = opened; _revision = null; _sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
            _services.Remember(_services.Recent() with { BackendId = _backend.SimId, WorkspaceId = null, DisplayName = "" });
            ReloadChoices(); Refresh(); StatusText.Text = "Controls imported. Save this profile before exporting.";
        }
        /// <summary>Saves through the shared optimistic revision boundary; failures retain pending edits.</summary>
        internal bool SaveProfile()
        {
            try
            {
                if (_session is null) return false;
                _session.Rename(ProfileName.Text);
                _revision = _services.Store.Save(_session.Snapshot, _revision);
                _session.AcceptSaved(); Remember(); ReloadChoices(); Refresh();
                StatusText.Text = "Squadrons profile saved."; return true;
            }
            catch (Exception error) { StatusText.Text = "Unable to save: " + error.Message; UpdateAvailability(); return false; }
        }
        /// <summary>Writes a new review folder after saving; no active game settings are replaced.</summary>
        internal string ExportReview(string parent)
        {
            if (_session is null || Pending) throw new InvalidOperationException("Save the Squadrons profile before exporting.");
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var roots = new List<string> { Path.Combine(documents, "STAR WARS Squadrons", "settings"), Path.Combine(documents, "STAR WARS Squadrons Steam", "settings") };
            if (_sourceDirectory is not null) roots.Add(_sourceDirectory);
            var directory = GameReviewExport.Write(parent, _session.Export().Files, roots, backendId: _backend.SimId);
            StatusText.Text = "Review copy exported to " + directory + ". Close Squadrons and back up its settings before manually installing this copy.";
            return directory;
        }
        #endregion END Public
        #region BEGIN private helpers
        private bool Pending => _session is not null && (_revision is null || _session.HasChanges || ProfileName.Text != _session.Snapshot.Name);
        private GameProfileSession CreateSession(GameWorkspace workspace)
        {
            if (workspace.Profile.BackendId != _backend.SimId) throw new InvalidDataException("This is not a Squadrons profile.");
            return new(_backend, workspace, EditionAccess.Policy, [], _services.Discover, controllerInputs: () => _controllerInputs);
        }
        private void LoadProfile(Guid id)
        {
            _identification.Stop();
            var saved = _services.Store.Load(id); var opened = CreateSession(saved.Workspace);
            _session = opened; _revision = saved.Revision; _sourceDirectory = null;
            Remember(); ReloadChoices(); Refresh();
        }
        private void Remember()
        {
            if (_session is not null) _services.Remember(_services.Recent() with { BackendId = _backend.SimId, WorkspaceId = _session.Snapshot.Id, DisplayName = _session.Snapshot.Name });
        }
        private void ReloadChoices()
        {
            _refreshing = true;
            try
            {
                ProfilePicker.ItemsSource = _services.Store.List().Where(x => x.BackendId == _backend.SimId).ToArray();
                ProfilePicker.SelectedItem = ProfilePicker.Items.Cast<GameWorkspaceEntry>().FirstOrDefault(x => x.Id == _session?.Snapshot.Id);
            }
            finally { _refreshing = false; }
        }
        private void Refresh(bool resetName = true)
        {
            var selected = (DevicePicker.SelectedItem as GameDeviceChoice)?.Id;
            _refreshing = true;
            var snapshot = _session?.Snapshot;
            if (resetName) ProfileName.Text = snapshot?.Name ?? "";
            var devices = snapshot is null ? [] : SquadronsWorkspaceProjection.OrderedDevices(snapshot.Profile).Select(x => new GameDeviceChoice(x.LogicalId, "Slot " + x.Identity!.SimSpecificIds["squadrons.slot"] + " · " + x.Identity.WindowsName)).ToArray();
            DevicePicker.ItemsSource = new[] { new GameDeviceChoice("all", "All devices and inputs") }.Concat(devices).ToArray();
            DevicePicker.SelectedItem = DevicePicker.Items.Cast<GameDeviceChoice>().FirstOrDefault(x => x.Id == selected) ?? DevicePicker.Items[0];
            TargetDevice.ItemsSource = devices;
            var verified = new Dictionary<string, IReadOnlyCollection<string>>();
            if (snapshot is not null)
                foreach (var (id, pin) in snapshot.Hardware)
                    if (GameControllerInputs.VerifiedKeys(pin, _controllerInputs) is { } keys) verified[id] = keys;
            _rows = snapshot is null ? [] : SquadronsWorkspaceProjection.ReviewInputs(SquadronsWorkspaceProjection.Rows(snapshot.Profile), verified);
            _refreshing = false; RefreshRows(); UpdateAvailability(); UpdateControllerAvailability();
        }
        private void RefreshRows()
        {
            if (_refreshing) return;
            var selected = (BindingsGrid.SelectedItem as SquadronsWorkspaceRow)?.RecordId;
            var review = ReviewCaptured.IsChecked == true;
            var section = SectionPicker.SelectedItem as string ?? "Flight";
            var rows = SquadronsWorkspaceProjection.Filter(_rows, SectionPicker.SelectedItem as string ?? "All", TypePicker.SelectedItem as string ?? "All inputs", Search.Text,
                (DevicePicker.SelectedItem as GameDeviceChoice)?.Id ?? "all", includeUnverified: review);
            BindingsGrid.ItemsSource = rows;
            BindingsGrid.SelectedItem = rows.FirstOrDefault(x => x.RecordId == selected);
            ContextColumn.Visibility = section == "All" ? Visibility.Visible : Visibility.Collapsed;
            ControllerColumn.Visibility = review || SelectedController is null ? Visibility.Visible : Visibility.Collapsed;
            HardwareColumn.Visibility = BindingColumn.Visibility = review ? Visibility.Visible : Visibility.Collapsed;
            CountText.Text = review ? rows.Count + " captured records · " + rows.Count(x => x.HardwareStatus == "Input absent on linked device") + " inputs absent on linked hardware" :
                rows.Count + " captured joystick assignments · " + section + ". Check hardware status in advanced review; saved slot ownership is not physical identity.";
            EmptyState.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyState.Text = _session is null ? "Import a Squadrons controls file to get started." : review ? "No matching captured records. Change the context, input filter or search." :
                _scanningInputs ? "Inspecting controller inputs…" : "No captured joystick assignments match these filters. Change the context or input filter, or enable advanced review for preserved native records.";
            UpdateEditor();
        }
        private void UpdateEditor()
        {
            var row = BindingsGrid.SelectedItem as SquadronsWorkspaceRow;
            AssignmentEditor.Visibility = row is null ? Visibility.Collapsed : Visibility.Visible;
            var editable = row?.Editable == true && _session?.AccessMessage.Length == 0;
            TargetDevice.IsEnabled = editable && !_scanningInputs;
            TargetDevice.SelectedItem = TargetDevice.Items.Cast<GameDeviceChoice>().FirstOrDefault(x => x.Id == row?.DeviceId);
            AssignmentHint.Text = row is null ? "Select an assignment to edit." : editable
                ? row.CommandName + " · " + row.BindingLabel + ". Applying replaces only this record; other assignments remain intact."
                : "This native record is preserved. Editing its input type or combination is not supported yet.";
            UpdateInputChoices();
        }
        private void UpdateInputChoices()
        {
            var row = BindingsGrid.SelectedItem as SquadronsWorkspaceRow;
            var pin = TargetDevice.SelectedItem is GameDeviceChoice target ? _session?.Snapshot.Hardware.GetValueOrDefault(target.Id) : null;
            var keys = _scanningInputs ? null : GameControllerInputs.VerifiedKeys(pin, _controllerInputs);
            InputPicker.ItemsSource = keys is null ? [] : SquadronsInputCodec.Choices.Where(x => keys.Contains(x.PhysicalKey, StringComparer.Ordinal)).ToArray();
            InputPicker.SelectedItem = InputPicker.Items.Cast<SquadronsInput>().FirstOrDefault(x => x == row?.Native.Input);
            InputPicker.IsEnabled = row?.Editable == true && TargetDevice.IsEnabled && keys is not null;
            UpdateApplyAvailability();
            if (row?.Editable == true)
                AssignmentHint.Text = keys is null ? "Link the destination captured slot to a connected controller and Rescan to verify its real inputs. No generic controls can be assigned." :
                    row.CommandName + " · " + row.BindingLabel + ". Only enumerated controls on the destination device are offered." +
                    (row.HardwareStatus == "Input absent on linked device" ? " The captured input does not exist in the linked device's verified controls; its native record is preserved for review." : "");
        }
        private void UpdateApplyAvailability() => ApplyButton.IsEnabled = InputPicker.IsEnabled && InputPicker.SelectedItem is SquadronsInput && _session?.AccessMessage.Length == 0;
        private void TargetChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing) Run(UpdateInputChoices); }
        private void InputChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing) UpdateApplyAvailability(); }
        private void UpdateAvailability()
        {
            if (_refreshing || SaveState is null) return;
            SaveButton.IsEnabled = DiscardButton.IsEnabled = Pending;
            ExportButton.IsEnabled = _session is not null && !Pending && _session.AccessMessage.Length == 0;
            ProfileName.IsEnabled = _session is not null;
            SaveState.Text = _session is null ? "No profile" : Pending ? "Unsaved changes" : "Saved";
        }
        private string? SelectedController => DevicePicker.SelectedItem is GameDeviceChoice { Id: not "all" } device ? device.Id : null;
        private void RescanControllers()
        {
            _identification.Stop();
            var observed = _services.Discover();
            ConnectedPicker.ItemsSource = observed.Where(x => x.Connected && x.PhysicalGuid != Guid.Empty && x.ProductGuid != Guid.Empty && EditionAccess.Policy.AllowsDevice(x))
                .GroupBy(x => x.PhysicalGuid).Where(x => x.Count() == 1).Select(x => x.Single()).ToArray();
            ConnectedPicker.SelectedIndex = -1; UpdateControllerAvailability();
        }
        private void UpdateControllerAvailability()
        {
            var selected = SelectedController;
            var ready = selected is not null && !_scanningInputs && _session?.CanLinkCapturedDevices == true && _session.AccessMessage.Length == 0;
            var running = _identification.Running;
            var pin = selected is null ? null : _session?.Snapshot.Hardware.GetValueOrDefault(selected);
            RescanControllerButton.IsEnabled = !running && !_scanningInputs;
            ConnectedPicker.IsEnabled = ready && !running;
            IdentifyControllerButton.IsEnabled = ready && !running && ConnectedPicker.Items.Count > 0;
            CancelIdentificationButton.IsEnabled = running;
            LinkControllerButton.IsEnabled = ready && !running && ConnectedPicker.SelectedItem is DetectedDcsDevice;
            UnlinkControllerButton.IsEnabled = ready && !running && pin is not null;
            RemoveUnusedSlotButton.IsEnabled = ready && !running && pin is null;
            ClearSlotButton.IsEnabled = ready && !running && _rows.Any(x => x.Native.Type == 3 && x.DeviceId == selected);
            LinkStatus.Text = selected is null ? "Select a captured slot to manage its physical link." : pin is null ? "Unlinked captured slot" :
                "Linked: " + pin.Device.Name + "\n" + pin.Device.PhysicalGuid + "\n" +
                (GameInputReadiness.Ready(pin, _services.Discover()) ? "Connected · exact instance and product" : "Unavailable · disconnected or identity changed. Rescan after reconnecting.");
            if (selected is not null && _session is not null)
                LinkStatus.Text += "\n" + SquadronsWorkspaceProjection.SlotDiagnostic(_session.Snapshot.Profile, _rows, selected);
            ControllerSummary.Text = selected is null ? "Select a controller." : pin is null ? "Not linked · open Controller setup." :
                pin.Device.Name + (GameInputReadiness.Ready(pin, _services.Discover()) ? " · Connected" : " · Unavailable");
        }
        private void CaptureReviewChanged(object sender, RoutedEventArgs e) { if (!_refreshing) RefreshRows(); }
        private void SetupCollapsed(object sender, RoutedEventArgs e) { if (!_refreshing && ReferenceEquals(e.OriginalSource, ControllerSetup)) _identification.Stop(); }
        private void ClearIdentification()
        {
            IdentificationStatus.Text = ""; ConnectedPicker.SelectedIndex = -1;
            if (!_refreshing) Run(UpdateControllerAvailability);
        }
        private void ShowIdentification(IReadOnlyDictionary<DetectedDcsDevice, string> activity)
        {
            ConnectedPicker.SelectedIndex = -1;
            if (activity.Count != 1) { IdentificationStatus.Text = "Several controllers moved. Operate just one physical unit."; return; }
            var detected = activity.Single();
            _identification.Stop();
            ConnectedPicker.SelectedItem = ConnectedPicker.Items.Cast<DetectedDcsDevice>().SingleOrDefault(x => x.PhysicalGuid == detected.Key.PhysicalGuid);
            IdentificationStatus.Text = detected.Key.Name + " · " + detected.Value + "\n" + detected.Key.PhysicalGuid + "\nPhysical unit identified. Verify its captured slot in Squadrons, then review the link.";
            UpdateControllerAvailability();
        }
        private void ReviewLink(GameControllerChange change)
        {
            if (!(_reviewChange?.Invoke(change) ?? new ControllerChangeReviewWindow(this, change).ShowDialog() == true)) return;
            _session!.ApplyControllerChange(change); Refresh(false); StatusText.Text = "Physical link staged. Native slots and bindings are unchanged. Save to keep this link.";
        }
        private void LinkClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (!LinkControllerButton.IsEnabled || SelectedController is not { } id || ConnectedPicker.SelectedItem is not DetectedDcsDevice device) return;
            ReviewLink(_session!.PreviewCapturedDeviceLink(id, device.PhysicalGuid));
        });
        private void UnlinkClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (UnlinkControllerButton.IsEnabled && SelectedController is { } id) ReviewLink(_session!.PreviewCapturedDeviceUnlink(id));
        });
        private void ReviewNativeChange(GameControllerChange change)
        {
            if (!(_reviewChange?.Invoke(change) ?? new ControllerChangeReviewWindow(this, change).ShowDialog() == true)) return;
            _session!.ApplyControllerChange(change);
            Refresh(false);
            if (change.ResultDeviceId is { } id)
                DevicePicker.SelectedItem = DevicePicker.Items.Cast<GameDeviceChoice>().FirstOrDefault(x => x.Id == id);
            StatusText.Text = "Native Squadrons assignments / slots updated in the draft. Save before exporting a review copy.";
        }
        private void ClearSlotClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (ClearSlotButton.IsEnabled && SelectedController is { } id)
                ReviewNativeChange(_session!.PreviewClearCapturedAssignments(id));
        });
                private void RemoveUnusedSlotClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (!RemoveUnusedSlotButton.IsEnabled || SelectedController is not { } id) return;
            var change = _session!.PreviewRemoveUnusedCapturedDevice(id);
            if (!(_reviewChange?.Invoke(change) ?? new ControllerChangeReviewWindow(this, change).ShowDialog() == true)) return;
            _session.ApplyControllerChange(change);
            Refresh(false);
            StatusText.Text = "Unused slot removed from staged profile. Save before exporting.";
        });
        private void IdentifyClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (!IdentifyControllerButton.IsEnabled) return;
            _identification.Start(_services.Discover()); UpdateControllerAvailability();
            IdentificationStatus.Text = "Press a button, move a hat or move an axis noticeably on one controller. Held switches are ignored until pressed again.";
        });
        private void StopIdentificationClicked(object sender, RoutedEventArgs e) => _identification.Stop();
        private async void RescanClicked(object sender, RoutedEventArgs e)
        {
            if (!RescanControllerButton.IsEnabled) return;
            Run(RescanControllers); await RefreshControllerInputsAsync();
        }
        private void ConnectedChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing) Run(UpdateControllerAvailability); }
        private void ApplyClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            if (BindingsGrid.SelectedItem is not SquadronsWorkspaceRow { Editable: true } row || TargetDevice.SelectedItem is not GameDeviceChoice device || InputPicker.SelectedItem is not SquadronsInput input)
                throw new InvalidOperationException("Choose an editable binding, captured controller and input.");
            _session!.RebindNative(row.RecordId, device.Id, input.Key, input.Type, input.Direction);
            Refresh(false); StatusText.Text = "Assignment staged. Save before exporting.";
        });
        private void ImportClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            var dialog = new OpenFileDialog { Title = "Import Squadrons controls", Filter = "Squadrons controls|ProfileOptions_profile_synced|All files|*.*" };
            if (dialog.ShowDialog(this) == true) ImportFile(dialog.FileName);
        });
        private void ExportClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            var dialog = new OpenFolderDialog { Title = "Choose a folder for a separate Squadrons review copy" };
            if (dialog.ShowDialog(this) == true) ExportReview(dialog.FolderName);
        });
        private void SaveClicked(object sender, RoutedEventArgs e) => SaveProfile();
        private void DiscardClicked(object sender, RoutedEventArgs e)
        {
            _identification.Stop();
            _session?.Discard(); Refresh();
            if (_revision is null) { _session = null; Refresh(); }
        }
        private void ProfileChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing || ProfilePicker.SelectedItem is not GameWorkspaceEntry entry || entry.Id == _session?.Snapshot.Id) return;
            if (ConfirmLeave()) Run(() => LoadProfile(entry.Id));
            _refreshing = true; ProfilePicker.SelectedItem = ProfilePicker.Items.Cast<GameWorkspaceEntry>().FirstOrDefault(x => x.Id == _session?.Snapshot.Id); _refreshing = false;
        }
        private void GameChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing || GamePicker.SelectedItem is not ComboBoxItem { Tag: string game } || game == "star-wars-squadrons") return;
            if (!ConfirmLeave()) { _refreshing = true; GamePicker.SelectedIndex = 2; _refreshing = false; return; }
            RequestedGame = game; _allowClose = true; Close();
        }
        private bool ConfirmLeave()
        {
            if (!Pending) return true;
            var choice = _pendingChoice?.Invoke() ?? ThemeMessageBox.Show(this, "Save changes to this Squadrons profile?", "Unsaved changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (choice == MessageBoxResult.Yes) return SaveProfile();
            if (choice != MessageBoxResult.No) return false;
            _session?.Discard(); return true;
        }
        private void ConfirmClose(object? sender, CancelEventArgs e) { _identification.Stop(); if (!_allowClose) e.Cancel = !ConfirmLeave(); }
        private void FilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing) return;
            if (ReferenceEquals(sender, DevicePicker)) { _identification.Stop(); Run(UpdateControllerAvailability); }
            RefreshRows();
        }
        private void SearchChanged(object sender, TextChangedEventArgs e) { if (!_refreshing) RefreshRows(); }
        private void BindingChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing) UpdateEditor(); }
        private void NameChanged(object sender, TextChangedEventArgs e) { if (!_refreshing) UpdateAvailability(); }
        private void Run(Action action) { try { action(); } catch (Exception error) { StatusText.Text = error.Message; } }
        #endregion END private helpers
    }
}
