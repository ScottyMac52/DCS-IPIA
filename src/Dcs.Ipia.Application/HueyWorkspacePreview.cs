using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application;

/// <summary>Pairs a validated Huey import with its existing committed kneeboard images and shared hardware graphics.</summary>
/// <param name="Import">The read-only module source import.</param>
/// <param name="Images">Committed PNGs in kneeboard page order.</param>
/// <param name="Diagrams">Read-only UI Layer graphics with explicit module assignments.</param>
public sealed record HueyWorkspacePreview(HueyImportResult Import,IReadOnlyList<KneeboardImageAsset> Images,IReadOnlyList<UiLayerDiagram> Diagrams) {
 #region BEGIN Public
 /// <summary>Gets graphics problems that did not prevent the module and kneeboard preview.</summary>
 public IReadOnlyList<ImportDiagnostic> GraphicDiagnostics {get;init;}=[];
 /// <summary>Gets the validated DCS-Common UI Layer catalog, even when device graphics are unavailable.</summary>
 public IReadOnlyList<DcsCommandDefinition> UiCommands {get;init;}=[];
 #endregion END Public
}
