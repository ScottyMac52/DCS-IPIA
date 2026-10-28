using System.Text.Json;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises shared browser facts and selection transitions without WPF or native edits.</summary>
    public sealed class SharedAssignmentPresentationTests
    {
        private static readonly string[] AllCategories = ["Flight", "Other", "Weapons"];
        private static readonly string[] ShipCategories = ["Other", "Weapons"];
        private static readonly string[] SortedIds = ["other", "a", "z"];
        #region BEGIN Public
        /// <summary>Ownership follows the adapter projection without mutating source records.</summary>
        [Theory]
        [InlineData(GameCommandAssignmentStatus.All, "left", 4)]
        [InlineData(GameCommandAssignmentStatus.AssignedToController, "left", 2)]
        [InlineData(GameCommandAssignmentStatus.NotAssignedToController, "left", 2)]
        [InlineData(GameCommandAssignmentStatus.UnassignedEverywhere, "left", 1)]
        [InlineData(GameCommandAssignmentStatus.AssignedAnywhere, "left", 3)]
        [InlineData(GameCommandAssignmentStatus.AssignedToController, null, 0)]
        [InlineData(GameCommandAssignmentStatus.NotAssignedToController, null, 4)]
        public void Ownership_filters_use_adapter_supplied_owners(GameCommandAssignmentStatus status, string? controller, int count)
        {
            var entries = Entries();
            var frozen = JsonSerializer.Serialize(entries);
            Assert.Equal(count, GameCommandBrowser.Filter(entries, new(AssignmentStatus: status), controller).Count);
            Assert.Equal(frozen, JsonSerializer.Serialize(entries));
        }

        /// <summary>Native types, categories and text filters intersect without inferring controls.</summary>
        [Fact]
        public void Search_scope_type_and_category_intersect_without_guessing_from_labels()
        {
            var entries = Entries();
            Assert.Equal(AllCategories, GameCommandBrowser.Categories(entries));
            Assert.Equal(ShipCategories, GameCommandBrowser.Categories(entries, "Ship"));
            Assert.Empty(GameCommandBrowser.Categories(entries, "Missing"));
            Assert.Equal("fire", Assert.Single(GameCommandBrowser.Filter(entries,
                new("Ship", "Weapons", CommandType.Button, "  FIRE  "))).Command.Id);
            Assert.Empty(GameCommandBrowser.Filter(entries, new("Foot", "Weapons", CommandType.Button)));
            Assert.Empty(GameCommandBrowser.Filter(entries, new(InputType: CommandType.Hat)));
            Assert.Equal(2, GameCommandBrowser.Filter(entries, new(InputType: CommandType.Axis)).Count);
            Assert.Equal("future", Assert.Single(GameCommandBrowser.Filter(entries, new(Search: "future"))).Command.Id);
            Assert.Single(GameCommandBrowser.Filter(entries, new(Search: "flight")));
            Assert.Empty(GameCommandBrowser.Filter(entries, new(Search: "absent")));
            Assert.Equal(4, GameCommandBrowser.Filter(entries, new(Search: "  ")).Count);
            Assert.Throws<ArgumentOutOfRangeException>(() => GameCommandBrowser.Filter(entries,
                new(AssignmentStatus: (GameCommandAssignmentStatus)999)));
        }

        /// <summary>Catalog refresh uses a stable native-identity tie breaker.</summary>
        [Fact]
        public void Display_order_is_category_then_label_then_native_identity()
        {
            var commands = new[] { Command("z", "Same"), Command("a", "Same"), Command("other", "A") };
            var entries = commands.Select(x => new GameCommandEntry(x, "Context", "Category", [], [])).ToArray();
            Assert.Equal(SortedIds, GameCommandBrowser.Filter(entries, new()).Select(x => x.Command.Id));
        }

        /// <summary>Controller changes and hidden commands invalidate stale pending input.</summary>
        [Fact]
        public void Refresh_preserves_identity_and_pending_input_only_for_the_same_controller_and_command()
        {
            var selection = new GameAssignmentSelection();
            var commands = Entries().Select(x => x.Command).ToArray();
            selection.Refresh("left", commands, "fire");
            selection.SelectInput("Native_Button_1");
            selection.Refresh("left", commands.Reverse().ToArray());
            Assert.Equal("fire", selection.CommandId);
            Assert.Equal("Native_Button_1", selection.InputKey);
            selection.Refresh("right", commands);
            Assert.Equal("right", selection.ControllerId);
            Assert.Equal("fire", selection.CommandId);
            Assert.Null(selection.InputKey);
            selection.SelectInput("Native_Button_2");
            selection.Refresh("right", [commands[1]], "hidden-command");
            Assert.Equal(commands[1].Id, selection.CommandId);
            Assert.Null(selection.InputKey);
            selection.Refresh("right", []);
            selection.SelectInput("must-not-survive");
            Assert.Null(selection.CommandId);
            Assert.Null(selection.InputKey);
        }

        /// <summary>Command, slot and workspace transitions cannot reuse pending inputs.</summary>
        [Fact]
        public void Command_slot_and_workspace_transitions_clear_stale_input()
        {
            var selection = new GameAssignmentSelection();
            selection.SelectInput("ignored");
            Assert.Null(selection.InputKey);
            selection.SelectCommand("first");
            selection.SelectInput("one");
            selection.SelectCommand("first");
            selection.SelectSlot("Primary");
            Assert.Equal("one", selection.InputKey);
            selection.SelectSlot("Secondary");
            Assert.Equal("Secondary", selection.Slot);
            Assert.Null(selection.InputKey);
            selection.SelectInput("two");
            selection.SelectCommand("second");
            Assert.Null(selection.InputKey);
            selection.Refresh("controller", [Command("second", "Second")]);
            selection.SelectInput("three");
            selection.Reset();
            Assert.Null(selection.ControllerId);
            Assert.Null(selection.CommandId);
            Assert.Null(selection.InputKey);
            Assert.Equal("Primary", selection.Slot);
        }

        /// <summary>Elite supplies typed filter capabilities and native ownership facts.</summary>
        [Fact]
        public void Elite_declares_filters_and_projects_native_categories_and_owners()
        {
            var command = Command("native", "Native");
            command.Category = "Ship";
            command.Type = CommandType.Axis;
            command.Metadata["ed.category"] = "Flight";
            var rows = new[] { new EliteAssignmentRow("left", command.Id, command.DisplayName, "Ship", "Binding",
                "Joy_XAxis", BindingMode.Press, [], true) };
            var entry = Assert.Single(EliteCommandBrowser.Entries([command], rows));
            Assert.Equal("Ship", entry.Section);
            Assert.Equal("Flight", entry.Category);
            Assert.Equal(CommandType.Axis, Assert.Single(entry.InputTypes));
            Assert.Equal("left", Assert.Single(entry.AssignedControllers));
            Assert.Equal(new CommandType?[] { null, CommandType.Button, CommandType.Axis }, EliteCommandBrowser.InputTypes.Select(x => x.Type));
            Assert.All(EliteCommandBrowser.InputTypes, x => Assert.NotEmpty(x.Label));
            Assert.Equal(Enum.GetValues<GameCommandAssignmentStatus>(), EliteCommandBrowser.AssignmentStatuses.Select(x => x.Status));
            Assert.All(EliteCommandBrowser.AssignmentStatuses, x => Assert.NotEmpty(x.Label));
        }

        /// <summary>Editor feedback and native actions consistently follow review and live testing.</summary>
        [Theory]
        [InlineData(true, true, false, false, true, "Native replacement review")]
        [InlineData(true, false, false, false, false, "Assignment unchanged.")]
        [InlineData(false, false, false, false, false, "Native validation failed")]
        [InlineData(true, true, true, false, false, "Stop input testing to apply or clear assignments.")]
        [InlineData(true, true, false, true, false, "Native replacement review")]
        public void Editor_feedback_and_actions_follow_review_and_live_test_state(bool valid, bool changed,
            bool running, bool busy, bool apply, string expected)
        {
            var state = GameAssignmentEditorState.For(true, new(valid, changed, "Native validation failed"),
                true, true, running, busy, "Native replacement review");
            Assert.Equal(expected, state.Feedback);
            Assert.Equal(apply, state.Actions.Apply);
            Assert.Equal(!running && !busy, state.Actions.Clear);
            Assert.Equal(running, state.Actions.Stop);
        }

        #endregion END Public
        #region BEGIN private helpers
        private static SimCommand Command(string id, string name) => new() { Id = id, DisplayName = name };
        private static GameCommandEntry[] Entries() =>
        [
            new(Command("fire", "Fire"), "Ship", "Weapons", [CommandType.Button], ["left"]),
            new(Command("pitch", "Pitch"), "Foot", "Flight", [CommandType.Axis], ["right"]),
            new(Command("future", "Unknown Axis Button"), "Ship", "Other", [], []),
            new(Command("mixed", "Mixed"), "Ship", "Weapons", [CommandType.Axis, CommandType.Button], ["left", "right"])
        ];
        #endregion END private helpers
    }
}
