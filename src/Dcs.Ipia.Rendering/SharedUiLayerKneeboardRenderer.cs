using Dcs.Ipia.Application;

namespace Dcs.Ipia.Rendering
{
    /// <summary>Renders independent shared-default device pages from a frozen module-neutral projection.</summary>
    public static class SharedUiLayerKneeboardRenderer
    {
        #region BEGIN Public
        /// <summary>Renders every pinned shared joystick graphic with current UI assignments and modifier legends.</summary>
        /// <param name="projection">Portable shared output projection.</param>
        /// <returns>Independent PNG paths and bytes.</returns>
        public static IReadOnlyDictionary<string, byte[]> Generate(EditorWorkspaceSnapshot projection)
        {
            var result = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var diagram in projection.Diagrams)
            {
                var page = KneeboardDraftProjection.CreateCombined(projection, diagram, projection.Assignments, projection.Labels)
                    with { Kicker = "SHARED DEFAULT UI LAYER" };
                result.Add("KNEEBOARD/UiLayer/" + diagram.ProfileId.ToString("N") + ".png", CompositeKneeboardRenderer.Render(diagram, page, ""));
            }
            return result;
        }
        #endregion END Public
    }
}
