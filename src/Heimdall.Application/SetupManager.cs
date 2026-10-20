using Heimdall.Application.Interfaces;

namespace Heimdall.Application
{
    /// <summary>Coordinates explicit setup confirmation using the existing inventory adoption rules.</summary>
    public sealed class SetupManager
    {
        private readonly HardwareInventoryManager _inventory;
        private readonly ISetupRepository _repository;
        /// <summary>Creates setup for one selected data profile.</summary>
        /// <param name="inventory">Existing discovery and hardware review use cases.</param>
        /// <param name="repository">Profile-owned transactional setup storage.</param>
        public SetupManager(HardwareInventoryManager inventory, ISetupRepository repository)
        {
            _inventory = inventory;
            _repository = repository;
        }
        #region BEGIN Public
        /// <summary>Reads the saved setup state without scanning or writing.</summary>
        /// <param name="ct">Read cancellation.</param>
        /// <returns>Profile setup state.</returns>
        public Task<SetupState> ReadAsync(CancellationToken ct = default) => _repository.ReadAsync(ct);
        /// <summary>Confirms reviewed selections and destination; empty/offline setup requires explicit consent.</summary>
        /// <param name="edits">Reviewed physical units, labels and capability decisions.</param>
        /// <param name="destination">Absolute Saved Games destination, or empty to defer deployment.</param>
        /// <param name="allowOffline">Explicit consent to finish without a destination or selected controller.</param>
        /// <param name="ct">Commit cancellation.</param>
        /// <returns>Completion only after all reviewed state commits.</returns>
        public async Task ConfirmAsync(IReadOnlyList<HardwareInventoryEdit> edits, string destination, bool allowOffline, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(destination);
            destination = destination.Trim();
            if (destination.Length > 0 && !Path.IsPathFullyQualified(destination))
                throw new ArgumentException("Choose an absolute Saved Games DCS folder, or leave it empty for offline setup.", nameof(destination));
            if (!allowOffline && (destination.Length == 0 || !edits.Any(x => x.Selected)))
                throw new InvalidOperationException("Confirm offline setup explicitly, or select hardware and a destination.");
            if (destination.Length > 0) destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
            var expected = await _inventory.ReadSnapshotAsync(ct);
            var reviewed = HardwareInventoryManager.Prepare(expected, edits);
            ct.ThrowIfCancellationRequested();
            await _repository.CommitAsync(expected, reviewed, destination, ct);
        }
        #endregion END Public
    }
}
