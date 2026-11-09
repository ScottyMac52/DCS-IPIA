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
    /// <summary>Supplies native paired-axis, record and drawing actions to the shared assignment workflow.</summary>
    internal sealed class SquadronsAssignmentEditing(
        SquadronsWorkspaceContext context,
        Func<SquadronsControllerPresenter> controllers,
        Func<ISquadronsAssignmentBrowser<SquadronsWorkspaceRow>> browser,
        Action<bool> refresh,
        Action<Action> run,
        TextBlock StatusText,
        SquadronsAssignmentsView _assignmentsView,
        Window owner)
    {
        #region BEGIN Public
        #endregion END Public
        #region BEGIN private helpers
        internal void UpdateEditor()
        {
            var row = _assignmentsView.BindingsGrid.SelectedItem as SquadronsWorkspaceRow;
            var command = _assignmentsView.CommandBrowser.CommandsList.SelectedItem as Heimdall.Domain.MultiSim.SimCommand;
            _assignmentsView.AssignmentEditor.Visibility = command is null ? Visibility.Collapsed : Visibility.Visible;
            _assignmentsView.TargetDevice.IsEnabled = command is not null && (row is null || row.Editable) &&
                context.AssignmentSource?.StartsWith("system.", StringComparison.Ordinal) != true &&
                context.Session?.AccessMessage.Length == 0 && !context.ScanningInputs;
            if (_assignmentsView.TargetDevice.SelectedItem is null)
                _assignmentsView.TargetDevice.SelectedItem = _assignmentsView.TargetDevice.Items.Cast<GameDeviceChoice>()
                    .FirstOrDefault(x => x.Id == (row?.DeviceId ?? context.SelectedController));
            _assignmentsView.SelectionEditor.ShowCommand(command);
            UpdateInputChoices();
        }

        private void UpdateInputChoices()
        {
            var pin = _assignmentsView.TargetDevice.SelectedItem is GameDeviceChoice target ? context.Session?.HardwarePin(target.Id) : null;
            var keys = context.ScanningInputs ? null : GameControllerInputs.VerifiedKeys(pin, context.ControllerInputs);
            var oldRefreshing = context.Refreshing;
            context.Refreshing = true;
            try
            {
                // One picker entry per physical control, just like a drawing callout.
                var drawing = context.Drawings.FirstOrDefault(item => item.DeviceId == pin?.HardwareId)?.Diagram;
                _assignmentsView.InputPicker.ItemsSource = keys is null ? [] : SquadronsInputCodec.PhysicalChoices(keys)
                    .Select(input => input with
                    {
                        DefaultLabel = drawing?.Controls.FirstOrDefault(control =>
                            SquadronsInputCodec.PhysicalKey(control.InputKey) == input.PhysicalKey)?.Label
                    }).ToArray();
                _assignmentsView.InputPicker.SelectedItem = _assignmentsView.InputPicker.Items.Cast<SquadronsInput>()
                    .FirstOrDefault(x => x.PhysicalKey == SquadronsInputCodec.PhysicalKey(context.Selection.InputKey ?? ""));
                _assignmentsView.InputPicker.IsEnabled = _assignmentsView.TargetDevice.IsEnabled && keys is not null;
            }
            finally { context.Refreshing = oldRefreshing; }
            _assignmentsView.AssignmentHint.Text = _assignmentsView.BindingsGrid.SelectedItem is SquadronsWorkspaceRow { Editable: false } ||
                context.AssignmentSource?.StartsWith("system.", StringComparison.Ordinal) == true
                ? "This native system or combination record is read-only."
                : keys is null
                ? "On Devices, link the destination controller and Rescan to verify its inputs."
                : "Select an input, then Apply assignment. Analog axes update both directions together. Individual native-record edits are available explicitly in the advanced record menu.";
            UpdateApplyAvailability();
        }

        internal void UpdateApplyAvailability()
        {
            var review = ReviewAssignmentTarget();
            AssignmentActionPresentation.Show(_assignmentsView.ApplyButton, AssignmentAction(WorkspaceAssignmentAction.Apply,
                review.Valid && review.Changed, review.Valid ? "Assignment unchanged." : review.Message));
            AssignmentActionPresentation.Show(_assignmentsView.ReverseAxisButton, AssignmentAction(WorkspaceAssignmentAction.ReverseAxis,
                CanReverseSelectedAxis(), "Select both saved directions of an axis to reverse it."));
        }

        private bool CanReverseSelectedAxis()
        {
            if (context.Session is null || _assignmentsView.CommandBrowser.CommandsList.SelectedItem is not Heimdall.Domain.MultiSim.SimCommand command ||
                _assignmentsView.TargetDevice.SelectedItem is not GameDeviceChoice device ||
                _assignmentsView.InputPicker.SelectedItem is not SquadronsInput { Type: CommandType.Axis } input) return false;
            return SquadronsAxisPair.Find(context.Session.Snapshot.Profile, command.Id, device.Id, input.PhysicalKey) is not null;
        }

        internal void TargetChanged(object sender, SelectionChangedEventArgs e) { if (!context.Refreshing) run(UpdateInputChoices); }

        internal void InputChanged(object sender, SelectionChangedEventArgs e)
        {
            if (context.Refreshing) return;
            context.Selection.SelectInput((_assignmentsView.InputPicker.SelectedItem as SquadronsInput)?.Key ?? "");
            UpdateApplyAvailability();
        }

        internal void ClearSelectedClicked(object sender, RoutedEventArgs e) => run(() =>
        {
            if (_assignmentsView.BindingsGrid.SelectedItem is not SquadronsWorkspaceRow { Native.Type: 3 } row)
                throw new InvalidOperationException("Choose an existing joystick assignment to clear.");
            controllers().ReviewNativeChange(context.Session!.PreviewClearCapturedRecord(row.RecordId));
        });

        internal void ApplyClicked(object sender, RoutedEventArgs e) => RunAssignment(() =>
        {
            if (!_assignmentsView.ApplyButton.IsEnabled || _assignmentsView.CommandBrowser.CommandsList.SelectedItem is not Heimdall.Domain.MultiSim.SimCommand command ||
                _assignmentsView.TargetDevice.SelectedItem is not GameDeviceChoice device || _assignmentsView.InputPicker.SelectedItem is not SquadronsInput input)
                return;
            StageAssignment(command.Id, device.Id, input);
        });

        internal void ReverseAxisClicked(object sender, RoutedEventArgs e) => RunAssignment(() =>
        {
            if (!_assignmentsView.ReverseAxisButton.IsEnabled || context.Session is null ||
                _assignmentsView.CommandBrowser.CommandsList.SelectedItem is not Heimdall.Domain.MultiSim.SimCommand command ||
                _assignmentsView.TargetDevice.SelectedItem is not GameDeviceChoice device ||
                _assignmentsView.InputPicker.SelectedItem is not SquadronsInput { Type: CommandType.Axis } input) return;
            var edit = new SquadronsCommandEdit(SquadronsCommandEditKind.ReverseAxis, command.Id, device.Id, input.Key, input.Type);
            var result = ExecuteNativeAssignment(edit);
            refresh(false);
            AssignmentFeedback(result);
        });

        internal void ApplyNativeRecordClicked(object sender, RoutedEventArgs e) => RunAssignment(() =>
        {
            if (!_assignmentsView.ApplyButton.IsEnabled || _assignmentsView.BindingsGrid.SelectedItem is not SquadronsWorkspaceRow { Editable: true } row ||
                _assignmentsView.CommandBrowser.CommandsList.SelectedItem is not Heimdall.Domain.MultiSim.SimCommand command ||
                _assignmentsView.TargetDevice.SelectedItem is not GameDeviceChoice device || _assignmentsView.InputPicker.SelectedItem is not SquadronsInput input)
                return;
            var edit = new SquadronsCommandEdit(SquadronsCommandEditKind.AssignRecord, command.Id,
                device.Id, input.Key, input.Type, row.RecordId);
            var result = ExecuteNativeAssignment(edit);
            refresh(false);
            AssignmentFeedback(result);
        });

        private void SelectDrawingInput(HardwareGraphicControl control)
        {
            // Match Elite: clicking selects; Apply and drag/drop stage native changes.
            SelectDrawingControl(control, showRecords: false);
            context.Selection.SelectInput(control.InputKey);
            UpdateInputChoices();
            _assignmentsView.DrawingDropFeedback.Text = control.Label + " selected. Apply assignment to stage the selected command.";
        }

        private void InspectDrawingInput(HardwareGraphicControl control) => SelectDrawingControl(control, showRecords: true);

        internal void SelectDrawingControl(HardwareGraphicControl control, bool showRecords)
        {
            if (context.SelectedController is not { } slot) return;
            context.FocusedConcept = null;
            context.Selection.SelectInput("");
            context.InspectedControl = control;
            if (showRecords)
            {
                _assignmentsView.ReviewCaptured.IsChecked = true;
                _assignmentsView.CapturedAssignments.IsExpanded = true;
            }
            browser().RefreshRows();
            var section = context.DrawingSection;
            var matches = SquadronsDrawingProjection.ForControl(control, context.Rows, slot, section);
            if (matches.Count > 0)
            {
                if (showRecords) _assignmentsView.CapturedAssignments.IsExpanded = true;
                var preferred = matches.FirstOrDefault(x => x.Native.Concept ==
                    (_assignmentsView.CommandBrowser.CommandsList.SelectedItem as Heimdall.Domain.MultiSim.SimCommand)?.Metadata["squadrons.concept"]) ?? matches[0];
                context.Refreshing = true;
                try
                {
                    _assignmentsView.BindingsGrid.SelectedItem = _assignmentsView.BindingsGrid.Items.Cast<SquadronsWorkspaceRow>()
                        .FirstOrDefault(row => row.RecordId == preferred.RecordId);
                }
                finally { context.Refreshing = false; }
                browser().SelectRecord(preferred);
                if (_assignmentsView.BindingsGrid.SelectedItem is not null) _assignmentsView.BindingsGrid.ScrollIntoView(_assignmentsView.BindingsGrid.SelectedItem);
            }
            _assignmentsView.DrawingDropFeedback.Text = control.Label + ": " +
                SquadronsDrawingProjection.AssignmentSummary(control, context.Rows, slot, section)
                    .Replace("\n", " · ", StringComparison.Ordinal);
        }

        private void DropDrawingCommand(HardwareGraphicControl control, string commandId) => RunAssignment(() =>
        {
            if (context.SelectedController is not { } slot)
                throw new InvalidOperationException("Select a linked joystick controller.");
            StageAssignment(commandId, slot, SquadronsInputCodec.FromDrawing(control.InputKey, control.IsAxis));
        });

        private void RunAssignment(Action action)
        {
            try { action(); }
            catch (Exception error)
            {
                _assignmentsView.SelectionEditor.AssignmentDraftStatus.Text = _assignmentsView.DrawingDropFeedback.Text = "Assignment not applied: " + error.Message;
                StatusText.Text = _assignmentsView.DrawingDropFeedback.Text;
            }
        }

        private void StageAssignment(string commandId, string slot, SquadronsInput input)
        {
            if (context.Session is null || context.ScanningInputs || controllers().Identification.Running || controllers().Input.Running)
                throw new InvalidOperationException("Stop live input and finish controller scanning before assigning.");
            var result = ExecuteNativeAssignment(new(SquadronsCommandEditKind.AssignControl,
                commandId, slot, input.Key, input.Type));
            if (result.Outcome != DraftOutcome.Applied) { AssignmentFeedback(result); return; }
            context.FocusedConcept = null;
            context.InspectedControl = null;
            browser().SelectCommand(commandId);
            context.Selection.SelectInput(input.Key);
            context.Refreshing = true;
            try { _assignmentsView.TargetDevice.SelectedItem = _assignmentsView.TargetDevice.Items.Cast<GameDeviceChoice>().FirstOrDefault(x => x.Id == slot); }
            finally { context.Refreshing = false; }
            refresh(false);
            AssignmentFeedback(result);
        }

        private bool CanClearDrawingInput(HardwareGraphicControl control) =>
            !context.ScanningInputs && !controllers().Identification.Running && !controllers().Input.Running && context.Session?.AccessMessage.Length == 0 &&
            context.SelectedController is { } slot &&
            SquadronsDrawingProjection.ForControl(control, context.Rows, slot, context.DrawingSection).Count > 0;

        private void ClearDrawingInput(HardwareGraphicControl control) => run(() =>
        {
            if (!CanClearDrawingInput(control) || context.SelectedController is not { } slot) return;
            var records = SquadronsDrawingProjection.ForControl(control, context.Rows, slot, context.DrawingSection);
            controllers().ReviewNativeChange(context.Session!.PreviewClearCapturedRecords(records.Select(row => row.RecordId).ToArray()));
        });

        internal WorkspaceAssignmentActionState AssignmentAction(WorkspaceAssignmentAction action, bool ready, string reason = "") =>
            _assignmentsView.ActionState(action, ready, reason);

        private WorkspaceAssignmentActionState ControlAction(HardwareGraphicControl control, WorkspaceAssignmentAction action)
        {
            var ready = action switch
            {
                WorkspaceAssignmentAction.Clear => context.SelectedController is { } slot &&
                    SquadronsDrawingProjection.ForControl(control, context.Rows, slot, context.DrawingSection).Count > 0,
                WorkspaceAssignmentAction.TuneAxis => DrawingAxisPair(control) is not null,
                _ => true
            };
            return AssignmentAction(action, ready, "Select an editable assignment or a complete axis pair.");
        }

        private SquadronsAxisPair? DrawingAxisPair(HardwareGraphicControl control)
        {
            if (!control.IsAxis || context.SelectedController is not { } slot || context.Session is not { } session) return null;
            var pin = session.HardwarePin(slot);
            if (GameControllerInputs.VerifiedKeys(pin, context.ControllerInputs)?.Contains(control.InputKey) != true) return null;
            return SquadronsAxisPair.For(SquadronsDrawingProjection.ForControl(control, context.Rows, slot, context.DrawingSection));
        }

        private void TuneDrawingAxis(HardwareGraphicControl control) => RunAssignment(() =>
        {
            if (!ControlAction(control, WorkspaceAssignmentAction.TuneAxis).Enabled ||
                DrawingAxisPair(control) is not { } pair || context.Session is not { } session) return;

            var reverse = new CheckBox { Content = "Reverse the saved axis directions" };
            reverse.SetResourceReference(FrameworkElement.StyleProperty, "AppCheckBox");
            var dialog = new AxisTuningWindow(owner, pair.Positive.CommandName, reverse, () => null);
            var command = session.Catalog.Commands.Single(item => item.Metadata["squadrons.concept"] == pair.Positive.Native.Concept);
            var edit = new SquadronsCommandEdit(SquadronsCommandEditKind.ReverseAxis, command.Id,
                pair.Positive.DeviceId, control.InputKey, CommandType.Axis);
            var result = ExecuteAssignment(command.Id, control.InputKey, pair.Positive.RecordId,
                () => dialog.ShowDialog() == true
                    ? reverse.IsChecked == true ? session.ReviewNativeEdit(edit) : new(true, false, "")
                    : new(false, false, "Axis tuning canceled."),
                () => session.ApplyNativeEdit(edit));
            refresh(false);
            AssignmentFeedback(result);
        });

        private bool CanDropCommand(HardwareGraphicControl control, string commandId)
        {
            if (context.Session is null || context.SelectedController is not { } slot || context.Session.Catalog.Find(commandId) is not { } command ||
                (command.Type == CommandType.Axis) != control.IsAxis) return false;
            var pin = context.Session.HardwarePin(slot);
            if (GameControllerInputs.VerifiedKeys(pin, context.ControllerInputs)?.Contains(control.InputKey) != true) return false;
            try { _ = SquadronsDrawingAssignment.For(context.Backend, context.Session.Snapshot.Profile, commandId, slot, control.InputKey, control.IsAxis); return true; }
            catch (InvalidOperationException) { return false; }
        }

        private GameAssignmentReview ReviewAssignmentTarget()
        {
            if (context.Session is null || _assignmentsView.CommandBrowser.CommandsList.SelectedItem is not SimCommand command ||
                _assignmentsView.TargetDevice.SelectedItem is not GameDeviceChoice target || _assignmentsView.InputPicker.SelectedItem is not SquadronsInput input || !_assignmentsView.InputPicker.IsEnabled)
                return new(false, false, "Select a compatible command and verified controller input.");
            return context.Session.ReviewNativeEdit(new SquadronsCommandEdit(SquadronsCommandEditKind.AssignControl,
                command.Id, target.Id, input.Key, input.Type));
        }

        internal DraftResult ExecuteNativeAssignment(SquadronsCommandEdit edit)
        {
            var session = context.Session ?? throw new InvalidOperationException("Open a configuration before editing assignments.");
            var result = ExecuteAssignment(edit.CommandId, edit.Key, edit.RecordId,
                () => session.ReviewNativeEdit(edit), () => session.ApplyNativeEdit(edit));
            return result.Outcome != DraftOutcome.Applied ? result : result with
            {
                Message = edit.Kind switch
                {
                    SquadronsCommandEditKind.AssignRecord => "One native record staged. Other direction records are unchanged. Save before exporting.",
                    SquadronsCommandEditKind.ReverseAxis => "Axis reversed. Save before exporting.",
                    _ => result.Message
                }
            };
        }

        internal DraftResult ExecuteAssignment(string command, string input, string alternative,
            Func<GameAssignmentReview> review, Action apply) =>
            _assignmentsView.ExecuteNativeAssignment(command, input, alternative, review, apply);

        internal void AssignmentFeedback(DraftResult result)
        {
            _assignmentsView.SelectionEditor.AssignmentDraftStatus.Text = _assignmentsView.DrawingDropFeedback.Text = StatusText.Text = result.Message;
        }

        internal HardwareDiagramInteractions DrawingInteractions() =>
            new(SelectDrawingInput, DropDrawingCommand, CanClearDrawingInput, ClearDrawingInput,
                CanTune: control => ControlAction(control, WorkspaceAssignmentAction.TuneAxis).Enabled, Tune: TuneDrawingAxis,
                DragFormat: SquadronsWorkspaceContext.CommandDragFormat, ClearLabel: "Clear assignment", Inspect: InspectDrawingInput, CanDrop: CanDropCommand, ActionState: ControlAction);
        #endregion END private helpers
    }
}
