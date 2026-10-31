using Heimdall.Application;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>Read-only native system assignments on shared keyboard, mouse and gamepad artwork.</summary>
    public static class SquadronsSystemInputDrawings
    {
        private static readonly IReadOnlyDictionary<string, string> KeyboardInputs = SystemInputDrawings.Layout("Keyboard")
            .GroupBy(control => Normalize(control.Label), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().InputKey, StringComparer.Ordinal);
        #region BEGIN Public
        /// <summary>Creates built-in artwork without hardware pins; unknown encodings remain exact-name extra controls.</summary>
        public static UiLayerDiagram? Create(string deviceId, IEnumerable<SquadronsWorkspaceRow> rows)
        {
            var token = Token(deviceId);
            if (token is null) return null;
            var assigned = rows.Where(row => row.DeviceId == deviceId).ToArray();
            var inputs = assigned.Select(row => InputKey(row, token)).ToArray();
            return SystemInputDrawings.Create(token, inputs, []);
        }
        /// <summary>Builds labels for the selected game context without changing any native record.</summary>
        public static KneeboardComposite Diagram(string title, UiLayerDiagram diagram,
            IEnumerable<SquadronsWorkspaceRow> rows, string deviceId, string section)
        {
            var labels = diagram.Controls.ToDictionary(control => control.Control, control =>
                (IReadOnlyList<KneeboardLabelVariant>)ForControl(control, rows, deviceId, section)
                    .Select(row => new KneeboardLabelVariant(Caption(row), KneeboardStateColors.Base, AssignmentLayer.Module, ""))
                    .Distinct().ToArray(), StringComparer.Ordinal);
            return new KneeboardComposite(title, "Star Wars: Squadrons · " + section + " · read-only", labels,
                new Dictionary<string, string>(StringComparer.Ordinal), []);
        }
        /// <summary>Resolves a clicked control to captured system records only.</summary>
        public static IReadOnlyList<SquadronsWorkspaceRow> ForControl(HardwareGraphicControl control,
            IEnumerable<SquadronsWorkspaceRow> rows, string deviceId, string section)
        {
            var token = Token(deviceId);
            if (token is null) return [];
            return rows.Where(row => row.DeviceId == deviceId && (section == "All" || row.Section == section)
                && InputKey(row, token) == control.InputKey).ToArray();
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string? Token(string deviceId) => deviceId switch
        {
            "system.0" => "Mouse", "system.1" => "Gamepad", "system.2" => "Keyboard", _ => null
        };
        private static string Caption(SquadronsWorkspaceRow row)
        {
            var modifier = row.Native.Fields["modifier"];
            var alternate = row.Native.Fields["altbutton"];
            return row.CommandName + (modifier is not (0 or 255) ? " · modifier " + modifier : "")
                + (alternate != 0 ? " · altbutton " + alternate : "");
        }
        private static string InputKey(SquadronsWorkspaceRow row, string token)
        {
            var name = SquadronsSystemInputDisplay.Describe(row);
            name = name[(name.IndexOf(" · ", StringComparison.Ordinal) + 3)..];
            if (token == "Gamepad") return name;
            if (token == "Mouse") return name switch
            {
                "Wheel up" => "Pos_Mouse_ZAxis", "Wheel down" => "Neg_Mouse_ZAxis", _ => name
            };
            if (name == "Numpad Enter") return "Key_Numpad_Enter";
            if (name == "\\") return "Key_backslash";
            return KeyboardInputs.GetValueOrDefault(Normalize(name)) ?? name;
        }
        private static string Normalize(string value) => value.Replace("Numpad ", "Num", StringComparison.Ordinal)
            .Replace("Left ", "L", StringComparison.Ordinal).Replace("Right ", "R", StringComparison.Ordinal)
            .Replace("Backspace", "Back", StringComparison.Ordinal).Replace("Caps Lock", "Caps", StringComparison.Ordinal)
            .Replace("Page Up", "PgUp", StringComparison.Ordinal).Replace("Page Down", "PgDn", StringComparison.Ordinal);
        #endregion END private helpers
    }
}
