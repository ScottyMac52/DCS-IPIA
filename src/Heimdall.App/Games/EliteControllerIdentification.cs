using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Heimdall.Application;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Games
{
    /// <summary>Owns foreground discovery across all attached controllers independently of existing ED links.</summary>
    internal sealed class EliteControllerIdentification : IDisposable
    {
        private readonly Window _owner;
        private readonly Func<nint, IReadOnlyList<Guid>, IDeviceInputSource> _factory;
        private readonly Action<IReadOnlyDictionary<DetectedDcsDevice, string>> _activity;
        private readonly Action _clear;
        private readonly Action<string> _status;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(30) };
        private IDeviceInputSource? _source;
        private ControllerInputIdentification? _identification;
        private IReadOnlyList<DetectedDcsDevice> _devices = [];
        internal EliteControllerIdentification(Window owner, Action<IReadOnlyDictionary<DetectedDcsDevice, string>> activity,
            Action clear, Action<string> status, Func<nint, IReadOnlyList<Guid>, IDeviceInputSource>? factory = null)
        {
            _owner = owner; _activity = activity; _clear = clear; _status = status;
            _factory = factory ?? ((handle, devices) => new WindowsDeviceInputSource(handle, devices));
            _timer.Tick += Poll; _owner.Deactivated += Deactivated;
        }
        #region BEGIN Public
        /// <summary>Gets whether identification owns live input handles.</summary>
        internal bool Running => _source is not null;
        /// <summary>Acquires every eligible attached instance and establishes neutral baselines before listening.</summary>
        internal void Start(IReadOnlyList<DetectedDcsDevice> observed)
        {
            Stop(); EditionAccess.Policy.RequirePremium();
            _devices = observed.Where(x => x.Connected && EditionAccess.Policy.AllowsDevice(x)).ToArray();
            var ids = _devices.Select(x => x.PhysicalGuid).ToArray();
            _identification = new(ids);
            try
            {
                _source = _factory(new WindowInteropHelper(_owner).EnsureHandle(), ids);
                _identification.Sample(_source.Read());
                _timer.Start();
            }
            catch { Stop(); throw; }
        }
        /// <summary>Samples all participants without ever choosing the first discovered controller.</summary>
        internal void Sample()
        {
            if (_source is null) return;
            try
            {
                var active = _identification!.Sample(_source.Read());
                if (active.Count > 0) _activity(active.ToDictionary(x => _devices.Single(d => d.PhysicalGuid == x.Key), x => x.Value));
            }
            catch { Stop(); throw; }
        }
        /// <summary>Releases handles and invalidates any pending confirmation.</summary>
        internal void Stop()
        {
            _timer.Stop(); _source?.Dispose(); _source = null; _identification = null; _devices = []; _clear();
        }
        /// <summary>Releases native resources and foreground lifecycle subscriptions.</summary>
        public void Dispose() { Stop(); _timer.Tick -= Poll; _owner.Deactivated -= Deactivated; }
        #endregion END Public
        #region BEGIN private helpers
        private void Deactivated(object? sender, EventArgs e)
        {
            if (_source is null) return;
            Stop(); _status("Controller identification stopped on focus loss. Identify again before linking.");
        }
        private void Poll(object? sender, EventArgs e)
        {
            try { Sample(); }
            catch (Exception error) { _status("Controller identification stopped: " + error.Message); }
        }
        #endregion END private helpers
    }
}
