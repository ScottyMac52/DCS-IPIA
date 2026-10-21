namespace Heimdall.Application.Games
{
    /// <summary>Validates fresh exact-instance and product evidence independently of optional artwork.</summary>
    public static class GameInputReadiness
    {
        #region BEGIN Public
        /// <summary>Requires one connected observation matching the previously linked physical unit and product.</summary>
        public static bool Ready(GameHardwarePin? pin, IReadOnlyList<DetectedDcsDevice> observed)
        {
            if (pin is null || pin.Device.PhysicalGuid == Guid.Empty || pin.Device.ProductGuid == Guid.Empty) return false;
            var matches = observed.Where(x => x.Connected && x.PhysicalGuid == pin.Device.PhysicalGuid).ToArray();
            return matches.Length == 1 && matches[0].ProductGuid == pin.Device.ProductGuid;
        }
        #endregion END Public
    }
}
