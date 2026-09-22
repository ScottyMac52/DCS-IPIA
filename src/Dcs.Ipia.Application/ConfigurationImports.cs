using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application;

public sealed record ProfileImportSource(string StableDeviceId,string? Instance,string Role,string SourceName,string Content);
public sealed record ModuleImportRequest(DcsModule Module,IReadOnlyList<ProfileImportSource> Profiles,string? ModifiersContent,string Provenance);
public sealed record ImportDiagnostic(string Code,string Message,bool IsError);
public sealed record ModuleImportResult(ModuleConfiguration? Configuration,IReadOnlyList<ImportDiagnostic> Diagnostics) { public bool Succeeded=>Configuration is not null && Diagnostics.All(x=>!x.IsError); }

public interface IModuleConfigurationImporter { Task<ModuleImportResult> ImportAsync(ModuleImportRequest request,CancellationToken ct=default); }
public interface IModuleConfigurationRepository {
 Task<ModuleConfiguration?> GetAsync(Guid moduleId,CancellationToken ct=default);
 Task<IReadOnlyList<ModuleConfiguration>> ListAsync(CancellationToken ct=default);
 Task ReplaceAsync(ModuleConfiguration configuration,CancellationToken ct=default);
}

public sealed class ConfigurationImportService(IModuleConfigurationImporter importer,IModuleConfigurationRepository repository) {
 public async Task<ModuleImportResult> ImportAsync(ModuleImportRequest request,CancellationToken ct=default) {
  ArgumentNullException.ThrowIfNull(request); var result=await importer.ImportAsync(request,ct);
  if(result.Succeeded) await repository.ReplaceAsync(result.Configuration!,ct);
  return result;
 }
}
