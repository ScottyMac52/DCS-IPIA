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
    /// <summary>Supplies native physical, record and axis-tuning actions to the shared assignment workflow.</summary>
    internal sealed class WarThunderAssignmentEditing(
        WarThunderWorkspaceContext context,
        Action refreshChrome,
        Action<Action> run,
        TextBlock Status,
        WarThunderAssignmentsView _assignments,
        Window owner,
        Func<WarThunderControllerPresenter> controllers,
        Func<WarThunderAssignmentDrawing> drawing,
        IGameAssignmentBrowser browser)
    {
        #region BEGIN Public
        internal Action<AxisTuningWindow>? AxisTuningOpened { get; set; }
        #endregion END Public
        #region BEGIN private helpers
        internal void RefreshEditor()
        {
            _assignments.Editor.MappedInputs = MappedEditorInputs;
            var selection = context.State.Selection;
            var row = context.Rows.FirstOrDefault(candidate => candidate.RecordId == selection.AlternativeId);
            var command = selection.CommandId is { } id ? context.Session?.Catalog.Find(id) : null;
            var blocked = context.Selection.InputBlockedReason;
            _assignments.Editor.Show(context.Session, command, row, context.Testing || context.Busy || context.ScanningHardware, context.Selection);
            if (blocked.Length != 0) _assignments.Editor.BlockPhysicalSelection(blocked);
            else if (context.SelectedPhysicalInput is not null && context.SelectedDevice is { } id) _assignments.Editor.SelectPhysical(id, context.SelectedPhysicalInput);
        }

        private WarThunderPhysicalInput[] MappedEditorInputs(string deviceId)
        {
            if (context.Session is null) return [];
            if (context.Session.HardwarePin(deviceId) is not { } pin) return [];
            var observations = context.ControllerInputs.Where(item => item.Device.PhysicalGuid == pin.Device.PhysicalGuid
                && item.Device.ProductGuid == pin.Device.ProductGuid).Take(2).ToArray();
            if (observations.Length != 1) return [];
            var inputs = context.PhysicalMapFor(deviceId, observations[0]).Inputs;
            var drawingId = context.Session.DrawingId(deviceId);
            var diagram = context.Drawings.FirstOrDefault(item => item.DeviceId == drawingId)?.Diagram;
            return inputs.Select(input =>
            {
                var control = diagram?.Controls.FirstOrDefault(item => item.InputKey == input.Key
                    && item.IsAxis == (input.Type == CommandType.Axis));
                return input with { Key = AssignmentInputLabel.ForPhysicalKey(input.Key, control?.Label) };
            }).ToArray();
        }

        internal void RefreshInputState()
        {
            var pin = context.SelectedDevice is { } id ? context.Session?.HardwarePin(id) : null;
            AssignmentActionPresentation.Show(_assignments.Start, AssignmentAction(WorkspaceAssignmentAction.StartInput,
                pin is not null && GameInputReadiness.Ready(pin, context.Observed), "Select one linked, connected controller for input testing."));
            AssignmentActionPresentation.Show(_assignments.Stop, AssignmentAction(WorkspaceAssignmentAction.StopInput, true));
            FeatureActionBar.ShowAlternate(_assignments.Start, _assignments.Stop, controllers().Input.Running);
            _assignments.Fit.IsEnabled = drawing().Presenter.Regions.Count > 0;
            refreshChrome(); RefreshAvailability();
        }

        internal void RefreshAvailability() => _assignments.Editor.RefreshAvailability(context.Testing || context.Busy || context.ScanningHardware);

        internal void SelectPhysicalControl(HardwareGraphicControl control)
        {
            var type = (_assignments.Browser.CommandsList.SelectedItem as Heimdall.Domain.MultiSim.SimCommand)?.Type
                ?? (_assignments.Records.SelectedItem as WarThunderWorkspaceRow)?.Command.Type;
            var controlType = control.IsAxis ? CommandType.Axis : CommandType.Button;
            context.SelectedPhysicalInput = type == controlType
                ? context.PhysicalMap.Inputs.FirstOrDefault(input => input.Key == control.InputKey && input.Type == controlType)
                : null;
            var feedback = context.SelectedPhysicalInput is not null
                ? control.Label + " selected. Apply assignment to stage the selected command."
                : type != controlType
                    ? control.IsAxis ? "Select an axis command before choosing this axis."
                        : "Select a button command before choosing this control."
                    : "This physical input has no verified native mapping. " + context.PhysicalMap.Message;
            if (context.SelectedPhysicalInput is null) context.Selection.BlockInput(feedback);
            else context.Selection.SelectInput(context.SelectedPhysicalInput.Key);
            RefreshEditor();
            _assignments.Artwork.Feedback.Text = feedback;
        }

        internal void DropPhysicalCommand(HardwareGraphicControl control, string commandId) => run(() =>
        {
            if (context.Session is null) return;
            _assignments.Browser.CommandsList.SelectedItem = _assignments.Browser.CommandsList.Items.Cast<Heimdall.Domain.MultiSim.SimCommand>().FirstOrDefault(command => command.Id == commandId);
            var type = context.Session.Catalog.Find(commandId)?.Type;
            var input = context.PhysicalMap.Inputs.FirstOrDefault(candidate => candidate.Key == control.InputKey && candidate.Type == type)
                ?? throw new InvalidOperationException("This physical input has no verified native mapping.");
            ApplyPhysicalAssignment(commandId, input);
        });

        internal bool CanDropPhysicalCommand(HardwareGraphicControl control, string commandId)
        {
            if (context.Session is null || context.SelectedDevice is not { } id || controllers().Input.Running || context.Session.AccessMessage.Length != 0) return false;
            var type = context.Session.Catalog.Find(commandId)?.Type;
            var input = context.PhysicalMap.Inputs.FirstOrDefault(candidate => candidate.Key == control.InputKey && candidate.Type == type);
            if (input is null) return false;
            try { _ = WarThunderDrawingProjection.Assignment(context.Session.Snapshot.Profile, commandId, id, input); return true; }
            catch (InvalidOperationException) { return false; }
        }

        internal void ApplyPhysicalAssignment(string commandId, WarThunderPhysicalInput input)
        {
            if (context.Session is null || context.SelectedDevice is not { } id || controllers().Input.Running) throw new InvalidOperationException("Select a linked controller and stop input testing before assigning.");
            if (!context.PhysicalMap.Inputs.Contains(input)) throw new InvalidOperationException("The physical input mapping is no longer current.");
            _assignments.Browser.CommandsList.SelectedItem = _assignments.Browser.CommandsList.Items.Cast<Heimdall.Domain.MultiSim.SimCommand>().FirstOrDefault(command => command.Id == commandId);
            ApplyDrawingEdit(WarThunderDrawingProjection.Assignment(context.Session.Snapshot.Profile, commandId, id, input));
        }

        internal void ApplyDrawingEdit(WarThunderNativeEdit edit)
        {
            if (context.Session is null || controllers().Input.Running) throw new InvalidOperationException("Stop input testing before editing.");
            var result = ExecuteAssignment(edit);
            context.SelectedPhysicalInput = null; context.Selection.SelectInput(null); RefreshEditor(); refreshChrome();
            Status.Text = _assignments.Artwork.Feedback.Text = _assignments.Editor.Frame.AssignmentDraftStatus.Text = result.Message;
        }

        private IReadOnlyList<WarThunderWorkspaceRow> PhysicalAssignments(HardwareGraphicControl control)
        {
            if (context.Session is null || context.SelectedDevice is not { } id) return [];
            if (!context.DrawingAssignments.TryGetValue(control.InputKey, out var rows))
            {
                rows = WarThunderDrawingProjection.Assignments(context.Session.Snapshot.Profile, id, control.InputKey, context.PhysicalMap);
                context.DrawingAssignments.Add(control.InputKey, rows);
            }
            return rows;
        }

        private WarThunderWorkspaceRow[] ClearableAssignments(HardwareGraphicControl control)
        {
            if (context.Session is null || controllers().Input.Running || context.Session.AccessMessage.Length != 0) return [];
            return PhysicalAssignments(control).Where(row =>
                WarThunderProfileBackend.GetBindingCapabilities(context.Session.Snapshot.Profile, row.RecordId).ClearAssignment).ToArray();
        }

        internal bool CanClearPhysicalControl(HardwareGraphicControl control) => ClearableAssignments(control).Length > 0;

        internal HardwareDiagramClearChoice[] ClearPhysicalChoices(HardwareGraphicControl control) =>
            ClearableAssignments(control).Select((row, index) => new HardwareDiagramClearChoice(
                row.Name + " · " + row.Inputs + " · " + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                () => run(() => ApplyDrawingEdit(new(WarThunderEditKind.Clear, row.RecordId))))).ToArray();

        internal void ClearPhysicalControl(HardwareGraphicControl control) => run(() =>
        {
            var rows = ClearableAssignments(control);
            if (rows.Length != 1) throw new InvalidOperationException("Choose the native alternative in the Clear Assignment submenu.");
            ApplyDrawingEdit(new(WarThunderEditKind.Clear, rows[0].RecordId));
        });

        internal string ClearUnavailableReason(HardwareGraphicControl control)
        {
            if (controllers().Input.Running) return "Stop input testing before clearing an assignment.";
            if (context.Session is null) return "Import or open a profile first.";
            if (context.Session.AccessMessage.Length != 0) return context.Session.AccessMessage;
            var rows = PhysicalAssignments(control);
            if (rows.Count == 0) return "No captured assignment is mapped to this physical control. " + context.PhysicalMap.Message;
            if (control.IsAxis) return "This axis record is nested, repeated, unsupported or already unassigned. View assignments to inspect it.";
            return "These native alternatives contain system inputs, nested data or unsupported syntax. View assignments to inspect the exact records.";
        }

        internal bool CanTunePhysicalControl(HardwareGraphicControl control) => context.Session is not null && !controllers().Input.Running
            && context.Session.AccessMessage.Length == 0 && PhysicalAssignments(control).Any(row =>
            {
                var capabilities = WarThunderProfileBackend.GetBindingCapabilities(context.Session.Snapshot.Profile, row.RecordId);
                return capabilities.SetAxisInverse || capabilities.EditableParameterAddresses.Count > 0;
            });

        internal void TunePhysicalControl(HardwareGraphicControl control) => run(() =>
        {
            if (!ControlAction(control, WorkspaceAssignmentAction.TuneAxis).Enabled || context.Session is not { } session) return;
            var profile = session.Snapshot.Profile;
            var row = PhysicalAssignments(control).First(candidate =>
            {
                var support = WarThunderProfileBackend.GetBindingCapabilities(profile, candidate.RecordId);
                return support.SetAxisInverse || support.EditableParameterAddresses.Count > 0;
            });
            TuneNativeAxis(session, row, control.InputKey);
        });

        internal void TuneSelectedNativeAxis() => run(() =>
        {
            if (context.Session is { } session && _assignments.Records.SelectedItem is WarThunderWorkspaceRow row &&
                _assignments.Editor.ActionState(WorkspaceAssignmentAction.TuneAxis).Enabled)
                TuneNativeAxis(session, row, "");
        });

        private void TuneNativeAxis(GameProfileSession session, WarThunderWorkspaceRow row, string inputKey)
        {
            var capabilities = WarThunderProfileBackend.GetBindingCapabilities(session.Snapshot.Profile, row.RecordId);
            var fields = new StackPanel();
            var inverted = WarThunderWorkspaceProjection.IsInverted(row.Binding);
            var invert = new CheckBox { Content = "Invert axis", IsChecked = inverted, Margin = new Thickness(0, 0, 0, 8) };
            invert.SetResourceReference(FrameworkElement.StyleProperty, "AppCheckBox");
            if (capabilities.SetAxisInverse) fields.Children.Add(invert);
            var editors = new List<(WarThunderBlkScalar Field, TextBox Editor)>();
            foreach (var field in row.Binding.Fields.Where(field => capabilities.EditableParameterAddresses.Contains(field.Address)
                && (!capabilities.SetAxisInverse || WarThunderWorkspaceProjection.FieldName(field) != "inverse")))
            {
                var label = WarThunderWorkspaceProjection.FieldName(field);
                var editor = new TextBox { Text = field.Value, Margin = new Thickness(0, 3, 0, 10) };
                editor.SetResourceReference(FrameworkElement.StyleProperty, "AppTextBox");
                fields.Children.Add(new Label { Content = label, Target = editor });
                fields.Children.Add(editor);
                editors.Add((field, editor));
            }

            WarThunderAxisTuningEdit? edit = null;
            var review = new GameAssignmentReview(false, false, "Axis tuning canceled.");
            var dialog = new AxisTuningWindow(owner, row.Command.DisplayName, fields, () =>
            {
                var changes = editors.Where(item => item.Field.Value != item.Editor.Text)
                    .Select(item => new WarThunderNativeEdit(WarThunderEditKind.Parameter, row.RecordId,
                        item.Field.Address, Literal: item.Editor.Text)).ToList();
                if (capabilities.SetAxisInverse && (invert.IsChecked == true) != inverted)
                    changes.Add(new(WarThunderEditKind.Inverse, row.RecordId, Inverted: invert.IsChecked == true));
                edit = new WarThunderAxisTuningEdit(changes);
                review = session.ReviewNativeEdit(edit);
                return review.Valid ? null : review.Message;
            });
            dialog.Loaded += (_, _) => AxisTuningOpened?.Invoke(dialog);
            var result = _assignments.ExecuteNativeAssignment(row.Command.Id, inputKey, row.RecordId,
                () => dialog.ShowDialog() == true ? review : new(false, false, "Axis tuning canceled."),
                () => session.ApplyNativeEdit(edit!));
            Status.Text = _assignments.Artwork.Feedback.Text = _assignments.Editor.Frame.AssignmentDraftStatus.Text = result.Message;
        }

        internal void InspectPhysicalControl(HardwareGraphicControl control) => run(() =>
        {
            var rows = PhysicalAssignments(control);
            context.SelectedPhysicalInput = null; context.Selection.SelectInput(null);
            browser.SelectCommand(rows.FirstOrDefault()?.Command.Id);
            browser.SelectRecord(rows.FirstOrDefault()?.RecordId);
            context.Refreshing = true;
            try
            {
                _assignments.Records.ItemsSource = rows;
                _assignments.Records.SelectedItem = rows.Count > 0 ? rows[0] : null;
                _assignments.CapturedAssignments.IsExpanded = true;
            }
            finally { context.Refreshing = false; }
            RefreshEditor();
            _assignments.Artwork.Feedback.Text = rows.Count == 0 ? "No captured assignment on " + control.Label + "." : "Showing assignments on " + control.Label + ".";
        });

        private WorkspaceAssignmentActionState AssignmentAction(WorkspaceAssignmentAction action, bool ready, string reason = "") =>
            _assignments.ActionState(action, ready, reason);

        internal WorkspaceAssignmentActionState ControlAction(HardwareGraphicControl control, WorkspaceAssignmentAction action) =>
            AssignmentAction(action, action switch
            {
                WorkspaceAssignmentAction.Clear => ClearableAssignments(control).Length > 0,
                WorkspaceAssignmentAction.TuneAxis => CanTunePhysicalControl(control),
                _ => true
            }, action == WorkspaceAssignmentAction.Clear ? ClearUnavailableReason(control) : "Select an assigned axis that supports tuning.");

        private DraftResult ExecuteAssignment(WarThunderNativeEdit edit)
        {
            var session = context.Session ?? throw new InvalidOperationException("Open a configuration before editing assignments.");
            var input = context.SelectedPhysicalInput is { } selected && context.Selection.InputKey == selected.Key ? selected.Key : edit.FieldAddress;
            return _assignments.ExecuteNativeAssignment(edit.CommandId, input, edit.RecordId,
                () => session.ReviewNativeEdit(edit), () => session.ApplyNativeEdit(edit));
        }
        #endregion END private helpers
    }
}

