using Dcs.Ipia.Domain.MultiSim;

namespace Dcs.Ipia.EliteDangerous
{
    /// <summary>A native assignment presented without assuming an aircraft or a physical instance.</summary>
    /// <param name="DeviceId">Captured logical device reference.</param>
    /// <param name="CommandId">Scoped catalog command.</param>
    /// <param name="CommandName">Display label.</param>
    /// <param name="Section">Display-only context filter.</param>
    /// <param name="Slot">Primary, Secondary or Binding.</param>
    /// <param name="Input">Native input key.</param>
    /// <param name="Mode">Per-binding press or hold.</param>
    /// <param name="Modifiers">Explicit device/key chord.</param>
    /// <param name="IsAxis">Whether this is an axis.</param>
    /// <param name="Invert">Axis inversion.</param>
    /// <param name="Deadzone">Axis deadzone in percentage points.</param>
    public sealed record EliteAssignmentRow(string DeviceId, string CommandId, string CommandName, string Section,
        string Slot, string Input, BindingMode Mode, IReadOnlyList<Modifier> Modifiers, bool IsAxis = false,
        bool Invert = false, float Deadzone = 0)
    {
        #region BEGIN Public
        /// <summary>Gets a concise native chord summary; source identity remains in Modifiers.</summary>
        public string Chord => string.Join(" + ", Modifiers.Select(x => x.Value));
        #endregion END Public
    }
}
