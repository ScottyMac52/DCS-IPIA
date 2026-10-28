using Heimdall.Squadrons;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects native system-input labels, direction signs, sentinels and numeric fallbacks.</summary>
    public sealed class SquadronsSystemInputDisplayTests
    {
        #region BEGIN Public
        /// <summary>Opposite stick directions retain distinct native labels, including the right-stick Y convention.</summary>
        [Theory]
        [InlineData(2, "Gamepad · Left stick X (+)")]
        [InlineData(4, "Gamepad · Left stick X (-)")]
        [InlineData(3, "Gamepad · Left stick Y (+)")]
        [InlineData(5, "Gamepad · Left stick Y (-)")]
        [InlineData(8, "Gamepad · Right stick X (+)")]
        [InlineData(10, "Gamepad · Right stick X (-)")]
        [InlineData(9, "Gamepad · Right stick Y (-)")]
        [InlineData(11, "Gamepad · Right stick Y (+)")]
        public void Gamepad_stick_directions_are_not_interchanged(int axis, string expected) =>
            AssertLabel(1, axis, 174, expected);

        /// <summary>Captured gamepad buttons are readable without assigning labels to unknown button codes.</summary>
        [Theory]
        [InlineData(0, "Gamepad · D-pad Up")]
        [InlineData(1, "Gamepad · D-pad Down")]
        [InlineData(2, "Gamepad · D-pad Left")]
        [InlineData(3, "Gamepad · D-pad Right")]
        [InlineData(4, "Gamepad · Y")]
        [InlineData(5, "Gamepad · A")]
        [InlineData(6, "Gamepad · X")]
        [InlineData(7, "Gamepad · B")]
        [InlineData(10, "Gamepad · Left stick press")]
        [InlineData(11, "Gamepad · Right stick press")]
        [InlineData(12, "Gamepad · Menu / Start")]
        [InlineData(13, "Gamepad · View / Back")]
        [InlineData(14, "Gamepad · Left trigger")]
        [InlineData(15, "Gamepad · Right trigger")]
        [InlineData(16, "Gamepad · Left bumper")]
        [InlineData(17, "Gamepad · Right bumper")]
        [InlineData(99, "Gamepad · button code 99")]
        public void Gamepad_buttons_use_the_captured_button_encoding(int button, string expected) =>
            AssertLabel(1, 26, button, expected);

        /// <summary>Mouse movement, wheel and unassigned-button encodings stay distinct.</summary>
        [Theory]
        [InlineData(2, 8, "Mouse · X axis (+)")]
        [InlineData(4, 8, "Mouse · X axis (-)")]
        [InlineData(3, 8, "Mouse · Y axis (+)")]
        [InlineData(5, 8, "Mouse · Y axis (-)")]
        [InlineData(14, 8, "Mouse · Wheel up")]
        [InlineData(15, 8, "Mouse · Wheel down")]
        [InlineData(26, 8, "Mouse · unassigned (button code 8)")]
        [InlineData(26, 0, "Mouse · Button 0")]
        [InlineData(99, 8, "Mouse · axis code 99")]
        public void Mouse_labels_preserve_direction_and_unassigned_sentinel(int axis, int button, string expected) =>
            AssertLabel(0, axis, button, expected);

        /// <summary>Unsupported inputs preserve numeric evidence instead of inventing an editable control.</summary>
        [Theory]
        [InlineData(1, 99, 5, "Gamepad · axis code 99")]
        [InlineData(2, 26, 0, "Keyboard · unassigned (code 0)")]
        [InlineData(2, 26, 250, "Keyboard · scan code 250")]
        [InlineData(3, 99, 174, "Joystick · unknown input (axis 99, button 174)")]
        [InlineData(99, 7, 42, "Native type 99 · axis 7, button 42")]
        public void Unknown_and_unassigned_records_remain_identifiable(int type, int axis, int button, string expected) =>
            AssertLabel(type, axis, button, expected);
        #endregion END Public

        #region BEGIN private helpers
        private static void AssertLabel(int type, int axis, int button, string expected)
        {
            var fields = new Dictionary<string, int>
            {
                ["type"] = type, ["axis"] = axis, ["button"] = button,
                ["deviceid"] = -1, ["identifier"] = 123, ["modifier"] = 0,
                ["altbutton"] = 0, ["negate"] = 0
            };
            var original = fields.ToDictionary(x => x.Key, x => x.Value);
            var row = new SquadronsBindingRow("IncomDefaultInputConcepts.ConceptMenu", 0, fields);
            Assert.Equal(expected, SquadronsSystemInputDisplay.Describe(row));
            Assert.Equal(original.Count, row.Fields.Count);
            Assert.All(original, pair => Assert.Equal(pair.Value, row.Fields[pair.Key]));
        }
        #endregion END private helpers
    }
}
