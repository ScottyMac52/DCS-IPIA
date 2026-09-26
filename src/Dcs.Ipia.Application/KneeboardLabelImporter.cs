using System.Text.Json;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application;

/// <summary>Imports authored module callout labels from the committed kneeboard configuration.</summary>
public static class KneeboardLabelImporter {
 #region BEGIN Public
 /// <summary>Maps binding label IDs to editor assignment slots.</summary>
 /// <param name="metadata">Read-only imported source JSON keyed by path.</param>
 /// <param name="configuration">Validated module bindings and device profiles.</param>
 /// <returns>Authored displayed labels keyed by assignment slot.</returns>
 public static IReadOnlyDictionary<string,string> Import(
  IReadOnlyDictionary<string,JsonElement> metadata,ModuleConfiguration configuration) {
  ArgumentNullException.ThrowIfNull(metadata);
  ArgumentNullException.ThrowIfNull(configuration);
  var labels=new Dictionary<string,string>(StringComparer.Ordinal);
  if(!metadata.TryGetValue("config/kneeboard.json",out var root)||
     !root.TryGetProperty("labels",out var names)||!root.TryGetProperty("pages",out var pages))return labels;
  foreach(var page in pages.EnumerateArray()) {
   var device=page.GetProperty("deviceId").GetString();
   var instance=page.TryGetProperty("deviceInstance",out var value)&&value.ValueKind!=JsonValueKind.Null
    ?value.GetString():null;
   var profile=configuration.Profiles.FirstOrDefault(x=>x.DeviceId==device&&x.Instance==instance);
   if(profile is null||!page.TryGetProperty("layers",out var layers))continue;
   foreach(var layer in layers.EnumerateArray()) {
    if(!layer.TryGetProperty("controls",out var controls))continue;
    foreach(var item in controls.EnumerateObject()) {
     var control=item.Value;
     if(!control.TryGetProperty("labelId",out var id)||!names.TryGetProperty(id.GetString()??"",out var label)||
        string.IsNullOrWhiteSpace(label.GetString()))continue;
     var key=control.GetProperty("key").GetString();
     var command=control.GetProperty("command").GetString();
     var reformers=control.TryGetProperty("modifiers",out var modifierValues)
      ?modifierValues.EnumerateArray().Select(x=>x.GetString()??"").ToArray():[];
     var binding=configuration.Bindings.FirstOrDefault(x=>x.ProfileId==profile.Id&&x.CommandId==command&&
      DcsInputKey.SamePhysicalInput(x.InputKey,key??"")&&
      x.Reformers.SequenceEqual(reformers,StringComparer.Ordinal));
     if(binding is null)continue;
     var slot=new DraftAssignmentSlot(configuration.Module.Id,profile.Id,AssignmentLayer.Module,
      binding.Section,binding.InputKey,string.Join("+",binding.Reformers));
     labels.TryAdd(slot.Key,label.GetString()!);
    }
   }
  }
  return labels;
 }
 #endregion END Public
}
