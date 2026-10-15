using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace DcsIpia.Presentation
{
    /// <summary>Supplies scripted sample-controller states to the actual input tester without native acquisition.</summary>
    public sealed class TrainingInputSource : IDeviceInputSource
    {
        private readonly Dictionary<string, double> _values = Enumerable.Range(1, 10)
            .ToDictionary(index => "JOY_BTN" + index, _ => 0.0, StringComparer.Ordinal);
        private bool _disposed;
        /// <summary>Initializes both stick axes at their neutral midpoint.</summary>
        public TrainingInputSource()
        {
            _values["JOY_X"] = 0.5;
            _values["JOY_Y"] = 0.5;
        }
        #region BEGIN Public
        /// <inheritdoc/>
        public IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> SupportedInputs =>
            new Dictionary<Guid, IReadOnlyCollection<string>> { [TrainingDiscovery.Unit.PhysicalGuid] = _values.Keys.ToArray() };
        /// <summary>Changes one supported sample input; no real device is touched.</summary>
        public void Set(string key, double value)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_values.ContainsKey(key) || !double.IsFinite(value) || value < 0 || value > 1)
                throw new ArgumentOutOfRangeException(nameof(value));
            _values[key] = value;
        }
        /// <inheritdoc/>
        public IReadOnlyList<DeviceInputFrame> Read()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return [new(TrainingDiscovery.Unit.PhysicalGuid, new Dictionary<string, double>(_values, StringComparer.Ordinal))];
        }
        /// <inheritdoc/>
        public void Dispose() => _disposed = true;
        #endregion END Public
    }
}
