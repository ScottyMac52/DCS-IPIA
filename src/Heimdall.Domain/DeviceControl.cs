namespace Heimdall.Domain
{
    /// <summary>Represents device control.</summary>
    /// <param name="Id">The identifier.</param>
    /// <param name="DeviceId">The device identifier.</param>
    /// <param name="StableKey">The stable key.</param>
    /// <param name="Label">The label.</param>
    /// <param name="Kind">The kind.</param>
    /// <param name="Geometry">The geometry.</param>
    public sealed record DeviceControl(Guid Id, Guid DeviceId, string StableKey, string Label, ControlKind Kind,
                                       GraphicGeometry? Geometry);
}
