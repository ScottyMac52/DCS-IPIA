namespace Dcs.Ipia.Domain
{
    /// <summary>Represents UI Layer overlay.</summary>
    /// <param name="ModuleId">The module identifier.</param>
    /// <param name="DeviceId">The device identifier.</param>
    /// <param name="ControlId">The control identifier.</param>
    /// <param name="CommandId">The command identifier.</param>
    /// <param name="Label">The label.</param>
    public sealed record UiLayerOverlay(Guid ModuleId, Guid DeviceId, Guid ControlId, Guid? CommandId, string Label);
}
