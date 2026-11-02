using System.Text.Json;
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
        private WorkspaceAssignmentActionState ControlAction(HardwareGraphicControl control, WorkspaceAssignmentAction action) =>
            WorkspaceAssignmentActionState.For(action, AssignmentContext(), new(action != WorkspaceAssignmentAction.TuneAxis,
                action != WorkspaceAssignmentAction.Clear || (SelectedController is { } slot &&
                    SquadronsDrawingProjection.ForControl(control, _rows, slot, DrawingSection).Count > 0),
                "No editable assignment on this control in the selected context."));
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
