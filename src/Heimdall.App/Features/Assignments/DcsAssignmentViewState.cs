using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;

namespace Heimdall.App.Features.Assignments
{
    /// <summary>Native selection shared by DCS assignment components; scope reads always reflect the current panels.</summary>
    internal sealed class DcsAssignmentViewState(Func<UiLayerDiagram?> diagram, Func<AssignmentLayer> layer, Func<string?> chord)
    {
        #region BEGIN Public
        internal IDcsModuleWorkspaceContext? Context { get; set; }
        internal HardwareGraphicControl? Selected { get; set; }
        internal string? InputKey { get; set; }
        internal Guid? DeviceId { get; set; }
        internal bool UpdatingLabel { get; set; }
        internal UiLayerDiagram? Diagram => diagram();
        internal AssignmentLayer Layer => layer();
        internal string LayerName => chord() is { } value && value != "Base" ? value : "";
        internal string Chord => chord() is { } value && value != "Base" ? value.Replace(" + ", "+", StringComparison.Ordinal) : "";
        internal IReadOnlyList<DcsWorkspaceModifier> Reserved(UiLayerDiagram target, string key) =>
            Context?.Working is { } workspace
                ? DcsModifierAssignmentReservation.Find(DcsWorkspaceStructureEditor.Describe(workspace), target.ProfileId, key) : [];
        internal string? ReservationError(UiLayerDiagram target, string key)
        {
            var held = Reserved(target, key);
            return held.Count == 0 ? null : DcsModifierAssignmentReservation.BlockReason(key, held);
        }
        #endregion END Public
    }
}
