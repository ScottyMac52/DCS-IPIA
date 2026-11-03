using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.App.Games;
using Heimdall.Dcs;

namespace Heimdall.App.Features.Assignments
{
    /// <summary>Uses the common selection and action policy around module-native slots and reserved modifiers.</summary>
    public partial class AssignmentsView
    {
        private readonly GameAssignmentSelection _assignmentSelection = new();
        #region BEGIN Public
        /// <summary>Gets the shared selection state for deterministic interaction checks.</summary>
        internal GameAssignmentSelection AssignmentSelection => _assignmentSelection;
        #endregion END Public
        #region BEGIN private helpers
        private void SynchronizeAssignmentSelection()
        {
            if (DeviceChoice is null || Layers is null || Scope is null || CommandManager is null) return;
            _assignmentSelection.SelectScope((_context?.Working?.Configuration.Module.Id.ToString("N") ?? "") + ":" + CurrentLayer + ":" + Layers.SelectedItem);
            _assignmentSelection.SelectController((DeviceChoice.SelectedItem as UiLayerDiagram)?.ProfileId.ToString("N"));
            _assignmentSelection.SelectCommand(CommandManager.SelectedCommand?.Id);
            _assignmentSelection.SelectInput(_selected?.InputKey);
        }
        private WorkspaceAssignmentActionState AssignmentAction(HardwareGraphicControl? control, WorkspaceAssignmentAction action)
        {
            var diagram = DeviceChoice.SelectedItem as UiLayerDiagram;
            var chord = Layers.SelectedItem as string == "Base" ? "" : (Layers.SelectedItem as string ?? "").Replace(" + ", "+", StringComparison.Ordinal);
            var slot = _context?.Working is { } workspace && diagram is not null && control is not null
                ? KneeboardDraftProjection.SlotForControl(workspace, diagram, control, CurrentLayer, chord) : null;
            var assigned = slot is not null && _context?.Draft?.Snapshot.Any(item =>
                DcsInputKey.PhysicalSlotKey(item.Slot) == DcsInputKey.PhysicalSlotKey(slot)) == true;
            var reserved = diagram is not null && control is not null ? ReservationError(diagram, control.InputKey) : null;
            var ready = action switch
            {
                WorkspaceAssignmentAction.Apply => slot is not null && reserved is null && CommandManager.SelectedCommand is { } command && command.IsAxis == control!.IsAxis,
                WorkspaceAssignmentAction.Clear => assigned,
                WorkspaceAssignmentAction.TuneAxis => assigned && control?.IsAxis == true && reserved is null,
                WorkspaceAssignmentAction.Inspect => control is not null,
                _ => false
            };
            var supported = action is WorkspaceAssignmentAction.Apply or WorkspaceAssignmentAction.Clear or WorkspaceAssignmentAction.TuneAxis or WorkspaceAssignmentAction.Inspect;
            return WorkspaceAssignmentActionState.For(action, AssignmentContext(), new(supported, ready,
                action != WorkspaceAssignmentAction.Clear && reserved is not null ? reserved : "Select a compatible command and controller input; Clear and Tune require an existing assignment."));
        }
        private void RefreshAssignmentActions()
        {
            SynchronizeAssignmentSelection();
            if (AssignButton is { } assign) AssignmentActionPresentation.Show(assign, AssignmentAction(_selected, WorkspaceAssignmentAction.Apply));
            if (ActionBar.FindButton(this, "Clear assignment") is { } clear) AssignmentActionPresentation.Show(clear, AssignmentAction(_selected, WorkspaceAssignmentAction.Clear));
            if (TuneAxisButton is { } tune) AssignmentActionPresentation.Show(tune, AssignmentAction(_selected, WorkspaceAssignmentAction.TuneAxis));
        }
        #endregion END private helpers
    }
}
