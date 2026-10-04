using System.IO;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Supplies deterministic button activity and disconnects for the Windows test UI.</summary>
    /// <param name="physical">Native device identity.</param>
    public sealed class FakeDeviceInputSource(Guid physical) : IDeviceInputSource
    {
        private int _reads;
        #region BEGIN Public
        /// <summary>Gets whether Stop/focus loss released the input source.</summary>
        public bool Disposed { get; private set; }
        /// <summary>Gets or sets an injected device loss.</summary>
        public bool Fail { get; set; }
        /// <summary>Gets the supported native inputs.</summary>
        public IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> SupportedInputs => new Dictionary<Guid, IReadOnlyCollection<string>> { [physical] = new[] { "JOY_BTN1" } };
        /// <summary>Returns an initial neutral sample followed by a held press.</summary>
        /// <returns>One complete controller sample.</returns>
        public IReadOnlyList<DeviceInputFrame> Read()
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            if (Fail) throw new IOException("Injected disconnect.");
            return [new(physical, new Dictionary<string, double> { ["JOY_BTN1"] = _reads++ == 0 ? 0 : 1 })];
        }
        /// <summary>Records native handle release.</summary>
        public void Dispose() => Disposed = true;
        #endregion END Public
    }
}
