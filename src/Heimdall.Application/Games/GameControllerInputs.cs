namespace Heimdall.Application.Games
{
    /// <summary>Uses enumerated controls only when their physical instance and product match an explicit workspace link.</summary>
    public static class GameControllerInputs
    {
        #region BEGIN Public
        /// <summary>Returns verified shared input keys; null means unavailable evidence, while an empty collection means no supported controls.</summary>
        public static IReadOnlyCollection<string>? VerifiedKeys(GameHardwarePin? pin, IReadOnlyList<ControllerObservation> observations)
        {
            if (pin is null || !GameInputReadiness.Ready(pin, observations.Select(x => x.Device).ToArray())) return null;
            var match = observations.Single(x => x.Device.Connected && x.Device.PhysicalGuid == pin.Device.PhysicalGuid);
            return match.Error is null && match.Capabilities is { SchemaVersion: 1 } capabilities ? capabilities.DcsKeys : null;
        }
        #endregion END Public
    }
}
