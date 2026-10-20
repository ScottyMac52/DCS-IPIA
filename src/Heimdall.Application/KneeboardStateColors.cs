namespace Heimdall.Application
{
    /// <summary>Shares functional colors between assignment callouts, live feedback and modifier legends.</summary>
    public static class KneeboardStateColors
    {
        #region BEGIN Public
        /// <summary>Blue identifies unmodified aircraft assignments independently of the UI skin.</summary>
        public const string Base = "#007ACC";
        /// <summary>Cyan identifies unmodified UI Layer assignments.</summary>
        public const string UiBase = "#0891b2";
        /// <summary>Neutral ink identifies an unused modifier without inventing a shift palette slot.</summary>
        public const string UnusedModifier = "#94a3b8";
        /// <summary>Resolves live/editor ink while preserving the shared color of UI and shifted variants.</summary>
        public static string Resolve(AssignmentLayer layer, string chord, string variantColor)
        {
            ArgumentNullException.ThrowIfNull(chord);
            ArgumentException.ThrowIfNullOrWhiteSpace(variantColor);
            return layer == AssignmentLayer.Module && chord.Length == 0 ? Base : variantColor;
        }
        #endregion END Public
    }
}
