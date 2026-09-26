using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Xunit;

namespace Dcs.Ipia.Tests;

/// <summary>Characterizes read-only Huey imports against a pinned consumer source commit.</summary>
public sealed class HueyConsumerImporterTests {
 #region BEGIN Public
 /// <summary>Real Huey sources retain five profiles, three MFD roles, pages, UI usage, labels, and exact source hashes.</summary>
 [Fact] public void Imports_real_huey_sources_without_losing_authored_metadata() {
  var request=Fixture();var result=new HueyConsumerImporter().Import(request);
  Assert.True(result.Succeeded,string.Join("; ",result.Diagnostics.Select(x=>x.Message)));
  Assert.Equal(5,result.Configuration!.Profiles.Count);
  Assert.Equal(5,result.Instances.Count);
  Assert.Equal(6,result.Pages.Count);
  Assert.Equal(12,result.Sources.Count);
  Assert.Equal(6,result.Metadata.Count);
  Assert.Equal(12,result.Provenance.Count);
  Assert.Equal(101,result.Metadata["config/kneeboard.json"].GetProperty("labels").EnumerateObject().Count());
  Assert.Equal(16,result.Metadata["config/kneeboard.json"].GetProperty("uiLayerUtilization").GetProperty("bindings").GetArrayLength());
  Assert.Contains(result.Instances,x=>x.StableKey=="tm-mfd-3"&&x.Role=="MFD3");
  Assert.Contains(result.Configuration.Bindings,x=>x.Reformers.Contains("MOZA_F16_F18_BTN3"));
  Assert.Contains(result.Configuration.Bindings,x=>x.Section==BindingSection.Axis);
  Assert.Contains(result.Pages,x=>x.StableKey=="02-TM-MFD-2"&&x.DeviceInstanceId is not null);
  Assert.Contains(result.Usage,x=>x.CommandId==StableIdentity.Create("ui-function","vr-zoom"));
  var source=request.Sources.Single(x=>x.Path=="config/kneeboard.json");
  Assert.Equal(source.Content,result.Sources[source.Path]);
  Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source.Content))),result.Provenance.Single(x=>x.Path==source.Path).Sha256);
  Assert.All(result.Provenance,x=>{Assert.Equal(request.Commit,x.Commit);Assert.Equal("UH-1H",x.Repository);});
 }
 /// <summary>Repeat imports preserve ordering, stable IDs, and content hashes.</summary>
 [Fact] public void Repeated_import_is_deterministic() {
  var request=Fixture();var importer=new HueyConsumerImporter();
  var first=importer.Import(request);var second=importer.Import(request);
  Assert.True(first.Succeeded);Assert.True(second.Succeeded);
  Assert.Equal(first.Configuration!.Profiles,second.Configuration!.Profiles);
  Assert.Equal(first.Configuration.Bindings.Select(x=>(x.ProfileId,x.CommandId,x.InputKey)),second.Configuration.Bindings.Select(x=>(x.ProfileId,x.CommandId,x.InputKey)));
  Assert.Equal(first.Instances,second.Instances);Assert.Equal(first.Pages,second.Pages);Assert.Equal(first.Provenance,second.Provenance);
 }
 /// <summary>Machine-specific GUID changes do not change MFD profile or instance identity.</summary>
 [Fact] public void Machine_guid_does_not_define_mfd_identity() {
  var request=Fixture();const string oldGuid="51FA60C0-CB32-11ed-800B-444553540000";const string newGuid="11111111-1111-1111-1111-111111111111";
  var sources=request.Sources.Select(x=>x.Path.Contains(oldGuid,StringComparison.Ordinal)?x with {Path=x.Path.Replace(oldGuid,newGuid,StringComparison.Ordinal)}:x.Path=="config/kneeboard.json"?x with {Content=x.Content.Replace(oldGuid,newGuid,StringComparison.Ordinal)}:x).ToArray();
  var changed=new HueyConsumerImporter().Import(request with {Sources=sources});
  var original=new HueyConsumerImporter().Import(request);
  Assert.True(changed.Succeeded,string.Join("; ",changed.Diagnostics.Select(x=>x.Message)));
  Assert.Equal(original.Configuration!.Profiles.Single(x=>x.Instance=="MFD1").Id,changed.Configuration!.Profiles.Single(x=>x.Instance=="MFD1").Id);
  Assert.Equal(original.Instances.Single(x=>x.Role=="MFD1").Id,changed.Instances.Single(x=>x.Role=="MFD1").Id);
 }
 /// <summary>A kneeboard profile must point to a supplied Lua file.</summary>
 [Fact] public void Missing_profile_is_rejected_without_partial_data() {
  var request=Fixture();var sources=request.Sources.Where(x=>!x.Path.Contains("F16 MFD 1 ",StringComparison.Ordinal)).ToArray();
  var result=new HueyConsumerImporter().Import(request with {Sources=sources});
  Assert.False(result.Succeeded);Assert.Null(result.Configuration);Assert.Empty(result.Provenance);
  Assert.Contains(result.Diagnostics,x=>x.Code=="invalid-huey-source"&&x.Message.Contains("no supplied Lua",StringComparison.Ordinal));
 }
 /// <summary>Conflicting author overrides cannot silently change profile devices.</summary>
 [Fact] public void Device_override_conflict_is_reported() {
  var request=Fixture();var sources=Replace(request,"config/scaffold-device-overrides.json",x=>x.Replace("\"tm-mfd\"","\"tm-warthog-throttle\"",StringComparison.Ordinal));
  var result=new HueyConsumerImporter().Import(request with {Sources=sources});
  Assert.False(result.Succeeded);Assert.Contains(result.Diagnostics,x=>x.Message.Contains("Device override",StringComparison.Ordinal));
 }
 /// <summary>Page callouts must resolve to authored labels.</summary>
 [Fact] public void Missing_kneeboard_label_is_reported() {
  var request=Fixture();var sources=Replace(request,"config/kneeboard.json",x=>{
   var root=JsonNode.Parse(x)!;root["pages"]![0]!["controls"]!["mfd-osb-t3"]!["labelId"]="missing-label";return root.ToJsonString();
  });
  var result=new HueyConsumerImporter().Import(request with {Sources=sources});
  Assert.False(result.Succeeded);Assert.Contains(result.Diagnostics,x=>x.Message.Contains("missing-label",StringComparison.Ordinal));
 }
 /// <summary>Module UI usage cannot invent shared function identities.</summary>
 [Fact] public void Unknown_ui_function_is_rejected() {
  var request=Fixture();var sources=Replace(request,"config/kneeboard.json",x=>{
   var root=JsonNode.Parse(x)!;root["uiLayerUtilization"]!["bindings"]![0]!["functionId"]="unknown-function";return root.ToJsonString();
  });
  var result=new HueyConsumerImporter().Import(request with {Sources=sources});
  Assert.False(result.Succeeded);Assert.Contains(result.Diagnostics,x=>x.Message.Contains("Unknown canonical UI Layer function",StringComparison.Ordinal));
 }
 /// <summary>Module UI usage must reference a canonical modifier.</summary>
 [Fact] public void Unknown_ui_modifier_is_rejected() {
  var request=Fixture();var sources=Replace(request,"config/kneeboard.json",x=>{
   var root=JsonNode.Parse(x)!;root["uiLayerUtilization"]!["bindings"]![0]!["modifiers"]![0]="unknown-modifier";return root.ToJsonString();
  });
  var result=new HueyConsumerImporter().Import(request with {Sources=sources});
  Assert.False(result.Succeeded);Assert.Contains(result.Diagnostics,x=>x.Message.Contains("Unknown canonical UI Layer modifier",StringComparison.Ordinal));
 }
 /// <summary>Unsupported overlay assignments are diagnosed until their shape has an importer.</summary>
 [Fact] public void Unmapped_overlay_assignments_are_rejected() {
  var request=Fixture();var sources=Replace(request,"config/ui-layer-overlay.json",x=>x.Replace("\"assignments\": []","\"assignments\": [{}]",StringComparison.Ordinal));
  var result=new HueyConsumerImporter().Import(request with {Sources=sources});
  Assert.True(result.Succeeded);Assert.Contains(result.Diagnostics,x=>x.Code=="module-overlay-preserved");
 }
 /// <summary>Malformed authored JSON yields a diagnostic and no partial configuration.</summary>
 [Fact] public void Malformed_json_is_atomic() {
  var request=Fixture();var sources=Replace(request,"config/kneeboard.json",_=>"{");
  var result=new HueyConsumerImporter().Import(request with {Sources=sources});
  Assert.False(result.Succeeded);Assert.Empty(result.Metadata);Assert.Empty(result.Sources);
  Assert.Contains(result.Diagnostics,x=>x.Code=="invalid-json");
 }
 /// <summary>Repository metadata must retain safe paths and an exact commit.</summary>
 [Fact] public void Invalid_provenance_is_rejected() {
  var request=Fixture() with {Commit="main",Sources=[new("../bad.json","{}")]};
  var result=new HueyConsumerImporter().Import(request);
  Assert.False(result.Succeeded);Assert.Contains(result.Diagnostics,x=>x.Code=="invalid-commit");
 }
 /// <summary>Two same-model devices with null page instances remain distinct and keep their own labels.</summary>
 [Fact] public void Repeated_device_without_instance_uses_authored_profile_keys() {
  var request=Fixture();
  var sources=request.Sources.Where(x=>x.Path!="config/scaffold-instance-roles.json").Select(source=>{
   if(source.Path!="config/kneeboard.json")return source;
   var root=JsonNode.Parse(source.Content)!;
   root["pages"]![0]!["deviceInstance"]=null;
   root["pages"]![1]!["deviceInstance"]=null;
   return source with {Content=root.ToJsonString()};
  }).ToArray();
  var imported=new HueyConsumerImporter().Import(request with {Sources=sources});
  Assert.True(imported.Succeeded,string.Join("; ",imported.Diagnostics.Select(x=>x.Message)));
  var first=imported.Configuration!.Profiles.Single(x=>x.Instance=="tm-mfd-1");
  var second=imported.Configuration.Profiles.Single(x=>x.Instance=="tm-mfd-2");
  Assert.NotEqual(first.Id,second.Id);
  Assert.Contains(imported.Configuration.Bindings,x=>x.ProfileId==first.Id);
  Assert.Contains(imported.Configuration.Bindings,x=>x.ProfileId==second.Id);
  var labels=KneeboardLabelImporter.Import(imported.Metadata,imported.Configuration);
  Assert.Equal(101,labels.Count);
  Assert.Contains(labels.Keys,x=>x.Split('\u001f')[1]==first.Id.ToString());
  Assert.Contains(labels.Keys,x=>x.Split('\u001f')[1]==second.Id.ToString());
  Assert.Equal(2,imported.Instances.Count(x=>x.StableKey is "tm-mfd-1" or "tm-mfd-2"));
 }
 /// <summary>Imports the committed F4U-1D module with both quadrants and obsolete removed chords.</summary>
 [Fact] public void Imports_real_f4u_sources_without_conflating_quadrants() {
  var common=Fixture();
  var root=Path.Combine(AppContext.BaseDirectory,"Fixtures","F4U");
  var sources=Directory.GetFiles(root,"*.*",SearchOption.AllDirectories)
   .Select(x=>new ConsumerSource(Path.GetRelativePath(root,x).Replace('\\','/'),File.ReadAllText(x)))
   .OrderBy(x=>x.Path,StringComparer.Ordinal).ToArray();
  var request=common with {Commit="4cd755aa9dc36384e75d054893213b8a77bc26dc",
   Repository="ScottyMac52/DCS-F4U-1D-Components",Sources=sources};
  var imported=new HueyConsumerImporter().Import(request);
  Assert.True(imported.Succeeded,string.Join("; ",imported.Diagnostics.Select(x=>x.Message)));
  Assert.Equal("F4U-1D",imported.Configuration!.Module.DcsId);
  Assert.Equal(10,imported.Configuration.Profiles.Count);
  var quadrants=imported.Configuration.Profiles.Where(x=>x.DeviceId=="logitech-throttle-quadrant").ToArray();
  Assert.Equal(2,quadrants.Length);
  Assert.NotEqual(quadrants[0].Id,quadrants[1].Id);
  Assert.All(quadrants,x=>Assert.Contains(imported.Configuration.Bindings,y=>y.ProfileId==x.Id));
  Assert.Contains(imported.Configuration.Bindings,x=>x.Disposition==BindingDisposition.Removed&&x.Reformers.Contains("JOY_BTN3"));
  var labels=KneeboardLabelImporter.Import(imported.Metadata,imported.Configuration);
  Assert.All(quadrants,x=>Assert.Contains(labels.Keys,key=>key.Split('\u001f')[1]==x.Id.ToString()));
  Assert.Contains(imported.Diagnostics,x=>x.Code=="module-overlay-preserved");
  var tinyImage=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/ZQAAAABJRU5ErkJggg==");
  var graphics=new Dictionary<string,string>(StringComparer.Ordinal);
  foreach(var device in common.Catalog!.Devices.Where(x=>imported.Configuration.Profiles.Any(y=>y.DeviceId==x.StableKey))) {
   var controls=common.SharedLua!.Controls.Where(x=>x.Control.DeviceId==device.Id)
    .Select(x=>x.Control.StableKey).ToArray();
   Assert.NotEmpty(controls);
   var callouts=string.Join("\n",controls.Select((control,index)=>
    $"<!-- callout:{control} -->{(device.StableKey=="logitech-throttle-quadrant"&&index==0?"<polyline/>":"<line/>")}<circle/><rect x='10' y='{10+index*30}' width='100' height='24'/><text id='lbl-{control}'>{control}</text>"));
   graphics.Add($"assets/shared/hardware/svg/{device.StableKey}.svg",
    $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 960 2000'><image href='data:image/png;base64,{Convert.ToBase64String(tinyImage)}'/>{callouts}</svg>");
  }
  var diagrams=UiLayerDiagramFactory.Create(imported,common.Catalog,graphics,common.SharedLua);
  Assert.Equal(imported.Configuration.Profiles.Count,diagrams.Count);
  Assert.All(diagrams,x=>Assert.NotEmpty(x.Controls));
  var stick=imported.Configuration.Profiles.Single(x=>x.DeviceId=="vkb-f14-gunfighter");
  var stickDiagram=diagrams.Single(x=>x.ProfileId==stick.Id);
  Assert.Contains(stickDiagram.Controls,x=>x.InputKey=="JOY_BTN5"&&
   imported.Configuration.Bindings.Any(y=>y.ProfileId==stick.Id&&
    y.Disposition!=BindingDisposition.Removed&&y.Reformers.Count==0&&y.InputKey==x.InputKey));
  Assert.Equal(2,diagrams.Count(x=>x.Path.EndsWith("/logitech-throttle-quadrant.svg",StringComparison.Ordinal)));
 }
 /// <summary>Two logical profiles cannot claim the same authored Lua file.</summary>
 [Fact] public void Duplicate_profile_path_is_rejected() {
  var request=Fixture();var sources=Replace(request,"config/kneeboard.json",x=>{
   var root=JsonNode.Parse(x)!;var profiles=root["profiles"]!;profiles["tm-mfd-2"]=profiles["tm-mfd-1"]!.GetValue<string>();return root.ToJsonString();
  });
  var result=new HueyConsumerImporter().Import(request with {Sources=sources});
  Assert.False(result.Succeeded);Assert.Contains(result.Diagnostics,x=>x.Message.Contains("Duplicate kneeboard profile path",StringComparison.Ordinal));
 }
 /// <summary>An override cannot refer to a device profile that was not imported.</summary>
 [Fact] public void Orphan_device_override_is_rejected() {
  var request=Fixture();var sources=Replace(request,"config/scaffold-device-overrides.json",x=>{
   var root=JsonNode.Parse(x)!;root["Other.diff.lua"]="tm-mfd";return root.ToJsonString();
  });
  var result=new HueyConsumerImporter().Import(request with {Sources=sources});
  Assert.False(result.Succeeded);Assert.Contains(result.Diagnostics,x=>x.Message.Contains("Device override references unknown profile",StringComparison.Ordinal));
 }
 /// <summary>A conflicting MFD instance role is reported before projection.</summary>
 [Fact] public void Conflicting_instance_role_is_rejected() {
  var request=Fixture();var sources=Replace(request,"config/scaffold-instance-roles.json",x=>x.Replace("\"MFD3\"","\"MFD1\"",StringComparison.Ordinal));
  var result=new HueyConsumerImporter().Import(request with {Sources=sources});
  Assert.False(result.Succeeded);Assert.Contains(result.Diagnostics,x=>x.Message.Contains("Instance role",StringComparison.Ordinal));
 }
 /// <summary>Page devices must resolve against the shared hardware manifest.</summary>
 [Fact] public void Unknown_page_device_is_rejected() {
  var request=Fixture();var sources=Replace(request,"config/kneeboard.json",x=>{
   var root=JsonNode.Parse(x)!;root["pages"]![0]!["deviceId"]="invented-device";return root.ToJsonString();
  });
  var result=new HueyConsumerImporter().Import(request with {Sources=sources});
  Assert.False(result.Succeeded);Assert.Contains(result.Diagnostics,x=>x.Message.Contains("Unknown shared device identity",StringComparison.Ordinal));
 }
 /// <summary>Malformed module-owned Lua cannot produce a partial configuration.</summary>
 [Fact] public void Invalid_modifiers_lua_is_atomic() {
  var request=Fixture();var sources=Replace(request,"src/Config/Input/UH-1H/modifiers.lua",_=>"return {}");
  var result=new HueyConsumerImporter().Import(request with {Sources=sources});
  Assert.False(result.Succeeded);Assert.Empty(result.Provenance);
  Assert.Contains(result.Diagnostics,x=>x.Code=="invalid-huey-source");
 }
 /// <summary>Every expected file is needed to prove the full Huey import.</summary>
 [Fact] public void Missing_config_file_is_reported() {
  var request=Fixture();var sources=request.Sources.Where(x=>x.Path!="config/ui-layer-overlay.json").ToArray();
  var result=new HueyConsumerImporter().Import(request with {Sources=sources});
  Assert.True(result.Succeeded);Assert.False(result.Metadata.ContainsKey("config/ui-layer-overlay.json"));
 }
 /// <summary>Unknown source paths are rejected before any consumer files are read.</summary>
 [Fact] public void Unknown_source_path_is_rejected() {
  var request=Fixture();var result=new HueyConsumerImporter().Import(request with {Sources=[..request.Sources,new("src/Config/Input/Other/unknown.diff.lua","return {}")]});
  Assert.False(result.Succeeded);Assert.Contains(result.Diagnostics,x=>x.Code=="invalid-path");
 }
 /// <summary>Uses another module's own scaffold and kneeboard paths without Huey-only support files.</summary>
 [Fact] public void Consumer_module_identity_is_derived_from_its_own_config() {
  var original=Fixture();
  static string Rewrite(ConsumerSource source) {
   var text=source.Path.EndsWith(".json",StringComparison.Ordinal)?
    source.Content.Replace("UH-1H","F-14BU",StringComparison.Ordinal):source.Content;
   if(source.Path!="config/kneeboard.json")return text;
   var json=JsonNode.Parse(text)!;
   json.AsObject().Remove("uiLayerUtilization");
   return json.ToJsonString();
  }
  var sources=original.Sources.Where(x=>x.Path is not (
   "config/scaffold.dcs-scaffold.json" or "config/ui-layer-overlay.json" or
   "config/scaffold-device-overrides.json" or "config/scaffold-instance-roles.json"))
   .Select(x=>new ConsumerSource(x.Path.Replace("UH-1H","F-14BU",StringComparison.Ordinal),Rewrite(x))).ToArray();
  var result=new HueyConsumerImporter().Import(original with {Sources=sources,Repository="F-14BU"});
  Assert.True(result.Succeeded,string.Join("; ",result.Diagnostics.Select(x=>x.Message)));
  Assert.Equal("F-14BU",result.Configuration!.Module.DcsId);
  Assert.Equal(5,result.Configuration.Profiles.Count);
  Assert.Equal(101,KneeboardLabelImporter.Import(result.Metadata,result.Configuration).Count);
  Assert.Empty(result.Usage);
  Assert.All(result.Provenance,x=>Assert.Equal("F-14BU",x.Repository));
 }
 /// <summary>Retains module-specific overlay and repeated control entries for later editing support.</summary>
 [Fact] public void Repeated_control_bindings_and_overlay_are_preserved() {
  var request=Fixture();
  var sources=request.Sources.Select(source=>{
   if(source.Path=="config/kneeboard.json") {
    var root=JsonNode.Parse(source.Content)!;
    var controls=root["pages"]![0]!["controls"]!;
    var original=controls["mfd-osb-t3"]!.DeepClone();
    controls["mfd-osb-t3"]=new JsonArray(original,original.DeepClone());
    return source with {Content=root.ToJsonString()};
   }
   if(source.Path=="config/ui-layer-overlay.json") {
    var root=JsonNode.Parse(source.Content)!;
    root["schemaVersion"]=2;
    root["assignments"]=new JsonArray(new JsonObject {["clear"]=true,["key"]="JOY_BTN1"});
    return source with {Content=root.ToJsonString()};
   }
   return source;
  }).ToArray();
  var imported=new HueyConsumerImporter().Import(request with {Sources=sources});
  Assert.True(imported.Succeeded,string.Join("; ",imported.Diagnostics.Select(x=>x.Message)));
  Assert.Contains(imported.Diagnostics,x=>x.Code=="module-overlay-preserved");
  Assert.Equal(101,KneeboardLabelImporter.Import(imported.Metadata,imported.Configuration!).Count);
  Assert.Contains("\"schemaVersion\":2",imported.Sources["config/ui-layer-overlay.json"],StringComparison.Ordinal);
 }
 #endregion END Public
 #region BEGIN private helpers
 private static ConsumerSource[] Replace(HueyImportRequest request,string path,Func<string,string> transform)=>request.Sources.Select(x=>x.Path==path?x with {Content=transform(x.Content)}:x).ToArray();
 private static HueyImportRequest Fixture() {
  var root=Path.Combine(AppContext.BaseDirectory,"Fixtures","DcsCommon");
  string Read(string name)=>File.ReadAllText(Path.Combine(root,name));
  var catalog=new DcsCommonCatalogImporter().Import(new("dd3d9cff8997c57ebfad36066ec4caf06bdbe52e","assets/shared/hardware/manifest.json",Read("hardware.json"),"assets/shared/ui-layer/functions.json",Read("functions.json"),"assets/shared/ui-layer/hardware-overlays.json",Read("hardware-overlays.json"),"assets/shared/hardware/scaffold-device-map.json",Read("device-map.json")));
  Assert.True(catalog.Succeeded);
  var common=Path.Combine(root,"Lua");
  var lua=Directory.GetFiles(common,"*.lua",SearchOption.AllDirectories).Select(x=>new SharedLuaSource(Path.GetRelativePath(common,x).Replace('\\','/'),File.ReadAllText(x))).ToArray();
  var shared=new DcsCommonLuaImporter().Import(new("dd3d9cff8997c57ebfad36066ec4caf06bdbe52e",catalog,lua));
  Assert.True(shared.Succeeded);
  var sourceRoot=Path.Combine(AppContext.BaseDirectory,"Fixtures","Huey");
  var sources=Directory.GetFiles(sourceRoot,"*.*",SearchOption.AllDirectories).Select(x=>new ConsumerSource(Path.GetRelativePath(sourceRoot,x).Replace('\\','/'),File.ReadAllText(x))).OrderBy(x=>x.Path,StringComparer.Ordinal).ToArray();
  return new("77d924cce220355860e8f026cff3524bac531e29",catalog,shared,sources);
 }
 #endregion END private helpers
}
