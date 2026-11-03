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

        private (SquadronsWorkspaceRow Positive, SquadronsWorkspaceRow Negative)? DrawingAxisPair(HardwareGraphicControl control)
        {
            if (!control.IsAxis || SelectedController is not { } slot || _session is not { } session) return null;
            var pin = session.Snapshot.Hardware.GetValueOrDefault(slot);
            if (GameControllerInputs.VerifiedKeys(pin, _controllerInputs)?.Contains(control.InputKey) != true) return null;
            var rows = SquadronsDrawingProjection.ForControl(control, _rows, slot, DrawingSection);
            if (rows.Count != 2) return null;
            var positive = rows.FirstOrDefault(row => row.Native.Input is { Type: CommandType.Axis, Direction: 1 });
            var negative = rows.FirstOrDefault(row => row.Native.Input is { Type: CommandType.Axis, Direction: -1 });
            return positive is not null && negative is not null && positive.Native.Concept == negative.Native.Concept
                ? (positive, negative) : null;
        }

        private void TuneDrawingAxis(HardwareGraphicControl control) => RunAssignment(() =>
        {
            if (!ControlAction(control, WorkspaceAssignmentAction.TuneAxis).Enabled ||
                DrawingAxisPair(control) is not { } pair || _session is not { } session) return;

            var reverse = new CheckBox { Content = "Reverse the saved axis directions" };
            reverse.SetResourceReference(FrameworkElement.StyleProperty, "AppCheckBox");
            var dialog = new AxisTuningWindow(this, pair.Positive.CommandName, reverse, () => null);
            var result = ExecuteAssignment(pair.Positive.Native.Concept, control.InputKey, pair.Positive.RecordId,
                () => dialog.ShowDialog() == true
                    ? new(true, reverse.IsChecked == true, "")
                    : new(false, false, "Axis tuning canceled."),
                () => session.ReverseNativeAxisPair(pair.Positive.RecordId, pair.Negative.RecordId,
                    pair.Positive.DeviceId, control.InputKey));
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
        private bool CompatibleAssignmentTarget()
        {
            if (_session is null || CommandPicker.SelectedItem is not SimCommand command ||
                TargetDevice.SelectedItem is not GameDeviceChoice target || InputPicker.SelectedItem is not SquadronsInput input || !InputPicker.IsEnabled) return false;
            try { _ = SquadronsDrawingAssignment.For(_backend, _session.Snapshot.Profile, command.Id, target.Id, input.PhysicalKey, input.Type == CommandType.Axis); return true; }
            catch (InvalidOperationException) { return false; }
        }
        private DraftResult ExecuteAssignment(string command, string input, string alternative,
            Func<GameAssignmentReview> review, Action apply)
        {
            var target = new WorkspaceAssignmentTarget(AssignmentContext().Scope, command, input, alternative);
            var host = new WorkspaceAssignmentHost(AssignmentContext, () => JsonSerializer.Serialize(_session?.Snapshot));
            return AssignmentWorkflow.Execute(host, new WorkspaceAssignmentOperation(target, review, () =>
            {
                apply();
                return new(DraftOutcome.Applied, "");
            }));
        }
        private void AssignmentFeedback(DraftResult result)
        {
            SelectionEditor.AssignmentDraftStatus.Text = DrawingDropFeedback.Text = StatusText.Text = result.Message;
        }
        #endregion END private helpers
    }
}
