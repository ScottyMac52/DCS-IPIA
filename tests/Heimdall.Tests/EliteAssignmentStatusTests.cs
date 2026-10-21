using System.Text.Json;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks selected-controller and whole-profile ownership across native slots, modifiers and filter intersections.</summary>
    public sealed class EliteAssignmentStatusTests
    {
        #region BEGIN Public
        /// <summary>Any evaluates the whole profile, while selected ownership ignores modifier-only references.</summary>
        [Fact]
        public void Ownership_filters_distinguish_selected_and_any_without_mutating_native_state()
        {
            var backend = new EliteDangerousProfileBackend();
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "EliteDangerous", "Synthetic.4.2.binds");
            var profile = backend.Import("controls", new Dictionary<string, string> { ["Synthetic.4.2.binds"] = File.ReadAllText(path) });
            var before = JsonSerializer.Serialize(profile); var commands = backend.GetCatalog(profile).Commands;
            var rows = EliteWorkspaceProjection.Rows(profile, backend.GetCatalog(profile));
            var stick = profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "ExampleStick").LogicalId;
            var keyboard = profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "Keyboard").LogicalId;
            Assert.Equal(5, EliteCommandBrowser.Filter(commands, assignments: rows, deviceId: stick).Count);
            Assert.Equal(3, EliteCommandBrowser.Filter(commands, assignments: rows, deviceId: stick, status: EliteAssignmentStatus.AssignedOnly).Count);
            Assert.Equal(2, EliteCommandBrowser.Filter(commands, assignments: rows, deviceId: stick, status: EliteAssignmentStatus.NotAssigned).Count);
            Assert.Equal("Landing Gear Toggle", Assert.Single(EliteCommandBrowser.Filter(commands, assignments: rows, deviceId: stick,
                status: EliteAssignmentStatus.NotAssignedByAny)).DisplayName);
            Assert.Equal(4, EliteCommandBrowser.Filter(commands, assignments: rows, deviceId: stick, status: EliteAssignmentStatus.AssignedByAny).Count);
            Assert.Equal("Primary Fire", Assert.Single(EliteCommandBrowser.Filter(commands, assignments: rows, deviceId: keyboard,
                status: EliteAssignmentStatus.AssignedOnly)).DisplayName);
            Assert.Empty(EliteCommandBrowser.Filter(commands, assignments: rows, status: EliteAssignmentStatus.AssignedOnly));
            Assert.Equal(5, EliteCommandBrowser.Filter(commands, assignments: rows, status: EliteAssignmentStatus.NotAssigned).Count);
            Assert.Equal("Pitch Axis Raw", Assert.Single(EliteCommandBrowser.Filter(commands, "Ship", "Flight Rotation", CommandType.Axis, "pitch",
                rows, stick, EliteAssignmentStatus.AssignedByAny)).DisplayName);
            Assert.Empty(EliteCommandBrowser.Filter(commands, "Ship", "Flight Rotation", CommandType.Axis, "pitch",
                rows, stick, EliteAssignmentStatus.NotAssignedByAny));
            Assert.Throws<ArgumentOutOfRangeException>(() => EliteCommandBrowser.Filter(commands, status: (EliteAssignmentStatus)99));
            Assert.Equal(before, JsonSerializer.Serialize(profile));
        }
        #endregion END Public
    }
}
