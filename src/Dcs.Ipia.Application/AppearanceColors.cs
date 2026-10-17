using System.Globalization;

namespace Dcs.Ipia.Application
{
    /// <summary>Platform-independent RGB parsing and contrast calculations for appearance validation.</summary>
    public static class AppearanceColors
    {
        #region BEGIN Public
        /// <summary>Parses an opaque #RRGGBB color without markup or named-color interpretation.</summary>
        public static uint Parse(string value)
        {
            if (value is not { Length: 7 } || value[0] != '#' || value.Skip(1).Any(c => !Uri.IsHexDigit(c)) ||
                !uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
                throw new ArgumentException("Use an opaque RGB color such as #2F80ED.", nameof(value));
            return rgb;
        }
        /// <summary>Returns the relative-luminance contrast ratio of two opaque RGB colors.</summary>
        public static double Contrast(uint first, uint second)
        {
            var a = Luminance(first); var b = Luminance(second);
            return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
        }
        /// <summary>Composites an opaque foreground at the supplied opacity over a background.</summary>
        public static uint Blend(uint foreground, uint background, double opacity)
        {
            uint Channel(int shift) => (uint)Math.Round(((foreground >> shift) & 255) * opacity +
                ((background >> shift) & 255) * (1 - opacity));
            return (Channel(16) << 16) | (Channel(8) << 8) | Channel(0);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static double Luminance(uint color)
        {
            static double Linear(uint channel)
            {
                var value = channel / 255.0;
                return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Linear((color >> 16) & 255) + 0.7152 * Linear((color >> 8) & 255) + 0.0722 * Linear(color & 255);
        }
        #endregion END private helpers
    }
}
