using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks local and module-wide command binding state filters.</summary>
    public sealed class CommandBindingStateFilterTests
    {
        #region BEGIN Public
        /// <summary>Local layers and all-device intersection/absence use staged assignments in the selected scope.</summary>
        [Fact]
        public void Filters_local_and_all_device_assignments_independently()
        {
            var module = Guid.NewGuid();
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            var third = Guid.NewGuid();
            var commands = new[] { new CommandPaletteEntry("A", "A", "Category", false),
                                   new CommandPaletteEntry("B", "B", "Category", false),
                                   new CommandPaletteEntry("C", "C", "Category", false) };
            DraftAssignment Assignment(Guid profile, string id, string chord = "",
                                       AssignmentLayer layer = AssignmentLayer.Module) =>
                new(new(module, profile, layer, BindingSection.Key, "JOY_BTN" + id, chord),
                    new DraftCommand(id, id, false, layer));
            var assignments =
                new[] { Assignment(first, "A"), Assignment(second, "A", "SHIFT"), Assignment(first, "B", "SHIFT"),
                        Assignment(third, "C", layer: AssignmentLayer.UiLayer) };
            string[] Ids(CommandBindingState state, string chord = "", AssignmentLayer layer = AssignmentLayer.Module,
                         Guid[]? eligible = null) =>
                CommandBindingStateFilter
                    .Filter(commands, assignments, layer, eligible ?? [first, second], first, chord, state)
                    .Select(x => x.Id)
                    .ToArray();
            Assert.Equal(["A", "B", "C"], Ids(CommandBindingState.All));
            Assert.Equal(["A"], Ids(CommandBindingState.Bound));
            Assert.Equal(["B", "C"], Ids(CommandBindingState.Unbound));
            Assert.Equal(["B"], Ids(CommandBindingState.Bound, "SHIFT"));
            Assert.Equal(["A"], Ids(CommandBindingState.BoundByAll));
            Assert.Equal(["C"], Ids(CommandBindingState.UnboundByAll));
            Assert.Equal(["C"], Ids(CommandBindingState.BoundByAll, layer: AssignmentLayer.UiLayer, eligible: [third]));
            Assert.Equal(["A", "B"],
                         Ids(CommandBindingState.UnboundByAll, layer: AssignmentLayer.UiLayer, eligible: [third]));
        }
        #endregion END Public
    }
}
