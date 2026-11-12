using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Smoke
{
    /// <summary>Connects the assignment layout probe to the production editing workflow.</summary>
    internal sealed class AssignmentsEditingProbe(IGameWorkspaceContext context, Func<WorkspaceAssignmentContext> readState)
        : GameAssignmentEditingBase(() => context)
    {
        protected override WorkspaceAssignmentContext ReadAssignmentContext(IGameWorkspaceContext context) => readState();
        protected override WorkspaceAssignmentActionSupport ReadNativeSupport(WorkspaceAssignmentAction action) => new(true, true);
        protected override void RefreshNativeEditor() { }
        protected override void ReportNativeResult(DraftResult result) { }
    }
}
