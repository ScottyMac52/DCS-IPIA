using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.WarThunder;

namespace Heimdall.App.Games
{
    /// <summary>Routes both native editor and drawing operations through the shared assignment workflow.</summary>
    public partial class WarThunderWorkspaceWindow
    {
        #region BEGIN Public
        internal WorkspaceAssignmentWorkflow AssignmentWorkflow { get; } = new();
        /// <summary>Gets the shared selection state for deterministic interaction checks.</summary>
        internal GameAssignmentSelection AssignmentSelection => _selection;
        #endregion END Public
        #region BEGIN private helpers
        private WorkspaceAssignmentContext AssignmentContext() => new(
            (_session?.Snapshot.Id.ToString("N") ?? "") + ":" + (SelectedDevice ?? "*"),
            _session is not null, _session?.AccessMessage.Length > 0, false, _input.Running);
        private WorkspaceAssignmentActionState AssignmentAction(WorkspaceAssignmentAction action, bool ready, string reason = "") =>
            WorkspaceAssignmentActionState.For(action, AssignmentContext(), new(true, ready, reason));
        private WorkspaceAssignmentActionState ControlAction(HardwareGraphicControl control, WorkspaceAssignmentAction action) =>
            AssignmentAction(action, action switch
            {
                WorkspaceAssignmentAction.Clear => ClearableAssignments(control).Length > 0,
                WorkspaceAssignmentAction.TuneAxis => CanTunePhysicalControl(control),
                _ => true
            }, action == WorkspaceAssignmentAction.Clear ? ClearUnavailableReason(control) : "Select an assigned axis that supports tuning.");
        private DraftResult ExecuteAssignment(WarThunderNativeEdit edit)
        {
            var session = _session ?? throw new InvalidOperationException("Open a configuration before editing assignments.");
            var target = new WorkspaceAssignmentTarget(AssignmentContext().Scope, edit.CommandId,
                _selectedPhysicalInput is { } input && _selection.InputKey == input.Key ? input.Key : edit.FieldAddress, edit.RecordId);
            var host = new WorkspaceAssignmentHost(AssignmentContext, () => JsonSerializer.Serialize(_session?.Snapshot));
            var operation = new WorkspaceAssignmentOperation(target, () => session.ReviewNativeEdit(edit), () =>
            {
                session.ApplyNativeEdit(edit);
                return new(DraftOutcome.Applied, "");
            });
            return AssignmentWorkflow.Execute(host, operation);
        }
        #endregion END private helpers
    }
}
