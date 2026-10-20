namespace Heimdall.Application.Interfaces
{
    /// <summary>Commits confirmed onboarding without touching module data or DCS files.</summary>
    public interface ISetupRepository
    {
        #region BEGIN Public
        /// <summary>Reads profile-specific completion and destination.</summary>
        /// <param name="ct">Read cancellation.</param>
        /// <returns>Persisted setup state.</returns>
        Task<SetupState> ReadAsync(CancellationToken ct = default);
        /// <summary>Atomically commits reviewed inventory, destination and completion after checking stale inventory.</summary>
        /// <param name="expected">Inventory read before staging.</param>
        /// <param name="reviewed">Validated reviewed inventory.</param>
        /// <param name="destination">Absolute default destination, or empty for explicitly confirmed offline setup.</param>
        /// <param name="ct">Transaction cancellation.</param>
        /// <returns>Completion after the transaction commits.</returns>
        Task CommitAsync(HardwareInventorySnapshot expected, HardwareInventorySnapshot reviewed, string destination, CancellationToken ct = default);
        #endregion END Public
    }
}
