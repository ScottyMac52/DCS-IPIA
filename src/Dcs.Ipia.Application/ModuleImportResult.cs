using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application;

/// <summary>Represents module import result.</summary>
/// <param name="Configuration">The configuration.</param>
/// <param name="Diagnostics">The diagnostics.</param>
public sealed record ModuleImportResult(ModuleConfiguration? Configuration,IReadOnlyList<ImportDiagnostic> Diagnostics) {
 #region BEGIN Public
 /// <summary>Gets whether an error-free configuration was imported.</summary>
 public bool Succeeded=>Configuration is not null && Diagnostics.All(x=>!x.IsError);
 #endregion END Public
}