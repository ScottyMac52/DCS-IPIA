using Heimdall.Application.Games;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Shared diagram menus must never advertise operations without
    /// an actual handler and a currently valid native target.</summary>
    public sealed class HardwareDiagramActionAvailabilityTests
    {
        [Fact]
        public void Unsupported_actions_are_absent()
        {
            var actions = HardwareDiagramActionAvailability.For(
                selectHandler: true, dropHandler: false, clearHandler: false,
                clearTarget: true, tuneHandler: false, tuneTarget: true);
            Assert.True(actions.Select);
            Assert.False(actions.Drop);
            Assert.False(actions.Clear);
            Assert.False(actions.Tune);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void Clear_requires_handler_and_native_target(bool handler, bool target)
        {
            var actions = HardwareDiagramActionAvailability.For(
                true, true, handler, target, false, false);
            Assert.Equal(handler && target, actions.Clear);
            Assert.True(actions.Drop);
            Assert.False(actions.Tune);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void Tune_is_not_a_synonym_for_select(bool handler, bool target)
        {
            var actions = HardwareDiagramActionAvailability.For(
                true, false, false, false, handler, target);
            Assert.Equal(handler && target, actions.Tune);
            Assert.True(actions.Select);
        }

        [Fact]
        public void Read_only_diagram_may_have_no_actions()
        {
            Assert.Equal(new HardwareDiagramActionAvailability(false, false, false, false),
                HardwareDiagramActionAvailability.For(false, false, false, false, false, false));
        }
    }
}
