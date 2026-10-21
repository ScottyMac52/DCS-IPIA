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
            string? category = null, CommandType? type = null, string search = "",
            IReadOnlyList<EliteAssignmentRow>? assignments = null, string? deviceId = null, EliteAssignmentStatus status = EliteAssignmentStatus.All)
        {
            if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
            var text = search.Trim();
            var rows = assignments ?? [];
            var any = rows.Select(x => x.CommandId).ToHashSet(StringComparer.Ordinal);
            var selected = rows.Where(x => x.DeviceId == deviceId).Select(x => x.CommandId).ToHashSet(StringComparer.Ordinal);
            return commands.Where(x => InSection(x, section) && (category is null || Category(x) == category)
                && (type is null || x.Type == type) && Matches(x, text) && InStatus(x.Id, status, selected, any))
                .OrderBy(Category, StringComparer.Ordinal).ThenBy(x => x.DisplayName, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
        }
        #endregion END Public
        #region BEGIN private helpers
        private static bool InStatus(string id, EliteAssignmentStatus status, HashSet<string> selected, HashSet<string> any) => status switch
        {
            EliteAssignmentStatus.AssignedOnly => selected.Contains(id),
            EliteAssignmentStatus.NotAssigned => !selected.Contains(id),
            EliteAssignmentStatus.NotAssignedByAny => !any.Contains(id),
            EliteAssignmentStatus.AssignedByAny => any.Contains(id),
            _ => true
        };
        private static bool InSection(SimCommand command, string section) => section == "All" || command.Category == section;
        private static bool Matches(SimCommand command, string text) => command.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase)
            || command.Id.Contains(text, StringComparison.OrdinalIgnoreCase) || Category(command).Contains(text, StringComparison.OrdinalIgnoreCase);
        #endregion END private helpers
    }
}
