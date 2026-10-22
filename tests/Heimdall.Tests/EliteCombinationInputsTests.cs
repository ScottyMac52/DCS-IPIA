using System.Text.Json;
using Heimdall.Application;
using Heimdall.EliteDangerous;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks device-scoped combination choices, original captions and exact native keys without hardware access.</summary>
    public sealed class EliteCombinationInputsTests
    {
        private static readonly string[] PhysicalKeys = ["Joy_1", "Joy_2", "Joy_3", "Joy_10", "Joy_24", "Joy_POV1Left"];
        private static readonly string[] CapturedKeys = ["Joy_1", "Joy_24"];
        #region BEGIN Public
        /// <summary>Authored buttons and hats include unassigned controls, while captured cross-device modifiers remain available.</summary>
        [Fact]
        public void Physical_choices_use_default_labels_and_native_button_numbers_without_axes_or_other_devices()
        {
            var profile = Source(); var frozen = JsonSerializer.Serialize(profile);
            var stick = profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "Stick");
            var diagram = new UiLayerDiagram("Stick", null, "stick.svg", 400, 300, [], [])
            {
                Controls = [new("ten", "Hat up", "JOY_BTN10", 0, 0, 100, 50), new("two", "Trigger", "JOY_BTN2", 0, 60, 100, 50),
                    new("duplicate", "Alternate caption", "JOY_BTN2", 0, 120, 100, 50), new("three", "", "JOY_BTN3", 0, 180, 100, 50),
                    new("hat", "Trim left", "JOY_POV1_L", 100, 0, 100, 50), new("axis", "Pitch", "JOY_Y", 100, 60, 100, 50, true),
                    new("unknown", "Unknown", "JOY_SLIDER1", 100, 120, 100, 50)]
            };
            var choices = EliteCombinationInputs.ForDevice(profile, stick.LogicalId, diagram);
            Assert.Equal(PhysicalKeys, choices.Select(x => x.Key));
            Assert.Equal("Trigger · Button 2 · Joy_2", choices.Single(x => x.Key == "Joy_2").DisplayName);
            Assert.Equal("Hat up", choices.Single(x => x.Key == "Joy_10").DefaultLabel);
            Assert.Equal("Button 3 · Joy_3", choices.Single(x => x.Key == "Joy_3").DisplayName);
            Assert.Equal("Trim left · Joy_POV1Left", choices.Single(x => x.Key == "Joy_POV1Left").DisplayName);
            Assert.Equal(frozen, JsonSerializer.Serialize(profile));
        }
        /// <summary>Unknown hardware offers captured keys only; no guessed button counts or captions are created.</summary>
        [Fact]
        public void Devices_without_drawings_preserve_captured_keys_and_reject_missing_references()
        {
            var profile = Source(); var stick = profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "Stick");
            stick.Buttons[0].Modifiers.Add(new() { Source = stick.LogicalId, Value = null });
            stick.Buttons[0].Modifiers.Add(new() { Source = stick.LogicalId, Value = " " });
            var choices = EliteCombinationInputs.ForDevice(profile, stick.LogicalId);
            Assert.Equal(CapturedKeys, choices.Select(x => x.Key));
            Assert.All(choices, x => Assert.Equal(x.Key, x.DefaultLabel));
            Assert.Throws<InvalidOperationException>(() => EliteCombinationInputs.ForDevice(profile, "missing"));
        }
        /// <summary>System device choices use exact keyboard and mouse geometry, retaining unassigned keys and excluding axes.</summary>
        [Theory]
        [InlineData("Keyboard", "Key_LeftShift")]
        [InlineData("Mouse", "Mouse_2")]
        public void System_choices_are_native_and_device_scoped(string token, string unassigned)
        {
            var profile = Source(); var device = profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == token);
            var choices = EliteCombinationInputs.ForDevice(profile, device.LogicalId);
            Assert.Contains(choices, x => x.Key == unassigned);
            Assert.DoesNotContain(choices, x => x.Key is "Mouse_XAxis" or "Joy_24");
            if (token == "Keyboard") Assert.All(choices, x => Assert.StartsWith("Key_", x.Key));
            else
            {
                Assert.Contains(choices, x => x.Key == "Pos_Mouse_ZAxis");
                Assert.Contains(choices, x => x.Key == "Neg_Mouse_ZAxis");
                Assert.DoesNotContain(choices, x => x.Key.StartsWith("Key_", StringComparison.Ordinal));
            }
        }
        /// <summary>Display formatting never substitutes a descriptive caption for the native key or invents numbers for hats and keys.</summary>
        [Theory]
        [InlineData("Joy_7", "B07", 7, "B07 · Button 7 · Joy_7")]
        [InlineData("Joy_24", "Joy_24", 24, "Button 24 · Joy_24")]
        [InlineData("Joy_POV1Up", "Hat up", null, "Hat up · Joy_POV1Up")]
        [InlineData("Key_LeftShift", "LShift", null, "LShift · Key_LeftShift")]
        [InlineData("Mouse_1", "Left button", null, "Left button · Mouse_1")]
        [InlineData("Joy_0", "Joy_0", null, "Joy_0")]
        [InlineData("Joy_future", "Joy_future", null, "Joy_future")]
        public void Choice_format_preserves_identity(string key, string caption, int? number, string display)
        {
            var choice = new EliteCombinationInput(key, caption);
            Assert.Equal(key, choice.Key); Assert.Equal(number, choice.ButtonNumber); Assert.Equal(display, choice.DisplayName);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static Heimdall.Domain.MultiSim.ProfileDocument Source() => new EliteDangerousProfileBackend().Import("controls", new Dictionary<string, string>
        {
            ["Combinations.4.2.binds"] = """
            <Root PresetName="Combinations" MajorVersion="4" MinorVersion="2">
              <PrimaryFire><Primary Device="Stick" Key="Joy_1"><Modifier Device="Keyboard" Key="Key_LeftShift"/></Primary><Secondary Device="Other" Key="Joy_8"><Modifier Device="Stick" Key="Joy_24"/></Secondary></PrimaryFire>
              <PitchAxisRaw><Binding Device="Stick" Key="Joy_YAxis"/><Inverted Value="0"/><Deadzone Value="0"/></PitchAxisRaw>
              <SecondaryFire><Primary Device="Mouse" Key="Mouse_1"/><Secondary Device="{NoDevice}" Key=""/></SecondaryFire>
              <YawAxisRaw><Binding Device="Mouse" Key="Mouse_XAxis"/><Inverted Value="0"/><Deadzone Value="0"/></YawAxisRaw>
            </Root>
            """
        });
        #endregion END private helpers
    }
}
