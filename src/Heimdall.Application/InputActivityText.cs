using System.Globalization;

namespace Heimdall.Application
{
    /// <summary>Maintains a bounded raw-input activity display independently of hardware artwork or assignments.</summary>
    public sealed class InputActivityText(string deviceName, Guid physicalGuid)
    {
        private readonly Queue<string> _events = new();
        private readonly Dictionary<string, double> _reported = new(StringComparer.Ordinal);
        #region BEGIN Public
        /// <summary>Gets the exact device identity and the most recent twenty input transitions.</summary>
        public string Text => deviceName + Environment.NewLine + physicalGuid + Environment.NewLine +
            (_events.Count == 0 ? "Waiting for input…" : string.Join(Environment.NewLine, _events));
        /// <summary>Records digital transitions and axis changes exceeding 1.5 percent since the last report.</summary>
        /// <param name="values">One complete sample from the explicitly selected physical controller; axes range from zero to one.</param>
        public void Sample(IReadOnlyDictionary<string, double> values)
        {
            foreach (var (key, value) in values.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var digital = key.StartsWith("JOY_BTN", StringComparison.Ordinal);
                if (!_reported.TryGetValue(key, out var previous))
                {
                    _reported[key] = value;
                    if (!digital || value < .5) continue;
                    previous = 0;
                }
                if (digital ? (previous >= .5) == (value >= .5) : Math.Abs(previous - value) <= .015) continue;
                _reported[key] = value;
                var state = digital ? (value >= .5 ? "Pressed" : "Released") :
                    "Axis " + value.ToString("F3", CultureInfo.InvariantCulture) + " (" + (value * 100).ToString("F1", CultureInfo.InvariantCulture) + "%)";
                _events.Enqueue(key + " · " + state);
                while (_events.Count > 20) _events.Dequeue();
            }
        }
        #endregion END Public
    }
}
