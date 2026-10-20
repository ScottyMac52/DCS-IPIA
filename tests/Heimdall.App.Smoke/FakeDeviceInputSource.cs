using System.IO;
using Heimdall.Application;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Smoke
{
    /// <summary>Supplies deterministic button activity and disconnects for the Windows test UI.</summary>
    /// <param name="physical">Native device identity.</param>
    /// <param name="otherDevice">Optional modifier controller with noisy axes.</param>
    public sealed class FakeDeviceInputSource(Guid physical, Guid? otherDevice = null) : IDeviceInputSource
    {
        private static readonly string[] ButtonInputs = ["JOY_BTN1", "JOY_BTN2", "JOY_X"];
        private static readonly string[] ModifierInputs = ["JOY_BTN1", "JOY_X"];
        private int _reads;
        #region BEGIN Public
        /// <summary>Gets whether Stop/focus loss released the input source.</summary>
        public bool Disposed { get; private set; }
        /// <summary>Gets or sets an injected device loss.</summary>
        public bool Fail { get; set; }
        /// <summary>Gets or sets a controlled button value after the initial sample.</summary>
        public double? ButtonValue { get; set; }
        /// <summary>Controlled hold value on the separate physical modifier controller.</summary>
        public double ModifierValue { get; set; }
        /// <summary>Gets the supported native inputs.</summary>
        public IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> SupportedInputs
        {
            get
            {
                var inputs = new Dictionary<Guid, IReadOnlyCollection<string>> { [physical] = ButtonInputs };
                if (otherDevice is { } other) inputs.Add(other, ModifierInputs);
                return inputs;
            }
        }
        /// <summary>Returns an initial neutral sample followed by a held press.</summary>
        /// <returns>One complete controller sample.</returns>
        public IReadOnlyList<DeviceInputFrame> Read()
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            if (Fail) throw new IOException("Injected disconnect.");
            var value = ButtonValue ?? (_reads == 0 ? 0 : 1);
            _reads++;
            var frames = new List<DeviceInputFrame> { new(physical, new Dictionary<string, double> { ["JOY_BTN1"] = value, ["JOY_BTN2"] = 1, ["JOY_X"] = 0.5 }) };
            if (otherDevice is { } other)
                frames.Add(new(other, new Dictionary<string, double> { ["JOY_BTN1"] = ModifierValue, ["JOY_X"] = _reads % 2 == 0 ? 0.25 : 0.75 }));
            return frames;
        }
        /// <summary>Records native handle release.</summary>
        public void Dispose() => Disposed = true;
        #endregion END Public
    }
}
