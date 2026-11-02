using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;

namespace Heimdall.App.Games
{
    /// <summary>Shares assignment execution rules with the other game workspaces.</summary>
    public sealed partial class EliteWorkspaceWindow
    {
        #region BEGIN Public
        internal WorkspaceAssignmentWorkflow AssignmentWorkflow { get; } = new();
        #endregion END Public
        #region BEGIN private helpers
        private WorkspaceAssignmentContext AssignmentContext() => new(
            (_session?.Snapshot.Id.ToString("N") ?? "") + ":" + Device?.Id + ":" + Section + ":" + Slot,
            _session is not null, _session?.AccessMessage.Length > 0, _busy || _identification.Running, _input.Running);
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
            Status(result.Message);
            _assignmentsView.AssignmentDraftStatus.Text = result.Message;
        }
        #endregion END private helpers
    }
}
