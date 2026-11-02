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
        #endregion END Public
        #region BEGIN private helpers
        private WorkspaceAssignmentContext AssignmentContext() => new(
            (_session?.Snapshot.Id.ToString("N") ?? "") + ":" + (SelectedDevice ?? "*"),
            _session is not null, _session?.AccessMessage.Length > 0, false, _input.Running);
        private DraftResult ExecuteAssignment(WarThunderNativeEdit edit)
        {
            var session = _session ?? throw new InvalidOperationException("Open a configuration before editing assignments.");
            var target = new WorkspaceAssignmentTarget(AssignmentContext().Scope, edit.CommandId,
                _selectedPhysicalInput?.Key ?? edit.FieldAddress, edit.RecordId);
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
