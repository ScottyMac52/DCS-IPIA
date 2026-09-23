using Dcs.Ipia.Application;

using Dcs.Ipia.Application.Interfaces;

using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Tests.Fakes;

/// <summary>Represents memory configuration repository.</summary>
internal sealed class MemoryConfigurationRepository:IModuleConfigurationRepository {
 private readonly Dictionary<Guid,ModuleConfiguration> values=[];
 #region BEGIN Public
 /// <summary>Gets the writes.</summary>
 public int Writes{get;private set;}
 /// <summary>Gets an item by its stable identifier.</summary>
 /// <param name="id">The identifier.</param>
 /// <param name="ct">Cancellation token for the operation.</param>
 /// <returns>The result of the operation.</returns>
 public Task<ModuleConfiguration?> GetAsync(Guid id,CancellationToken ct=default)=>Task.FromResult(values.GetValueOrDefault(id));
 /// <summary>Lists stored items.</summary>
 /// <param name="ct">Cancellation token for the operation.</param>
 /// <returns>The result of the operation.</returns>
 public Task<IReadOnlyList<ModuleConfiguration>> ListAsync(CancellationToken ct=default)=>Task.FromResult<IReadOnlyList<ModuleConfiguration>>(values.Values.ToArray());
 /// <summary>Atomically replaces a module configuration.</summary>
 /// <param name="value">The value.</param>
 /// <param name="ct">Cancellation token for the operation.</param>
 /// <returns>A task that completes when the operation finishes</returns>
 public Task ReplaceAsync(ModuleConfiguration value,CancellationToken ct=default){Writes++;values[value.Module.Id]=value;return Task.CompletedTask;}
 #endregion END Public
}