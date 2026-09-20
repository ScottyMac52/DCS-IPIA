using Dcs.Ipia.Application;
namespace Dcs.Ipia.Packaging;
public sealed record PackageManifest(string SchemaVersion,IReadOnlyList<string> ModuleIds);
public sealed class PackageManifestBuilder {public PackageManifest Build(IEnumerable<ModuleDto> values){ArgumentNullException.ThrowIfNull(values);return new("1",values.Select(x=>x.DcsId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());}}
