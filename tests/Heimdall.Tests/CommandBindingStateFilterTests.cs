using Heimdall.Application;
using Heimdall.Domain;
using Xunit;

namespace Heimdall.Tests
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
            var commands = new[] { new DcsCommandPaletteEntry("A", "A", "Category", false),
                                   new DcsCommandPaletteEntry("B", "B", "Category", false),
                                   new DcsCommandPaletteEntry("C", "C", "Category", false) };
            DcsDraftAssignment Assignment(Guid profile, string id, string chord = "",
                                       AssignmentLayer layer = AssignmentLayer.Module) =>
                new(new(module, profile, layer, BindingSection.Key, "JOY_BTN" + id, chord),
                    new DcsDraftCommand(id, id, false, layer));
            var assignments =
                new[] { Assignment(first, "A"), Assignment(second, "A", "SHIFT"), Assignment(first, "B", "SHIFT"),
                        Assignment(third, "C", layer: AssignmentLayer.UiLayer) };
            string[] Ids(DcsCommandBindingState state, string chord = "", AssignmentLayer layer = AssignmentLayer.Module,
                         Guid[]? eligible = null) =>
                DcsCommandBindingStateFilter
                    .Filter(commands, assignments, layer, eligible ?? [first, second], first, chord, state)
                    .Select(x => x.Id)
                    .ToArray();
            Assert.Equal(["A", "B", "C"], Ids(DcsCommandBindingState.All));
            Assert.Equal(["A"], Ids(DcsCommandBindingState.Bound));
            Assert.Equal(["B", "C"], Ids(DcsCommandBindingState.Unbound));
            Assert.Equal(["B"], Ids(DcsCommandBindingState.Bound, "SHIFT"));
            Assert.Equal(["A"], Ids(DcsCommandBindingState.BoundByAll));
            Assert.Equal(["C"], Ids(DcsCommandBindingState.UnboundByAll));
            Assert.Equal(["C"], Ids(DcsCommandBindingState.BoundByAll, layer: AssignmentLayer.UiLayer, eligible: [third]));
            Assert.Equal(["A", "B"],
                         Ids(DcsCommandBindingState.UnboundByAll, layer: AssignmentLayer.UiLayer, eligible: [third]));
        }
        #endregion END Public
    }
}
