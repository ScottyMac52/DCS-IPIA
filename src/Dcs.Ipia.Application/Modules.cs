using Dcs.Ipia.Domain;
namespace Dcs.Ipia.Application;
public sealed record ModuleDto(Guid Id,string DcsId,string DisplayName);
public sealed record SaveModuleCommand(Guid? Id,string DcsId,string DisplayName);
public interface IModuleRepository {
 Task<IReadOnlyList<Module>> ListAsync(CancellationToken ct=default);
 Task<Module?> GetAsync(Guid id,CancellationToken ct=default);
 Task UpsertAsync(Module module,CancellationToken ct=default);
 Task<bool> DeleteAsync(Guid id,CancellationToken ct=default);
}
public sealed class ModuleService(IModuleRepository repository) {
 public async Task<IReadOnlyList<ModuleDto>> ListAsync(CancellationToken ct=default)=>(await repository.ListAsync(ct)).Select(ToDto).ToArray();
 public async Task<ModuleDto?> GetAsync(Guid id,CancellationToken ct=default){var value=await repository.GetAsync(id,ct); return value is null?null:ToDto(value);}
 public async Task<ModuleDto> SaveAsync(SaveModuleCommand command,CancellationToken ct=default){var value=new Module(command.Id??Guid.NewGuid(),command.DcsId,command.DisplayName);await repository.UpsertAsync(value,ct);return ToDto(value);}
 public Task<bool> DeleteAsync(Guid id,CancellationToken ct=default)=>repository.DeleteAsync(id,ct);
 private static ModuleDto ToDto(Module value)=>new(value.Id,value.DcsId,value.DisplayName);
}
