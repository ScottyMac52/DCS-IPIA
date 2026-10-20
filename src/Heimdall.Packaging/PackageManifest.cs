using Heimdall.Application;
namespace Heimdall.Packaging
{
    /// <summary>Represents package manifest.</summary>
    /// <param name="SchemaVersion">The schema version.</param>
    /// <param name="ModuleIds">The module ids.</param>
    public sealed record PackageManifest(string SchemaVersion, IReadOnlyList<string> ModuleIds);
}
