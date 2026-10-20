using Heimdall.Domain;

namespace Heimdall.Dcs.SnapshotModels
{
    /// <summary>Represents configuration profile dto.</summary>
    /// <param name="Id">The identifier.</param>
    /// <param name="DeviceId">The device identifier.</param>
    /// <param name="Instance">The instance.</param>
    /// <param name="Role">The role.</param>
    /// <param name="SourceName">The source name.</param>
    internal sealed record ConfigurationProfileDto(Guid Id, string DeviceId, string? Instance, string Role,
                                                   string SourceName);
}
