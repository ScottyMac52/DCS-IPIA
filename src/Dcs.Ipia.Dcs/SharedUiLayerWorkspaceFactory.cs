using Dcs.Ipia.Application;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Seeds independent shared defaults from verified application-owned assets.</summary>
    public static class SharedUiLayerWorkspaceFactory
    {
        #region BEGIN Public
        /// <summary>Loads catalogs and canonical Lua without reading or modifying any module.</summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Unsaved shared default sources with pinned provenance.</returns>
        public static async Task<SharedUiLayerWorkspace> CreateAsync(CancellationToken ct = default)
        {
            // Reuse the catalog/Lua validation performed for built-in hardware loading.
            await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root, ct);
            var sources = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var path in BundledSharedCatalog.Paths("assets/shared/ui-layer"))
                sources[path] = await BundledSharedCatalog.ReadTextAsync(path, ct);
            return new(0, BundledSharedCatalog.SourceCommit(), sources);
        }
        #endregion END Public
    }
}
