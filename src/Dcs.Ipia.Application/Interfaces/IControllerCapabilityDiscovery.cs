namespace Dcs.Ipia.Application.Interfaces
{
    /// <summary>Reads native identities and capabilities without acquiring controls or deploying files.</summary>
    public interface IControllerCapabilityDiscovery
    {
        #region BEGIN Public
        /// <summary>Runs cancellable discovery away from the UI thread; failures never imply device removal.</summary>
        /// <param name="ct">Discovery cancellation.</param>
        /// <returns>Attached controllers with verified input evidence or per-device errors.</returns>
        Task<IReadOnlyList<ControllerObservation>> DiscoverAsync(CancellationToken ct = default);
        #endregion END Public
    }
}
