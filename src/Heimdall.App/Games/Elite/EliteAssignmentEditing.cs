using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using BindingMode = Heimdall.Domain.MultiSim.BindingMode;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;

namespace Heimdall.App.Games.Elite
{
    /// <summary>Owns ED primary/secondary slots, modifiers, input choices and native assignment review/application.</summary>
    internal sealed class EliteAssignmentEditing(
        EliteWorkspaceContext context,
        EliteAssignmentsView _assignmentsView,
        ListBox DevicePicker,
        Func<IReadOnlyList<ControllerObservation>> evidence,
        Action refreshAvailability,
        Action<Action> Run,
        Action<string> Status)
    {
        private readonly List<EliteModifierChoice> _modifiers = [];
        private bool _loadingAssignment;
        private GameAssignmentActions _actions = new(false, false, false, false);
        private GameProfileSession? _session => context.Session;
        private bool _refreshing { get => context.Refreshing; set => context.Refreshing = value; }
        private bool _busy => context.Busy;
        private IReadOnlyList<EliteAssignmentRow> _rows => context.Rows;
        private GameAssignmentSelection _selection => context.Selection;
        private GameWorkspaceServices _services => context.Services;
        private IReadOnlyList<WorkspaceHardware> _hardware => context.Hardware;
        private GameDeviceChoice? Device => context.Device;
        private SimCommand? Command => context.Command;
        private string Slot => context.Selection.Slot;
        #region BEGIN Public
        internal void AssignmentTextChanged(object sender, TextChangedEventArgs e) => DraftChanged();

        internal void AssignmentModeChanged(object sender, SelectionChangedEventArgs e) => DraftChanged();

        internal void AssignmentOptionChanged(object sender, RoutedEventArgs e) => DraftChanged();

        internal void LoadAssignment()
        {
            if (_refreshing || _session is null) return;
            _loadingAssignment = true;
            _assignmentsView.Selection.ShowCommand(Command);
            var axis = Command?.Type == CommandType.Axis;
            _selection.SelectSlot(axis ? "Binding" : _assignmentsView.SlotPicker.SelectedIndex == 1 ? "Secondary" : "Primary");
            _assignmentsView.AxisTools.Visibility = axis ? Visibility.Visible : Visibility.Collapsed;
            _assignmentsView.ModifierTools.Visibility = axis ? Visibility.Collapsed : Visibility.Visible;
            _assignmentsView.SlotPicker.IsEnabled = _assignmentsView.ModePicker.IsEnabled = !axis;
            _assignmentsView.SlotPicker.Visibility = _assignmentsView.ModePicker.Visibility = axis ? Visibility.Collapsed : Visibility.Visible;
            _assignmentsView.AxisSlotLabel.Visibility = axis ? Visibility.Visible : Visibility.Collapsed;
            var row = _rows.FirstOrDefault(x => x.DeviceId == Device?.Id && x.CommandId == Command?.Id && x.Slot == Slot);
            _assignmentsView.InputKey.ItemsSource = Device is not null && Command is not null ? DeviceInputs(Device.Id, axis) : [];
            SelectInput(row?.Input ?? ""); _assignmentsView.InvertAxis.IsChecked = row?.Invert ?? false;
            _assignmentsView.AxisDeadzone.Text = (row?.Deadzone ?? 0).ToString(CultureInfo.CurrentCulture);
            _assignmentsView.ModePicker.SelectedIndex = row?.Mode == BindingMode.Hold ? 1 : 0;
            _modifiers.Clear();
            if (row is not null) foreach (var modifier in row.Modifiers) _modifiers.Add(new(modifier,
                (_assignmentsView.ModifierDevicePicker.Items.Cast<GameDeviceChoice>().FirstOrDefault(x => x.Id == modifier.Source)?.Name ?? modifier.Source)
                    + ": " + (DeviceInputs(modifier.Source).FirstOrDefault(x => x.Key == modifier.Value)?.DisplayName ?? modifier.Value)));
            _assignmentsView.ModifierTools.IsExpanded = _modifiers.Count > 0;
            _assignmentsView.ModifiersList.ItemsSource = null; _assignmentsView.ModifiersList.ItemsSource = _modifiers;
            var key = "ed.toggle:" + Command?.Id;
            var metadata = _session.Snapshot.Profile.Metadata;
            _assignmentsView.CommandToggle.Visibility = metadata.ContainsKey(key) ? Visibility.Visible : Visibility.Collapsed;
            _assignmentsView.CommandToggle.IsChecked = metadata.GetValueOrDefault(key) == "1";
            _loadingAssignment = false; refreshAvailability();
        }

        internal void ApplyClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            refreshAvailability();
            if (_session is not { } session || Draft() is not { } draft) return;
            AssignmentFeedback(ExecuteAssignment(draft.CommandId, draft.Key, draft.Slot,
                () => session.ReviewAssignment(draft), () => session.Assign(draft)));
        });

        internal void ClearClicked(object sender, RoutedEventArgs e) => Run(() =>
        {
            refreshAvailability();
            if (_session is not { } session || Command is not { } command) return;
            var slot = Slot;
            var assigned = _rows.Any(row => row.CommandId == command.Id && row.Slot == slot);
            AssignmentFeedback(ExecuteAssignment(command.Id, "", slot,
                () => new(true, assigned, ""), () => session.Clear(command.Id, slot)));
        });

        internal void AddModifierClicked(object sender, RoutedEventArgs e)
        {
            if (_assignmentsView.ModifierDevicePicker.SelectedItem is not GameDeviceChoice device || string.IsNullOrWhiteSpace(_assignmentsView.ModifierKey.Text)) return;
            var key = NativeInput(_assignmentsView.ModifierKey);
            var label = DeviceInputs(device.Id).FirstOrDefault(x => x.Key == key)?.DisplayName ?? key;
            _modifiers.Add(new(new() { Source = device.Id, Value = key }, device.Name + ": " + label));
            _assignmentsView.ModifiersList.Items.Refresh(); _assignmentsView.ModifierKey.SelectedIndex = -1; _assignmentsView.ModifierKey.Text = ""; DraftChanged();
        }

        internal void SelectInput(string key)
        {
            var choice = _assignmentsView.InputKey.Items.Cast<EliteInputChoice>().FirstOrDefault(x => x.Key == key);
            _assignmentsView.InputKey.SelectedItem = choice;
            _selection.SelectInput(choice?.Key);
        }

        internal void RefreshCombinationInputs()
        {
            _assignmentsView.ModifierKey.ItemsSource = _assignmentsView.ModifierDevicePicker.SelectedItem is GameDeviceChoice device ? DeviceInputs(device.Id) : [];
            _assignmentsView.ModifierKey.SelectedIndex = -1; _assignmentsView.ModifierKey.Text = "";
        }

        internal void RemoveModifierClicked(object sender, RoutedEventArgs e)
        { if (_assignmentsView.ModifiersList.SelectedItem is EliteModifierChoice modifier) { _modifiers.Remove(modifier); _assignmentsView.ModifiersList.Items.Refresh(); DraftChanged(); } }
        internal void RefreshInputChoices()
        {
            if (Device is not { } device || Command is not { } command) return;
            var key = _selection.InputKey;
            _loadingAssignment = true;
            try
            {
                _assignmentsView.InputKey.ItemsSource = DeviceInputs(device.Id, command.Type == CommandType.Axis);
                SelectInput(key ?? "");
            }
            finally { _loadingAssignment = false; }
            DraftChanged();
        }
        internal void UpdateAvailability()
        {
            var opened = _session is not null;
            var pin = Device is not null ? _session?.HardwarePin(Device.Id) : null;
            var drawing = _session is not null && Device is not null ? context.DrawingFor(_session.Snapshot, Device.Id) : null;
            _assignmentsView.AssignmentEditor.IsEnabled = Command is not null && _session?.AccessMessage.Length == 0 && !_busy;
            var draft = Draft();
            var review = draft is null ? new GameAssignmentReview(false, false, "Choose a command, controller and valid input settings.") : _session!.ReviewAssignment(draft);
            var assigned = _rows.Any(x => x.DeviceId == Device?.Id && x.CommandId == Command?.Id && x.Slot == Slot);
            var ready = opened && EditionAccess.Policy.HasPremium && pin is not null && GameInputReadiness.Ready(pin, _services.Discover());
            var deviceNames = DevicePicker.Items.Cast<GameDeviceChoice>().ToDictionary(x => x.Id, x => x.Name, StringComparer.Ordinal);
            var editor = GameAssignmentEditorState.For(_assignmentsView.AssignmentEditor.IsEnabled && Device is not null,
                review, assigned, ready, context.Testing, _busy,
                Command is null ? "" : EliteCommandSlots.Replacement(_rows, Command.Id, Slot, deviceNames));
            _actions = editor.Actions;
            AssignmentActionPresentation.Show(_assignmentsView.ApplyAssignmentButton, AssignmentAction(WorkspaceAssignmentAction.Apply,
                review.Valid && review.Changed, review.Valid ? "Assignment unchanged." : review.Message));
            AssignmentActionPresentation.Show(_assignmentsView.ClearAssignmentButton, AssignmentAction(WorkspaceAssignmentAction.Clear,
                assigned, "No editable assignment in the selected slot."));
            AssignmentActionPresentation.Show(_assignmentsView.StopInputButton, AssignmentAction(WorkspaceAssignmentAction.StopInput, true));
            _assignmentsView.CommandSlotSummary.Text = Command is null ? "" : EliteCommandSlots.Summary(_rows, Command.Id, Command.Type == CommandType.Axis, deviceNames);
            _assignmentsView.AssignmentDraftStatus.Text = editor.Feedback;
            _assignmentsView.FitDiagramButton.IsEnabled = drawing is not null;
            AssignmentActionPresentation.Show(_assignmentsView.StartInputButton, AssignmentAction(WorkspaceAssignmentAction.StartInput,
                ready, "Select one linked, connected controller for input testing."));
            FeatureActionBar.ShowAlternate(_assignmentsView.StartInputButton, _assignmentsView.StopInputButton, context.Testing);
            _assignmentsView.ExportSheetButton.Visibility = Visibility.Collapsed;
            _assignmentsView.ExportSheetButton.IsEnabled = opened && drawing is not null && (_session?.AccessMessage ?? "No profile").Length == 0;
        }
        internal GameAssignmentActions Actions => _actions;
        #endregion END Public
        #region BEGIN private helpers
        private void DraftChanged()
        {
            if (_refreshing || _loadingAssignment || _session is null) return;
            _selection.SelectInput((_assignmentsView.InputKey.SelectedItem as EliteInputChoice)?.Key);
            refreshAvailability();
        }

        private GameAssignmentEdit? Draft()
        {
            if (_session is null || Device is null || Command is null) return null;
            var axis = Command.Type == CommandType.Axis;
            if (axis && !float.TryParse(_assignmentsView.AxisDeadzone.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out _)) return null;
            var deadzone = axis ? float.Parse(_assignmentsView.AxisDeadzone.Text, CultureInfo.CurrentCulture) : 0;
            return new(Device.Id, Command.Id, Slot, _selection.InputKey ?? "", axis,
                _assignmentsView.InvertAxis.IsChecked == true, deadzone,
                _assignmentsView.ModePicker.SelectedIndex == 1 ? BindingMode.Hold : BindingMode.Press,
                _modifiers.Select(x => x.Modifier).ToArray(), _assignmentsView.CommandToggle.Visibility == Visibility.Visible ? _assignmentsView.CommandToggle.IsChecked == true : null);
        }

        private static string NativeInput(ComboBox picker) => picker.SelectedItem is EliteInputChoice choice && picker.Text == choice.DisplayName
            ? choice.Key : picker.Text.Trim();

        private IReadOnlyList<EliteInputChoice> DeviceInputs(string deviceId, bool axes = false)
        {
            if (_session is null) return [];
            var document = _session.Snapshot;
            var drawing = document.Hardware.TryGetValue(deviceId, out var pin)
                ? _hardware.FirstOrDefault(x => x.DeviceId == pin.HardwareId)?.Diagram : null;
            var choices = EliteDeviceInputs.ForDevice(document.Profile, deviceId, drawing, axes);
            var keys = GameControllerInputs.VerifiedKeys(pin, evidence());
            if (keys is null) return choices;
            return choices.Concat(keys.Where(key => key.StartsWith("JOY_", StringComparison.Ordinal)
                    && axes == (!key.StartsWith("JOY_BTN", StringComparison.Ordinal) && !key.StartsWith("JOY_POV", StringComparison.Ordinal)))
                .Select(EliteInputKeys.FromShared).OfType<string>().Select(key => new EliteInputChoice(key, key)))
                .DistinctBy(choice => choice.Key, StringComparer.Ordinal).ToArray();
        }

        private WorkspaceAssignmentActionState AssignmentAction(WorkspaceAssignmentAction action, bool ready, string reason = "") =>
            _assignmentsView.ActionState(action, ready, reason);
        private DraftResult ExecuteAssignment(string command, string input, string alternative, Func<GameAssignmentReview> review, Action apply) =>
            _assignmentsView.ExecuteNativeAssignment(command, input, alternative, review, apply);
        private void AssignmentFeedback(DraftResult result)
        {
            Status(result.Message); _assignmentsView.AssignmentDraftStatus.Text = result.Message;
        }
        #endregion END private helpers
    }
}
