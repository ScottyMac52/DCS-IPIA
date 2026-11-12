using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
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
        Func<WorkspaceAssignmentContext> assignmentContext,
        Action refreshActions) : GameAssignmentEditingBase(() => state.Context)
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
            var result = ExecuteNative(WorkspaceAssignmentAction.Clear, "", slot.InputKey, slot.Key, Ready, () =>
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

        internal DraftResult AssignWithConfirmation(DcsDraftAssignmentSlot slot, DcsDraftCommand command)
        {
            if (state.Context?.Draft is not { } draft) return new(DraftOutcome.Blocked, "No draft.");
            DraftConflictChoice? choice = null;
            return ExecuteNative(WorkspaceAssignmentAction.Apply, command.Id, slot.InputKey, slot.Key, () =>
            {
                if (state.Context.Working?.Diagrams.FirstOrDefault(x => x.ProfileId == slot.ProfileId) is { } diagram &&
                    state.ReservationError(diagram, slot.InputKey) is { } blocked)
                    return new(false, false, blocked);
                return ReviewConflict(decision => draft.ReviewAssign(slot, command, decision), ref choice);
            }, () => draft.Assign(slot, command, OccupiedDestinationBehavior.Ask, choice));
        }

        internal DcsEditorControlDrag CaptureDrag(DcsDraftAssignmentSlot slot) =>
            new(slot, assignmentContext().Scope, state.Context!.CaptureSnapshot());

        internal DraftResult Transfer(DcsEditorControlDrag drag, DcsDraftAssignmentSlot destination, bool copy)
        {
            if (state.Context is not { Draft: { } draft } context || state.Diagram is not UiLayerDiagram diagram)
                return new(DraftOutcome.Blocked, "No draft.");
            var source = drag.Slot;
            var sourceReservation = state.ReservationError(diagram, source.InputKey);
            DraftConflictChoice? choice = null;
            var target = new WorkspaceAssignmentTarget(drag.Scope, "", destination.InputKey, destination.Key);
            return Execute(WorkspaceAssignmentAction.Apply, new WorkspaceAssignmentOperation(target, () =>
            {
                if (context.CaptureSnapshot() != drag.Fingerprint)
                    return new(false, false, "The assignment changed during drag. Select the current control again.");
                if (state.ReservationError(diagram, destination.InputKey) is { } blocked)
                    return new(false, false, blocked);
                if (copy && sourceReservation is not null)
                    return new(false, false, "Copying an assignment from a reserved modifier is not permitted. " + sourceReservation);
                var result = ReviewConflict(decision => draft.ReviewTransfer(source, destination, copy, decision), ref choice);
                return choice == DraftConflictChoice.Swap && sourceReservation is not null
                    ? new(false, false, "Cannot swap an assignment into a reserved modifier. " + sourceReservation) : result;
            }, () =>
            {
                var result = copy ? draft.Copy(source, destination, OccupiedDestinationBehavior.Ask, choice)
                    : draft.Move(source, destination, OccupiedDestinationBehavior.Ask, choice);
                if (result.Outcome != DraftOutcome.Applied) return result;
                var labels = new List<KeyValuePair<string, string?>>();
                if (!copy) labels.Add(new(source.Key, null));
                if (context.Labels.TryGetValue(source.Key, out var label)) labels.Add(new(destination.Key, label));
                if (labels.Count > 0) context.SetLabels(labels);
                return result;
            }));
        }

        private GameAssignmentReview ReviewConflict(Func<DraftConflictChoice?, DraftResult> preview, ref DraftConflictChoice? choice)
        {
            var result = preview(null);
            if (result.Outcome == DraftOutcome.ConfirmationRequired)
            {
                choice = ThemeMessageBox.Show(Window.GetWindow(owner),
                    "Destination is occupied. Yes: replace; No: swap; Cancel: keep both.",
                    "Occupied control", MessageBoxButton.YesNoCancel, MessageBoxImage.Question) switch
                {
                    MessageBoxResult.Yes => DraftConflictChoice.Replace,
                    MessageBoxResult.No => DraftConflictChoice.Swap,
                    _ => DraftConflictChoice.Cancel
                };
                result = preview(choice);
            }
            return new(result.Outcome is DraftOutcome.Applied or DraftOutcome.NoChange,
                result.Outcome == DraftOutcome.Applied, result.Message);
        }

        internal void UndoClicked(object sender, RoutedEventArgs e)
        {
            if (state.Context?.Draft is null) return;
            var result = ExecuteNative(WorkspaceAssignmentAction.Undo, "", "", "", Ready, state.Context.Draft.Undo);
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

            var context = state.Context;
            var draft = context.Draft;
            var target = CaptureTarget(assignment.Command.Id, slot.InputKey, slot.Key);
            GameAssignmentReview ReviewTarget() =>
                state.ReservationError(diagram, slot.InputKey) is { } blocked ? new(false, false, blocked) : Ready();
            void ApplyDialog(AxisTuningWindow dialog, Func<GameAssignmentReview> review)
            {
                var result = Execute(WorkspaceAssignmentAction.TuneAxis, new WorkspaceAssignmentOperation(target, review,
                    () => draft.TuneAxis(slot, dialog.Filter)));
                if (result.Outcome == DraftOutcome.Applied) refresh();
            }

            if (AxisTuningPresenter is { } presenter)
            {
                if (!ActionState(WorkspaceAssignmentAction.TuneAxis).Enabled) return;
                var before = context.CaptureSnapshot();
                AxisTuningWindow? trainingDialog = null;
                trainingDialog = new AxisTuningWindow(assignment.Filter, () => ApplyDialog(trainingDialog!, () =>
                    context.CaptureSnapshot() == before ? ReviewTarget() : new(false, false, "The draft changed during review.")))
                    { Owner = Window.GetWindow(owner) };
                presenter(trainingDialog);
                return;
            }

            var dialog = new AxisTuningWindow(assignment.Filter) { Owner = Window.GetWindow(owner) };
            ApplyDialog(dialog, () =>
            {
                var ready = ReviewTarget();
                if (!ready.Valid) return ready;
                return dialog.ShowDialog() == true ? Ready() : new(false, false, "Axis tuning canceled.");
            });
        }

        internal DraftResult EditLabels(string target, Func<GameAssignmentReview> review, Action apply) =>
            ExecuteNative(WorkspaceAssignmentAction.EditLabel, "", target, "", review, apply);

        protected override WorkspaceAssignmentContext ReadAssignmentContext(IGameWorkspaceContext context) => assignmentContext();
        protected override WorkspaceAssignmentActionSupport ReadNativeSupport(WorkspaceAssignmentAction action) =>
            new(action is WorkspaceAssignmentAction.Apply or WorkspaceAssignmentAction.Clear or WorkspaceAssignmentAction.TuneAxis
                or WorkspaceAssignmentAction.Undo or WorkspaceAssignmentAction.EditLabel or WorkspaceAssignmentAction.Inspect,
                state.Context?.Draft is not null);
        protected override void RefreshNativeEditor() => refreshActions();
        protected override void ReportNativeResult(DraftResult result) => SelectedCallout.Text = result.Message;
        private static GameAssignmentReview Ready() => new(true, true, "");
        #endregion END Public
    }
}
