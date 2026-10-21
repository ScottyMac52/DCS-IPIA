using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Games
{
    /// <summary>A complete assignment draft, including an optional native command toggle.</summary>
    /// <param name="DeviceId">Selected native controller reference.</param>
    /// <param name="CommandId">Native command identity.</param>
    /// <param name="Slot">Primary, Secondary or Binding.</param>
    /// <param name="Key">Native input key.</param>
    /// <param name="IsAxis">Whether this edits an axis binding.</param>
    /// <param name="Invert">Axis inversion.</param>
    /// <param name="Deadzone">Axis deadzone percentage.</param>
    /// <param name="Mode">Button press or hold mode.</param>
    /// <param name="Modifiers">Required combination inputs.</param>
    /// <param name="Toggle">Optional existing command toggle value.</param>
    public sealed record GameAssignmentEdit(string DeviceId, string CommandId, string Slot, string Key,
        bool IsAxis = false, bool Invert = false, float Deadzone = 0, BindingMode Mode = BindingMode.Press,
        IReadOnlyList<Modifier>? Modifiers = null, bool? Toggle = null);
}
