using Dcs.Ipia.Application;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Versioned canonical shared-output document.</summary>
    /// <param name="SchemaVersion">Portable output schema version.</param>
    /// <param name="Snapshot">Saved defaults and pinned output assets.</param>
    public sealed record SharedUiLayerOutputDocument(int SchemaVersion, SharedUiLayerOutputSnapshot Snapshot);
}
