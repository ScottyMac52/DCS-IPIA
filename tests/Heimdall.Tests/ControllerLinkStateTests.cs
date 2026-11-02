using Heimdall.Application;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks the full saved-link and readiness policy independently of live controller availability.</summary>
    public sealed class ControllerLinkStateTests
    {
        #region BEGIN Public
        /// <summary>All sixteen ownership/readiness combinations preserve explicit unlink requirements.</summary>
        [Theory]
        [InlineData(false, false, false, false, false, false, false, false)]
        [InlineData(false, false, false, true, false, false, false, false)]
        [InlineData(false, false, true, false, true, false, false, false)]
        [InlineData(false, false, true, true, false, false, false, false)]
        [InlineData(false, true, false, false, false, false, false, false)]
        [InlineData(false, true, false, true, false, false, false, false)]
        [InlineData(false, true, true, false, true, false, false, true)]
        [InlineData(false, true, true, true, false, false, false, false)]
        [InlineData(true, false, false, false, false, false, false, false)]
        [InlineData(true, false, false, true, false, false, false, false)]
        [InlineData(true, false, true, false, false, true, true, false)]
        [InlineData(true, false, true, true, false, false, false, false)]
        [InlineData(true, true, false, false, false, false, false, false)]
        [InlineData(true, true, false, true, false, false, false, false)]
        [InlineData(true, true, true, false, false, true, false, true)]
        [InlineData(true, true, true, true, false, false, false, false)]
        public void All_link_and_readiness_states_match_the_policy(bool physical, bool drawing, bool ready, bool busy,
            bool selectPhysical, bool unlinkPhysical, bool selectDrawing, bool unlinkDrawing)
        {
            Assert.Equal(new ControllerLinkState(selectPhysical, unlinkPhysical, selectDrawing, unlinkDrawing),
                ControllerLinkState.For(physical, drawing, ready, busy));
        }
        #endregion END Public
    }
}
