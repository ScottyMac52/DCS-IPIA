using System.Security.Cryptography;
using System.Text;

namespace Dcs.Ipia.Domain;

public enum BindingSection { Key, Axis }
public enum BindingDisposition { Added, Changed, Removed }
public enum ModifierMode { Hold, Toggle }

public sealed record AxisFilter(
 double? Deadzone=null,
 double? SaturationX=null,
 double? SaturationY=null,
 IReadOnlyList<double>? Curvature=null,
 bool? Invert=null,
 bool? Slider=null,
 bool? HardwareDetent=null,
 double? HardwareDetentAB=null,
 double? HardwareDetentMax=null);

public sealed record DeviceProfile {
 public DeviceProfile(Guid id,string deviceId,string? instance,string role,string sourceName) {
  if(id==Guid.Empty) throw new ArgumentException("A profile ID is required.",nameof(id));
  Id=id; DeviceId=Required(deviceId,nameof(deviceId)); Instance=Optional(instance); Role=Required(role,nameof(role)); SourceName=Required(sourceName,nameof(sourceName));
 }
 public Guid Id {get;} public string DeviceId {get;} public string? Instance {get;} public string Role {get;} public string SourceName {get;}
 private static string Required(string value,string name)=>string.IsNullOrWhiteSpace(value)?throw new ArgumentException("A value is required.",name):value.Trim();
 private static string? Optional(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
}

public sealed record DcsModifier {
 public DcsModifier(string name,string device,string key,ModifierMode mode) {
  Name=Required(name,nameof(name)); Device=Required(device,nameof(device)); Key=Required(key,nameof(key)); Mode=mode;
 }
 public string Name {get;} public string Device {get;} public string Key {get;} public ModifierMode Mode {get;}
 private static string Required(string value,string name)=>string.IsNullOrWhiteSpace(value)?throw new ArgumentException("A value is required.",name):value.Trim();
}

public sealed record DcsBinding {
 public DcsBinding(Guid profileId,BindingSection section,string commandId,string commandName,BindingDisposition disposition,string inputKey,IEnumerable<string>? reformers=null,AxisFilter? filter=null) {
  if(profileId==Guid.Empty) throw new ArgumentException("A profile ID is required.",nameof(profileId));
  if(section==BindingSection.Key && filter is not null) throw new ArgumentException("Only axis bindings may have a filter.",nameof(filter));
  ProfileId=profileId; Section=section; CommandId=Required(commandId,nameof(commandId)); CommandName=Required(commandName,nameof(commandName)); Disposition=disposition; InputKey=Required(inputKey,nameof(inputKey)); Filter=filter;
  Reformers=(reformers??[]).Select(x=>Required(x,nameof(reformers))).Order(StringComparer.Ordinal).ToArray();
  if(Reformers.Distinct(StringComparer.Ordinal).Count()!=Reformers.Count) throw new ArgumentException("A modifier chord cannot contain duplicates.",nameof(reformers));
 }
 public Guid ProfileId {get;} public BindingSection Section {get;} public string CommandId {get;} public string CommandName {get;} public BindingDisposition Disposition {get;} public string InputKey {get;} public IReadOnlyList<string> Reformers {get;} public AxisFilter? Filter {get;}
 private static string Required(string value,string name)=>string.IsNullOrWhiteSpace(value)?throw new ArgumentException("A value is required.",name):value.Trim();
}

public sealed record ModuleConfiguration {
 public ModuleConfiguration(DcsModule module,IEnumerable<DeviceProfile> profiles,IEnumerable<DcsModifier> modifiers,IEnumerable<DcsBinding> bindings,string provenance) {
  Module=module??throw new ArgumentNullException(nameof(module)); Profiles=(profiles??throw new ArgumentNullException(nameof(profiles))).OrderBy(x=>x.DeviceId,StringComparer.Ordinal).ThenBy(x=>x.Instance??string.Empty,StringComparer.Ordinal).ToArray();
  Modifiers=(modifiers??throw new ArgumentNullException(nameof(modifiers))).OrderBy(x=>x.Name,StringComparer.Ordinal).ToArray(); Bindings=(bindings??throw new ArgumentNullException(nameof(bindings))).OrderBy(x=>x.ProfileId).ThenBy(x=>x.Section).ThenBy(x=>x.CommandId,StringComparer.Ordinal).ThenBy(x=>x.Disposition).ThenBy(x=>x.InputKey,StringComparer.Ordinal).ToArray();
  EnsureUnique(Profiles.Select(x=>x.Id),"profile ID"); EnsureUnique(Profiles.Select(x=>$"{x.DeviceId}\0{x.Instance}"),"device instance",StringComparer.OrdinalIgnoreCase); EnsureUnique(Modifiers.Select(x=>x.Name),"modifier name",StringComparer.OrdinalIgnoreCase); EnsureUnique(Modifiers.Select(x=>$"{x.Device}\0{x.Key}"),"modifier input",StringComparer.OrdinalIgnoreCase);
  var profileIds=Profiles.Select(x=>x.Id).ToHashSet(); if(Bindings.Any(x=>!profileIds.Contains(x.ProfileId))) throw new ArgumentException("Every binding must reference a profile.",nameof(bindings));
  var modifierNames=Modifiers.Select(x=>x.Name).ToHashSet(StringComparer.Ordinal); if(Bindings.SelectMany(x=>x.Reformers).Any(x=>!modifierNames.Contains(x))) throw new ArgumentException("Every reformer must reference a declared modifier.",nameof(bindings));
  Provenance=string.IsNullOrWhiteSpace(provenance)?throw new ArgumentException("Provenance is required.",nameof(provenance)):provenance.Trim();
 }
 public DcsModule Module {get;} public IReadOnlyList<DeviceProfile> Profiles {get;} public IReadOnlyList<DcsModifier> Modifiers {get;} public IReadOnlyList<DcsBinding> Bindings {get;} public string Provenance {get;}
 private static void EnsureUnique<T>(IEnumerable<T> values,string label,IEqualityComparer<T>? comparer=null) {var all=values.ToArray();if(all.Distinct(comparer).Count()!=all.Length)throw new ArgumentException($"Duplicate {label}.");}
}

public static class StableIdentity {
 public static Guid Create(string scope,string value) {
  ArgumentException.ThrowIfNullOrWhiteSpace(scope); ArgumentException.ThrowIfNullOrWhiteSpace(value);
  var bytes=SHA256.HashData(Encoding.UTF8.GetBytes($"{scope.Trim()}\0{value.Trim()}"));
  Span<byte> guid=stackalloc byte[16]; bytes.AsSpan(0,16).CopyTo(guid); guid[6]=(byte)((guid[6]&0x0f)|0x50); guid[8]=(byte)((guid[8]&0x3f)|0x80); return new Guid(guid);
 }
}
