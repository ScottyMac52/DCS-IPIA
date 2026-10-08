using Dcs.Ipia.Application;

namespace Dcs.Ipia.Rendering
{
    /// <summary>Renders pinned custom artwork with readable generic continuation pages for every omitted control.</summary>
    public static class DeviceKneeboardPages
    {
        #region BEGIN Public
        /// <summary>Renders all pages in deterministic order for preview, documentation and package output.</summary>
        /// <param name="diagram">Verified module-owned layout.</param>
        /// <param name="page">Current combined labels and modifier legend.</param>
        /// <param name="footer">Optional draft attribution.</param>
        /// <returns>Artwork page followed by paginated unmapped inputs, or existing generic/bundled pages.</returns>
        public static IReadOnlyList<byte[]> Render(UiLayerDiagram diagram, KneeboardComposite page, string footer = "")
        {
            GenericHardwareLayout.Validate(diagram);
            if (diagram.CustomMapping is not { } revision)
                return diagram.GenericCapabilities is null ? [CompositeKneeboardRenderer.Render(diagram, page, footer)]
                    : GenericKneeboardRenderer.RenderPages(diagram, page, footer);
            UserMappingImageVerifier.Validate(revision);
            var result = new List<byte[]> { CompositeKneeboardRenderer.Render(diagram, page, footer) };
            var missing = revision.Definition.Capabilities.DcsKeys.Except(revision.MappedInputs, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
            if (missing.Count > 0)
            {
                var labels = diagram.Controls.GroupBy(x => x.InputKey, StringComparer.Ordinal)
                    .ToDictionary(x => x.Key, x => x.OrderBy(y => y.Control.EndsWith("-shifted", StringComparison.Ordinal)).First().Label, StringComparer.Ordinal);
                var generic = GenericHardwareLayout.Create(revision.Definition, labels).Diagram;
                result.AddRange(GenericKneeboardRenderer.RenderPages(generic, page, footer, missing));
            }
            return result;
        }
        #endregion END Public
    }
}
