using System.Globalization;
using System.Text.RegularExpressions;

namespace Heimdall.Application
{
    /// <summary>Formats physical input labels independently of simulator binding values.</summary>
    public static partial class AssignmentInputLabel
    {
        public static string WithCaption(string identifier, string? caption)
        {
            var label = caption?.Trim();
            if (label is not null && identifier.StartsWith("Button ", StringComparison.Ordinal))
            {
                var prefix = ButtonCaptionPrefix().Match(label);
                if (prefix.Success && int.TryParse(prefix.Groups[1].ValueSpan, NumberStyles.None,
                    CultureInfo.InvariantCulture, out var number)
                    && identifier.AsSpan(7).SequenceEqual(number.ToString(CultureInfo.InvariantCulture)))
                    label = label[prefix.Length..].TrimStart();
            }

            return string.IsNullOrWhiteSpace(label) || string.Equals(identifier, label, StringComparison.OrdinalIgnoreCase)
                ? identifier : identifier + " — " + label;
        }

        [GeneratedRegex(@"^(?:Button\s+|B)([0-9]+)\s*(?:[-–—|:·]\s*|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex ButtonCaptionPrefix();

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
