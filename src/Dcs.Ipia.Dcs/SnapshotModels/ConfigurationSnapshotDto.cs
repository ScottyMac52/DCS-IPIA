using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs.SnapshotModels
{
    /// <summary>Represents configuration snapshot dto.</summary>
    /// <param name="SchemaVersion">The schema version.</param>
    /// <param name="Modules">The modules.</param>
    internal sealed record ConfigurationSnapshotDto(int SchemaVersion, IReadOnlyList<ConfigurationModuleDto> Modules);
}
