using Heimdall.Application;

using Heimdall.Application.Interfaces;

using Heimdall.Domain;

namespace Heimdall.Tests.Fakes
{
    /// <summary>Represents memory repository.</summary>
    internal sealed class MemoryRepository : IModuleRepository
    {
        private readonly Dictionary<Guid, DcsModule> values = [];
        #region BEGIN Public
        /// <summary>Lists stored items.</summary>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>The result of the operation.</returns>
        public Task<IReadOnlyList<DcsModule>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DcsModule>>(values.Values.ToArray());
        /// <summary>Gets an item by its stable identifier.</summary>
        /// <param name="id">The identifier.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>The result of the operation.</returns>
        public Task<DcsModule?> GetAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(values.GetValueOrDefault(id));
        /// <summary>Inserts or updates an item.</summary>
        /// <param name="value">The value.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>A task that completes when the operation finishes</returns>
        public Task UpsertAsync(DcsModule value, CancellationToken ct = default)
        {
            values[value.Id] = value;
            return Task.CompletedTask;
        }
        /// <summary>Deletes an item by its stable identifier.</summary>
        /// <param name="id">The identifier.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>The result of the operation.</returns>
        public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => Task.FromResult(values.Remove(id));
        #endregion END Public
    }
}
