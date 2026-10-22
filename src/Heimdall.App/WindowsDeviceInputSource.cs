using Heimdall.Application;
using Heimdall.Application.Interfaces;

namespace Heimdall.App
{
    /// <summary>Owns the exact attached controllers participating in one test session.</summary>
    public sealed class WindowsDeviceInputSource : IDeviceInputSource
    {
        private readonly WindowsDirectInputConnection _connection;
        private readonly Dictionary<Guid, WindowsDirectInputController> _controllers = [];
        private bool _disposed;
        /// <summary>Acquires requested devices by exact instance GUID.</summary>
        /// <param name="window">Foreground top-level window.</param>
        /// <param name="devices">Attached physical identities.</param>
        public WindowsDeviceInputSource(nint window, IReadOnlyList<Guid> devices)
        {
            EditionAccess.Policy.RequirePremium();
            _connection = new WindowsDirectInputConnection();
            try
            {
                foreach (var guid in devices.Distinct()) _controllers.Add(guid, new(_connection.CreateDevice(guid), window));
            }
            catch { Dispose(); throw; }
        }
        #region BEGIN Public
        /// <summary>Gets supported inputs for every acquired controller.</summary>
        public IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> SupportedInputs => _controllers.ToDictionary(x => x.Key, x => x.Value.SupportedInputs);
        /// <summary>Reads every controller before the resolver updates modifier state.</summary>
        /// <returns>One complete sample or an acquisition/disconnect error.</returns>
        public IReadOnlyList<DeviceInputFrame> Read()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _controllers.Select(x => new DeviceInputFrame(x.Key, x.Value.Read())).ToArray();
        }
        /// <summary>Releases all controller handles before unloading DirectInput.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            foreach (var controller in _controllers.Values) controller.Dispose();
            _controllers.Clear(); _connection.Dispose(); _disposed = true;
        }
        #endregion END Public
    }
}
