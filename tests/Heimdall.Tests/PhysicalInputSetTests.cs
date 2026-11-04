using Heimdall.Application;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Matches authored and observed controls by the existing physical POV identity without broadening the input set.</summary>
    public sealed class PhysicalInputSetTests
    {
        #region BEGIN Public
        /// <summary>Every POV direction accepts either spelling, independently of order, without rewriting source keys.</summary>
        [Theory]
        [InlineData("U")][InlineData("UR")][InlineData("R")][InlineData("DR")]
        [InlineData("D")][InlineData("DL")][InlineData("L")][InlineData("UL")]
        public void Pov_aliases_are_the_same_physical_set(string direction)
        {
            var authored = new[] { "JOY_X", "JOY_POV1_" + direction, "JOY_BTN1" };
            var observed = new[] { "JOY_BTN1", "JOY_BTN_POV1_" + direction, "JOY_X" };
            Assert.True(DcsInputKey.SamePhysicalInputs(authored, observed));
            Assert.True(DcsInputKey.SamePhysicalInputs(observed, authored));
            Assert.Contains("JOY_POV1_" + direction, authored);
            Assert.Contains("JOY_BTN_POV1_" + direction, observed);
            Assert.False(DcsInputKey.SamePhysicalInputs(authored, observed.Append("JOY_Y")));
            Assert.False(DcsInputKey.SamePhysicalInputs(authored, observed.Take(2)));
        }

        /// <summary>Buttons, axes, POV number/direction and case remain distinct physical identities.</summary>
        [Theory]
        [InlineData("JOY_X", "JOY_Y")]
        [InlineData("JOY_BTN1", "JOY_BTN2")]
        [InlineData("JOY_POV1_U", "JOY_BTN_POV2_U")]
        [InlineData("JOY_POV1_U", "JOY_BTN_POV1_D")]
        [InlineData("JOY_POV1_U", "joy_btn_pov1_u")]
        public void Different_controls_are_not_compatible(string authored, string observed) =>
            Assert.False(DcsInputKey.SamePhysicalInputs([authored], [observed]));

        /// <summary>Empty sets retain normal set semantics; missing collections are rejected explicitly.</summary>
        [Fact]
        public void Empty_and_null_sets_are_explicit()
        {
            Assert.True(DcsInputKey.SamePhysicalInputs([], []));
            Assert.False(DcsInputKey.SamePhysicalInputs([], ["JOY_X"]));
            Assert.Throws<ArgumentNullException>(() => DcsInputKey.SamePhysicalInputs(null!, []));
            Assert.Throws<ArgumentNullException>(() => DcsInputKey.SamePhysicalInputs([], null!));
        }
        #endregion END Public
    }
}
