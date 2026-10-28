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
            var search = query.Search.Trim();
            return entries.Where(x => InScope(x, query) && MatchesText(x, search) &&
                    InStatus(x.AssignedControllers, controllerId, query.AssignmentStatus))
                .OrderBy(x => x.Category, StringComparer.Ordinal)
                .ThenBy(x => x.Command.DisplayName, StringComparer.Ordinal)
                .ThenBy(x => x.Command.Id, StringComparer.Ordinal).ToArray();
        }
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
        private static bool InStatus(IReadOnlyCollection<string> owners, string? controllerId,
            GameCommandAssignmentStatus status) => status switch
        {
            GameCommandAssignmentStatus.AssignedToController => owners.Contains(controllerId, StringComparer.Ordinal),
            GameCommandAssignmentStatus.NotAssignedToController => !owners.Contains(controllerId, StringComparer.Ordinal),
            GameCommandAssignmentStatus.UnassignedEverywhere => owners.Count == 0,
            GameCommandAssignmentStatus.AssignedAnywhere => owners.Count > 0,
            _ => true
        };
        #endregion END private helpers
    }
}
