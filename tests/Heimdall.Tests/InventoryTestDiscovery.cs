using Heimdall.Application;
using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>Supplies mutable live evidence independently from persisted inventory history.</summary>
    internal sealed class InventoryTestDiscovery : IConnectedDeviceDiscovery, IControllerCapabilityDiscovery
    {
        #region BEGIN Public
        /// <summary>Gets or sets current machine observations.</summary>
        public IReadOnlyList<ControllerObservation> Observations { get; set; } = [];
        /// <summary>Gets or sets a native scan failure.</summary>
        public Exception? Failure { get; set; }
        /// <inheritdoc/>
        public IReadOnlyList<DetectedDcsDevice> Discover() => Failure is not null ? throw Failure : Observations.Select(x => x.Device).ToArray();
        /// <inheritdoc/>
        public Task<IReadOnlyList<ControllerObservation>> DiscoverAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (Failure is not null) throw Failure;
            return Task.FromResult(Observations);
        }
        #endregion END Public
    }
}
