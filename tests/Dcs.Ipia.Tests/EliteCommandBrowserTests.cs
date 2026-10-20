using Dcs.Ipia.Domain.MultiSim;
using Dcs.Ipia.EliteDangerous;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Verifies command organization and intersecting palette filters over captured native catalogs.</summary>
    public sealed class EliteCommandBrowserTests
    {
        private static readonly string[] ShipCategories = ["Flight Rotation", "Weapons"];
        private static readonly string[] SortedLabels = ["Pitch Axis Raw", "Unknown", "Primary Fire", "Secondary Fire"];
        #region BEGIN Public
        /// <summary>Representative native commands have useful categories within the correct activity.</summary>
        [Theory]
        [InlineData("PitchAxisRaw", "Ship", "Flight Rotation")]
        [InlineData("YawLeftButton", "Ship", "Flight Rotation")]
        [InlineData("LateralThrustRaw", "Ship", "Flight Thrust")]
        [InlineData("SetSpeed50", "Ship", "Flight Throttle")]
        [InlineData("ToggleFlightAssist", "Ship", "Flight Assistance")]
        [InlineData("PitchAxis_Landing", "Ship", "Landing Overrides")]
        [InlineData("SelectHighestThreat", "Ship", "Targeting")]
        [InlineData("CycleNextSubsystem", "Ship", "Targeting")]
        [InlineData("PrimaryFire", "Ship", "Weapons")]
        [InlineData("DeployHardpointToggle", "Ship", "Weapons")]
        [InlineData("IncreaseEnginesPower", "Ship", "Power Management")]
        [InlineData("HyperSuperCombination", "Ship", "Navigation & Travel")]
        [InlineData("LandingGearToggle", "Ship", "Landing & Cargo")]
        [InlineData("UseHeatSink", "Ship", "Cooling & Countermeasures")]
        [InlineData("ExplorationFSSEnter", "Ship", "Scanning")]
        [InlineData("OrderRequestDock", "Ship", "Fighter Orders")]
        [InlineData("BuggyPitchAxis", "SRV", "Driving")]
        [InlineData("DriveSpeedAxis", "SRV", "Driving")]
        [InlineData("VerticalThrustersButton", "SRV", "Driving")]
        [InlineData("BuggyTurretYawAxisRaw", "SRV", "Turret & Weapons")]
        [InlineData("BuggyPrimaryFireButton", "SRV", "Turret & Weapons")]
        [InlineData("SelectTarget_Buggy", "SRV", "Targeting")]
        [InlineData("AutoBreakBuggyButton", "SRV", "Driving")]
        [InlineData("HumanoidForwardButton", "On Foot", "Movement")]
        [InlineData("HumanoidPrimaryFireButton", "On Foot", "Combat")]
        [InlineData("HumanoidSelectPrimaryWeaponButton", "On Foot", "Weapons & Equipment")]
        [InlineData("HumanoidInteractButton", "On Foot", "Interaction")]
        [InlineData("QuickCommsPanel_Humanoid", "On Foot", "Communication")]
        [InlineData("UI_Up", "General", "Interface")]
        [InlineData("FocusLeftPanel", "General", "Interface")]
        [InlineData("GalaxyMapOpen", "General", "Maps")]
        [InlineData("CamTranslateForward", "General", "Maps")]
        [InlineData("PhotoCameraToggle", "General", "Camera Suite")]
        [InlineData("YawAxis_Buggy", "SRV", "Other")]
        [InlineData("HeadLookYaw", "General", "Headlook")]
        [InlineData("QuickCommsPanel", "General", "Communication")]
        [InlineData("MultiCrewPrimaryFire", "General", "Multicrew")]
        [InlineData("FutureCommand", "General", "Other")]
        [InlineData("HumanoidFutureCommand", "On Foot", "Other")]
        public void Native_commands_have_activity_and_category(string name, string section, string category)
        {
            var command = Catalog(name, false).Single();
            Assert.Equal(section, command.Category);
            Assert.Equal(category, EliteCommandBrowser.Category(command));
        }
        /// <summary>Filters intersect without guessing a type from an Axis-looking label or changing native evidence.</summary>
        [Fact]
        public void Search_category_section_and_type_intersect_without_mutating_the_profile()
        {
            const string source = "<Root PresetName='Browser' MajorVersion='4' MinorVersion='2'><PrimaryFire><Primary Device='Keyboard' Key='Key_Space'/></PrimaryFire><PitchAxisRaw><Binding Device='{NoDevice}' Key=''/><Inverted Value='0'/><Deadzone Value='0'/></PitchAxisRaw><PitchUpButton><Primary Device='{NoDevice}' Key=''/></PitchUpButton><HumanoidPrimaryFireButton><Primary Device='{NoDevice}' Key=''/></HumanoidPrimaryFireButton><FutureAxisButton><Primary Device='{NoDevice}' Key=''/></FutureAxisButton></Root>";
            var backend = new EliteDangerousProfileBackend();
            var profile = backend.Import("controls", new Dictionary<string, string> { ["Browser.4.2.binds"] = source });
            var commands = backend.GetCatalog(profile).Commands;
            Assert.Equal(5, EliteCommandBrowser.Filter(commands).Count);
            Assert.Equal("Pitch Axis Raw", Assert.Single(EliteCommandBrowser.Filter(commands, "Ship", "Flight Rotation", CommandType.Axis, "pitch")).DisplayName);
            Assert.Equal("Pitch Up Button", Assert.Single(EliteCommandBrowser.Filter(commands, "Ship", "Flight Rotation", CommandType.Button, "pitch")).DisplayName);
            Assert.Single(EliteCommandBrowser.Filter(commands, type: CommandType.Button, search: "FutureAxisButton"));
            Assert.Empty(EliteCommandBrowser.Filter(commands, "SRV", type: CommandType.Axis));
            Assert.Single(EliteCommandBrowser.Filter(commands, category: "Other"));
            Assert.Equal(2, EliteCommandBrowser.Filter(commands, search: "rotation").Count);
            Assert.Equal(5, EliteCommandBrowser.Filter(commands, search: "   ").Count);
            Assert.Equal(ShipCategories, EliteCommandBrowser.Categories(commands, "Ship"));
            Assert.Empty(EliteCommandBrowser.Categories(commands, "SRV"));
            Assert.Equal(source, backend.Export(profile).Files["Browser.4.2.binds"]);
        }
        /// <summary>Missing display metadata still has an accessible category and deterministic sorted results.</summary>
        [Fact]
        public void Unclassified_commands_remain_visible_and_results_sort_by_category_then_label()
        {
            var plain = new SimCommand { Id = "plain", DisplayName = "Unknown", Category = "General" };
            Assert.Equal("Other", EliteCommandBrowser.Category(plain));
            var commands = Catalog("SecondaryFire", false).Concat(Catalog("PitchAxisRaw", true)).Concat(Catalog("PrimaryFire", false)).Append(plain).ToArray();
            Assert.Equal(SortedLabels, EliteCommandBrowser.Filter(commands).Select(x => x.DisplayName));
            Assert.Empty(EliteCommandBrowser.Filter(commands, search: "absent"));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static IReadOnlyList<SimCommand> Catalog(string name, bool axis)
        {
            var source = $"<Root PresetName='Categories' MajorVersion='4' MinorVersion='2'><{name}>" +
                (axis ? "<Binding Device='{NoDevice}' Key=''/><Inverted Value='0'/><Deadzone Value='0'/>" : "<Primary Device='{NoDevice}' Key=''/><Secondary Device='{NoDevice}' Key=''/>") + $"</{name}></Root>";
            var backend = new EliteDangerousProfileBackend();
            return backend.GetCatalog(backend.Import("controls", new Dictionary<string, string> { ["Categories.4.2.binds"] = source })).Commands;
        }
        #endregion END private helpers
    }
}
