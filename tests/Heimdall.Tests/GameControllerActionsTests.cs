using Heimdall.Application.Games;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks shared device-action states for idle, identification, ambiguity, testing and busy operations.</summary>
    public sealed class GameControllerActionsTests
    {
        #region BEGIN Public
        /// <summary>Only Stop survives busy identification; a matching live result enables Confirm.</summary>
        [Theory]
        [InlineData(false, false, false, true, true, false, false)]
        [InlineData(true, false, false, true, false, true, true)]
        [InlineData(true, true, false, true, false, true, false)]
        [InlineData(true, false, true, true, false, true, false)]
        [InlineData(false, true, false, true, false, false, false)]
        [InlineData(false, false, true, true, false, false, false)]
        [InlineData(false, false, false, false, false, false, false)]
        public void Live_and_busy_states_gate_conflicting_actions(bool identifying, bool testing, bool busy,
            bool devices, bool idle, bool stop, bool confirm)
        {
            var result = GameControllerActions.For(true, true, true, true, true, true, identifying, testing, busy, devices);
            Assert.Equal(idle, result.Start); Assert.Equal(stop, result.Stop); Assert.Equal(confirm, result.Confirm);
            Assert.Equal(idle, result.Add); Assert.Equal(idle, result.Replace); Assert.Equal(idle, result.Remove);
            Assert.Equal(idle, result.Rescan); Assert.Equal(idle, result.Drawing);
        }
        /// <summary>Missing profile/device/connection/permission prerequisites independently disable their actions.</summary>
        [Fact]
        public void Individual_prerequisites_are_required()
        {
            var none = GameControllerActions.For(false, false, false, false, false, false, false, false, false, true);
            Assert.Equal(new GameControllerActions(false, false, false, false, false, false, true, false), none);
            var ambiguous = GameControllerActions.For(true, false, true, true, true, true, true, false, false, true);
            Assert.False(ambiguous.Confirm); Assert.True(ambiguous.Stop); Assert.False(ambiguous.Start);
            var away = GameControllerActions.For(true, true, true, true, true, true, true, false, false, false);
            Assert.True(away.Stop); Assert.False(away.Confirm); Assert.False(away.Rescan);
            var add = GameControllerActions.For(false, false, true, false, false, false, false, false, false, true);
            Assert.True(add.Add); Assert.False(add.Start); Assert.False(add.Remove);
        }
        #endregion END Public
    }
}
