using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Xunit;

namespace Dcs.Ipia.Tests;

/// <summary>Characterizes read-only imports against pinned DCS-Common authored manifests.</summary>
public sealed class SharedCatalogImporterTests {
 #region BEGIN Public
 /// <summary>The three current catalogs import with stable identities, aliases, templates and exact source hashes.</summary>
 [Fact] public void Imports_real_shared_catalogs_with_provenance() {
  var request=Fixture();
  var result=new DcsCommonCatalogImporter().Import(request);
  Assert.True(result.Succeeded,string.Join("; ",result.Diagnostics.Select(x=>x.Message)));
  Assert.Equal(12,result.Devices.Count);
  Assert.Equal(13,result.Commands.Count);
  Assert.Equal(5,result.Templates.Count);
  Assert.Equal(17,result.DeviceMap!.Rules.Count);
  Assert.Equal(2,result.Diagnostics.Count(x=>x.Code=="unresolved-device-map"));
  Assert.Equal(5,result.DeviceMap.InputKeyAliases.Count);
  Assert.Equal("tm-mfd",Assert.Single(result.DeviceMap.InstancePatterns).DeviceKey);
  Assert.Equal("moza-ab9-hornet-grip",result.DeviceMap.Rules[4].DeviceKey);
  Assert.Contains(result.Devices,x=>x.StableKey=="tm-warthog-grip" && x.Aliases.Contains("moza-ab9-warthog-grip"));
  Assert.Contains(result.Templates,x=>x.DeviceKey=="tm-mfd" && x.Instances.SequenceEqual(["MFD3"]) && x.Bindings["vr-zoom"]=="mfd-osb-t1-shifted");
  Assert.All(result.Provenance,x=>{Assert.Equal("ScottyMac52/DCS-Common",x.Repository);Assert.Equal(request.Commit,x.Commit);});
  Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(request.FunctionsJson))),result.Provenance.Single(x=>x.Path==request.FunctionsPath).Sha256);
 }
 /// <summary>Repeated imports preserve canonical ordering and stable identities.</summary>
 [Fact] public void Repeated_imports_are_idempotent() {
  var importer=new DcsCommonCatalogImporter();var request=Fixture();
  var first=importer.Import(request);var second=importer.Import(request);
  Assert.Equal(first.Devices.Select(x=>(x.Id,x.StableKey,x.DisplayName,string.Join(",",x.Aliases))),second.Devices.Select(x=>(x.Id,x.StableKey,x.DisplayName,string.Join(",",x.Aliases))));
  Assert.Equal(first.Commands,second.Commands);
  Assert.Equal(first.Provenance,second.Provenance);
  Assert.Equal(first.DeviceMap!.Rules.Select(x=>(x.DeviceKey,string.Join(",",x.Patterns))),second.DeviceMap!.Rules.Select(x=>(x.DeviceKey,string.Join(",",x.Patterns))));
  Assert.Equal(first.Templates.Select(x=>x.DeviceKey),second.Templates.Select(x=>x.DeviceKey));
 }
 /// <summary>Malformed JSON produces a diagnostic without partial data.</summary>
 [Fact] public void Invalid_json_returns_diagnostics() {
  var result=new DcsCommonCatalogImporter().Import(Fixture() with {FunctionsJson="{"});
  Assert.False(result.Succeeded);Assert.Contains(result.Diagnostics,x=>x.Code=="invalid-shared-source");
  Assert.Empty(result.Devices);Assert.Empty(result.Provenance);
 }
 /// <summary>A missing function reference is rejected rather than guessed.</summary>
 [Fact] public void Unknown_overlay_function_is_rejected() {
  var request=Fixture();
  var result=new DcsCommonCatalogImporter().Import(request with {OverlaysJson=request.OverlaysJson.Replace("\"vr-zoom\":", "\"unknown-function\":",StringComparison.Ordinal)});
  Assert.False(result.Succeeded);Assert.Contains(result.Diagnostics,x=>x.Message.Contains("Unknown UI Layer function",StringComparison.Ordinal));
 }
 /// <summary>Conflicting aliases cannot silently resolve to two devices.</summary>
 [Fact] public void Ambiguous_alias_is_rejected() {
  var request=Fixture();var json=request.HardwareJson.Replace("\"ava-base-f16c\"", "\"moza-ab9\"",StringComparison.Ordinal);
  var result=new DcsCommonCatalogImporter().Import(request with {HardwareJson=json});
  Assert.False(result.Succeeded);Assert.Contains(result.Diagnostics,x=>x.Message.Contains("Ambiguous device alias",StringComparison.Ordinal));
 }
 /// <summary>Unknown map IDs are retained and flagged without creating fake devices.</summary>
 [Fact] public void Unknown_mapping_device_is_reported() {
  var request=Fixture();
  var result=new DcsCommonCatalogImporter().Import(request with {DeviceMapJson=request.DeviceMapJson.Replace("\"winctrl-pto2\"", "\"unknown-device\"",StringComparison.Ordinal)});
  Assert.True(result.Succeeded);
  Assert.Contains(result.DeviceMap!.Rules,x=>x.DeviceKey=="unknown-device");
  Assert.DoesNotContain(result.Devices,x=>x.StableKey=="unknown-device");
  Assert.Contains(result.Diagnostics,x=>x.Code=="unresolved-device-map" && x.Message.Contains("unknown-device",StringComparison.Ordinal));
 }
 /// <summary>An unsupported map version is a fatal validation error.</summary>
 [Fact] public void Unsupported_map_version_fails_atomically() {
  var request=Fixture();
  var result=new DcsCommonCatalogImporter().Import(request with {DeviceMapJson=request.DeviceMapJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2",StringComparison.Ordinal)});
  Assert.False(result.Succeeded);Assert.Empty(result.Provenance);
 }
 /// <summary>Empty filename rules cannot be used for device recognition.</summary>
 [Fact] public void Empty_mapping_patterns_fail_atomically() {
  var request=Fixture();var map=JsonNode.Parse(request.DeviceMapJson)!;
  map["mappings"]![0]!["patterns"]=new JsonArray();
  var result=new DcsCommonCatalogImporter().Import(request with {DeviceMapJson=map.ToJsonString()});
  Assert.False(result.Succeeded);Assert.Empty(result.DeviceMap?.Rules??[]);
 }
 /// <summary>An instance rule requires an existing capture group.</summary>
 [Fact] public void Invalid_instance_capture_group_fails_atomically() {
  var request=Fixture();var map=JsonNode.Parse(request.DeviceMapJson)!;
  map["instancePatterns"]![0]!["group"]=99;
  var result=new DcsCommonCatalogImporter().Import(request with {DeviceMapJson=map.ToJsonString()});
  Assert.False(result.Succeeded);Assert.Contains(result.Diagnostics,x=>x.Message.Contains("Invalid instance capture group",StringComparison.Ordinal));
 }
 /// <summary>Source paths and commits must allow precise provenance.</summary>
 [Fact] public void Invalid_source_metadata_returns_structured_diagnostics() {
  var request=Fixture() with {Commit="main",HardwarePath="../manifest.json",FunctionsPath="../manifest.json"};
  var result=new DcsCommonCatalogImporter().Import(request);
  Assert.False(result.Succeeded);
  Assert.Contains(result.Diagnostics,x=>x.Code=="invalid-commit");
  Assert.Contains(result.Diagnostics,x=>x.Code=="invalid-path");
  Assert.Contains(result.Diagnostics,x=>x.Code=="duplicate-path");
 }
 #endregion END Public
 #region BEGIN private helpers
 private static SharedCatalogImportRequest Fixture() {
  var root=Path.Combine(AppContext.BaseDirectory,"Fixtures","DcsCommon");
  return new("dd3d9cff8997c57ebfad36066ec4caf06bdbe52e","assets/shared/hardware/manifest.json",File.ReadAllText(Path.Combine(root,"hardware.json")),"assets/shared/ui-layer/functions.json",File.ReadAllText(Path.Combine(root,"functions.json")),"assets/shared/ui-layer/hardware-overlays.json",File.ReadAllText(Path.Combine(root,"hardware-overlays.json")),"assets/shared/hardware/scaffold-device-map.json",File.ReadAllText(Path.Combine(root,"device-map.json")));
 }
 #endregion END private helpers
}
