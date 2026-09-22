using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs;

public sealed record ImmutableSnapshot(string Content,string Sha256);

public static class ConfigurationSnapshotSerializer {
 public const int CurrentSchemaVersion=1;
 private static readonly JsonSerializerOptions Options=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true,DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull,Converters={new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)}};
 public static ImmutableSnapshot Serialize(IEnumerable<ModuleConfiguration> configurations) {
  ArgumentNullException.ThrowIfNull(configurations); var modules=configurations.OrderBy(x=>x.Module.DcsId,StringComparer.Ordinal).ThenBy(x=>x.Module.Id).Select(ToDto).ToArray(); var content=JsonSerializer.Serialize(new SnapshotDto(CurrentSchemaVersion,modules),Options).Replace("\r\n","\n",StringComparison.Ordinal)+"\n"; return new(content,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant());
 }
 public static IReadOnlyList<ModuleConfiguration> Deserialize(string content) {
  ArgumentException.ThrowIfNullOrWhiteSpace(content); var snapshot=JsonSerializer.Deserialize<SnapshotDto>(content,Options)??throw new JsonException("Snapshot is empty."); if(snapshot.SchemaVersion!=CurrentSchemaVersion)throw new JsonException($"Unsupported snapshot schema {snapshot.SchemaVersion}."); return snapshot.Modules.Select(FromDto).ToArray();
 }
 private static ModuleDto ToDto(ModuleConfiguration value)=>new(value.Module.Id,value.Module.DcsId,value.Module.DisplayName,value.Provenance,value.Profiles.Select(x=>new ProfileDto(x.Id,x.DeviceId,x.Instance,x.Role,x.SourceName)).ToArray(),value.Modifiers.Select(x=>new ModifierDto(x.Name,x.Device,x.Key,x.Mode)).ToArray(),value.Bindings.Select(x=>new BindingDto(x.ProfileId,x.Section,x.CommandId,x.CommandName,x.Disposition,x.InputKey,x.Reformers,x.Filter)).ToArray());
 private static ModuleConfiguration FromDto(ModuleDto value)=>new(new(value.Id,value.DcsId,value.DisplayName),value.Profiles.Select(x=>new DeviceProfile(x.Id,x.DeviceId,x.Instance,x.Role,x.SourceName)),value.Modifiers.Select(x=>new DcsModifier(x.Name,x.Device,x.Key,x.Mode)),value.Bindings.Select(x=>new DcsBinding(x.ProfileId,x.Section,x.CommandId,x.CommandName,x.Disposition,x.InputKey,x.Reformers,x.Filter)),value.Provenance);
 private sealed record SnapshotDto(int SchemaVersion,IReadOnlyList<ModuleDto> Modules);
 private sealed record ModuleDto(Guid Id,string DcsId,string DisplayName,string Provenance,IReadOnlyList<ProfileDto> Profiles,IReadOnlyList<ModifierDto> Modifiers,IReadOnlyList<BindingDto> Bindings);
 private sealed record ProfileDto(Guid Id,string DeviceId,string? Instance,string Role,string SourceName);
 private sealed record ModifierDto(string Name,string Device,string Key,ModifierMode Mode);
 private sealed record BindingDto(Guid ProfileId,BindingSection Section,string CommandId,string CommandName,BindingDisposition Disposition,string InputKey,IReadOnlyList<string> Reformers,AxisFilter? Filter);
}
