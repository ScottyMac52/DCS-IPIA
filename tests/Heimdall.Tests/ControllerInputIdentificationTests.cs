using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Verifies settled state-change discovery without initial-state or acquisition activity.</summary>
    public sealed class ControllerInputIdentificationTests
    {
        #region BEGIN Public
        /// <summary>Held buttons establish a baseline; only later presses or releases identify a device.</summary>
        [Fact]
        public void Requires_a_state_change_after_settling()
        {
            var id = Guid.NewGuid(); var time = new IdentificationTestTime();
            var identification = new ControllerInputIdentification([id], time);
            Assert.Empty(identification.Sample([Frame(id, 1, .95)]));
            time.Advance(); Assert.Empty(identification.Sample([Frame(id, 1, .95)]));
            Assert.Empty(identification.Sample([Frame(id, 1, .95)]));
            Assert.Equal("JOY_BTN1", identification.Sample([Frame(id, 0, .95)])[id]);
            Assert.Empty(identification.Sample([Frame(id, 0, .95)]));
            Assert.Equal("JOY_BTN1", identification.Sample([Frame(id, 1, .95)])[id]);
        }
        /// <summary>Delayed hardware initialization restarts settling and never counts as deliberate input.</summary>
        [Fact]
        public void Acquisition_transients_and_resting_hats_are_silent()
        {
            var id = Guid.NewGuid(); var time = new IdentificationTestTime();
            var identification = new ControllerInputIdentification([id], time);
            Assert.Empty(identification.Sample([Frame(id, 0, 0)]));
            time.Advance(200);
            var held = new DeviceInputFrame(id, new Dictionary<string, double> { ["JOY_BTN1"] = 1, ["JOY_X"] = 1, ["JOY_BTN_POV1_U"] = 1 });
            Assert.Empty(identification.Sample([held]));
            time.Advance(400); Assert.Empty(identification.Sample([held]));
            time.Advance(100); Assert.Empty(identification.Sample([held]));
            Assert.Empty(identification.Sample([held]));
            var released = held with { Inputs = new Dictionary<string, double> { ["JOY_BTN1"] = 1, ["JOY_X"] = 1, ["JOY_BTN_POV1_U"] = 0 } };
            Assert.Equal("JOY_BTN_POV1_U", identification.Sample([released])[id]);
        }
        /// <summary>Axis jitter remains silent after settling and deliberate gradual motion still identifies an axis-only device.</summary>
        [Fact]
        public void Finds_axis_only_devices_and_filters_jitter()
        {
            var id = Guid.NewGuid(); var time = new IdentificationTestTime();
            var identification = new ControllerInputIdentification([id], time);
            Assert.Empty(identification.Sample([Frame(id, 0, .5)]));
            time.Advance(); Assert.Empty(identification.Sample([Frame(id, 0, .51)]));
            Assert.Empty(identification.Sample([Frame(id, 0, .52)]));
            Assert.Empty(identification.Sample([Frame(id, 0, .56)]));
            Assert.Equal("JOY_X", identification.Sample([Frame(id, 0, .65)])[id]);
            Assert.Empty(identification.Sample([Frame(id, 0, .66)]));
            Assert.Equal("JOY_X", identification.Sample([Frame(id, 0, 0)])[id]);
        }
        /// <summary>A slowly settling axis cannot arm merely because each individual poll changes by less than the noise tolerance.</summary>
        [Fact]
        public void Slow_initial_axis_settling_restarts_the_quiet_window()
        {
            var id = Guid.NewGuid(); var time = new IdentificationTestTime();
            var identification = new ControllerInputIdentification([id], time);
            identification.Sample([Frame(id, 0, 0)]);
            for (var i = 1; i <= 60; i++)
            {
                time.Advance(30); Assert.Empty(identification.Sample([Frame(id, 0, i * .01)]));
            }
            time.Advance(); Assert.Empty(identification.Sample([Frame(id, 0, .6)]));
            Assert.Equal("JOY_X", identification.Sample([Frame(id, 0, .9)])[id]);
        }
        /// <summary>Each controller settles independently and simultaneous changes remain ambiguous.</summary>
        [Fact]
        public void Reports_exact_instances_and_simultaneous_activity()
        {
            var first = Guid.NewGuid(); var second = Guid.NewGuid(); var unrelated = Guid.NewGuid(); var time = new IdentificationTestTime();
            var identification = new ControllerInputIdentification([first, second], time);
            identification.Sample([Frame(first, 0, .5), Frame(second, 0, .5)]);
            time.Advance(400); identification.Sample([Frame(first, 0, .5), Frame(second, 1, .5)]);
            time.Advance(100); Assert.Empty(identification.Sample([Frame(first, 0, .5), Frame(second, 1, .5)]));
            var single = identification.Sample([Frame(first, 1, .5), Frame(second, 1, .5)]);
            Assert.Single(single); Assert.Contains(first, single.Keys);
            time.Advance(); Assert.Empty(identification.Sample([Frame(first, 1, .5), Frame(second, 1, .5)]));
            var activity = identification.Sample([Frame(first, 0, .5), Frame(second, 0, .5), Frame(unrelated, 1, 1), Frame(Guid.Empty, 1, 1)]);
            Assert.Equal(2, activity.Count); Assert.Contains(first, activity.Keys); Assert.Contains(second, activity.Keys);
        }
        /// <summary>The adjustable duration is respected at both ends of the slider range.</summary>
        [Theory]
        [InlineData(100)]
        [InlineData(1200)]
        [InlineData(2000)]
        public void Settling_period_controls_when_state_changes_can_identify(int milliseconds)
        {
            var id = Guid.NewGuid(); var time = new IdentificationTestTime();
            var identification = new ControllerInputIdentification([id], time, TimeSpan.FromMilliseconds(milliseconds));
            identification.Sample([Frame(id, 0, .5)]);
            time.Advance(milliseconds - 1); Assert.Empty(identification.Sample([Frame(id, 1, .5)]));
            time.Advance(milliseconds); Assert.Empty(identification.Sample([Frame(id, 1, .5)]));
            Assert.Equal("JOY_BTN1", identification.Sample([Frame(id, 0, .5)])[id]);
        }
        /// <summary>Invalid identity sets, durations and missing or duplicate samples fail explicitly.</summary>
        [Fact]
        public void Rejects_invalid_configuration_or_disconnected_frames()
        {
            var id = Guid.NewGuid();
            Assert.Throws<ArgumentException>(() => new ControllerInputIdentification([]));
            Assert.Throws<ArgumentException>(() => new ControllerInputIdentification([Guid.Empty]));
            Assert.Throws<ArgumentException>(() => new ControllerInputIdentification([id, id]));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ControllerInputIdentification([id], settlingTime: TimeSpan.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ControllerInputIdentification([id], settlingTime: TimeSpan.FromSeconds(3)));
            var identification = new ControllerInputIdentification([id]);
            Assert.Throws<InvalidOperationException>(() => identification.Sample([]));
            Assert.Throws<InvalidOperationException>(() => identification.Sample([Frame(id, 0, 0), Frame(id, 1, 1)]));
        }
        /// <summary>Preferences survive reopening and old documents receive the half-second default.</summary>
        [Fact]
        public void Settling_preference_is_persisted_and_validated()
        {
            var root = Path.Combine(Path.GetTempPath(), "identify-preference-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new FileGameWorkspaceStore(root);
                Assert.Equal(.5, store.ReadSelection().EliteIdentificationSettlingSeconds);
                store.SaveSelection(new() { EliteIdentificationSettlingSeconds = 1.2 });
                Assert.Equal(1.2, new FileGameWorkspaceStore(root).ReadSelection().EliteIdentificationSettlingSeconds);
                Assert.Equal(.5, System.Text.Json.JsonSerializer.Deserialize<GameSelectionState>("{\"BackendId\":\"dcs\"}")!.EliteIdentificationSettlingSeconds);
                foreach (var value in new[] { 0, .09, 2.1, double.NaN, double.PositiveInfinity })
                    Assert.Throws<InvalidDataException>(() => store.SaveSelection(new() { EliteIdentificationSettlingSeconds = value }));
                Assert.Equal(1.2, store.ReadSelection().EliteIdentificationSettlingSeconds);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DeviceInputFrame Frame(Guid id, double button, double axis) =>
            new(id, new Dictionary<string, double> { ["JOY_BTN1"] = button, ["JOY_X"] = axis });
        #endregion END private helpers
    }
}
