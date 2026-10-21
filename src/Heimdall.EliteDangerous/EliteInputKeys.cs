using System.Globalization;

namespace Heimdall.EliteDangerous
{
    /// <summary>Translates known shared physical-control keys; unknown keys remain unassignable rather than guessed.</summary>
    public static class EliteInputKeys
    {
        private static readonly Dictionary<string, string> Axes = new(StringComparer.Ordinal)
        {
            ["JOY_X"] = "Joy_XAxis", ["JOY_Y"] = "Joy_YAxis", ["JOY_Z"] = "Joy_ZAxis",
            ["JOY_RX"] = "Joy_RXAxis", ["JOY_RY"] = "Joy_RYAxis", ["JOY_RZ"] = "Joy_RZAxis"
        };
        private static readonly Dictionary<string, string> Directions = new(StringComparer.Ordinal)
        {
            ["U"] = "Up", ["UR"] = "UpRight", ["R"] = "Right", ["DR"] = "DownRight",
            ["D"] = "Down", ["DL"] = "DownLeft", ["L"] = "Left", ["UL"] = "UpLeft"
        };
        #region BEGIN Public
        /// <summary>Uses native keys only for built-in system geometry; other drawings retain explicit shared-key translation.</summary>
        public static string? FromDiagram(Heimdall.Application.UiLayerDiagram diagram, Heimdall.Application.HardwareGraphicControl control) =>
            diagram.Path.StartsWith("elite-system/", StringComparison.Ordinal) ? control.InputKey : FromShared(control.InputKey);
        /// <summary>Gets a known native key for a shared drawing control, or null when not supported.</summary>
        public static string? FromShared(string key)
        {
            if (Axes.TryGetValue(key, out var axis)) return axis;
            var normalized = key.StartsWith("JOY_POV", StringComparison.Ordinal) ? "JOY_BTN_" + key[4..] : key;
            if (normalized.StartsWith("JOY_BTN_POV", StringComparison.Ordinal))
            {
                var parts = normalized[11..].Split('_');
                if (parts.Length == 2 && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hat)
                    && hat > 0 && Directions.TryGetValue(parts[1], out var direction)) return "Joy_POV" + hat.ToString(CultureInfo.InvariantCulture) + direction;
                return null;
            }
            if (normalized.StartsWith("JOY_BTN", StringComparison.Ordinal)
                && int.TryParse(normalized[7..], NumberStyles.None, CultureInfo.InvariantCulture, out var button) && button > 0)
                return "Joy_" + button.ToString(CultureInfo.InvariantCulture);
            return null;
        }
        #endregion END Public
    }
}
