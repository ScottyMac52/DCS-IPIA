using Heimdall.Domain.MultiSim;

namespace Heimdall.EliteDangerous
{
    /// <summary>Composes display-only command filters independently of WPF and native profile editing.</summary>
    public static class EliteCommandBrowser
    {
        #region BEGIN Public
        /// <summary>Gets an assigned display category, retaining unknown commands under Other.</summary>
        public static string Category(SimCommand command) => command.Metadata.GetValueOrDefault("ed.category") ?? "Other";
        /// <summary>Lists all categories in an activity, independent of the current text and type filters.</summary>
        public static IReadOnlyList<string> Categories(IEnumerable<SimCommand> commands, string section = "All") =>
            commands.Where(x => InSection(x, section)).Select(Category).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        /// <summary>Intersects activity, category, native input type and case-insensitive text; never mutates the catalog.</summary>
        public static IReadOnlyList<SimCommand> Filter(IEnumerable<SimCommand> commands, string section = "All",
            string? category = null, CommandType? type = null, string search = "")
        {
            var text = search.Trim();
            return commands.Where(x => InSection(x, section) && (category is null || Category(x) == category)
                && (type is null || x.Type == type) && Matches(x, text))
                .OrderBy(Category, StringComparer.Ordinal).ThenBy(x => x.DisplayName, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
        }
        #endregion END Public
        #region BEGIN private helpers
        private static bool InSection(SimCommand command, string section) => section == "All" || command.Category == section;
        private static bool Matches(SimCommand command, string text) => command.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase)
            || command.Id.Contains(text, StringComparison.OrdinalIgnoreCase) || Category(command).Contains(text, StringComparison.OrdinalIgnoreCase);
        #endregion END private helpers
    }
}
