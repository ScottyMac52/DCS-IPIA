namespace Dcs.Ipia.Application
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
            ArgumentNullException.ThrowIfNull(activeModifiers);
            var active = activeModifiers.GetValueOrDefault(entry.Layer) ?? "";
            return string.Equals(Normalize(entry.Chord), Normalize(active), StringComparison.Ordinal);
        }
        #endregion END Public

        #region BEGIN private helpers
        private static string Normalize(string chord) =>
            string.Join("+", chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                  .Order(StringComparer.Ordinal));
        #endregion END private helpers
    }
}
