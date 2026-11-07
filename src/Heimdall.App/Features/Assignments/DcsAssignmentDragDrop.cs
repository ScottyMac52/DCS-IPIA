using Heimdall.Application;
using Heimdall.Dcs;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace Heimdall.App.Features.Assignments
{
    /// <summary>Owns callout drag lifetime, cursor feedback and DCS move/copy drop handling.</summary>
    internal sealed class DcsAssignmentDragDrop(
        DcsAssignmentViewState state,
        FrameworkElement owner,
        FrameworkElement AssignmentWorkspace,
        TextBlock SelectedCallout,
        DcsAssignmentEditing editing,
        Action refresh)
    {
        private Point? _calloutDragStart;
        private HardwareGraphicControl? _calloutDragControl;
        private TitleAdorner? _calloutAdorner;
        private AdornerLayer? _calloutLayer;
        #region BEGIN Public
        internal void ClearStart() { _calloutDragStart = null; _calloutDragControl = null; }
        internal void Start(HardwareGraphicControl control, MouseButtonEventArgs args, Border border)
        {
            _calloutDragStart = args.GetPosition(border); _calloutDragControl = control;
        }
        internal void Move(Border border, HardwareGraphicControl control, MouseEventArgs args)
        {
            if (_calloutDragStart is not Point start || _calloutDragControl != control || args.LeftButton != MouseButtonState.Pressed) return;
            var position = args.GetPosition(border);
            if (Math.Abs(position.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(position.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            _calloutDragStart = null;
            CalloutDrag(border, control, args);
        }
        internal void WorkspacePreviewDragOver(object sender, DragEventArgs e)
        {
            // Observe the preview event before a callout handles bubbling DragOver.
            // Leave Effects and Handled to the actual drop target.
            if (_calloutAdorner is not null && e.Data.GetDataPresent(typeof(DcsEditorControlDrag)))
                _calloutAdorner.Update(e.GetPosition(AssignmentWorkspace));
        }

        internal void WorkspaceDragOver(object sender, DragEventArgs e)
        {
            // Only reached when a callout has not already handled the event.
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        internal void WorkspaceDrop(object sender, DragEventArgs e)
        {
            // Blank workspace is not an assignment target.
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        internal void DropOnCallout(HardwareGraphicControl control, DragEventArgs e)
        {
            if (state.Context?.Draft is null || state.Diagram is not UiLayerDiagram diagram) return;
            if (state.ReservationError(diagram, control.InputKey) is { } blocked)
            {
                SelectedCallout.Text = blocked;
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var layer = state.Layer;
            var chord = state.Chord;
            var destination = DcsKneeboardDraftProjection.SlotForControl(state.Context.Working!, diagram, control, layer, chord);
            if (destination is null) return;

            if (e.Data.GetData(typeof(DcsEditorControlDrag)) is DcsEditorControlDrag sourceDrag)
            {
                var copy = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                var sourceReservation = state.ReservationError(diagram, sourceDrag.Slot.InputKey);
                if (copy && sourceReservation is not null)
                {
                    SelectedCallout.Text = "Copying an assignment from a reserved modifier is not permitted. " +
                        sourceReservation;
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                    return;
                }
                var result = copy
                    ? state.Context.Draft.Copy(sourceDrag.Slot, destination, OccupiedDestinationBehavior.Ask, null)
                    : state.Context.Draft.Move(sourceDrag.Slot, destination, OccupiedDestinationBehavior.Ask, null);

                if (result.Outcome == DraftOutcome.ConfirmationRequired)
                {
                    var decision = ThemeMessageBox.Show(Window.GetWindow(owner),
                        "Destination is occupied. Yes: replace; No: swap; Cancel: keep both.",
                        "Occupied control", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    var choice = decision switch
                    {
                        MessageBoxResult.Yes => DraftConflictChoice.Replace,
                        MessageBoxResult.No => DraftConflictChoice.Swap,
                        _ => DraftConflictChoice.Cancel
                    };
                    if (choice == DraftConflictChoice.Swap && sourceReservation is not null)
                    {
                        SelectedCallout.Text = "Cannot swap an assignment into a reserved modifier. " + sourceReservation;
                        e.Effects = DragDropEffects.None;
                        e.Handled = true;
                        return;
                    }
                    result = copy
                        ? state.Context.Draft.Copy(sourceDrag.Slot, destination, OccupiedDestinationBehavior.Ask, choice)
                        : state.Context.Draft.Move(sourceDrag.Slot, destination, OccupiedDestinationBehavior.Ask, choice);
                }

                if (result.Outcome == DraftOutcome.Applied)
                {
                    if (result.Outcome == DraftOutcome.Applied)
                    {
                        var sourceKey = sourceDrag.Slot.Key;
                        var destKey = destination.Key;
                        var label = state.Context.Labels.GetValueOrDefault(sourceKey);

                        if (!copy)
                            state.Context.SetLabel(sourceKey, null);

                        if (label is not null)
                            state.Context.SetLabel(destKey, label);

                        refresh();
                        SelectedCallout.Text = result.Message;
                    }
                }
                else
                {
                    SelectedCallout.Text = result.Message;
                }
            }
            else if (e.Data.GetData(typeof(DcsCommandPaletteEntry)) is DcsCommandPaletteEntry command)
            {
                editing.DropPaletteCommand(control, command);
            }
            e.Handled = true;
        }
        #endregion END Public
        #region BEGIN private helpers
        private void CalloutDrag(Border source, HardwareGraphicControl control, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || state.Context?.Draft is null) return;
            if (state.Diagram is not UiLayerDiagram diagram) return;

            var layer = state.Layer;
            var chord = state.Chord;
            var slot = DcsKneeboardDraftProjection.SlotForControl(state.Context.Working!, diagram, control, layer, chord);
            if (slot is null) return;

            var assignment = state.Context.Draft.Snapshot.FirstOrDefault(x =>
                DcsInputKey.PhysicalSlotKey(x.Slot) == DcsInputKey.PhysicalSlotKey(slot));
            if (assignment is null) return;

            // Adorn the unscaled workspace, not the Window or scaled Diagram.
            // The local AdornerDecorator guarantees a layer inside this view.
            _calloutLayer = AdornerLayer.GetAdornerLayer(AssignmentWorkspace);
            _calloutAdorner = new TitleAdorner(AssignmentWorkspace, control.Label)
            {
                IsHitTestVisible = false
            };
            _calloutAdorner.Update(e.GetPosition(AssignmentWorkspace));

            GiveFeedbackEventHandler feedback = CalloutGiveFeedback;
            source.AddHandler(DragDrop.GiveFeedbackEvent, feedback, true);
            try
            {
                _calloutLayer?.Add(_calloutAdorner);
                DragDrop.DoDragDrop(source,
                    new DataObject(typeof(DcsEditorControlDrag), new DcsEditorControlDrag(slot)),
                    DragDropEffects.Move | DragDropEffects.Copy);
            }
            finally
            {
                source.RemoveHandler(DragDrop.GiveFeedbackEvent, feedback);
                if (_calloutAdorner is not null)
                    _calloutLayer?.Remove(_calloutAdorner);
                _calloutAdorner = null;
                _calloutLayer = null;
                _calloutDragStart = null;
                _calloutDragControl = null;
            }
        }

        private void CalloutGiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            // Feedback is raised on the drag source by the OLE drag loop.
            // Read the cursor here on the UI thread; WPF mouse positions can
            // be stale while DoDragDrop owns mouse input.
            if (_calloutAdorner is not null && GetCursorPos(out var point))
            {
                var position = AssignmentWorkspace.PointFromScreen(new Point(point.X, point.Y));
                _calloutAdorner.Update(position);
            }

            e.UseDefaultCursors = false;
            Mouse.SetCursor(e.Effects == DragDropEffects.None ? Cursors.No : Cursors.Arrow);
            e.Handled = true;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out NativeScreenPoint point);
        #endregion END private helpers
    }
}
