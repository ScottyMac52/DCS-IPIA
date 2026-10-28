using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Domain.MultiSim;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects native DCS ownership/layer rules while using shared command filtering and selection.</summary>
    public sealed class DcsCommandBrowserTests
    {
        private static readonly Guid Module = Guid.NewGuid();
        private static readonly Guid Left = Guid.NewGuid();
        private static readonly Guid Right = Guid.NewGuid();
        private static readonly Guid[] Devices = [Left, Right];
        private static readonly CommandPaletteEntry[] Commands =
        [new("fire", "Fire", "Weapons", false), new("pitch", "Pitch", "Flight", true),
            new("shared", "Shared", "Weapons", false), new("unused", "Unused Axis Button", "Other", false)];
        #region BEGIN Public
        /// <summary>Ownership is evaluated for the actual selected controller and native module layer.</summary>
        [Theory]
        [InlineData(DcsCommandAssignmentStatus.All, 4)]
        [InlineData(DcsCommandAssignmentStatus.AssignedHere, 2)]
        [InlineData(DcsCommandAssignmentStatus.UnassignedHere, 2)]
        [InlineData(DcsCommandAssignmentStatus.AssignedEverywhere, 1)]
        [InlineData(DcsCommandAssignmentStatus.UnassignedEverywhere, 1)]
        public void Native_ownership_filters_keep_controller_and_layer_boundaries(DcsCommandAssignmentStatus status, int count)
        {
            var assignments = Assignments();
            var frozen = JsonSerializer.Serialize(assignments);
            var result = DcsCommandBrowser.Filter(Commands, assignments, AssignmentLayer.Module, Left, Devices, status: status);
            Assert.Equal(count, result.Count);
            Assert.Equal(frozen, JsonSerializer.Serialize(assignments));
            if (status == DcsCommandAssignmentStatus.AssignedEverywhere) Assert.Equal("shared", Assert.Single(result).Id);
        }
        /// <summary>Changing the selected device or UI-layer scope never conflates matching command codes.</summary>
        [Fact]
        public void Controller_and_ui_scope_filters_do_not_leak_assignments()
        {
            var assignments = Assignments();
            Assert.Equal("pitch", Assert.Single(DcsCommandBrowser.Filter(Commands, assignments, AssignmentLayer.Module,
                Right, Devices, search: "pitch", status: DcsCommandAssignmentStatus.AssignedHere)).Id);
            Assert.Empty(DcsCommandBrowser.Filter(Commands, assignments, AssignmentLayer.Module,
                Left, Devices, search: "pitch", status: DcsCommandAssignmentStatus.AssignedHere));
            Assert.Empty(DcsCommandBrowser.Filter(Commands, assignments, AssignmentLayer.UiLayer,
                Left, Devices, status: DcsCommandAssignmentStatus.AssignedHere));
            Assert.Equal("fire", Assert.Single(DcsCommandBrowser.Filter(Commands, assignments, AssignmentLayer.UiLayer,
                Right, Devices, status: DcsCommandAssignmentStatus.AssignedHere)).Id);
            Assert.Equal(2, DcsCommandBrowser.Filter(Commands, assignments, null, null, Devices,
                status: DcsCommandAssignmentStatus.AssignedEverywhere).Count);
            Assert.Empty(DcsCommandBrowser.Filter(Commands, assignments, AssignmentLayer.Module, Left, [],
                status: DcsCommandAssignmentStatus.AssignedEverywhere));
        }
        /// <summary>The standalone catalog retains all-device filtering when no controller is selected.</summary>
        [Theory]
        [InlineData(DcsCommandAssignmentStatus.AssignedHere, 3)]
        [InlineData(DcsCommandAssignmentStatus.UnassignedHere, 1)]
        public void Standalone_catalog_filters_keep_their_native_all_controller_meaning(DcsCommandAssignmentStatus status, int count) =>
            Assert.Equal(count, DcsCommandBrowser.Filter(Commands, Assignments(), AssignmentLayer.Module, null, Devices, status: status).Count);

        /// <summary>Shared text/category/type filtering uses native DCS type evidence, never an axis-looking name.</summary>
        [Fact]
        public void Native_category_type_and_search_intersect_and_unknown_filters_are_rejected()
        {
            Assert.Equal("fire", Assert.Single(DcsCommandBrowser.Filter(Commands, Assignments(), AssignmentLayer.Module,
                Left, Devices, "Weapons", CommandType.Button, "  FIRE  ")).Id);
            Assert.Equal("pitch", Assert.Single(DcsCommandBrowser.Filter(Commands, Assignments(), null,
                null, Devices, type: CommandType.Axis)).Id);
            Assert.Equal("unused", Assert.Single(DcsCommandBrowser.Filter(Commands, Assignments(), null,
                null, Devices, type: CommandType.Button, search: "Axis Button")).Id);
            Assert.Empty(DcsCommandBrowser.Filter(Commands, [], null, null, [], search: "missing"));
            Assert.Throws<ArgumentOutOfRangeException>(() => DcsCommandBrowser.Filter(Commands, [], null,
                null, [], status: (DcsCommandAssignmentStatus)999));
            var described = DcsCommandBrowser.Describe(Commands[1]);
            Assert.Equal(Commands[1].Id, described.Id);
            Assert.Equal(Commands[1].Name, described.DisplayName);
            Assert.Equal(Commands[1].Category, described.Category);
            Assert.Equal(CommandType.Axis, described.Type);
        }
        /// <summary>DCS preserves explicit empty selection and clears a hidden command rather than assigning a default.</summary>
        [Fact]
        public void Shared_selection_supports_native_explicit_selection_without_stale_input()
        {
            var selection = new GameAssignmentSelection();
            var commands = Commands.Select(DcsCommandBrowser.Describe).ToArray();
            selection.Refresh(Left.ToString("D"), commands, selectFirst: false);
            Assert.Null(selection.CommandId);
            selection.SelectCommand("fire"); selection.SelectInput("JOY_BTN1");
            selection.Refresh(Left.ToString("D"), commands, selectFirst: false);
            Assert.Equal("fire", selection.CommandId); Assert.Equal("JOY_BTN1", selection.InputKey);
            selection.Refresh(Right.ToString("D"), commands, selectFirst: false);
            Assert.Equal("fire", selection.CommandId); Assert.Null(selection.InputKey);
            selection.Refresh(Right.ToString("D"), [commands[1]], selectFirst: false);
            Assert.Null(selection.CommandId); Assert.Null(selection.InputKey);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DraftAssignment[] Assignments() =>
        [Row(Left, "fire"), Row(Right, "pitch"), Row(Left, "shared"), Row(Right, "shared"), Row(Right, "fire", AssignmentLayer.UiLayer)];
        private static DraftAssignment Row(Guid profile, string command, AssignmentLayer layer = AssignmentLayer.Module) =>
            new(new(Module, profile, layer, command == "pitch" ? BindingSection.Axis : BindingSection.Key, "JOY_" + command),
                new(command, command, command == "pitch", layer));
        #endregion END private helpers
    }
}
