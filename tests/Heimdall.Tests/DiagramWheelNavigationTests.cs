using Heimdall.Application;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Contract tests for every controller drawing viewport's modifier policy.
    /// WPF smoke tests additionally confirm routed wheel events hit the viewport.</summary>
    public sealed class DiagramWheelNavigationTests
    {
        [Theory]
        [InlineData(false, false, DiagramWheelMode.Vertical)]
        [InlineData(false, true, DiagramWheelMode.Horizontal)]
        [InlineData(true, false, DiagramWheelMode.Zoom)]
        [InlineData(true, true, DiagramWheelMode.Zoom)]
        public void Wheel_modifier_precedence_is_consistent(bool control, bool shift, DiagramWheelMode expected)
        {
            Assert.Equal(expected, DiagramWheelNavigation.Mode(control, shift));
        }

        [Fact]
        public void Ordinary_and_shift_wheel_have_symmetric_pixel_scroll_deltas()
        {
            Assert.Equal(48, DiagramWheelNavigation.ScrollDelta(120));
            Assert.Equal(-48, DiagramWheelNavigation.ScrollDelta(-120));
            Assert.Equal(24, DiagramWheelNavigation.ScrollDelta(60));
            Assert.Equal(0, DiagramWheelNavigation.ScrollDelta(0));
        }

        [Fact]
        public void Ctrl_zoom_is_symmetric_and_respects_bounds()
        {
            var increased = DiagramWheelNavigation.Zoom(1, 120, .1, 5);
            Assert.Equal(1.1, increased, 10);
            Assert.Equal(1, DiagramWheelNavigation.Zoom(increased, -120, .1, 5), 10);
            Assert.Equal(5, DiagramWheelNavigation.Zoom(5, 120, .1, 5));
            Assert.Equal(.1, DiagramWheelNavigation.Zoom(.1, -120, .1, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DiagramWheelNavigation.Zoom(0, 120, .1, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DiagramWheelNavigation.Zoom(1, 120, 5, .1));
        }

        [Fact]
        public void Cursor_anchored_zoom_preserves_the_same_image_point()
        {
            const double oldOffset = 120;
            const double mouse = 80;
            const double ratio = 1.5;
            var newOffset = DiagramWheelNavigation.AnchoredOffset(oldOffset, mouse, ratio);
            Assert.Equal(220, newOffset);
            Assert.Equal((oldOffset + mouse), (newOffset + mouse) / ratio, 10);
            Assert.Equal(0, DiagramWheelNavigation.AnchoredOffset(0, 0, .5));
            Assert.Equal(0, DiagramWheelNavigation.AnchoredOffset(0, 100, .5));
        }
    }
}
