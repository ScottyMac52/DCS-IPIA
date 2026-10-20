namespace Heimdall.Application
{
    /// <summary>Resolves whether a kneeboard legend entry applies to the current live modifier state.</summary>
    public static class KneeboardLegendActivity
    {
        #region BEGIN Public
        /// <summary>Matches the complete active chord within the entry's own Module/UI Layer scope.</summary>
        public static bool IsActive(KneeboardModifierLegend entry,
                                    IReadOnlyDictionary<AssignmentLayer, string> activeModifiers)
        {
            ArgumentNullException.ThrowIfNull(entry);
            return Matches(entry.Layer, entry.Chord, activeModifiers);
        }
        /// <summary>Checks the full modifier chord of one scope without interpreting display text.</summary>
        public static bool Matches(AssignmentLayer layer, string chord,
                                   IReadOnlyDictionary<AssignmentLayer, string> activeModifiers)
        {
            ArgumentNullException.ThrowIfNull(chord);
            ArgumentNullException.ThrowIfNull(activeModifiers);
            return string.Equals(Normalize(chord), Normalize(activeModifiers.GetValueOrDefault(layer) ?? ""),
                StringComparison.Ordinal);
        }
        #endregion END Public

        #region BEGIN private helpers
        private static string Normalize(string chord) =>
            string.Join("+", chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                  .Order(StringComparer.Ordinal));
        #endregion END private helpers
    }
}
