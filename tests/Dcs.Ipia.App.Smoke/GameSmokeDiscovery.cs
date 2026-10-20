using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Supplies exact native instance/manufacturer evidence for isolated game UI tests.</summary>
    /// <param name="device">Observed controller.</param>
    internal sealed class GameSmokeDiscovery(DetectedDcsDevice device) : IConnectedDeviceDiscovery
    {
        #region BEGIN Public
        /// <summary>Returns one deterministic attached controller.</summary>
        public IReadOnlyList<DetectedDcsDevice> Discover() => [device];
        #endregion END Public
    }
}
