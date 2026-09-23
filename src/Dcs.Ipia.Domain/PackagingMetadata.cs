namespace Dcs.Ipia.Domain;

/// <summary>Represents packaging metadata.</summary>
/// <param name="ModuleId">The module identifier.</param>
/// <param name="PackageId">The package identifier.</param>
/// <param name="CompleteRoot">The complete root.</param>
/// <param name="OvGmeRoot">The ov gme root.</param>
/// <param name="IncludePatterns">The include patterns.</param>
public sealed record PackagingMetadata(Guid ModuleId,string PackageId,string CompleteRoot,string OvGmeRoot,IReadOnlyList<string> IncludePatterns);
