using Heimdall.Application;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Guards bounded retention, concurrent accounting and valid performance evidence.</summary>
    public sealed class PerformanceCaptureTests
    {
        #region BEGIN Public
        /// <summary>Keeps first-N evidence and accounts for every omitted measurement under concurrent writers.</summary>
        [Fact]
        public void Concurrent_capture_is_bounded_and_accounts_for_dropped_measurements()
        {
            var capture = new PerformanceCapture(16);
            Parallel.For(0, 1000, index => capture.Record("browser.filter", "DCS", index));
            Assert.Equal(16, capture.Snapshot().Length);
            Assert.Equal(984, capture.Dropped);
        }
        /// <summary>Prevents a report consumer from changing retained capture history.</summary>
        [Fact]
        public void Snapshots_are_independent_and_preserve_first_measurements()
        {
            var capture = new PerformanceCapture(1);
            capture.Record("browser.filter", "Elite", 12);
            var snapshot = capture.Snapshot();
            snapshot[0] = new("other", "Shared", 99);
            capture.Record("browser.filter", "Elite", 18);
            Assert.Equal(12, capture.Snapshot()[0].DurationMs);
            Assert.Equal(1, capture.Dropped);
        }
        /// <summary>Rejects malformed duration evidence and invalid capacity before accepting samples.</summary>
        [Fact]
        public void Invalid_capacity_and_nonfinite_durations_are_rejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PerformanceCapture(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PerformanceCapture(100_001));
            var capture = new PerformanceCapture();
            foreach (var value in new[] { -1d, double.NaN, double.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => capture.Record("browser.filter", "Squadrons", value));
            Assert.Empty(capture.Snapshot());
        }
        /// <summary>Records an attempted operation even when its body exits by throwing.</summary>
        [Fact]
        public void Scope_disposal_records_exceptional_exits_without_capturing_exception_details()
        {
            Action failingOperation = () =>
            {
                using var scope = PerformanceTrace.Measure("test.scope", typeof(PerformanceCaptureTests));
                throw new InvalidOperationException("not retained");
            };
            Assert.Throws<InvalidOperationException>(failingOperation);
            var sample = Assert.Single(PerformanceTrace.Snapshot(), sample => sample.Operation == "test.scope");
            Assert.Equal("Shared", sample.Component);
            Assert.True(sample.DurationMs >= 0);
        }
        #endregion END Public
    }
}
