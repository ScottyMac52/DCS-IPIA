using Dcs.Ipia.Domain;
using Dcs.Ipia.Application;

namespace Dcs.Ipia.Application.Interfaces
{
    /// <summary>Defines module repository operations.</summary>
    public interface IModuleRepository
    {
        #region BEGIN Public
        /// <summary>Lists stored items.</summary>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>The result of the operation.</returns>
        Task<IReadOnlyList<DcsModule>> ListAsync(CancellationToken ct = default);
        /// <summary>Gets an item by its stable identifier.</summary>
        /// <param name="id">The identifier.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>The result of the operation.</returns>
        Task<DcsModule?> GetAsync(Guid id, CancellationToken ct = default);
        /// <summary>Inserts or updates an item.</summary>
        /// <param name="value">The value.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>The result of the operation.</returns>
        Task UpsertAsync(DcsModule value, CancellationToken ct = default);
        /// <summary>Deletes an item by its stable identifier.</summary>
        /// <param name="id">The identifier.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>The result of the operation.</returns>
        Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
        #endregion END Public
    }
}
