using Heimdall.Application;
namespace Heimdall.Packaging
{
    /// <summary>Represents package manifest.</summary>
    /// <param name="SchemaVersion">The schema version.</param>
    /// <param name="ModuleIds">The module ids.</param>
    public sealed record DcsPackageManifest(string SchemaVersion, IReadOnlyList<string> ModuleIds);
}
