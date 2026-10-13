using Dcs.Ipia.Application;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Verifies feature selection, notification and navigation key validation.</summary>
    public class NavigationServiceTests
    {

        #region BEGIN Public
        /// <summary>Selecting a known feature updates the current key and notifies subscribers.</summary>
        [Fact]
        public void NavigateTo_sets_current_key_and_raises_event()
        {
            var sut = Create("Devices", "Modifiers");
            var raised = false;
            sut.CurrentFeatureChanged += (_, _) => raised = true;

            sut.NavigateTo("Devices");

            Assert.Equal("Devices", sut.CurrentFeatureKey);
            Assert.True(raised);
        }

        /// <summary>Selecting the current feature does not emit another change notification.</summary>
        [Fact]
        public void NavigateTo_same_key_is_noop()
        {
            var sut = Create("Devices");
            var count = 0;
            sut.CurrentFeatureChanged += (_, _) => count++;

            sut.NavigateTo("Devices");
            sut.NavigateTo("Devices");

            Assert.Equal(1, count);
        }

        /// <summary>Unknown features are rejected before changing navigation state.</summary>
        [Fact]
        public void NavigateTo_unknown_key_throws()
        {
            var sut = Create("Devices");
            Assert.Throws<ArgumentException>(() => sut.NavigateTo("Nope"));
        }

        /// <summary>Known keys compare without regard to case and unknown keys are rejected.</summary>
        [Fact]
        public void CanNavigateTo_is_case_insensitive()
        {
            var sut = Create("Devices");
            Assert.True(sut.CanNavigateTo("devices"));
            Assert.False(sut.CanNavigateTo("Assignments"));
        }

        /// <summary>Null, empty and whitespace keys cannot select a feature.</summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NavigateTo_rejects_null_or_whitespace(string? key)
        {
            var sut = Create("Devices");
            Assert.Throws<ArgumentException>(() => sut.NavigateTo(key!));
        }
        #endregion END Public

        #region BEGIN private helpers
        private static NavigationService Create(params string[] keys) => new(keys);

        #endregion END private helpers
    }
}
