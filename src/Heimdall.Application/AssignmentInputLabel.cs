using System.Globalization;

namespace Heimdall.Application
{
    /// <summary>Formats physical input labels independently of simulator binding values.</summary>
    public static class AssignmentInputLabel
    {
        public static string WithCaption(string identifier, string? caption) =>
            string.IsNullOrWhiteSpace(caption) || string.Equals(identifier, caption, StringComparison.OrdinalIgnoreCase)
                ? identifier : identifier + " — " + caption;

        public static string ForPhysicalKey(string key, string? caption)
        {
            const string prefix = "JOY_BTN";
            var identifier = key.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(key.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                && number > 0 ? "Button " + number.ToString(CultureInfo.InvariantCulture) : key;
            return WithCaption(identifier, caption);
        }
    }
}
