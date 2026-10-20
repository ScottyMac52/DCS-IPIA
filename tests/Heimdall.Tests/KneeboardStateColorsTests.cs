using Heimdall.Application;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Ensures editor callouts and live legends share semantic ink independently of theme accents.</summary>
    public sealed class KneeboardStateColorsTests
    {
        #region BEGIN Public
        /// <summary>Aircraft Base overrides native lettering while UI and shifted colors remain authoritative.</summary>
        [Theory]
        [InlineData(AssignmentLayer.Module, "", "#ffffff", "#007ACC")]
        [InlineData(AssignmentLayer.UiLayer, "", "#0891b2", "#0891b2")]
        [InlineData(AssignmentLayer.Module, "SHIFT", "#c026d3", "#c026d3")]
        [InlineData(AssignmentLayer.UiLayer, "SHIFT", "#7c3aed", "#7c3aed")]
        public void Resolves_same_ink_for_editing_and_live_feedback(AssignmentLayer layer, string chord, string color, string expected) =>
            Assert.Equal(expected, KneeboardStateColors.Resolve(layer, chord, color));

        /// <summary>Invalid inputs fail at the shared state boundary.</summary>
        [Fact]
        public void Rejects_missing_chord_or_color()
        {
            Assert.Throws<ArgumentNullException>(() => KneeboardStateColors.Resolve(AssignmentLayer.Module, null!, "#ffffff"));
            Assert.Throws<ArgumentNullException>(() => KneeboardStateColors.Resolve(AssignmentLayer.Module, "", null!));
            Assert.Throws<ArgumentException>(() => KneeboardStateColors.Resolve(AssignmentLayer.Module, "", " "));
        }
        #endregion END Public
    }
}
