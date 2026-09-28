using Dcs.Ipia.Domain;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.Application
{
    /// <summary>Exposes module CRUD operations.</summary>
    /// <param name="repository">The repository.</param>
    public sealed class ModuleService(IModuleRepository repository)
    {
        #region BEGIN Public
        /// <summary>Lists stored items.</summary>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>The result of the operation.</returns>
        public async Task<IReadOnlyList<ModuleDto>> ListAsync(CancellationToken ct = default) =>
            (await repository.ListAsync(ct)).Select(ToDto).ToArray();
        /// <summary>Gets an item by its stable identifier.</summary>
        /// <param name="id">The identifier.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>The result of the operation.</returns>
        public async Task<ModuleDto?> GetAsync(Guid id, CancellationToken ct = default)
        {
            var value = await repository.GetAsync(id, ct);
            return value is null ? null : ToDto(value);
        }
        /// <summary>Creates or updates a module.</summary>
        /// <param name="command">The command.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>The result of the operation.</returns>
        public async Task<ModuleDto> SaveAsync(SaveModuleCommand command, CancellationToken ct = default)
        {
            var value = new DcsModule(command.Id ?? Guid.NewGuid(), command.DcsId, command.DisplayName);
            await repository.UpsertAsync(value, ct);
            return ToDto(value);
        }
        /// <summary>Deletes an item by its stable identifier.</summary>
        /// <param name="id">The identifier.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>The result of the operation.</returns>
        public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => repository.DeleteAsync(id, ct);
        #endregion END Public
        #region BEGIN private helpers
        private static ModuleDto ToDto(DcsModule value) => new(value.Id, value.DcsId, value.DisplayName);
        #endregion END private helpers
    }
}
