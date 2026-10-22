using System.Globalization;

namespace Heimdall.EliteDangerous
{
    /// <summary>A native device input with its original hardware caption, independent of assigned commands.</summary>
    /// <param name="Key">Exact native ED key stored in an assignment or combination.</param>
    /// <param name="DefaultLabel">Authored hardware caption, or the native key when no caption is available.</param>
    public sealed record EliteInputChoice(string Key, string DefaultLabel)
    {
        #region BEGIN Public
        /// <summary>Gets the positive native button number, leaving keyboard, mouse and hat names intact.</summary>
        public int? ButtonNumber => Key.StartsWith("Joy_", StringComparison.Ordinal)
            && int.TryParse(Key[4..], NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0 ? number : null;
        /// <summary>Gets the default label, explicit button number when available and exact native key.</summary>
        public string DisplayName => (DefaultLabel == Key ? "" : DefaultLabel + " · ")
            + (ButtonNumber is { } number ? "Button " + number.ToString(CultureInfo.InvariantCulture) + " · " : "") + Key;
        #endregion END Public
    }
}
