using Heimdall.Domain;

namespace Heimdall.Dcs.SnapshotModels
{
    /// <summary>Represents configuration snapshot dto.</summary>
    /// <param name="SchemaVersion">The schema version.</param>
    /// <param name="Modules">The modules.</param>
    internal sealed record DcsConfigurationSnapshotDto(int SchemaVersion, IReadOnlyList<DcsConfigurationModuleDto> Modules);
}
