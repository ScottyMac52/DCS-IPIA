using Heimdall.Application;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Smoke
{
    /// <summary>Supplies exact native instance/manufacturer evidence for isolated game UI tests.</summary>
    /// <param name="devices">Observed controllers, including indistinguishable products.</param>
    internal sealed class GameSmokeDiscovery(params DetectedDcsDevice[] devices) : IConnectedDeviceDiscovery, IControllerCapabilityDiscovery
    {
        #region BEGIN Public
        /// <summary>Gets or sets fresh observations for disconnect and rescan scenarios.</summary>
        public IReadOnlyList<DetectedDcsDevice> Devices { get; set; } = devices;
        /// <summary>Gets independently supplied capability evidence; missing entries remain unverified.</summary>
        public Dictionary<Guid, ControllerCapabilities> Inputs { get; } = [];
        /// <summary>Gets or sets asynchronous scan evidence for failure, stale-completion and close scenarios.</summary>
        public Func<Task<IReadOnlyList<ControllerObservation>>>? InputScan { get; set; }
        /// <summary>Returns synthetic capabilities without assuming every connected device has every control.</summary>
        public Task<IReadOnlyList<ControllerObservation>> DiscoverAsync(CancellationToken ct = default) => InputScan?.Invoke() ??
            Task.FromResult<IReadOnlyList<ControllerObservation>>(Devices.Select(x => new ControllerObservation(x, Inputs.GetValueOrDefault(x.PhysicalGuid))).ToArray());
        /// <summary>Returns deterministic attached units without choosing one.</summary>
        public IReadOnlyList<DetectedDcsDevice> Discover() => Devices;
        #endregion END Public
    }
}
