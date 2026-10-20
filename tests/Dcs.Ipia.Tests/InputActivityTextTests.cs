using Dcs.Ipia.Application;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Verifies drawing-independent activity, neutral baselines and bounded noise filtering.</summary>
    public sealed class InputActivityTextTests
    {
        #region BEGIN Public
        /// <summary>Neutral axes establish a baseline, while held buttons and hat transitions remain readable.</summary>
        [Fact]
        public void Reports_buttons_hats_and_releases_without_idle_noise()
        {
            var log = new InputActivityText("Test stick", Guid.Empty);
            Assert.Contains("Test stick", log.Text);
            Assert.Contains(Guid.Empty.ToString(), log.Text);
            log.Sample(Values(0, .5, 0));
            Assert.Contains("Waiting", log.Text);
            log.Sample(Values(1, .5, 1));
            Assert.Contains("JOY_BTN1 · Pressed", log.Text);
            Assert.Contains("JOY_BTN_POV1_U · Pressed", log.Text);
            Assert.DoesNotContain("JOY_X", log.Text);
            var held = log.Text;
            log.Sample(Values(1, .5, 1));
            Assert.Equal(held, log.Text);
            log.Sample(Values(0, .5, 0));
            Assert.Contains("JOY_BTN1 · Released", log.Text);
            Assert.Contains("JOY_BTN_POV1_U · Released", log.Text);
            var initial = new InputActivityText("Held at start", Guid.NewGuid());
            initial.Sample(Values(1, 1, 0));
            Assert.Contains("Pressed", initial.Text);
            Assert.DoesNotContain("JOY_X", initial.Text);
        }
        /// <summary>Axis changes accumulate against the last reported value, retaining slow intentional movement.</summary>
        [Fact]
        public void Filters_jitter_but_reports_slow_movement()
        {
            var log = new InputActivityText("Axis", Guid.Empty);
            log.Sample(Values(0, .5, 0));
            log.Sample(Values(0, .51, 0));
            Assert.DoesNotContain("JOY_X", log.Text);
            log.Sample(Values(0, .52, 0));
            Assert.Contains("JOY_X · Axis 0.520 (52.0%)", log.Text);
            var previous = log.Text;
            log.Sample(Values(0, .525, 0));
            Assert.Equal(previous, log.Text);
            log.Sample(Values(0, 0, 0));
            Assert.Contains("Axis 0.000 (0.0%)", log.Text);
        }
        /// <summary>Only the latest twenty events are retained even during extended input testing.</summary>
        [Fact]
        public void Bounds_activity_history()
        {
            var log = new InputActivityText("Bounded", Guid.Empty);
            for (var i = 0; i < 100; i++) log.Sample(Values(i % 2, .5, 0));
            Assert.Equal(22, log.Text.Split(Environment.NewLine).Length);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static Dictionary<string, double> Values(double button, double axis, double hat) =>
            new() { ["JOY_BTN1"] = button, ["JOY_X"] = axis, ["JOY_BTN_POV1_U"] = hat };
        #endregion END private helpers
    }
}
