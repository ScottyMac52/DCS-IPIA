using System.Text.RegularExpressions;

namespace Heimdall.Application.Games
{
    /// <summary>One shared name contract for saved configurations and exported Windows presets.</summary>
    public static class GameConfigurationName
    {
        #region BEGIN Public
        /// <summary>Rejects names that cannot identify a named game configuration safely.</summary>
        public static void Validate(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || name != name.Trim() || name.EndsWith('.')
                || name.Any(char.IsControl) || name.IndexOfAny(['/', '\\', ':', '<', '>', '"', '|', '*', '?']) >= 0 || name.Contains("..", StringComparison.Ordinal))
                throw new InvalidOperationException("Choose an configuration name of 1–100 characters without path characters, trailing dots or surrounding whitespace.");
            var stem = name.Split('.')[0].ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL" || Regex.IsMatch(stem, @"^(COM|LPT)[1-9¹²³]$"))
                throw new InvalidOperationException("Choose an configuration name that is not a reserved Windows filename.");
        }
        #endregion END Public
    }
}
