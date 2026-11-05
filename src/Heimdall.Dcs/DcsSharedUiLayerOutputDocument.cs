using Heimdall.Application;

namespace Heimdall.Dcs
{
    /// <summary>Versioned canonical shared-output document.</summary>
    /// <param name="SchemaVersion">Portable output schema version.</param>
    /// <param name="Snapshot">Saved defaults and pinned output assets.</param>
    public sealed record DcsSharedUiLayerOutputDocument(int SchemaVersion, DcsSharedUiLayerOutputSnapshot Snapshot);
}
