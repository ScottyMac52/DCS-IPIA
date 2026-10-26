namespace Heimdall.Squadrons
{
    /// <summary>Read-only labels for captured Squadrons input codes.
    /// Keeps uncertain encodings numeric; never converts display labels into editable inputs.</summary>
    public static class SquadronsSystemInputDisplay
    {
        // Squadrons v4 keyboard values follow DirectInput keyboard scan codes (DIK).
        // These values are independent of keyboard layout and do not represent typed characters.
        private static readonly Dictionary<int, string> Keys = new()
        {
            [1] = "Esc", [2] = "1", [3] = "2", [4] = "3", [5] = "4",
            [6] = "5", [7] = "6", [8] = "7", [9] = "8", [10] = "9", [11] = "0",
            [12] = "-", [13] = "=", [14] = "Backspace", [15] = "Tab",
            [16] = "Q", [17] = "W", [18] = "E", [19] = "R", [20] = "T",
            [21] = "Y", [22] = "U", [23] = "I", [24] = "O", [25] = "P",
            [26] = "[", [27] = "]", [28] = "Enter", [29] = "Left Ctrl",
            [30] = "A", [31] = "S", [32] = "D", [33] = "F", [34] = "G",
            [35] = "H", [36] = "J", [37] = "K", [38] = "L", [39] = ";",
            [40] = "'", [41] = "`", [42] = "Left Shift", [43] = "\\",
            [44] = "Z", [45] = "X", [46] = "C", [47] = "V", [48] = "B",
            [49] = "N", [50] = "M", [51] = ",", [52] = ".", [53] = "/",
            [54] = "Right Shift", [55] = "Numpad *", [56] = "Left Alt",
            [57] = "Space", [58] = "Caps Lock", [59] = "F1", [60] = "F2",
            [61] = "F3", [62] = "F4", [63] = "F5", [64] = "F6",
            [65] = "F7", [66] = "F8", [67] = "F9", [68] = "F10",
            [69] = "Num Lock", [70] = "Scroll Lock",
            [71] = "Numpad 7", [72] = "Numpad 8", [73] = "Numpad 9",
            [74] = "Numpad -", [75] = "Numpad 4", [76] = "Numpad 5",
            [77] = "Numpad 6", [78] = "Numpad +", [79] = "Numpad 1",
            [80] = "Numpad 2", [81] = "Numpad 3", [82] = "Numpad 0",
            [83] = "Numpad .", [87] = "F11", [88] = "F12",
            [156] = "Numpad Enter", [157] = "Right Ctrl", [181] = "Numpad /",
            [184] = "Right Alt", [199] = "Home", [200] = "Up",
            [201] = "Page Up", [203] = "Left", [205] = "Right",
            [207] = "End", [208] = "Down", [209] = "Page Down",
            [210] = "Insert", [211] = "Delete", [219] = "Left Windows",
            [220] = "Right Windows", [221] = "Menu"
        };

        /// <summary>Formats one native system record; unrecognized codes retain their original values.</summary>
        public static string Describe(SquadronsBindingRow row)
        {
            ArgumentNullException.ThrowIfNull(row);
            var axis = row.Fields["axis"];
            var button = row.Fields["button"];
            return row.Type switch
            {
                0 => Mouse(axis, button),
                1 => Gamepad(axis, button),
                2 => button == 0 ? "Keyboard · unassigned (code 0)" :
                    Keys.TryGetValue(button, out var key) ? "Keyboard · " + key : "Keyboard · scan code " + button,
                3 => "Joystick · unknown input (axis " + axis + ", button " + button + ")",
                _ => "Native type " + row.Type + " · axis " + axis + ", button " + button
            };
        }

        private static string Mouse(int axis, int button)
        {
            var direction = axis switch
            {
                2 => "X axis (+)", 4 => "X axis (-)",
                3 => "Y axis (+)", 5 => "Y axis (-)",
                14 => "Wheel up", 15 => "Wheel down",
                26 => null,
                _ => "axis code " + axis
            };
            if (direction is not null) return "Mouse · " + direction;
            // Squadrons uses 8 as its unassigned mouse-button sentinel.
            return button == 8 ? "Mouse · unassigned (button code 8)"
                : "Mouse · Button " + button;
        }

        private static string Gamepad(int axis, int button)
        {
            var direction = axis switch
            {
                2 => "Left stick X (+)", 4 => "Left stick X (-)",
                3 => "Left stick Y (+)", 5 => "Left stick Y (-)",
                8 => "Right stick X (+)", 10 => "Right stick X (-)",
                9 => "Right stick Y (-)", 11 => "Right stick Y (+)",
                26 => null,
                _ => "axis code " + axis
            };
            if (direction is not null) return "Gamepad · " + direction;
            var control = button switch
            {
                0 => "D-pad Up", 1 => "D-pad Down",
                2 => "D-pad Left", 3 => "D-pad Right",
                4 => "Y", 5 => "A", 6 => "X", 7 => "B",
                10 => "Left stick press", 11 => "Right stick press",
                12 => "Menu / Start", 13 => "View / Back",
                14 => "Left trigger", 15 => "Right trigger",
                16 => "Left bumper", 17 => "Right bumper",
                _ => "button code " + button
            };
            return "Gamepad · " + control;
        }
    }
}
