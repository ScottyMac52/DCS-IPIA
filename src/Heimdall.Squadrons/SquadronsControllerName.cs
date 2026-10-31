using System.Globalization;

namespace Heimdall.Squadrons
{
    /// <summary>Builds game-facing occurrence names without confusing model numbers with occurrence suffixes.</summary>
    internal static class SquadronsControllerName
    {
        #region BEGIN Public
        /// <summary>Preserves an existing distinct game name or suggests the first unused occurrence of a reported Windows name.</summary>
        internal static string Suggest(string reportedName, IEnumerable<string> declaredNames, string? currentName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reportedName);
            if (!string.IsNullOrWhiteSpace(currentName) && !string.Equals(currentName, reportedName, StringComparison.OrdinalIgnoreCase))
                return currentName;
            var taken = declaredNames.Where(x => !string.Equals(x, currentName, StringComparison.OrdinalIgnoreCase)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Enumerable.Range(1, taken.Count + 1).Select(index => reportedName + " " + index.ToString(CultureInfo.InvariantCulture))
                .First(name => !taken.Contains(name));
        }
        #endregion END Public
    }
}
