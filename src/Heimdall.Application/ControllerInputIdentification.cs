namespace Heimdall.Application
{
    /// <summary>Identifies deliberate activity across exact controller instances without requiring profile associations.</summary>
    public sealed class ControllerInputIdentification
    {
        private readonly Guid[] _devices;
        private readonly TimeProvider _time;
        private readonly Dictionary<Guid, long> _settling = [];
        private readonly HashSet<Guid> _armed = [];
        private readonly TimeSpan _settlingTime;
        private readonly Dictionary<Guid, Dictionary<string, double>> _baselines = [];
        /// <summary>Starts an independent identification session for a nonempty set of unique physical controllers.</summary>
        public ControllerInputIdentification(IReadOnlyList<Guid> devices, TimeProvider? time = null, TimeSpan? settlingTime = null)
        {
            if (devices.Count == 0 || devices.Contains(Guid.Empty) || devices.Distinct().Count() != devices.Count)
                throw new ArgumentException("Identification requires unique connected controller instances.", nameof(devices));
            _settlingTime = settlingTime ?? TimeSpan.FromMilliseconds(500);
            if (_settlingTime < TimeSpan.FromMilliseconds(100) || _settlingTime > TimeSpan.FromSeconds(2))
                throw new ArgumentOutOfRangeException(nameof(settlingTime), "Settling must be between 0.1 and 2.0 seconds.");
            _devices = devices.ToArray(); _time = time ?? TimeProvider.System;
        }
        #region BEGIN Public
        /// <summary>Reports button/hat transitions and axis movement only after each controller establishes a settled baseline.</summary>
        /// <param name="frames">Complete samples from all participating controllers; unrelated and keyboard frames are ignored.</param>
        /// <returns>All active physical instances, retaining ambiguity when multiple devices move together.</returns>
        public IReadOnlyDictionary<Guid, string> Sample(IReadOnlyList<DeviceInputFrame> frames)
        {
            var samples = new Dictionary<Guid, DeviceInputFrame>();
            foreach (var id in _devices)
            {
                var matching = frames.Where(x => x.PhysicalGuid == id).ToArray();
                if (matching.Length != 1) throw new InvalidOperationException("A controller disconnected or returned ambiguous identification samples. Reconnect and identify again.");
                samples.Add(id, matching[0]);
            }
            var active = new Dictionary<Guid, string>();
            foreach (var (id, frame) in samples)
            {
                if (!_baselines.TryGetValue(id, out var baseline))
                {
                    _baselines[id] = new(frame.Inputs, StringComparer.Ordinal); _settling[id] = _time.GetTimestamp();
                    continue;
                }
                if (!_armed.Contains(id))
                {
                    var changed = frame.Inputs.Count != baseline.Count || frame.Inputs.Any(x =>
                        !baseline.TryGetValue(x.Key, out var previous) || (x.Key.StartsWith("JOY_BTN", StringComparison.Ordinal)
                            ? (x.Value >= .5) != (previous >= .5) : Math.Abs(x.Value - previous) > .02));
                    if (changed)
                    {
                        _settling[id] = _time.GetTimestamp(); _baselines[id] = new(frame.Inputs, StringComparer.Ordinal);
                    }
                    if (_time.GetElapsedTime(_settling[id]) >= _settlingTime)
                    {
                        _baselines[id] = new(frame.Inputs, StringComparer.Ordinal); _armed.Add(id);
                    }
                    continue;
                }
                foreach (var (key, value) in frame.Inputs.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    if (!baseline.TryGetValue(key, out var previous)) { baseline[key] = value; continue; }
                    var digital = key.StartsWith("JOY_BTN", StringComparison.Ordinal);
                    var moved = digital ? (value >= .5) != (previous >= .5) : Math.Abs(value - previous) > .1;
                    if (digital || moved) baseline[key] = value;
                    if (moved) active.TryAdd(id, key);
                }
            }
            return active;
        }
        #endregion END Public
    }
}
