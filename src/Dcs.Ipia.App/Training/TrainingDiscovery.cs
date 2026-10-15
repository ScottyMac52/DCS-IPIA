using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace DcsIpia.Presentation
{
    /// <summary>Supplies a clearly fictional unit without accessing native hardware.</summary>
    public sealed class TrainingDiscovery : IConnectedDeviceDiscovery
    {
        #region BEGIN Public
        /// <summary>Gets the stable sample unit shared by the scenarios and bundled native profiles.</summary>
        public static DetectedDcsDevice Unit { get; } = new(Guid.Parse("11111111-2222-3333-4444-555555555555"), "Generic training joystick", false);
        /// <inheritdoc/>
        public IReadOnlyList<DetectedDcsDevice> Discover() => [Unit];
        #endregion END Public
    }
}
