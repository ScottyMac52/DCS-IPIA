using Dcs.Ipia.Application;

using Dcs.Ipia.Application.Interfaces;

using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Tests.Fakes;

/// <summary>Represents stub importer.</summary>
/// <param name="result">The result.</param>
internal sealed class StubImporter(ModuleImportResult result):IModuleConfigurationImporter {
 #region BEGIN Public
 /// <summary>Gets the result.</summary>
 public ModuleImportResult Result{get;set;}=result;
 /// <summary>Imports the requested configuration.</summary>
 /// <param name="request">The request.</param>
 /// <param name="ct">Cancellation token for the operation.</param>
 /// <returns>The result of the operation.</returns>
 public Task<ModuleImportResult> ImportAsync(ModuleImportRequest request,CancellationToken ct=default)=>Task.FromResult(Result);
 #endregion END Public
}