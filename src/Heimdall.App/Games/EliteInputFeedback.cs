using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Application.Games;

namespace Heimdall.App.Games
{
    /// <summary>Foreground-only raw hardware feedback; it does not emulate native Elite command execution.</summary>
    internal sealed class EliteInputFeedback : IDisposable
    {
        private readonly Window _owner;
        private readonly HardwareDiagramPresenter _diagram;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(30) };
        private readonly Action<string> _status;
        private readonly Action<string> _activity;
        private InputActivityText? _activityText;
        private readonly Func<nint, IReadOnlyList<Guid>, IDeviceInputSource> _factory;
        private IDeviceInputSource? _source;
        private Dictionary<string, double> _previous = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTime> _axisUntil = new(StringComparer.Ordinal);
        private Guid _physical;
        internal EliteInputFeedback(Window owner, HardwareDiagramPresenter diagram, Action<string> status, Action<string> activity,
            Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? factory = null)
        {
            _owner = owner; _diagram = diagram; _status = status; _activity = activity;
            _factory = factory ?? ((handle, devices) => new WindowsDeviceInputSource(handle, devices));
            _timer.Tick += Poll; owner.Deactivated += Deactivated;
        }
        #region BEGIN Public
        /// <summary>Raised after every transition into or out of sampling, including focus loss and disconnect.</summary>
        internal event EventHandler? StateChanged;
        /// <summary>Gets whether native feedback is currently sampling.</summary>
        internal bool Running => _source is not null;
        /// <summary>Starts only after Owner entitlement and one exact attached instance are verified.</summary>
        internal void Start(GameHardwarePin pin, IReadOnlyList<DetectedDcsDevice> observed)
        {
            Stop(); EditionAccess.Policy.RequirePremium();
            if (!GameInputReadiness.Ready(pin, observed))
                throw new InvalidOperationException("Connect the explicitly associated controller and rescan before testing.");
            _physical = pin.Device.PhysicalGuid;
            _source = _factory(new WindowInteropHelper(_owner).EnsureHandle(), [_physical]);
            _activityText = new(pin.Device.Name, _physical); _activity(_activityText.Text);
            StateChanged?.Invoke(this, EventArgs.Empty);
            _timer.Start(); _status("Live hardware feedback · " + pin.Device.Name + ". Move an axis or press a button; feedback appears on the drawing or in the activity text.");
        }
        /// <summary>Stops sampling and clears feedback before view, device, profile or focus changes.</summary>
        internal void Stop()
        {
            var wasRunning = _source is not null;
            _timer.Stop(); _source?.Dispose(); _source = null; _previous.Clear(); _axisUntil.Clear();
            _activityText = null; _activity("");
            foreach (var region in _diagram.Regions) region.Outline.BorderBrush = Brushes.Transparent;
            if (wasRunning) { _status("Input test stopped."); StateChanged?.Invoke(this, EventArgs.Empty); }
        }
        /// <summary>Releases all native handles and event subscriptions.</summary>
        public void Dispose() { Stop(); _timer.Tick -= Poll; _owner.Deactivated -= Deactivated; }
        /// <summary>Processes one sample, also used by deterministic Windows smoke checks.</summary>
        internal void Sample()
        {
            if (_source is null) return;
            try
            {
                var frames = _source.Read().Where(x => x.PhysicalGuid == _physical).ToArray();
                if (frames.Length != 1)
                {
                    Stop(); throw new InvalidOperationException("The associated controller disconnected or returned ambiguous samples.");
                }
                var values = frames[0].Inputs;
                _activityText!.Sample(values); _activity(_activityText.Text);
                foreach (var (control, border) in _diagram.Regions)
                {
                    var pair = values.FirstOrDefault(x => DcsInputKey.SamePhysicalInput(x.Key, control.InputKey));
                    var active = pair.Key is not null && pair.Value >= .5;
                    if (control.IsAxis)
                    {
                        if (pair.Key is not null && _previous.TryGetValue(pair.Key, out var previous) && Math.Abs(previous - pair.Value) > .015)
                            _axisUntil[control.InputKey] = DateTime.UtcNow.AddMilliseconds(500);
                        active = _axisUntil.GetValueOrDefault(control.InputKey) > DateTime.UtcNow;
                    }
                    border.BorderBrush = active ? Brushes.DeepSkyBlue : Brushes.Transparent;
                }
                _previous = new(values, StringComparer.Ordinal);
            }
            catch { Stop(); throw; }
        }
        #endregion END Public
        #region BEGIN private helpers
        private void Deactivated(object? sender, EventArgs e) { Stop(); _status("Input test stopped on focus loss."); }
        private void Poll(object? sender, EventArgs e)
        {
            try { Sample(); }
            catch (Exception error) { Stop(); _status("Input test stopped: " + error.Message); }
        }
        #endregion END private helpers
    }
}
