using Heimdall.Domain;

namespace Heimdall.Application
{
    /// <summary>Represents profile import source.</summary>
    /// <param name="StableDeviceId">The stable device identifier.</param>
    /// <param name="Instance">The instance.</param>
    /// <param name="Role">The role.</param>
    /// <param name="SourceName">The source name.</param>
    /// <param name="Content">The content.</param>
    public sealed record ProfileImportSource(string StableDeviceId, string? Instance, string Role, string SourceName,
                                             string Content);
}
