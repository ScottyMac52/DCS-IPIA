using Heimdall.Application;
using Heimdall.Dcs;
using System.Windows.Controls;

namespace Heimdall.App.Features.Assignments
{
    /// <summary>Keeps selected native input, saved/pending details and label editor state synchronized.</summary>
    internal sealed class DcsAssignmentInspector(
        DcsAssignmentViewState state,
        AssignmentInputPicker InputPicker,
        TextBlock SelectedCallout,
        TextBlock InspectorDetails,
        TextBox LabelEditor,
        TextBox GenericControlName,
        Button RenameGenericControlButton,
        Action refreshActions)
    {
        private const string UnAssignedText = "Unassigned";
        #region BEGIN Public
        internal void Select(HardwareGraphicControl control, UiLayerDiagram diagram, string caption, bool protectedInput, string modifierDescription)
        {
            state.Selected = control;
            state.InputKey = control.InputKey;
            InputPicker.SelectedValue = control.InputKey;
            SelectedCallout.Text = protectedInput
                ? $"{control.Label} · {control.InputKey} · MODIFIER (reserved)"
                : $"{control.Label} · {control.InputKey} → {caption}";
            var selectedSlot = DcsKneeboardDraftProjection.SlotForControl(
                state.Context!.Working!, diagram, control, state.Layer,
                state.Chord);

            var pendingAssignment = selectedSlot is null ? null : state.Context.Draft?.Snapshot.FirstOrDefault(x =>
                DcsInputKey.PhysicalSlotKey(x.Slot) == DcsInputKey.PhysicalSlotKey(selectedSlot));

            var original = selectedSlot is null ? null : state.Context.Saved?.Assignments.FirstOrDefault(x =>
                DcsInputKey.PhysicalSlotKey(x.Slot) == DcsInputKey.PhysicalSlotKey(selectedSlot));

            InspectorDetails.Text = selectedSlot is null
                ? ""
                : $"Input: {control.InputKey}   Chord: {(selectedSlot.ModifierChord.Length == 0 ? "Base" : selectedSlot.ModifierChord)}\n" +
                  $"Saved: {original?.Command.Name ?? "Unassigned"}   Pending: {pendingAssignment?.Command.Name ?? "Unassigned"}\n" +
                  $"Default label: {control.Label}";
            if (protectedInput)
                InspectorDetails.Text += "\n" + modifierDescription +
                    "\nReserved: change or remove it in Modifiers before assigning a command." +
                    (pendingAssignment is null ? "" : "\nExisting assignment can still be cleared.");
            refreshActions();
            state.UpdatingLabel = true;
            LabelEditor.Text = protectedInput || caption == UnAssignedText ? "" : caption;
            LabelEditor.IsEnabled = !protectedInput;
            state.UpdatingLabel = false;
            var isGeneric = diagram.GenericCapabilities is not null;
            GenericControlName.IsEnabled = isGeneric;
            RenameGenericControlButton.IsEnabled = isGeneric;
            GenericControlName.Text = isGeneric ? control.Label : "";
        }
        internal void Clear()
        {
            state.Selected = null; state.InputKey = null;
            InputPicker.SelectedIndex = -1;
            SelectedCallout.Text = "Select a callout and a command.";
            InspectorDetails.Text = "";
            state.UpdatingLabel = true; LabelEditor.Text = ""; state.UpdatingLabel = false;
            LabelEditor.IsEnabled = false;
            refreshActions();
        }
        internal void ClearLabel()
        {
            state.UpdatingLabel = true;
            try { LabelEditor.Text = ""; }
            finally { state.UpdatingLabel = false; }
        }
        #endregion END Public
    }
}
