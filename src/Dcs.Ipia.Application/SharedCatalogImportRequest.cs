namespace Dcs.Ipia.Application
{
    /// <summary>Supplies the current DCS-Common authored catalogs without granting filesystem write access.</summary>
    /// <param name="Commit">The exact DCS-Common source commit.</param>
    /// <param name="HardwarePath">The repository-relative hardware manifest path.</param>
    /// <param name="HardwareJson">The unmodified hardware manifest text.</param>
    /// <param name="FunctionsPath">The repository-relative UI Layer functions path.</param>
    /// <param name="FunctionsJson">The unmodified UI Layer functions text.</param>
    /// <param name="OverlaysPath">The repository-relative UI Layer overlay path.</param>
    /// <param name="OverlaysJson">The unmodified UI Layer overlay text.</param>
    /// <param name="DeviceMapPath">The shared hardware device map path.</param>
    /// <param name="DeviceMapJson">The unmodified ordered device map text.</param>
    public sealed record SharedCatalogImportRequest(string Commit, string HardwarePath, string HardwareJson,
                                                    string FunctionsPath, string FunctionsJson, string OverlaysPath,
                                                    string OverlaysJson, string DeviceMapPath, string DeviceMapJson);
}
