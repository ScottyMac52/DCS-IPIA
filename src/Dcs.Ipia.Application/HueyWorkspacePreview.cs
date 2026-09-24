namespace Dcs.Ipia.Application;

/// <summary>Pairs a validated Huey import with its existing committed kneeboard images.</summary>
/// <param name="Import">The read-only module source import.</param>
/// <param name="Images">Committed PNGs in kneeboard page order.</param>
public sealed record HueyWorkspacePreview(HueyImportResult Import,IReadOnlyList<KneeboardImageAsset> Images);
