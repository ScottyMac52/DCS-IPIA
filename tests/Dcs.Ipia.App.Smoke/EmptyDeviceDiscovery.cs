using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Provides deterministic offline controller discovery for creation UI smoke.</summary>
    public sealed class EmptyDeviceDiscovery : IConnectedDeviceDiscovery
    {
        #region BEGIN Public
        /// <summary>Returns no attached controllers; tests use captured native filenames.</summary>
        /// <returns>An empty physical-device list.</returns>
        public IReadOnlyList<DetectedDcsDevice> Discover() => [];
        #endregion END Public
    }
}
