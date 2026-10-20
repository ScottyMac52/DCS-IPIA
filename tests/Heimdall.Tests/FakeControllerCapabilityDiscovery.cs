using Heimdall.Application;
using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>Deterministic discovery with per-scan failures and cancellation controlled by tests.</summary>
    public sealed class FakeControllerCapabilityDiscovery : IControllerCapabilityDiscovery
    {
        #region BEGIN Public
        /// <summary>Gets or sets observations for the next scan.</summary>
        public IReadOnlyList<ControllerObservation> Observations { get; set; } = [];
        /// <summary>Gets or sets a discovery-level failure.</summary>
        public Exception? Failure { get; set; }
        /// <summary>Gets or sets whether discovery waits for cancellation.</summary>
        public bool WaitForCancellation { get; set; }
        /// <summary>Gets the signal that the discovery adapter was entered.</summary>
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <inheritdoc/>
        public async Task<IReadOnlyList<ControllerObservation>> DiscoverAsync(CancellationToken ct = default)
        {
            Started.TrySetResult();
            ct.ThrowIfCancellationRequested();
            if (WaitForCancellation) await Task.Delay(Timeout.Infinite, ct);
            if (Failure is not null) throw Failure;
            return Observations;
        }
        #endregion END Public
    }
}
