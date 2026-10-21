using System.Globalization;
using System.Xml.Linq;
using Heimdall.Application;
using Heimdall.Domain.MultiSim;

namespace Heimdall.EliteDangerous
{
    /// <summary>Creates built-in keyboard/mouse geometry without physical hardware links or changes to native data.</summary>
    public static class EliteSystemInputDrawings
    {
        private static readonly XNamespace SvgNamespace = "http://www.w3.org/2000/svg";
        #region BEGIN Public
        /// <summary>Returns system-device artwork, retaining additional imported keys exactly; other devices return null.</summary>
        public static UiLayerDiagram? Create(ProfileDocument profile, string deviceId)
        {
            var device = profile.Devices.Single(x => x.LogicalId == deviceId);
            var token = device.Identity!.SimSpecificIds["ed.device"];
            if (token is not ("Keyboard" or "Mouse")) return null;
            var controls = new List<HardwareGraphicControl>();
            if (token == "Keyboard") Keyboard(controls); else Mouse(controls);
            var inputs = device.Buttons.Select(x => x.SourceButton).Concat(device.Axes.Select(x => x.SourceAxis))
                .Concat(profile.Devices.SelectMany(x => x.Buttons).SelectMany(x => x.Modifiers)
                    .Where(x => x.Source == deviceId).Select(x => x.Value));
            var extra = inputs.OfType<string>().Distinct(StringComparer.Ordinal).Where(key => !controls.Any(x => x.InputKey == key)).Order(StringComparer.Ordinal).ToArray();
            var baseHeight = token == "Keyboard" ? 600 : 510;
            for (var i = 0; i < extra.Length; i++)
                controls.Add(new("extra-" + i, extra[i], extra[i], 24 + i % 6 * 200, baseHeight + i / 6 * 80, 188, 70,
                    device.Axes.Any(x => x.SourceAxis == extra[i])));
            var height = baseHeight + (extra.Length + 5) / 6 * 80 + 30;
            var svg = new XElement(SvgNamespace + "svg", new XAttribute("viewBox", $"0 0 1240 {height}"),
                new XAttribute("width", 1240), new XAttribute("height", height),
                new XElement(SvgNamespace + "rect", new XAttribute("width", 1240), new XAttribute("height", height), new XAttribute("fill", "#06101d")));
            Text(svg, 24, 28, token == "Keyboard" ? "Keyboard · US QWERTY reference layout" : "Mouse · buttons and wheel", 20);
            Text(svg, 24, 52, "Numbered inputs match the reference table. Additional imported inputs retain their exact native names.", 14);
            if (token == "Mouse")
                svg.Add(new XElement(SvgNamespace + "rect", new XAttribute("x", 430), new XAttribute("y", 90), new XAttribute("width", 380),
                    new XAttribute("height", 390), new XAttribute("rx", 150), new XAttribute("fill", "#162c41"), new XAttribute("stroke", "#89a3ba")));
            foreach (var control in controls)
            {
                svg.Add(new XElement(SvgNamespace + "rect", new XAttribute("id", "box-" + control.Control),
                    new XAttribute("x", control.X), new XAttribute("y", control.Y), new XAttribute("width", control.Width), new XAttribute("height", control.Height),
                    new XAttribute("rx", 5), new XAttribute("fill", "#102236"), new XAttribute("stroke", "#89a3ba")));
                Text(svg, control.X + 5, control.Y + 19, control.Label, control.Label.Length > 18 ? 10 : 12);
                var marker = Text(svg, control.X + 6, control.Y + 48, "", 16);
                marker.SetAttributeValue("id", "lbl-" + control.Control);
            }
            return new(token, null, "elite-system/" + token + ".svg", 1240, height, [], [])
            { Svg = svg.ToString(SaveOptions.DisableFormatting), Controls = controls, SupportsUiLayer = false };
        }
        /// <summary>Numbers primary input keys consistently across artwork, sections and reference tables.</summary>
        public static IReadOnlyDictionary<string, int> Markers(IEnumerable<EliteAssignmentRow> rows) => rows.Select(x => x.Input)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select((key, index) => (key, number: index + 1))
            .ToDictionary(x => x.key, x => x.number, StringComparer.Ordinal);
        #endregion END Public
        #region BEGIN private helpers
        private static void Keyboard(List<HardwareGraphicControl> controls)
        {
            Row(controls, "Escape:Esc F1 F2 F3 F4 F5 F6 F7 F8 F9 F10 F11 F12", 24, 80);
            Row(controls, "Grave:` 1 2 3 4 5 6 7 8 9 0 Minus:- Equals:= Backspace:Back:2", 24, 160);
            Row(controls, "Tab:Tab:1.5 Q W E R T Y U I O P LeftBracket:[ RightBracket:] backslash:Backslash:1.5", 24, 240);
            Row(controls, "CapsLock:Caps:1.75 A S D F G H J K L SemiColon:; Apostrophe:' Enter:Enter:2.25", 24, 320);
            Row(controls, "LeftShift:LShift:2.25 Z X C V B N M Comma:, Period:. Slash:/ RightShift:RShift:2.75", 24, 400);
            Row(controls, "LeftControl:LCtrl:2 LeftAlt:LAlt:2 Space:Space:7 RightAlt:RAlt:2 RightControl:RCtrl:2", 24, 480);
            Row(controls, "Insert:Ins Home:Home PageUp:PgUp", 820, 160);
            Row(controls, "Delete:Del End:End PageDown:PgDn", 820, 240);
            Row(controls, "UpArrow:Up", 872, 400);
            Row(controls, "LeftArrow:Left DownArrow:Down RightArrow:Right", 820, 480);
            Row(controls, "Numpad_Divide:Num/ Numpad_Multiply:Num* Numpad_Subtract:Num-", 1010, 160);
            Row(controls, "Numpad_7:Num7 Numpad_8:Num8 Numpad_9:Num9 Numpad_Add:Num+", 1010, 240);
            Row(controls, "Numpad_4:Num4 Numpad_5:Num5 Numpad_6:Num6", 1010, 320);
            Row(controls, "Numpad_1:Num1 Numpad_2:Num2 Numpad_3:Num3 Numpad_Enter:Enter", 1010, 400);
            Row(controls, "Numpad_0:Num0:2 Numpad_Decimal:Num.", 1010, 480);
        }
        private static void Row(List<HardwareGraphicControl> controls, string specification, double x, double y)
        {
            foreach (var item in specification.Split(' '))
            {
                var parts = item.Split(':');
                var units = parts.Length > 2 ? double.Parse(parts[2], CultureInfo.InvariantCulture) : 1;
                var key = "Key_" + parts[0];
                controls.Add(new(key, parts.Length > 1 ? parts[1] : parts[0], key, x, y, units * 52 - 6, 70));
                x += units * 52;
            }
        }
        private static void Mouse(List<HardwareGraphicControl> controls)
        {
            controls.Add(new("Mouse_1", "Left button", "Mouse_1", 440, 100, 145, 150));
            controls.Add(new("Mouse_2", "Right button", "Mouse_2", 655, 100, 145, 150));
            controls.Add(new("Mouse_3", "Middle", "Mouse_3", 590, 150, 60, 100));
            controls.Add(new("wheel-up", "Wheel up", "Pos_Mouse_ZAxis", 540, 270, 160, 70));
            controls.Add(new("wheel-down", "Wheel down", "Neg_Mouse_ZAxis", 540, 350, 160, 70));
        }
        private static XElement Text(XElement svg, double x, double y, string text, int size)
        {
            var element = new XElement(SvgNamespace + "text", new XAttribute("x", x), new XAttribute("y", y),
                new XAttribute("font-family", "Arial,sans-serif"), new XAttribute("font-size", size), new XAttribute("fill", "#f5f9ff"), text);
            svg.Add(element); return element;
        }
        #endregion END private helpers
    }
}
