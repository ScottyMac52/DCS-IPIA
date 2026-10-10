using System.Windows.Input;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;

namespace DcsIpia.Presentation
{
    /// <summary>Owns the live input-test session, polling, highlight tracking, and keyboard modifier mapping for the module workspace.</summary>
    public sealed class InputTestController
    {
        private readonly System.Windows.Threading.DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
        private IDeviceInputSource? _inputSource;
        private InputTestSession? _session;
        private GeneratedInputTestPlan? _plan;
        private Guid? _feedbackDevice;
        private readonly Dictionary<(Guid Device, string Key), DateTime> _liveAxisUntil = [];
        private readonly HashSet<(Guid Device, string Key)> _livePressedInputs = [];
        private readonly Dictionary<string, Key> _testKeyboard = new(StringComparer.Ordinal);

        #region BEGIN Public
        /// <summary>Raised when the controller needs the window to clear or update the input-test log and status.</summary>
        public event Action<string>? StatusChanged;

        /// <summary>Raised when live input highlights should be refreshed for the current device.</summary>
        public event Action? HighlightsChanged;

        /// <summary>Gets whether an input test is currently running.</summary>
        public bool IsRunning => _session is not null;

        /// <summary>Starts a new input-test session from the supplied plan and device set.</summary>
        /// <param name="plan">Generated plan from the frozen snapshot.</param>
        /// <param name="devices">Physical device GUIDs that are currently attached.</param>
        /// <param name="inputSource">Foreground input source (or null to use the default Windows adapter).</param>
        /// <param name="windowHandle">Window handle for the default input source.</param>
        public void Start(GeneratedInputTestPlan plan, IReadOnlyList<Guid> devices, IDeviceInputSource? inputSource, nint windowHandle)
        {
            Stop("Preparing generated Lua…");
            _plan = plan;
            _session = new(plan.Bindings, plan.Modifiers);
            _testKeyboard.Clear();
            _inputSource = inputSource ?? new WindowsDeviceInputSource(windowHandle, devices);
            var diagnostics = new List<string>(plan.Diagnostics);
            // Keyboard modifiers and unsupported-input diagnostics are collected by the caller before Start.
            StatusChanged?.Invoke($"Testing at {DateTime.Now:T} · {devices.Count} controllers.");
            _timer.Tick += OnTick;
            _timer.Start();
            OnTick(this, EventArgs.Empty);
        }

        /// <summary>Stops the current session and raises a status message.</summary>
        /// <param name="status">Status text to surface.</param>
        public void Stop(string status)
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
            _session?.Reset();
            _session = null;
            _plan = null;
            _feedbackDevice = null;
            _inputSource?.Dispose();
            _inputSource = null;
            _testKeyboard.Clear();
            _liveAxisUntil.Clear();
            _livePressedInputs.Clear();
            StatusChanged?.Invoke(status);
            HighlightsChanged?.Invoke();
        }

        /// <summary>Maps a DCS keyboard modifier name to a WPF Key, or null if unsupported.</summary>
        /// <param name="name">DCS input key name.</param>
        /// <returns>Corresponding Key or null.</returns>
        public static Key? KeyboardTestKey(string name) => name switch
        {
            "LShift" => Key.LeftShift, "RShift" => Key.RightShift,
            "LCtrl" => Key.LeftCtrl, "RCtrl" => Key.RightCtrl,
            "LAlt" => Key.LeftAlt, "RAlt" => Key.RightAlt,
            "LWin" => Key.LWin, "RWin" => Key.RWin,
            "Enter" => Key.Return, "Backspace" => Key.Back,
            _ => Enum.TryParse<Key>(name, true, out var key) && key != Key.None ? key : null
        };
        #endregion END Public

        #region BEGIN private helpers
        private void OnTick(object? sender, EventArgs e)
        {
            if (_inputSource is null || _session is null || _plan is null) return;
            try
            {
                var frames = _inputSource.Read().ToList();
                frames.Add(new(Guid.Empty, _testKeyboard.ToDictionary(x => x.Key, x => Keyboard.IsKeyDown(x.Value) ? 1.0 : 0.0)));
                // Feedback device and highlight refresh are driven by the window via HighlightsChanged.
                var results = _session.ProcessDevice(frames, _feedbackDevice);
                foreach (var result in results)
                {
                    if (result.Value > 0.5 && !result.IsAxis) _livePressedInputs.Add((result.PhysicalGuid, result.InputKey));
                    else if (!result.IsAxis) _livePressedInputs.Remove((result.PhysicalGuid, result.InputKey));
                    if (result.IsAxis) _liveAxisUntil[(result.PhysicalGuid, result.InputKey)] = DateTime.Now.AddMilliseconds(150);
                }
                var now = DateTime.Now;
                foreach (var key in _liveAxisUntil.Where(x => x.Value < now).Select(x => x.Key).ToArray())
                    _liveAxisUntil.Remove(key);
                HighlightsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Stop("Input test stopped: " + ex.Message);
            }
        }
        #endregion END private helpers
    }
}
