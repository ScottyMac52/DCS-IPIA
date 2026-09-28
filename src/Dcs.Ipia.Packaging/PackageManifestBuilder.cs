using Dcs.Ipia.Application;
namespace Dcs.Ipia.Packaging
{
    /// <summary>Builds deterministically ordered package manifests.</summary>
    public static class PackageManifestBuilder
    {
        #region BEGIN Public
        /// <summary>Builds a deterministic package manifest.</summary>
        /// <param name="values">The values.</param>
        /// <returns>The result of the operation.</returns>
        public static PackageManifest Build(IEnumerable<ModuleDto> values)
        {
            ArgumentNullException.ThrowIfNull(values);
            return new(
                "1",
                values.Select(x => x.DcsId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        }
        #endregion END Public
    }
}
