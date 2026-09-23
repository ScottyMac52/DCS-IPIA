using Dcs.Ipia.Domain;

using Dcs.Ipia.Application;

namespace Dcs.Ipia.Application.Interfaces;

/// <summary>Defines module configuration repository operations.</summary>
public interface IModuleConfigurationRepository {
 #region BEGIN Public
 /// <summary>Gets an item by its stable identifier.</summary>
 /// <param name="moduleId">The module identifier.</param>
 /// <param name="ct">Cancellation token for the operation.</param>
 /// <returns>The result of the operation.</returns>
 Task<ModuleConfiguration?> GetAsync(Guid moduleId,CancellationToken ct=default);
 /// <summary>Lists stored items.</summary>
 /// <param name="ct">Cancellation token for the operation.</param>
 /// <returns>The result of the operation.</returns>
 Task<IReadOnlyList<ModuleConfiguration>> ListAsync(CancellationToken ct=default);
 /// <summary>Atomically replaces a module configuration.</summary>
 /// <param name="configuration">The configuration.</param>
 /// <param name="ct">Cancellation token for the operation.</param>
 /// <returns>The result of the operation.</returns>
 Task ReplaceAsync(ModuleConfiguration configuration,CancellationToken ct=default);
 #endregion END Public
}