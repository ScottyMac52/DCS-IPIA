using Heimdall.Application;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Smoke
{
    /// <summary>Supplies exact native instance/manufacturer evidence for isolated game UI tests.</summary>
    /// <param name="devices">Observed controllers, including indistinguishable products.</param>
    internal sealed class GameSmokeDiscovery(params DetectedDcsDevice[] devices) : IConnectedDeviceDiscovery
    {
        #region BEGIN Public
        /// <summary>Gets or sets fresh observations for disconnect and rescan scenarios.</summary>
        public IReadOnlyList<DetectedDcsDevice> Devices { get; set; } = devices;
        /// <summary>Returns deterministic attached units without choosing one.</summary>
        public IReadOnlyList<DetectedDcsDevice> Discover() => Devices;
        #endregion END Public
    }
}
