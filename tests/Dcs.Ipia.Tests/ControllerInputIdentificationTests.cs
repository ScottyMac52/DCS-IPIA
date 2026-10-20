using Dcs.Ipia.Application;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Verifies discovery from deliberate input without any existing profile association.</summary>
    public sealed class ControllerInputIdentificationTests
    {
        #region BEGIN Public
        /// <summary>Held switches, releases and idle samples cannot identify a controller.</summary>
        [Fact]
        public void Requires_a_new_press_after_the_initial_baseline()
        {
            var id = Guid.NewGuid();
            var identification = new ControllerInputIdentification([id]);
            Assert.Empty(identification.Sample([Frame(id, 1, .5)]));
            Assert.Empty(identification.Sample([Frame(id, 1, .5)]));
            Assert.Empty(identification.Sample([Frame(id, 0, .5)]));
            Assert.Equal("JOY_BTN1", identification.Sample([Frame(id, 1, .5)])[id]);
            Assert.Empty(identification.Sample([Frame(id, 1, .5)]));
        }
        /// <summary>Axis drift is ignored, while gradual intentional movement accumulates from the last baseline.</summary>
        [Fact]
        public void Finds_axis_only_devices_and_filters_jitter()
        {
            var id = Guid.NewGuid();
            var identification = new ControllerInputIdentification([id]);
            Assert.Empty(identification.Sample([Frame(id, 0, .5)]));
            Assert.Empty(identification.Sample([Frame(id, 0, .52)]));
            Assert.Empty(identification.Sample([Frame(id, 0, .56)]));
            Assert.Equal("JOY_X", identification.Sample([Frame(id, 0, .65)])[id]);
            Assert.Empty(identification.Sample([Frame(id, 0, .66)]));
            Assert.Equal("JOY_X", identification.Sample([Frame(id, 0, 0)])[id]);
        }
        /// <summary>Every attached unit is listened to; simultaneous activity stays ambiguous instead of picking discovery order.</summary>
        [Fact]
        public void Reports_exact_instances_hats_and_simultaneous_activity()
        {
            var first = Guid.NewGuid(); var second = Guid.NewGuid(); var unrelated = Guid.NewGuid();
            var identification = new ControllerInputIdentification([first, second]);
            identification.Sample([Frame(first, 0, .5), Frame(second, 0, .5)]);
            var activity = identification.Sample([Frame(first, 1, .5), Frame(second, 1, .5), Frame(unrelated, 1, 1), Frame(Guid.Empty, 1, 1)]);
            Assert.Equal(2, activity.Count);
            Assert.Contains(first, activity.Keys); Assert.Contains(second, activity.Keys);
            identification.Sample([Frame(first, 0, .5), Frame(second, 0, .5)]);
            var hat = new DeviceInputFrame(second, new Dictionary<string, double> { ["JOY_BTN_POV1_U"] = 0 });
            identification.Sample([Frame(first, 0, .5), hat]);
            var pressed = hat with { Inputs = new Dictionary<string, double> { ["JOY_BTN_POV1_U"] = 1 } };
            Assert.Equal("JOY_BTN_POV1_U", identification.Sample([Frame(first, 0, .5), pressed])[second]);
        }
        /// <summary>Missing or duplicate controller frames invalidate a result instead of retaining stale evidence.</summary>
        [Fact]
        public void Rejects_invalid_identity_sets_and_disconnected_or_ambiguous_frames()
        {
            var id = Guid.NewGuid();
            Assert.Throws<ArgumentException>(() => new ControllerInputIdentification([]));
            Assert.Throws<ArgumentException>(() => new ControllerInputIdentification([Guid.Empty]));
            Assert.Throws<ArgumentException>(() => new ControllerInputIdentification([id, id]));
            var identification = new ControllerInputIdentification([id]);
            Assert.Throws<InvalidOperationException>(() => identification.Sample([]));
            Assert.Throws<InvalidOperationException>(() => identification.Sample([Frame(id, 0, 0), Frame(id, 1, 1)]));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DeviceInputFrame Frame(Guid id, double button, double axis) =>
            new(id, new Dictionary<string, double> { ["JOY_BTN1"] = button, ["JOY_X"] = axis });
        #endregion END private helpers
    }
}
