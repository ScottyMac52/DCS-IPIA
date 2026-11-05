using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Dcs;
using System.Windows;
using System.Windows.Controls;

namespace Heimdall.App.Features.Assignments
{
    /// <summary>Executes DCS assign, clear, undo and axis tuning through the shared assignment boundary.</summary>
    internal sealed class DcsAssignmentEditing(
        DcsAssignmentViewState state,
        FrameworkElement owner,
        TextBlock SelectedCallout,
        Func<DcsCommandPaletteEntry?> selectedCommand,
        Action refresh,
        Action clearLabel,
        Func<WorkspaceAssignmentContext> AssignmentContext,
        Func<string> AssignmentSnapshot,
        Func<DcsDraftAssignmentSlot, string, Func<DraftResult>, DraftResult> ExecuteAssignment)
    {
        #region BEGIN Public
        internal void AssignClicked(object sender, RoutedEventArgs e)
        {
            if (state.Context?.Working is { } workspace && state.Selected is { } selected &&
                state.Diagram is UiLayerDiagram target &&
                state.ReservationError(target, selected.InputKey) is { } reason)
            {
                SelectedCallout.Text = reason;
                return;
            }
            if (state.Context?.Draft is null || state.Selected is null || selectedCommand() is not { } command)
            {
                SelectedCallout.Text = "Select a callout and a command.";
                return;
            }
            if (state.Diagram is not UiLayerDiagram diagram) return;

            var layer = state.Layer;
            var chord = state.Chord;
            var slot = DcsKneeboardDraftProjection.SlotForControl(state.Context.Working!, diagram, state.Selected, layer, chord);
            if (slot is null)
            {
                SelectedCallout.Text = "No slot for the selected control.";
                return;
            }
            var result = AssignWithConfirmation(slot, new DcsDraftCommand(command.Id, command.Name, command.IsAxis, layer));
            if (result.Outcome == DraftOutcome.Applied)
            {
                refresh();
                SelectedCallout.Text = $"{state.Selected.Label} assigned to {command.Name} (staged).";

            }
            else
            {
                SelectedCallout.Text = result.Message;
            }
        }

        internal void ClearClicked(object sender, RoutedEventArgs e)
        {
            if (state.Context?.Draft is null || state.Selected is null)
            {
                SelectedCallout.Text = "Select a callout to clear.";
                return;
            }

            if (state.Diagram is not UiLayerDiagram diagram) return;

            var layer = state.Layer;
            var chord = state.Chord;
            var slot = DcsKneeboardDraftProjection.SlotForControl(state.Context.Working!, diagram, state.Selected, layer, chord);
            if (slot is null) return;

            var context = state.Context;
            var result = ExecuteAssignment(slot, "", () =>
            {
                var cleared = context.Draft.Remove(slot);
                if (cleared.Outcome == DraftOutcome.Applied) context.SetLabel(slot.Key, null);
                return cleared;
            });
            if (result.Outcome == DraftOutcome.Applied)
            {
                clearLabel();
                refresh();
                SelectedCallout.Text = $"{slot.InputKey} cleared (staged).";
            }
            else
            {
                SelectedCallout.Text = result.Message;
            }
        }

        internal void ClearLayerClicked(object sender, RoutedEventArgs e)
        {
            if (state.Context?.Working is null || state.Diagram is not UiLayerDiagram diagram) return;

            var device = DcsWorkspaceStructureEditor.Describe(state.Context.Working).Devices
                .FirstOrDefault(x => x.ProfileId == diagram.ProfileId && x.Layer == state.Layer);
            if (device is null)
            {
                SelectedCallout.Text = "Select a device to clear.";
                return;
            }

            var layerName = state.LayerName;
            state.Context.RunStructureEdit(workspace =>
                DcsWorkspaceStructureEditor.ClearLayerAssignments(workspace, device, layerName));

            refresh();
            SelectedCallout.Text = "Layer assignments cleared and staged.";
        }
        internal void DropPaletteCommand(HardwareGraphicControl control, DcsCommandPaletteEntry command)
        {
            if (state.Context?.Draft is null || state.Diagram is not UiLayerDiagram diagram) return;
            if (state.ReservationError(diagram, control.InputKey) is { } blocked)
            {
                SelectedCallout.Text = blocked;
                return;
            }
            var layer = state.Layer;
            var chord = state.Chord;
            var destination = DcsKneeboardDraftProjection.SlotForControl(state.Context.Working!, diagram, control, layer, chord);
            if (destination is null) return;
            var result = AssignWithConfirmation(destination, new DcsDraftCommand(command.Id, command.Name, command.IsAxis, layer));
            if (result.Outcome == DraftOutcome.Applied)
            {
                state.Selected = control;
                refresh();
                SelectedCallout.Text = $"{control.Label} assigned to {command.Name} (staged).";
            }
            else SelectedCallout.Text = result.Message;
        }

        internal DraftResult AssignWithConfirmation(DcsDraftAssignmentSlot slot, DcsDraftCommand command) =>
            ExecuteAssignment(slot, command.Id, () => AssignWithConfirmationCore(slot, command));

        internal DraftResult AssignWithConfirmationCore(DcsDraftAssignmentSlot slot, DcsDraftCommand command)
        {
            if (state.Context?.Draft is null)
                return new(DraftOutcome.Blocked, "No draft.");
            if (state.Context.Working is { } workspace &&
                workspace.Diagrams.FirstOrDefault(x => x.ProfileId == slot.ProfileId) is { } diagram &&
                state.ReservationError(diagram, slot.InputKey) is { } blocked)
                return new(DraftOutcome.Blocked, blocked);

            var draft = state.Context.Draft;
            var scope = AssignmentContext().Scope;
            var before = AssignmentSnapshot();
            var result = draft.Assign(slot, command, OccupiedDestinationBehavior.Ask, null);
            if (result.Outcome != DraftOutcome.ConfirmationRequired)
                return result;

            var decision = ThemeMessageBox.Show(
                Window.GetWindow(owner),
                "Destination is occupied. Yes: replace; No: swap; Cancel: keep both.",
                "Occupied control",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            var choice = decision switch
            {
                MessageBoxResult.Yes => DraftConflictChoice.Replace,
                MessageBoxResult.No => DraftConflictChoice.Swap,
                _ => DraftConflictChoice.Cancel
            };
            if (AssignmentContext().Scope != scope || AssignmentSnapshot() != before)
                return new(DraftOutcome.Blocked, "The assignment changed during confirmation. Review the current control again.");
            return draft.Assign(slot, command, OccupiedDestinationBehavior.Ask, choice);
        }

        internal void UndoClicked(object sender, RoutedEventArgs e)
        {
            if (state.Context?.Draft is null) return;
            var result = state.Context.Draft.Undo();
            refresh();
            SelectedCallout.Text = result.Message;
        }
        internal Action<AxisTuningWindow>? AxisTuningPresenter { get; set; }

        internal void TuneAxisClicked(object sender, RoutedEventArgs e)
        {
            if (state.Context?.Draft is null || state.Selected is null || !state.Selected.IsAxis) return;
            if (state.Diagram is not UiLayerDiagram diagram) return;

            var layer = state.Layer;
            var chord = state.Chord;
            var slot = DcsKneeboardDraftProjection.SlotForControl(state.Context.Working!, diagram, state.Selected, layer, chord);
            if (slot is null) return;

            var assignment = state.Context.Draft.Snapshot.FirstOrDefault(x =>
                DcsInputKey.PhysicalSlotKey(x.Slot) == DcsInputKey.PhysicalSlotKey(slot));
            if (assignment is null)
            {
                SelectedCallout.Text = "Assign a command before tuning.";
                return;
            }

            AxisTuningWindow? dialog = null;
            void Apply()
            {
                var result = state.Context.Draft.TuneAxis(slot, dialog!.Filter);
                SelectedCallout.Text = result.Message;
                if (result.Outcome == DraftOutcome.Applied) refresh();
            }
            dialog = new AxisTuningWindow(assignment.Filter, AxisTuningPresenter is null ? null : Apply)
                { Owner = Window.GetWindow(owner) };
            if (AxisTuningPresenter is { } presenter) presenter(dialog);
            else if (dialog.ShowDialog() == true) Apply();
        }
        #endregion END Public
    }
}
