using System.IO;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Provides independently controlled discovery frames for every acquired physical instance.</summary>
    public sealed class IdentificationInputSource : IDeviceInputSource
    {
        /// <summary>Creates neutral samples for every requested controller.</summary>
        public IdentificationInputSource(IReadOnlyList<Guid> devices)
        {
            Values = devices.ToDictionary(x => x, _ => new Dictionary<string, double> { ["JOY_BTN1"] = 0, ["JOY_X"] = .5 });
        }
        #region BEGIN Public
        /// <summary>Gets independently mutable device values for deterministic interaction tests.</summary>
        public Dictionary<Guid, Dictionary<string, double>> Values { get; }
        /// <summary>Gets whether lifecycle changes released the native source.</summary>
        public bool Disposed { get; private set; }
        /// <summary>Gets or sets an injected controller disconnect.</summary>
        public bool Fail { get; set; }
        /// <summary>Gets supported input keys for each exact instance.</summary>
        public IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> SupportedInputs =>
            Values.ToDictionary(x => x.Key, x => (IReadOnlyCollection<string>)x.Value.Keys.ToArray());
        /// <summary>Returns complete samples or an injected native failure.</summary>
        public IReadOnlyList<DeviceInputFrame> Read()
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            if (Fail) throw new IOException("Injected identification disconnect.");
            return Values.Select(x => new DeviceInputFrame(x.Key, x.Value)).ToArray();
        }
        /// <summary>Records source disposal.</summary>
        public void Dispose() => Disposed = true;
        #endregion END Public
    }
}
