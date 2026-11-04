using System.Globalization;

namespace Heimdall.Application
{
    /// <summary>Formats physical input labels independently of simulator binding values.</summary>
    public static class AssignmentInputLabel
    {
        /// <summary>Combines a physical identifier with its caption without repeating a matching button number.</summary>
        public static string WithCaption(string identifier, string? caption)
        {
            var label = caption?.Trim();
            if (label is not null && identifier.StartsWith("Button ", StringComparison.Ordinal))
                label = WithoutRepeatedButton(identifier, label);

            return string.IsNullOrWhiteSpace(label) || string.Equals(identifier, label, StringComparison.OrdinalIgnoreCase)
                ? identifier : identifier + " — " + label;
        }

        /// <summary>Formats a numbered physical button or retains the exact key for other input kinds.</summary>
        public static string ForPhysicalKey(string key, string? caption)
        {
            const string prefix = "JOY_BTN";
            var identifier = key.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(key.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                && number > 0 ? "Button " + number.ToString(CultureInfo.InvariantCulture) : key;
            return WithCaption(identifier, caption);
        }

        private static string WithoutRepeatedButton(string identifier, string label)
        {
            var numbered = CaptionNumber(label.AsSpan());
            var digits = 0;
            while (digits < numbered.Length && char.IsAsciiDigit(numbered[digits]))
                digits++;

            if (!int.TryParse(numbered[..digits], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                || !identifier.AsSpan(7).SequenceEqual(number.ToString(CultureInfo.InvariantCulture)))
                return label;

            var remaining = numbered[digits..].TrimStart();
            if (remaining.IsEmpty) return "";
            if ("-–—|:·".IndexOf(remaining[0]) < 0) return label;
            return remaining[1..].TrimStart().ToString();
        }

        private static ReadOnlySpan<char> CaptionNumber(ReadOnlySpan<char> label)
        {
            if (label.StartsWith("Button", StringComparison.OrdinalIgnoreCase))
            {
                var remaining = label[6..];
                return remaining.Length > 0 && char.IsWhiteSpace(remaining[0])
                    ? remaining.TrimStart() : ReadOnlySpan<char>.Empty;
            }

            return label.StartsWith("B", StringComparison.OrdinalIgnoreCase)
                ? label[1..] : ReadOnlySpan<char>.Empty;
        }
    }
}
