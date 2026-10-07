namespace Dcs.Ipia.Application.Interfaces
{
    /// <summary>Persists profile-owned inventory separately from every module's saved assignments.</summary>
    public interface IHardwareInventoryRepository
    {
        #region BEGIN Public
        /// <summary>Reads versioned inventory without assuming historical devices remain connected.</summary>
        /// <param name="ct">Read cancellation.</param>
        /// <returns>Portable inventory snapshot.</returns>
        Task<HardwareInventorySnapshot> ReadAsync(CancellationToken ct = default);
        /// <summary>Atomically saves reviewed definitions, physical units and discovery evidence.</summary>
        /// <param name="snapshot">Validated inventory.</param>
        /// <param name="ct">Write cancellation.</param>
        /// <returns>Completion of the committed inventory update.</returns>
        Task SaveAsync(HardwareInventorySnapshot snapshot, CancellationToken ct = default);
        #endregion END Public
    }
}
