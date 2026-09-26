using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs;

/// <summary>Composes a module's UI Layer from shared Lua and its authored overlay.</summary>
public static class ModuleUiLayerBindingImporter {
 #region BEGIN Public
 /// <summary>Gets the effective UI Layer bindings for one module device profile.</summary>
 /// <param name="module">The validated module source.</param>
 /// <param name="shared">Canonical DCS-Common UI Layer profiles and bindings.</param>
 /// <param name="profile">The module device profile.</param>
 /// <returns>Effective bindings after module-owned removals and assignments.</returns>
 public static IReadOnlyList<DcsBinding> Import(HueyImportResult module,SharedLuaImportResult shared,DeviceProfile profile) {
  ArgumentNullException.ThrowIfNull(module);
  ArgumentNullException.ThrowIfNull(shared);
  ArgumentNullException.ThrowIfNull(profile);
  var root=module.Metadata["config/kneeboard.json"];
  if(!root.TryGetProperty("includeUiLayer",out var include)||include.ValueKind!=JsonValueKind.True)
   return [];
  var matches=shared.Profiles.Where(x=>x.DeviceId==profile.DeviceId).ToArray();
  var canonical=matches.SingleOrDefault(x=>x.SourceName==profile.SourceName);
  if(canonical is null) {
   var byInstance=matches.Where(x=>SameInstance(x.Instance,profile.Instance)).ToArray();
   if(byInstance.Length==1)canonical=byInstance[0];
   else if(matches.Length==1&&module.Configuration!.Profiles.Count(x=>x.DeviceId==profile.DeviceId)==1)
    canonical=matches[0];
  }
  if(canonical is null)return [];
  module.Metadata.TryGetValue("config/ui-layer-overlay.json",out var overlay);
  var removed=overlay.ValueKind==JsonValueKind.Object&&overlay.TryGetProperty("removedModifiers",out var removedValues)
   ?removedValues.EnumerateArray().Select(x=>x.GetString()).ToHashSet(StringComparer.Ordinal)
   :new HashSet<string?>(StringComparer.Ordinal);
  var bindings=shared.Bindings.Where(x=>x.ProfileId==canonical.Id&&x.Disposition!=BindingDisposition.Removed&&
    !x.Reformers.Any(removed.Contains))
   .Select(x=>new DcsBinding(profile.Id,x.Section,x.CommandId,x.CommandName,x.Disposition,x.InputKey,x.Reformers,x.Filter))
   .ToList();
  if(overlay.ValueKind!=JsonValueKind.Object||!overlay.TryGetProperty("assignments",out var assignments))
   return bindings;
  foreach(var row in assignments.EnumerateArray()) {
   if(row.GetProperty("profileFile").GetString()!=canonical.SourceName)continue;
   var section=row.GetProperty("section").GetString() switch {
    "keyDiffs"=>BindingSection.Key,"axisDiffs"=>BindingSection.Axis,
    var value=>throw new FormatException($"Unsupported UI Layer section {value}.")
   };
   var key=row.GetProperty("key").GetString()??throw new FormatException("UI Layer assignment key is required.");
   var chord=row.GetProperty("reformers").EnumerateArray().Select(x=>x.GetString()??"").Order(StringComparer.Ordinal).ToArray();
   bindings.RemoveAll(x=>x.Section==section&&DcsInputKey.SamePhysicalInput(x.InputKey,key)&&x.Reformers.SequenceEqual(chord,StringComparer.Ordinal));
   if(row.GetProperty("clear").GetBoolean()||chord.Any(removed.Contains))continue;
   var command=row.GetProperty("command").GetString();
   if(string.IsNullOrWhiteSpace(command))throw new FormatException("UI Layer assignment command is required.");
   var name=row.TryGetProperty("name",out var authoredName)?authoredName.GetString():null;
   if(string.IsNullOrWhiteSpace(name))
    name=shared.Bindings.FirstOrDefault(x=>x.CommandId==command)?.CommandName??command;
   bindings.Add(new(profile.Id,section,command,name,BindingDisposition.Added,key,chord));
  }
  return bindings;
 }
 #endregion END Public
 #region BEGIN private helpers
 private static bool SameInstance(string? shared,string? module) =>
  shared==module||shared is not null&&module is not null&&
   module.EndsWith(shared,StringComparison.OrdinalIgnoreCase)&&
   module[..^shared.Length].EndsWith("MFD",StringComparison.OrdinalIgnoreCase);
 #endregion END private helpers
}
