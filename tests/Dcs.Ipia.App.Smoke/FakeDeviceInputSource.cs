using System.IO;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Supplies deterministic button activity and disconnects for the Windows test UI.</summary>
    /// <param name="physical">Native device identity.</param>
    /// <param name="otherDevice">Optional modifier controller with noisy axes.</param>
    public sealed class FakeDeviceInputSource(Guid physical, Guid? otherDevice = null) : IDeviceInputSource
    {
        private int _reads;
        #region BEGIN Public
        /// <summary>Gets whether Stop/focus loss released the input source.</summary>
        public bool Disposed { get; private set; }
        /// <summary>Gets or sets an injected device loss.</summary>
        public bool Fail { get; set; }
        /// <summary>Gets the supported native inputs.</summary>
        public IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> SupportedInputs
        {
            get
            {
                var inputs = new Dictionary<Guid, IReadOnlyCollection<string>> { [physical] = new[] { "JOY_BTN1" } };
                if (otherDevice is { } other) inputs.Add(other, new[] { "JOY_BTN1", "JOY_X" });
                return inputs;
            }
        }
        /// <summary>Returns an initial neutral sample followed by a held press.</summary>
        /// <returns>One complete controller sample.</returns>
        public IReadOnlyList<DeviceInputFrame> Read()
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            if (Fail) throw new IOException("Injected disconnect.");
            var frames = new List<DeviceInputFrame> { new(physical, new Dictionary<string, double> { ["JOY_BTN1"] = _reads++ == 0 ? 0 : 1 }) };
            if (otherDevice is { } other)
                frames.Add(new(other, new Dictionary<string, double> { ["JOY_BTN1"] = 0, ["JOY_X"] = _reads % 2 == 0 ? 0.25 : 0.75 }));
            return frames;
        }
        /// <summary>Records native handle release.</summary>
        public void Dispose() => Disposed = true;
        #endregion END Public
    }
}
