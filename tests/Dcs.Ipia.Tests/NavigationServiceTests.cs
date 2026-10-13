using Dcs.Ipia.Application;
using Xunit;

namespace Dcs.Ipia.Tests
{
    public class NavigationServiceTests
    {
        private static NavigationService Create(params string[] keys) => new(keys);

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

        [Fact]
        public void NavigateTo_unknown_key_throws()
        {
            var sut = Create("Devices");
            Assert.Throws<ArgumentException>(() => sut.NavigateTo("Nope"));
        }

        [Fact]
        public void CanNavigateTo_is_case_insensitive()
        {
            var sut = Create("Devices");
            Assert.True(sut.CanNavigateTo("devices"));
            Assert.False(sut.CanNavigateTo("Assignments"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NavigateTo_rejects_null_or_whitespace(string? key)
        {
            var sut = Create("Devices");
            Assert.Throws<ArgumentException>(() => sut.NavigateTo(key!));
        }
    }
}
