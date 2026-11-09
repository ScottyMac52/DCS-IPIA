namespace Heimdall.Application.Games
{
    /// <summary>Reusable native-independent command filtering and stable display ordering.</summary>
    public static class GameCommandBrowser
    {
        #region BEGIN Public
        /// <summary>Lists display categories for the selected activity, regardless of other filters.</summary>
        public static IReadOnlyList<string> Categories(IEnumerable<GameCommandEntry> entries, string? section = null) =>
            entries.Where(x => section is null || x.Section == section).Select(x => x.Category)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        /// <summary>Filters adapter facts without modifying commands, assignments or native source.</summary>
        public static IReadOnlyList<GameCommandEntry> Filter(IEnumerable<GameCommandEntry> entries,
            GameCommandQuery query, string? controllerId = null)
        {
            if (!Enum.IsDefined(query.AssignmentStatus)) throw new ArgumentOutOfRangeException(nameof(query));
            var normalized = query with { Search = query.Search.Trim() };
            return entries.Where(x => Matches(x, normalized, controllerId))
                .OrderBy(x => x.Category, StringComparer.Ordinal)
                .ThenBy(x => x.Command.DisplayName, StringComparer.Ordinal)
                .ThenBy(x => x.Command.Id, StringComparer.Ordinal).ToArray();
        }
        /// <summary>Tests one cached native projection without materializing a catalog or ownership index.</summary>
        public static bool Matches(GameCommandEntry entry, GameCommandQuery query, string? controllerId = null) =>
            InScope(entry, query) && MatchesText(entry, query.Search) && InStatus(entry, controllerId, query.AssignmentStatus);
        #endregion END Public
        #region BEGIN private helpers
        private static bool InScope(GameCommandEntry entry, GameCommandQuery query) =>
            (query.Section is null || entry.Section == query.Section) &&
            (query.Category is null || entry.Category == query.Category) &&
            (query.InputType is null || entry.InputTypes.Contains(query.InputType.Value));
        private static bool MatchesText(GameCommandEntry entry, string search) =>
            entry.Command.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            entry.Command.Id.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            entry.Category.Contains(search, StringComparison.OrdinalIgnoreCase);
        private static bool InStatus(GameCommandEntry entry, string? controllerId,
            GameCommandAssignmentStatus status) => status switch
        {
            GameCommandAssignmentStatus.AssignedToController => entry.AssignedControllers.Contains(controllerId, StringComparer.Ordinal),
            GameCommandAssignmentStatus.NotAssignedToController => !entry.AssignedControllers.Contains(controllerId, StringComparer.Ordinal),
            GameCommandAssignmentStatus.UnassignedEverywhere => !entry.HasAssignments,
            GameCommandAssignmentStatus.AssignedAnywhere => entry.HasAssignments,
            _ => true
        };
        #endregion END private helpers
    }
}
