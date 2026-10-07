using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Provides deterministic repeated controllers to exercise the Windows inventory UI.</summary>
    public sealed class InventorySmokeDiscovery : IControllerCapabilityDiscovery
    {
        #region BEGIN Public
        /// <summary>Gets or sets the current scan evidence.</summary>
        public IReadOnlyList<ControllerObservation> Observations { get; set; } = [];
        /// <inheritdoc/>
        public Task<IReadOnlyList<ControllerObservation>> DiscoverAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Observations);
        }
        #endregion END Public
    }
}
