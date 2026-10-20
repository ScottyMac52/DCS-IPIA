namespace Heimdall.Domain
{
    /// <summary>Represents kneeboard page.</summary>
    /// <param name="Id">The identifier.</param>
    /// <param name="ModuleId">The module identifier.</param>
    /// <param name="DeviceInstanceId">The device instance identifier.</param>
    /// <param name="Kind">The kind.</param>
    /// <param name="StableKey">The stable key.</param>
    /// <param name="Title">The title.</param>
    /// <param name="CategoryIds">The category ids.</param>
    /// <param name="Order">The order.</param>
    public sealed record KneeboardPage(Guid Id, Guid ModuleId, Guid? DeviceInstanceId, KneeboardPageKind Kind,
                                       string StableKey, string Title, IReadOnlyList<Guid> CategoryIds, int Order);
}
