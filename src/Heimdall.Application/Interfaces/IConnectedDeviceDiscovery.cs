namespace Heimdall.Application.Interfaces
{
    /// <summary>Enumerates attached native controller identities without opening or acquiring their controls.</summary>
    public interface IConnectedDeviceDiscovery
    {
        #region BEGIN Public
        /// <summary>Reads native instance GUIDs and names.</summary>
        /// <returns>Currently attached game controllers.</returns>
        IReadOnlyList<DetectedDcsDevice> Discover();
        #endregion END Public
    }
}
