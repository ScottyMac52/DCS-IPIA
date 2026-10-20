using Dcs.Ipia.Application;
using Dcs.Ipia.Domain.MultiSim;
using Dcs.Ipia.EliteDangerous;
using Xunit;

namespace Dcs.Ipia.Tests
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
        public void Artwork_labels_preserve_slots_holds_and_modifiers()
        {
            var backend = new EliteDangerousProfileBackend();
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "EliteDangerous", "Synthetic.4.2.binds");
            var source = File.ReadAllText(path);
            var profile = backend.Import("controls", new Dictionary<string, string> { ["Synthetic.4.2.binds"] = source });
            var rows = EliteWorkspaceProjection.Rows(profile, backend.GetCatalog(profile));
            Assert.Contains(rows, x => x.Mode == BindingMode.Hold); Assert.Contains(rows, x => x.IsAxis); Assert.Contains(rows, x => x.Chord == "Key_LeftShift");
            var diagram = new UiLayerDiagram("Stick", null, "stick.svg", 300, 200, [], [])
            { Controls = [new("trigger", "Trigger", "JOY_BTN1", 10, 10, 100, 40), new("aux", "Aux", "JOY_BTN2", 10, 60, 100, 40)] };
            var page = EliteWorkspaceProjection.Diagram("My Controls", diagram, rows, "All");
            Assert.Contains("[Hold]", Assert.Single(page.Labels["trigger"]).Text);
            Assert.Contains("[Key_LeftShift]", Assert.Single(page.Labels["aux"]).Text);
            Assert.Equal("Elite Dangerous · All", page.Kicker);
            Assert.All(EliteWorkspaceProjection.Diagram("My Controls", diagram, rows, "SRV").Labels.Values, Assert.Empty);
            Assert.Equal(source, backend.Export(profile).Files["Synthetic.4.2.binds"]);
        }
        #endregion END Public
    }
}
