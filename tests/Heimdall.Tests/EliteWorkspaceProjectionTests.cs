using Heimdall.Application;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;
using Xunit;
using System.Xml.Linq;

namespace Heimdall.Tests
{
    /// <summary>Tests the native/shared-control bridge and display-only game sections.</summary>
    public sealed class EliteWorkspaceProjectionTests
    {
        #region BEGIN Public
        /// <summary>Known physical inputs map explicitly while unsupported spellings remain unavailable.</summary>
        [Theory]
        [InlineData("JOY_BTN1", "Joy_1")]
        [InlineData("JOY_BTN24", "Joy_24")]
        [InlineData("JOY_X", "Joy_XAxis")]
        [InlineData("JOY_RZ", "Joy_RZAxis")]
        [InlineData("JOY_POV1_U", "Joy_POV1Up")]
        [InlineData("JOY_BTN_POV2_DR", "Joy_POV2DownRight")]
        [InlineData("JOY_BTN0", null)]
        [InlineData("JOY_BTNwat", null)]
        [InlineData("JOY_BTN_POV0_U", null)]
        [InlineData("JOY_BTN_POV1_X", null)]
        [InlineData("JOY_BTN_POV1", null)]
        [InlineData("JOY_SLIDER1", null)]
        [InlineData("future", null)]
        public void Physical_key_translation_is_explicit(string shared, string? native) => Assert.Equal(native, EliteInputKeys.FromShared(shared));
        /// <summary>Section hints never change source contexts and unknown commands remain available in General/All.</summary>
        [Theory]
        [InlineData("PrimaryFire", "Ship")]
        [InlineData("PitchAxisRaw", "Ship")]
        [InlineData("HumanoidPrimaryFireButton", "On Foot")]
        [InlineData("QuickCommsPanel_Humanoid", "On Foot")]
        [InlineData("BuggyPitchAxis", "SRV")]
        [InlineData("DriveSpeedAxis", "SRV")]
        [InlineData("SteeringAxis", "SRV")]
        [InlineData("VerticalThrustersButton", "SRV")]
        [InlineData("FutureCommand", "General")]
        public void Sections_are_conservative_display_filters(string command, string section) => Assert.Equal(section, EliteCommandSections.ForCommand(command));
        /// <summary>One projection supplies both interactive artwork and reference-sheet labels without touching native state.</summary>
        [Fact]
        public void Artwork_labels_show_commands_while_rows_preserve_native_combinations()
        {
            var backend = new EliteDangerousProfileBackend();
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "EliteDangerous", "Synthetic.4.2.binds");
            var source = File.ReadAllText(path);
            var profile = backend.Import("controls", new Dictionary<string, string> { ["Synthetic.4.2.binds"] = source });
            var rows = EliteWorkspaceProjection.Rows(profile, backend.GetCatalog(profile));
            Assert.Contains(rows, x => x.Mode == BindingMode.Hold); Assert.Contains(rows, x => x.IsAxis); Assert.Contains(rows, x => x.Chord == "Key_LeftShift");
            var diagram = new UiLayerDiagram("Stick", null, "stick.svg", 300, 200, [], [])
            { Svg = "<svg xmlns='http://www.w3.org/2000/svg'><text id='meta-title'>Shared title</text></svg>", Controls = [new("trigger", "Trigger", "JOY_BTN1", 10, 10, 100, 40), new("aux", "Aux", "JOY_BTN2", 10, 60, 100, 40)] };
            var page = EliteWorkspaceProjection.Diagram("My Controls", diagram, rows, "All");
            Assert.Contains("[Hold]", Assert.Single(page.Labels["trigger"]).Text);
            var label = Assert.Single(page.Labels["aux"]);
            Assert.DoesNotContain("Key_LeftShift", label.Text);
            Assert.Empty(label.ModifierChord);
            Assert.Contains(rows, x => x.Chord == "Key_LeftShift" && x.Modifiers.Count > 0);
            Assert.Equal("Elite Dangerous · All", page.Kicker);
            Assert.Equal("Shared title", page.CategoryLabels["title"]);
            Assert.All(EliteWorkspaceProjection.Diagram("My Controls", diagram, rows, "SRV").Labels.Values, Assert.Empty);
            Assert.Equal(source, backend.Export(profile).Files["Synthetic.4.2.binds"]);
        }
        /// <summary>Physical controller callouts show every assigned command on buttons, hats and axes using the shared renderer.</summary>
        [Theory]
        [InlineData("Stick", "JOY_Y", "Joy_YAxis")]
        [InlineData("Throttle", "JOY_Z", "Joy_ZAxis")]
        [InlineData("Rudders", "JOY_RZ", "Joy_RZAxis")]
        [InlineData("MFD", "JOY_X", "Joy_XAxis")]
        public void Physical_controller_callouts_show_commands_for_each_input(string device, string sharedAxis, string nativeAxis)
        {
            var backend = new EliteDangerousProfileBackend();
            var source = $$"""
            <Root PresetName="Physical" MajorVersion="4" MinorVersion="2">
              <PrimaryFire><Primary Device="{{device}}" Key="Joy_1"><Hold Value="1"/></Primary><Secondary Device="OtherController" Key="Joy_1"/></PrimaryFire>
              <LandingGearToggle><Primary Device="{NoDevice}" Key=""/><Secondary Device="{{device}}" Key="Joy_1"/></LandingGearToggle>
              <UI_Up><Primary Device="{{device}}" Key="Joy_POV1Up"/><Secondary Device="{NoDevice}" Key=""/></UI_Up>
              <PitchAxisRaw><Binding Device="{{device}}" Key="{{nativeAxis}}"/><Inverted Value="0"/><Deadzone Value="0"/></PitchAxisRaw>
              <BuggyPrimaryFireButton><Primary Device="{{device}}" Key="Joy_1"/><Secondary Device="{NoDevice}" Key=""/></BuggyPrimaryFireButton>
            </Root>
            """;
            var profile = backend.Import("physical", new Dictionary<string, string> { ["Physical.4.2.binds"] = source });
            var selected = profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == device);
            var rows = EliteWorkspaceProjection.Rows(profile, backend.GetCatalog(profile)).Where(x => x.DeviceId == selected.LogicalId).ToArray();
            var diagram = new UiLayerDiagram(device, null, "physical.svg", 400, 300, [], [])
            {
                Controls = [new("button", "Button", "JOY_BTN1", 0, 0, 400, 70), new("hat", "Hat", "JOY_POV1_U", 0, 75, 400, 70),
                    new("axis", "Axis", sharedAxis, 0, 150, 400, 70), new("empty", "Unassigned", "JOY_BTN24", 0, 225, 400, 70)],
                Svg = "<svg xmlns='http://www.w3.org/2000/svg' width='400' height='300'><text id='lbl-button'/><text id='lbl-hat'/><text id='lbl-axis'/><text id='lbl-empty'/></svg>"
            };
            var page = EliteWorkspaceProjection.Diagram("Physical", diagram, rows, "All");
            Assert.Equal(3, page.Labels["button"].Count);
            Assert.Contains(page.Labels["button"], x => x.Text.EndsWith("[Hold]", StringComparison.Ordinal));
            Assert.Contains(page.Labels["button"], x => x.Text.EndsWith("[S]", StringComparison.Ordinal));
            Assert.Single(page.Labels["hat"]); Assert.Single(page.Labels["axis"]); Assert.Empty(page.Labels["empty"]);
            var rendered = System.Xml.Linq.XDocument.Parse(Heimdall.Rendering.CompositeKneeboardRenderer.BuildDeviceSvg(diagram, page));
            var fullLabels = rendered.Descendants().Attributes("data-full-label").Select(x => x.Value).ToArray();
            Assert.All(rows, row => Assert.Contains(fullLabels, text => text.Contains(row.CommandName, StringComparison.Ordinal)));
            var ship = EliteWorkspaceProjection.Diagram("Physical", diagram, rows, "Ship");
            Assert.DoesNotContain(ship.Labels["button"], x => x.Text.Contains("Buggy", StringComparison.Ordinal));
            Assert.Equal(source, backend.Export(profile).Files["Physical.4.2.binds"]);
        }
        #endregion END Public
    }
}
