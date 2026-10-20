namespace Heimdall.Application
{
    /// <summary>Independent revision of shared default UI Layer source, separate from module overrides.</summary>
    /// <param name="Revision">Saved revision, or zero for a new workspace.</param>
    /// <param name="SourceCommit">Pinned shared asset source commit.</param>
    /// <param name="Sources">Canonical UI Layer catalogs and exact Lua source texts.</param>
    /// <param name="ContentSha256">Verified content identity assigned when saved.</param>
    public sealed record SharedUiLayerWorkspace(int Revision, string SourceCommit,
        IReadOnlyDictionary<string, string> Sources, string? ContentSha256 = null);
}
