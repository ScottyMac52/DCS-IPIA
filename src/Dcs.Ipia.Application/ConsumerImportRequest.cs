namespace Dcs.Ipia.Application
{
    /// <summary>Supplies pinned module consumer files and the imported shared authority.</summary>
    /// <param name="Commit">The exact module repository commit.</param>
    /// <param name="Catalog">The successfully imported DCS-Common catalog.</param>
    /// <param name="SharedLua">The successfully imported canonical UI Layer Lua.</param>
    /// <param name="Sources">Unmodified module source files.</param>
    public sealed record ConsumerImportRequest(string Commit, SharedCatalogImportResult Catalog,
                                               SharedLuaImportResult SharedLua, IReadOnlyList<ConsumerSource> Sources,
                                               string? Repository = null);
}
