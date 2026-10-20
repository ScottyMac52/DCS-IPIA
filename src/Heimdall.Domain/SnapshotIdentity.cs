namespace Heimdall.Domain
{
    /// <summary>Represents snapshot identity.</summary>
    /// <param name="SchemaVersion">The schema version.</param>
    /// <param name="Sha256">The sha256.</param>
    public sealed record SnapshotIdentity(int SchemaVersion, string Sha256);
}
