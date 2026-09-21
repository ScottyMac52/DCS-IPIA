using System.Text.Json; using Dcs.Ipia.Application;
namespace Dcs.Ipia.Dcs;
public static class CanonicalSnapshotSerializer {
 private static readonly JsonSerializerOptions Options=new(){WriteIndented=true};
 public static string Serialize(IEnumerable<ModuleDto> values){ArgumentNullException.ThrowIfNull(values);return JsonSerializer.Serialize(values.OrderBy(x=>x.DcsId,StringComparer.Ordinal).ThenBy(x=>x.Id),Options).Replace("\r\n","\n",StringComparison.Ordinal)+"\n";}
 public static IReadOnlyList<ModuleDto> Deserialize(string value){ArgumentException.ThrowIfNullOrWhiteSpace(value);return JsonSerializer.Deserialize<ModuleDto[]>(value,Options)??throw new JsonException("Snapshot did not contain modules.");}
}
