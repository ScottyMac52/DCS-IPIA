namespace Heimdall.Application
{
    /// <summary>Retains a bounded first-N capture and explicitly counts measurements beyond its limit.</summary>
    public sealed class PerformanceCapture
    {
        private readonly Lock _sync = new();
        private readonly List<PerformanceSample> _samples = [];
        private readonly int _capacity;
        private long _dropped;

        /// <summary>Creates a capture retaining at most 100,000 measurements.</summary>
        public PerformanceCapture(int capacity = 100_000)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(capacity, 100_000);
            _capacity = capacity;
        }

        #region BEGIN Public
        /// <summary>Gets the number of measurements omitted after the capture filled.</summary>
        public long Dropped { get { lock (_sync) return _dropped; } }
        /// <summary>Records a finite duration without retaining caller objects or unbounded history.</summary>
        public void Record(string operation, string component, double durationMs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(operation);
            ArgumentException.ThrowIfNullOrWhiteSpace(component);
            if (!double.IsFinite(durationMs) || durationMs < 0) throw new ArgumentOutOfRangeException(nameof(durationMs));
            lock (_sync)
            {
                if (_samples.Count == _capacity) { _dropped++; return; }
                _samples.Add(new(operation, component, durationMs));
            }
        }
        /// <summary>Returns an independent snapshot so reporting cannot mutate retained measurements.</summary>
        public PerformanceSample[] Snapshot() { lock (_sync) return _samples.ToArray(); }
        #endregion END Public
    }
}
