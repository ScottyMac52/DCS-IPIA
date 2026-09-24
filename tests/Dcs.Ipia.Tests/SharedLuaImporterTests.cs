using System.Security.Cryptography;
using System.Text;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Xunit;

namespace Dcs.Ipia.Tests;

/// <summary>Characterizes read-only Lua imports against a pinned DCS-Common commit.</summary>
public sealed class SharedLuaImporterTests {
 #region BEGIN Public
 /// <summary>All authored controls and canonical UI Layer files import with exact provenance.</summary>
 [Fact] public void Imports_pinned_hardware_and_ui_layer_lua() {
  var request=Fixture();
  var result=new DcsCommonLuaImporter().Import(request);
  Assert.True(result.Succeeded,string.Join("; ",result.Diagnostics.Select(x=>x.Message)));
  Assert.Equal(333,result.Controls.Count);
  Assert.Equal(6,result.Profiles.Count);
  Assert.NotEmpty(result.Bindings);
  Assert.NotEmpty(result.Modifiers);
  Assert.Equal(19,result.Provenance.Count);
  Assert.Equal(28,result.Controls.Count(x=>x.Control.DeviceId==request.Catalog.Devices.Single(y=>y.StableKey=="tm-mfd").Id));
  Assert.Contains(result.Controls,x=>x.Control.StableKey=="mfd-rocker-brt-up"&&x.InputKey=="JOY_BTN25"&&x.Group=="brightness"&&x.Position=="up");
  Assert.Contains(result.Controls,x=>x.SourceType=="button-position"&&x.Control.Kind==global::Dcs.Ipia.Domain.ControlKind.Button);
  Assert.Contains(result.Profiles,x=>x.DeviceId=="tm-mfd"&&x.Instance=="3");
  Assert.Contains(result.Profiles,x=>x.DeviceId=="moza-ab9");
  Assert.Contains(result.Profiles,x=>x.DeviceId=="keyboard");
  Assert.Contains(result.Bindings,x=>x.InputKey=="Back"&&x.Reformers.SequenceEqual(["LAlt","LCtrl"]));
  Assert.All(result.Provenance,x=>{Assert.Equal(request.Commit,x.Commit);Assert.Equal("ScottyMac52/DCS-Common",x.Repository);});
  var source=request.Sources.Single(x=>x.Path.EndsWith("/modifiers.lua",StringComparison.Ordinal));
  Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source.Content))),result.Provenance.Single(x=>x.Path==source.Path).Sha256);
 }
 /// <summary>Repeated imports yield the same stable IDs, ordering, and content hashes.</summary>
 [Fact] public void Repeated_import_is_deterministic() {
  var request=Fixture();var importer=new DcsCommonLuaImporter();
  var first=importer.Import(request);var second=importer.Import(request);
  Assert.True(first.Succeeded);Assert.True(second.Succeeded);
  Assert.Equal(first.Controls,second.Controls);
  Assert.Equal(first.Profiles,second.Profiles);
  Assert.Equal(first.Provenance,second.Provenance);
  Assert.Equal(first.Bindings.Select(x=>(x.ProfileId,x.CommandId,x.InputKey)),second.Bindings.Select(x=>(x.ProfileId,x.CommandId,x.InputKey)));
 }
 /// <summary>Unsupported hardware rows fail explicitly with no partial import.</summary>
 [Fact] public void Unsupported_hardware_lua_is_rejected() {
  var request=Fixture();
  var sources=request.Sources.Select(x=>x.Path.EndsWith("/moza-ab9.lua",StringComparison.Ordinal)?x with {Content=x.Content.Replace("type = \"axis\"","type = \"unknown\"",StringComparison.Ordinal)}:x).ToArray();
  var result=new DcsCommonLuaImporter().Import(request with {Sources=sources});
  Assert.False(result.Succeeded);Assert.Empty(result.Controls);Assert.Empty(result.Provenance);
  Assert.Contains(result.Diagnostics,x=>x.Code=="invalid-lua-source"&&x.Message.Contains("unknown",StringComparison.Ordinal));
 }
 /// <summary>An unmatched UI profile gets a diagnostic rather than an invented device.</summary>
 [Fact] public void Unmapped_profile_is_rejected() {
  var request=Fixture();var sources=request.Sources.Select(x=>x.Path.Contains("/joystick/Viper TQS ",StringComparison.Ordinal)?x with {Path=x.Path.Replace("Viper TQS ","Unknown Peripheral ",StringComparison.Ordinal)}:x).ToArray();
  var result=new DcsCommonLuaImporter().Import(request with {Sources=sources});
  Assert.False(result.Succeeded);Assert.Empty(result.Profiles);
  Assert.Contains(result.Diagnostics,x=>x.Message.Contains("No valid ordered device-map rule",StringComparison.Ordinal));
 }
 /// <summary>A missing modifier definition is reported with its name.</summary>
 [Fact] public void Undefined_modifier_is_rejected() {
  var request=Fixture();var sources=request.Sources.Select(x=>x.Path.EndsWith("/modifiers.lua",StringComparison.Ordinal)?x with {Content=x.Content.Replace("VKB_F14_BTN7","RENAMED_MODIFIER",StringComparison.Ordinal)}:x).ToArray();
  var result=new DcsCommonLuaImporter().Import(request with {Sources=sources});
  Assert.False(result.Succeeded);Assert.Contains(result.Diagnostics,x=>x.Code=="unknown-modifier"&&x.Message.Contains("VKB_F14_BTN7",StringComparison.Ordinal));
 }
 /// <summary>Wrong command IDs in a profile cannot silently become canonical functions.</summary>
 [Fact] public void Unknown_function_is_rejected() {
  var request=Fixture();var sources=request.Sources.Select(x=>x.Path.EndsWith("Keyboard.diff.lua",StringComparison.Ordinal)?x with {Content=x.Content.Replace("d257pnilunilcdnilvdnilvpnilvunil","unknown-command",StringComparison.Ordinal)}:x).ToArray();
  var result=new DcsCommonLuaImporter().Import(request with {Sources=sources});
  Assert.False(result.Succeeded);Assert.Contains(result.Diagnostics,x=>x.Code=="unknown-command");
 }
 /// <summary>Unsafe and duplicate paths are rejected before parsing.</summary>
 [Fact] public void Invalid_source_metadata_is_rejected() {
  var request=Fixture() with {Commit="main",Sources=[new("../bad.lua","return {}"),new("../bad.lua","return {}")]};
  var result=new DcsCommonLuaImporter().Import(request);
  Assert.False(result.Succeeded);Assert.Contains(result.Diagnostics,x=>x.Code=="invalid-commit");
 }
 #endregion END Public
 #region BEGIN private helpers
 private static SharedLuaImportRequest Fixture() {
  var root=Path.Combine(AppContext.BaseDirectory,"Fixtures","DcsCommon");
  string Read(string filename)=>File.ReadAllText(Path.Combine(root,filename));
  var catalog=new DcsCommonCatalogImporter().Import(new("dd3d9cff8997c57ebfad36066ec4caf06bdbe52e","assets/shared/hardware/manifest.json",Read("hardware.json"),"assets/shared/ui-layer/functions.json",Read("functions.json"),"assets/shared/ui-layer/hardware-overlays.json",Read("hardware-overlays.json"),"assets/shared/hardware/scaffold-device-map.json",Read("device-map.json")));
  Assert.True(catalog.Succeeded);
  var files=Directory.GetFiles(Path.Combine(root,"Lua"),"*.lua",SearchOption.AllDirectories).Order(StringComparer.Ordinal).Select(file=>new SharedLuaSource(Path.GetRelativePath(Path.Combine(root,"Lua"),file).Replace('\\','/'),File.ReadAllText(file))).ToArray();
  return new("dd3d9cff8997c57ebfad36066ec4caf06bdbe52e",catalog,files);
 }
 #endregion END private helpers
}
