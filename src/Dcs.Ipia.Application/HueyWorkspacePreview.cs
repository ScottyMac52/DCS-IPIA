namespace Dcs.Ipia.Application;

/// <summary>Pairs a validated Huey import with its existing committed kneeboard images and shared hardware graphics.</summary>
/// <param name="Import">The read-only module source import.</param>
/// <param name="Images">Committed PNGs in kneeboard page order.</param>
/// <param name="Diagrams">Read-only UI Layer graphics with explicit module assignments.</param>
public sealed record HueyWorkspacePreview(HueyImportResult Import,IReadOnlyList<KneeboardImageAsset> Images,IReadOnlyList<UiLayerDiagram> Diagrams) {
 #region BEGIN Public
 /// <summary>Gets graphics problems that did not prevent the module and kneeboard preview.</summary>
 public IReadOnlyList<ImportDiagnostic> GraphicDiagnostics {get;init;}=[];
 #endregion END Public
}
