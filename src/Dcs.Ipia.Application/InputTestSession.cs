using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application
{
    /// <summary>Resolves complete physical samples against a frozen generated payload.</summary>
    public sealed class InputTestSession
    {
        private readonly InputTestBinding[] _bindings;
        private readonly InputTestModifier[] _modifiers;
        private readonly Dictionary<Guid, IReadOnlyDictionary<string, double>> _previous = [];
        private readonly Dictionary<(Guid Device, string Input), double> _reportedAxes = [];
        private readonly Dictionary<(Guid Device, string Input), IReadOnlyList<InputTestBinding>> _pressed = [];
        private readonly HashSet<(AssignmentLayer Layer, string Name)> _toggles = [];
        private readonly Dictionary<AssignmentLayer, string> _active = [];
        /// <summary>Freezes parsed bindings and effective modifier definitions.</summary>
        /// <param name="bindings">Generated profile entries.</param>
        /// <param name="modifiers">Generated modifier entries.</param>
        public InputTestSession(IEnumerable<InputTestBinding> bindings, IEnumerable<InputTestModifier> modifiers)
        {
            _bindings = bindings.Where(x => x.Binding.Disposition != BindingDisposition.Removed).ToArray();
            _modifiers = modifiers.ToArray();
        }
        #region BEGIN Public
        /// <summary>Gets currently active modifier names separately for each scope.</summary>
        public IReadOnlyDictionary<AssignmentLayer, string> ActiveModifiers => _active;
        /// <summary>Resolves transitions after atomically sampling all modifier devices.</summary>
        /// <param name="frames">Complete sample from every connected session device.</param>
        /// <returns>Digital transitions and significant axis positions.</returns>
        public IReadOnlyList<InputTestResult> Process(IReadOnlyList<DeviceInputFrame> frames)
        {
            if (frames.Select(x => x.PhysicalGuid).Distinct().Count() != frames.Count ||
                _previous.Any(x => frames.All(f => f.PhysicalGuid != x.Key || x.Value.Keys.Any(k => !f.Inputs.ContainsKey(k)))) ||
                frames.Any(x => x.Inputs.Any(v => !double.IsFinite(v.Value) || v.Value < 0 || v.Value > 1)))
                throw new InvalidDataException("Incomplete or invalid input sample; restart the test session.");
            var sampled = frames.ToDictionary(x => x.PhysicalGuid, x => x.Inputs);
            var oldChords = new Dictionary<AssignmentLayer, string>(_active);
            foreach (var modifier in _modifiers)
            {
                var now = Value(sampled, modifier.PhysicalGuid, modifier.InputKey) > 0.5;
                var was = Value(_previous, modifier.PhysicalGuid, modifier.InputKey) > 0.5;
                if (modifier.Mode == ModifierMode.Toggle && _previous.ContainsKey(modifier.PhysicalGuid) && now && !was)
                {
                    var identity = (modifier.Layer, modifier.Name);
                    if (!_toggles.Remove(identity)) _toggles.Add(identity);
                }
            }
            foreach (var layer in Enum.GetValues<AssignmentLayer>())
                _active[layer] = string.Join("+", _modifiers.Where(x => x.Layer == layer &&
                    (x.Mode == ModifierMode.Toggle ? _toggles.Contains((layer, x.Name)) : Value(sampled, x.PhysicalGuid, x.InputKey) > 0.5))
                    .Select(x => x.Name).Order(StringComparer.Ordinal));
            var results = new List<InputTestResult>();
            foreach (var frame in frames)
                foreach (var (key, value) in frame.Inputs)
                {
                    var axis = key.StartsWith("JOY_", StringComparison.Ordinal) && !key.StartsWith("JOY_BTN", StringComparison.Ordinal) &&
                        !key.StartsWith("JOY_POV", StringComparison.Ordinal);
                    var identity = (frame.PhysicalGuid, key);
                    var known = _previous.TryGetValue(frame.PhysicalGuid, out var prior);
                    var old = prior?.GetValueOrDefault(key) ?? 0;
                    var chordChanged = _active.Any(x => oldChords.GetValueOrDefault(x.Key) != x.Value);
                    if (axis ? _reportedAxes.TryGetValue(identity, out var reported) && Math.Abs(reported - value) < 0.01 && !chordChanged
                             : !known || old == value) continue;
                    var matches = _bindings.Where(x => x.PhysicalGuid == frame.PhysicalGuid &&
                        (x.Binding.Section == BindingSection.Axis) == axis && DcsInputKey.SamePhysicalInput(x.Binding.InputKey, key) &&
                        x.Chord == _active.GetValueOrDefault(x.Layer, "")).ToArray();
                    if (axis) _reportedAxes[identity] = value;
                    else if (value > 0.5) _pressed[identity] = matches;
                    else
                    {
                        matches = _pressed.GetValueOrDefault(identity)?.ToArray() ?? [];
                        _pressed.Remove(identity);
                    }
                    results.Add(new(frame.PhysicalGuid, key, value, axis ? "Position" : value > 0.5 ? "Press" : "Release", matches));
                }
            _previous.Clear();
            foreach (var frame in frames) _previous.Add(frame.PhysicalGuid, new Dictionary<string, double>(frame.Inputs));
            return results;
        }
        /// <summary>Samples all modifier devices while returning feedback only for the selected physical device.</summary>
        /// <param name="frames">Complete sample from every connected session device.</param>
        /// <param name="physicalDevice">Exact selected instance, or null for an unresolved selection.</param>
        /// <returns>Selected-device transitions; other devices still update modifier state.</returns>
        public IReadOnlyList<InputTestResult> ProcessDevice(IReadOnlyList<DeviceInputFrame> frames, Guid? physicalDevice) =>
            Process(frames).Where(x => x.PhysicalGuid == physicalDevice).ToArray();
        /// <summary>Clears held, toggle, press and noise state after stop/focus loss/disconnect.</summary>
        public void Reset()
        {
            _previous.Clear(); _reportedAxes.Clear(); _pressed.Clear(); _toggles.Clear(); _active.Clear();
        }
        #endregion END Public
        #region BEGIN private helpers
        private static double Value(Dictionary<Guid, IReadOnlyDictionary<string, double>> frames, Guid device, string input) =>
            frames.TryGetValue(device, out var values) ? values.FirstOrDefault(x => DcsInputKey.SamePhysicalInput(x.Key, input)).Value : 0;
        #endregion END private helpers
    }
}
