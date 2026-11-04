using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;

namespace Heimdall.App.Features.Assignments
{
    /// <summary>Adapts the module draft to the common assignment execution boundary.</summary>
    public partial class DcsAssignmentsView
    {
        #region BEGIN Public
        internal WorkspaceAssignmentWorkflow AssignmentWorkflow { get; } = new();
        #endregion END Public
        #region BEGIN private helpers
        private WorkspaceAssignmentContext AssignmentContext() => new(
            (_context?.Working?.Configuration.Module.Id.ToString("N") ?? "") + ":" +
            (DeviceChoice.SelectedItem as UiLayerDiagram)?.ProfileId + ":" + CurrentLayer + ":" + Layers.SelectedItem,
            _context?.Draft is not null, false, false, false);
        private string AssignmentSnapshot() => JsonSerializer.Serialize(new
        {
            Assignments = _context?.Draft?.Snapshot,
            Labels = _context?.Labels.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray()
        });
        private DraftResult ExecuteAssignment(DcsDraftAssignmentSlot slot, string command, Func<DraftResult> apply)
        {
            var target = new WorkspaceAssignmentTarget(AssignmentContext().Scope, command, slot.InputKey, slot.Key);
            return AssignmentWorkflow.Execute(new WorkspaceAssignmentHost(AssignmentContext, AssignmentSnapshot),
                new WorkspaceAssignmentOperation(target, () => new(true, true, ""), apply));
        }
        #endregion END private helpers
    }
}
