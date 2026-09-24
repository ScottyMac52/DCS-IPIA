namespace Dcs.Ipia.Application;

/// <summary>Supplies pinned Huey consumer files and the imported shared authority.</summary>
/// <param name="Commit">The exact Huey repository commit.</param>
/// <param name="Catalog">The successfully imported DCS-Common catalog.</param>
/// <param name="SharedLua">The successfully imported canonical UI Layer Lua.</param>
/// <param name="Sources">Unmodified Huey source files.</param>
public sealed record HueyImportRequest(string Commit,SharedCatalogImportResult Catalog,SharedLuaImportResult SharedLua,IReadOnlyList<ConsumerSource> Sources);
