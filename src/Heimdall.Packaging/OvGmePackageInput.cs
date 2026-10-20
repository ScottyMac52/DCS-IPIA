namespace Heimdall.Packaging
{
    /// <summary>A frozen, validated module payload ready for OvGME archive assembly.</summary>
    /// <param name="PackageId">Consumer package name, for example DCS-UH-1H-Components.</param>
    /// <param name="ModuleId">DCS input module and kneeboard directory identifier.</param>
    /// <param name="Version">Package version.</param>
    /// <param name="Payload">Generated files keyed by their DCS Saved Games relative paths.</param>
    public sealed record OvGmePackageInput(string PackageId, string ModuleId, string Version,
                                           IReadOnlyDictionary<string, byte[]> Payload);
}
