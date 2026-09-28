namespace Dcs.Ipia.Domain
{
    /// <summary>Represents device instance.</summary>
    /// <param name="Id">The identifier.</param>
    /// <param name="ModuleId">The module identifier.</param>
    /// <param name="DeviceId">The device identifier.</param>
    /// <param name="StableKey">The stable key.</param>
    /// <param name="Role">The role.</param>
    public sealed record DeviceInstance(Guid Id, Guid ModuleId, Guid DeviceId, string StableKey, string Role);
}
