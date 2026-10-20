namespace Dcs.Ipia.Application
{
    /// <summary>Identifies deliberate activity across exact controller instances without requiring profile associations.</summary>
    public sealed class ControllerInputIdentification
    {
        private readonly Guid[] _devices;
        private readonly Dictionary<Guid, Dictionary<string, double>> _baselines = [];
        /// <summary>Starts an independent identification session for a nonempty set of unique physical controllers.</summary>
        public ControllerInputIdentification(IReadOnlyList<Guid> devices)
        {
            if (devices.Count == 0 || devices.Contains(Guid.Empty) || devices.Distinct().Count() != devices.Count)
                throw new ArgumentException("Identification requires unique connected controller instances.", nameof(devices));
            _devices = devices.ToArray();
        }
        #region BEGIN Public
        /// <summary>Reports new presses, hat directions and axis movement exceeding ten percent since the last baseline.</summary>
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
                if (!_baselines.TryGetValue(id, out var baseline)) _baselines[id] = baseline = new(StringComparer.Ordinal);
                foreach (var (key, value) in frame.Inputs.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    if (!baseline.TryGetValue(key, out var previous)) { baseline[key] = value; continue; }
                    var digital = key.StartsWith("JOY_BTN", StringComparison.Ordinal);
                    var moved = digital ? value >= .5 && previous < .5 : Math.Abs(value - previous) > .1;
                    if (digital || moved) baseline[key] = value;
                    if (moved) active.TryAdd(id, key);
                }
            }
            return active;
        }
        #endregion END Public
    }
}
