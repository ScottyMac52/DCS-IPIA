namespace Heimdall.Application
{
    /// <summary>Supplies pinned shared catalogs and their Lua sources for a read-only import.</summary>
    /// <param name="Commit">The exact DCS-Common source commit.</param>
    /// <param name="Catalog">The imported shared catalogs from that commit.</param>
    /// <param name="Sources">Hardware definitions, canonical UI Layer profiles, and modifiers.</param>
    public sealed record SharedLuaImportRequest(string Commit, SharedCatalogImportResult Catalog,
                                                IReadOnlyList<SharedLuaSource> Sources);
}
