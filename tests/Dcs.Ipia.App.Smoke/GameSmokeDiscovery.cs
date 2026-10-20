using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Supplies exact native instance/manufacturer evidence for isolated game UI tests.</summary>
    /// <param name="devices">Observed controllers, including indistinguishable products.</param>
    internal sealed class GameSmokeDiscovery(params DetectedDcsDevice[] devices) : IConnectedDeviceDiscovery
    {
        #region BEGIN Public
        /// <summary>Returns deterministic attached units without choosing one.</summary>
        public IReadOnlyList<DetectedDcsDevice> Discover() => devices;
        #endregion END Public
    }
}
