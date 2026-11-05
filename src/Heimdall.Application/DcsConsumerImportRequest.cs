namespace Heimdall.Application
{
    /// <summary>Supplies pinned module consumer files and the imported shared authority.</summary>
    /// <param name="Commit">The exact module repository commit.</param>
    /// <param name="Catalog">The successfully imported DCS-Common catalog.</param>
    /// <param name="SharedLua">The successfully imported canonical UI Layer Lua.</param>
    /// <param name="Sources">Unmodified module source files.</param>
    public sealed record DcsConsumerImportRequest(string Commit, DcsSharedCatalogImportResult Catalog,
                                               DcsSharedLuaImportResult SharedLua, IReadOnlyList<DcsConsumerSource> Sources,
                                               string? Repository = null);
}
