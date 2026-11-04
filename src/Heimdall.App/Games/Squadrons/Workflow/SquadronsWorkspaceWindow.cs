using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.Squadrons;

namespace Heimdall.App.Games
{
    /// <summary>Uses the shared action boundary while retaining native paired-axis and reviewed-record operations.</summary>
    public partial class SquadronsWorkspaceWindow
    {
        #region BEGIN Public
        internal WorkspaceAssignmentWorkflow AssignmentWorkflow { get; } = new();
        /// <summary>Gets the shared selection state for deterministic interaction checks.</summary>
        internal GameAssignmentSelection AssignmentSelection => _selection;
        #endregion END Public
        #region BEGIN private helpers
        private WorkspaceAssignmentContext AssignmentContext() => new(
            (_session?.Snapshot.Id.ToString("N") ?? "") + ":" + SelectedAssignmentSource + ":" + DrawingSection,
            _session is not null, _session?.AccessMessage.Length > 0 || SelectedAssignmentSource?.StartsWith("system.", StringComparison.Ordinal) == true,
            _scanningInputs || _identification.Running, _input.Running);
        private WorkspaceAssignmentActionState AssignmentAction(WorkspaceAssignmentAction action, bool ready, string reason = "") =>
            WorkspaceAssignmentActionState.For(action, AssignmentContext(), new(true, ready, reason));
        private WorkspaceAssignmentActionState ControlAction(HardwareGraphicControl control, WorkspaceAssignmentAction action)
        {
            var ready = action switch
            {
                WorkspaceAssignmentAction.Clear => SelectedController is { } slot &&
                    SquadronsDrawingProjection.ForControl(control, _rows, slot, DrawingSection).Count > 0,
                WorkspaceAssignmentAction.TuneAxis => DrawingAxisPair(control) is not null,
                _ => true
            };
            return AssignmentAction(action, ready, "Select an editable assignment or a complete axis pair.");
        }

        private SquadronsAxisPair? DrawingAxisPair(HardwareGraphicControl control)
        {
            if (!control.IsAxis || SelectedController is not { } slot || _session is not { } session) return null;
            var pin = session.Snapshot.Hardware.GetValueOrDefault(slot);
            if (GameControllerInputs.VerifiedKeys(pin, _controllerInputs)?.Contains(control.InputKey) != true) return null;
            return SquadronsAxisPair.For(SquadronsDrawingProjection.ForControl(control, _rows, slot, DrawingSection));
        }

        private void TuneDrawingAxis(HardwareGraphicControl control) => RunAssignment(() =>
        {
            if (!ControlAction(control, WorkspaceAssignmentAction.TuneAxis).Enabled ||
                DrawingAxisPair(control) is not { } pair || _session is not { } session) return;

            var reverse = new CheckBox { Content = "Reverse the saved axis directions" };
            reverse.SetResourceReference(FrameworkElement.StyleProperty, "AppCheckBox");
            var dialog = new AxisTuningWindow(this, pair.Positive.CommandName, reverse, () => null);
            var command = session.Catalog.Commands.Single(item => item.Metadata["squadrons.concept"] == pair.Positive.Native.Concept);
            var edit = new SquadronsCommandEdit(SquadronsCommandEditKind.ReverseAxis, command.Id,
                pair.Positive.DeviceId, control.InputKey, CommandType.Axis);
            var result = ExecuteAssignment(command.Id, control.InputKey, pair.Positive.RecordId,
                () => dialog.ShowDialog() == true
                    ? reverse.IsChecked == true ? session.ReviewNativeEdit(edit) : new(true, false, "")
                    : new(false, false, "Axis tuning canceled."),
                () => session.ApplyNativeEdit(edit));
            Refresh(false);
            AssignmentFeedback(result);
        });

        private bool CanDropCommand(HardwareGraphicControl control, string commandId)
        {
            if (_session is null || SelectedController is not { } slot || _session.Catalog.Find(commandId) is not { } command ||
                (command.Type == CommandType.Axis) != control.IsAxis) return false;
            var pin = _session.Snapshot.Hardware.GetValueOrDefault(slot);
            if (GameControllerInputs.VerifiedKeys(pin, _controllerInputs)?.Contains(control.InputKey) != true) return false;
            try { _ = SquadronsDrawingAssignment.For(_backend, _session.Snapshot.Profile, commandId, slot, control.InputKey, control.IsAxis); return true; }
            catch (InvalidOperationException) { return false; }
        }
        private GameAssignmentReview ReviewAssignmentTarget()
        {
            if (_session is null || CommandPicker.SelectedItem is not SimCommand command ||
                TargetDevice.SelectedItem is not GameDeviceChoice target || InputPicker.SelectedItem is not SquadronsInput input || !InputPicker.IsEnabled)
                return new(false, false, "Select a compatible command and verified controller input.");
            return _session.ReviewNativeEdit(new SquadronsCommandEdit(SquadronsCommandEditKind.AssignControl,
                command.Id, target.Id, input.Key, input.Type));
        }
        private DraftResult ExecuteNativeAssignment(SquadronsCommandEdit edit)
        {
            var session = _session ?? throw new InvalidOperationException("Open a configuration before editing assignments.");
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
        private DraftResult ExecuteAssignment(string command, string input, string alternative,
            Func<GameAssignmentReview> review, Action apply)
        {
            var target = new WorkspaceAssignmentTarget(AssignmentContext().Scope, command, input, alternative);
            var host = new WorkspaceAssignmentHost(AssignmentContext, () => JsonSerializer.Serialize(_session?.Snapshot));
            return AssignmentWorkflow.Execute(host, target, review, apply);
        }
        private void AssignmentFeedback(DraftResult result)
        {
            SelectionEditor.AssignmentDraftStatus.Text = DrawingDropFeedback.Text = StatusText.Text = result.Message;
        }
        #endregion END private helpers
    }
}

