using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace DcsIpia.Presentation
{
    /// <summary>Enumerates attached DirectInput controllers using native instance GUIDs.</summary>
    public sealed class WindowsConnectedDeviceDiscovery : IConnectedDeviceDiscovery
    {
        #region BEGIN Public
        /// <summary>Reads devices without acquiring them or changing DCS files.</summary>
        /// <returns>Native attached controller instances.</returns>
        public IReadOnlyList<DetectedDcsDevice> Discover()
        {
            using var connection = new WindowsDirectInputConnection();
            return connection.Discover();
        }
        #endregion END Public
    }
}
