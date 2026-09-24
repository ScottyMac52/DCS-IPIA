using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs;

/// <summary>Reads a pinned Huey consumer checkout into a validated, read-only module view.</summary>
public sealed class HueyConsumerImporter:IHueyImporter {
 #region BEGIN Public
 /// <summary>Parses the supplied texts without accessing local source paths or writing consumer files.</summary>
 /// <param name="request">The pinned Huey files and imported DCS-Common authority.</param>
 /// <returns>Normalized identities, source-preserving metadata, provenance, and diagnostics.</returns>
 public HueyImportResult Import(HueyImportRequest request) {
  ArgumentNullException.ThrowIfNull(request);
  var diagnostics=new List<ImportDiagnostic>();
  if(request.Commit is null||request.Commit.Length!=40||!request.Commit.All(Uri.IsHexDigit))
   diagnostics.Add(new("invalid-commit","A pinned 40-character Huey commit is required.",true));
  if(request.Catalog is null||!request.Catalog.Succeeded||request.SharedLua is null||!request.SharedLua.Succeeded)
   diagnostics.Add(new("invalid-shared-input","Import DCS-Common catalogs and canonical UI Layer Lua before the consumer.",true));
  if(request.Sources is null||request.Sources.Count==0)
   diagnostics.Add(new("missing-sources","Supply the Huey module's config and Lua source files.",true));
  if(diagnostics.Count>0)return Failure(diagnostics);
  var files=new SortedDictionary<string,string>(StringComparer.Ordinal);
  foreach(var source in request.Sources!) {
   if(source is null||string.IsNullOrWhiteSpace(source.Path)||source.Path.StartsWith('/')||source.Path.Contains('\\')||source.Path.Split('/').Any(x=>x is "" or "." or "..")||!Allowed(source.Path))
    diagnostics.Add(new("invalid-path",$"Invalid or unsupported Huey source path: {source?.Path}",true));
   else if(!files.TryAdd(source.Path,source.Content))
    diagnostics.Add(new("duplicate-path",$"Duplicate Huey source: {source.Path}",true));
   if(source is null||string.IsNullOrWhiteSpace(source.Content))
    diagnostics.Add(new("missing-source",$"Missing text for {source?.Path}",true));
  }
  foreach(var path in Required)
   if(!files.ContainsKey(path))diagnostics.Add(new("missing-source",$"Required Huey source is missing: {path}",true));
  if(diagnostics.Any(x=>x.IsError))return Failure(diagnostics);
  var metadata=new SortedDictionary<string,JsonElement>(StringComparer.Ordinal);
  foreach(var pair in files.Where(x=>x.Key.EndsWith(".json",StringComparison.Ordinal))) {
   try {using var json=JsonDocument.Parse(pair.Value);metadata.Add(pair.Key,json.RootElement.Clone());}
   catch(JsonException ex){diagnostics.Add(new("invalid-json",$"{pair.Key}: {ex.Message}",true));}
  }
  if(diagnostics.Any(x=>x.IsError))return Failure(diagnostics);
  try {
   var config=metadata[Scaffold].GetProperty("import");
   if(metadata[Scaffold].GetProperty("schemaVersion").GetInt32()!=1||config.GetProperty("target").GetString()!="consumer"||config.GetProperty("inputModuleId").GetString()!="UH-1H")
    throw new FormatException("Consumer scaffold must describe UH-1H schema v1.");
   var uiScaffold=metadata[UiScaffold];
   if(uiScaffold.GetProperty("schemaVersion").GetInt32()!=1||uiScaffold.GetProperty("import").GetProperty("target").GetString()!="ui-layer")
    throw new FormatException("UI Layer scaffold must use schema v1 and ui-layer target.");
   var module=new DcsModule(StableIdentity.Create("module","UH-1H"),"UH-1H",config.GetProperty("displayName").GetString()??"UH-1H");
   var kneeboard=metadata[Kneeboard];
   if(kneeboard.GetProperty("schemaVersion").GetInt32()!=1||kneeboard.GetProperty("aircraft").GetString()!=module.DcsId||kneeboard.GetProperty("modifiersFile").GetString()!=Modifiers)
    throw new FormatException("Kneeboard aircraft, schema, or modifiers reference does not match the pinned module.");
   var overlay=metadata[Overlay];
   if(overlay.GetProperty("schemaVersion").GetInt32()!=1||overlay.GetProperty("assignments").ValueKind!=JsonValueKind.Array||overlay.GetProperty("labels").ValueKind!=JsonValueKind.Object)
    throw new FormatException("Unsupported module-owned UI Layer overlay.");
   if(overlay.GetProperty("assignments").GetArrayLength()!=0)
    throw new FormatException("Module-owned overlay assignments require an explicit mapping importer before use.");
   var devices=request.Catalog!.Devices.SelectMany(x=>new[]{x.StableKey}.Concat(x.Aliases).Select(key=>(Key:key,Device:x))).ToDictionary(x=>x.Key,x=>x.Device,StringComparer.OrdinalIgnoreCase);
   var sourceProfiles=kneeboard.GetProperty("profiles");
   if(sourceProfiles.ValueKind!=JsonValueKind.Object)throw new FormatException("Kneeboard profiles must be a keyed object.");
   var pageRows=kneeboard.GetProperty("pages");
   if(pageRows.ValueKind!=JsonValueKind.Array)throw new FormatException("Kneeboard pages must be an array.");
   var pagesByProfile=pageRows.EnumerateArray().ToDictionary(x=>RequiredText(x,"profile"),StringComparer.Ordinal);
   var overrides=metadata[Overrides];
   var roles=metadata[Roles];
   if(overrides.ValueKind!=JsonValueKind.Object||roles.ValueKind!=JsonValueKind.Object)throw new FormatException("Device overrides and instance roles must be keyed objects.");
   var profilePaths=new HashSet<string>(StringComparer.Ordinal);
   var modifiers=DcsLuaParser.ParseModifiers(files[Modifiers]);
   var profiles=new List<DeviceProfile>();var bindings=new List<DcsBinding>();var instances=new List<DeviceInstance>();
   foreach(var entry in sourceProfiles.EnumerateObject()) {
    var path=entry.Value.GetString()??throw new FormatException($"Missing profile path for {entry.Name}.");
    if(!files.TryGetValue(path,out var lua)||!path.StartsWith(ProfileRoot,StringComparison.Ordinal))
     throw new FormatException($"Kneeboard profile {entry.Name} has no supplied Lua source: {path}");
    if(!profilePaths.Add(path))throw new FormatException($"Duplicate kneeboard profile path: {path}");
    if(!pagesByProfile.TryGetValue(entry.Name,out var page))throw new FormatException($"No kneeboard page for profile {entry.Name}.");
    var key=RequiredText(page,"deviceId");
    if(!devices.TryGetValue(key,out var device))throw new FormatException($"Unknown shared device identity: {key}");
    var instance=OptionalText(page,"deviceInstance");
    var filename=Path.GetFileName(path);
    if(overrides.TryGetProperty(filename,out var forced)&&forced.GetString()!=key)
     throw new FormatException($"Device override for {filename} conflicts with {key}.");
    if(roles.TryGetProperty(filename,out var assigned)&&assigned.GetString()!=instance)
     throw new FormatException($"Instance role for {filename} conflicts with {instance}.");
    var id=StableIdentity.Create(module.DcsId,$"{key}\0{instance}");
    profiles.Add(new(id,key,instance,instance??key,filename));
    instances.Add(new(StableIdentity.Create("device-instance",$"{module.DcsId}/{key}/{instance}"),module.Id,device.Id,entry.Name,instance??key));
    bindings.AddRange(DcsLuaParser.ParseProfile(lua,id));
   }
   foreach(var decision in overrides.EnumerateObject())
    if(!profiles.Any(x=>x.SourceName==decision.Name))throw new FormatException($"Device override references unknown profile {decision.Name}.");
   foreach(var decision in roles.EnumerateObject())
    if(!profiles.Any(x=>x.SourceName==decision.Name))throw new FormatException($"Instance role references unknown profile {decision.Name}.");
   if(profiles.Count!=pageRows.GetArrayLength()||profiles.Count!=files.Keys.Count(x=>x.EndsWith(".diff.lua",StringComparison.Ordinal)))
    throw new FormatException("Every supplied Lua profile requires exactly one kneeboard profile and page.");
   var configuration=new ModuleConfiguration(module,profiles,modifiers,bindings,$"ScottyMac52/DCS-UH-1H-Huey@{request.Commit}");
   var pageList=new List<KneeboardPage>();var order=0;
   foreach(var item in kneeboard.GetProperty("summaryPages").EnumerateArray())
    pageList.Add(new(StableIdentity.Create("kneeboard-page",$"{module.DcsId}/{RequiredText(item,"file")}"),module.Id,null,KneeboardPageKind.Summary,RequiredText(item,"file"),RequiredText(item,"title"),[],order++));
   foreach(var item in pageRows.EnumerateArray()) {
    var profileName=RequiredText(item,"profile");var physical=instances.Single(x=>x.StableKey==profileName);
    pageList.Add(new(StableIdentity.Create("kneeboard-page",$"{module.DcsId}/{RequiredText(item,"file")}"),module.Id,physical.Id,KneeboardPageKind.Device,RequiredText(item,"file"),RequiredText(item,"title"),[],order++));
   }
   if(pageList.Select(x=>x.Id).Distinct().Count()!=pageList.Count)throw new FormatException("Duplicate kneeboard page file.");
   var labels=kneeboard.GetProperty("labels");
   if(labels.ValueKind!=JsonValueKind.Object)throw new FormatException("Binding labels must be a keyed object.");
   ValidateLabels(pageRows,labels);
   var usage=kneeboard.GetProperty("uiLayerUtilization");
   if(RequiredText(usage,"mode")!="explicit")throw new FormatException("Only explicit UI Layer utilization is supported.");
   var validIds=request.Catalog.Commands.Select(x=>x.Id).ToHashSet();
   var uiModifiers=request.SharedLua!.Modifiers.Select(x=>x.Name).ToHashSet(StringComparer.Ordinal);
   var used=new HashSet<Guid>();var usageList=new List<UiLayerModuleUsage>();var functionKeys=new HashSet<string>(StringComparer.Ordinal);
   foreach(var item in usage.GetProperty("bindings").EnumerateArray()) {
    var function=RequiredText(item,"functionId");var id=StableIdentity.Create("ui-function",function);
    if(!validIds.Contains(id))throw new FormatException($"Unknown canonical UI Layer function {function}.");
    var deviceKey=RequiredText(item,"deviceId");
    if(!devices.ContainsKey(deviceKey)||!profiles.Any(x=>x.DeviceId==deviceKey&&x.Instance==OptionalText(item,"deviceInstance")))
     throw new FormatException($"UI Layer utilization has no matching Huey device instance: {deviceKey}.");
    foreach(var modifier in item.GetProperty("modifiers").EnumerateArray())
     if(!uiModifiers.Contains(modifier.GetString()??string.Empty))throw new FormatException($"Unknown canonical UI Layer modifier {modifier.GetString()}.");
    if(used.Add(id)){usageList.Add(new(module.Id,id,true));functionKeys.Add(function);}
   }
   var provenance=files.Select(x=>new SourceProvenance("ScottyMac52/DCS-UH-1H-Huey",request.Commit!,x.Key,Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(x.Value))))).ToArray();
   return new(configuration,instances.OrderBy(x=>x.StableKey,StringComparer.Ordinal).ToArray(),pageList,usageList.OrderBy(x=>x.CommandId).ToArray(),functionKeys.Order(StringComparer.Ordinal).ToArray(),metadata,files,provenance,diagnostics);
  }
  catch(Exception ex) when(ex is FormatException or ArgumentException or InvalidOperationException or KeyNotFoundException) {
   diagnostics.Add(new("invalid-huey-source",ex.Message,true));
   return Failure(diagnostics);
  }
 }
 #endregion END Public
 #region BEGIN private helpers
 private static bool Allowed(string path)=>Required.Contains(path,StringComparer.Ordinal)||path.StartsWith(ProfileRoot,StringComparison.Ordinal)&&path.EndsWith(".diff.lua",StringComparison.Ordinal);
 private static string RequiredText(JsonElement item,string property) {
  var value=item.GetProperty(property).GetString();
  return string.IsNullOrWhiteSpace(value)?throw new FormatException($"Missing {property}."):value;
 }
 private static string? OptionalText(JsonElement item,string property)=>item.TryGetProperty(property,out var value)&&value.ValueKind!=JsonValueKind.Null?value.GetString():null;
 private static void ValidateLabels(JsonElement value,JsonElement labels) {
  if(value.ValueKind==JsonValueKind.Array) {foreach(var child in value.EnumerateArray())ValidateLabels(child,labels);return;}
  if(value.ValueKind!=JsonValueKind.Object)return;
  if(value.TryGetProperty("labelId",out var id)&&!labels.TryGetProperty(id.GetString()??"",out _))
   throw new FormatException($"Kneeboard callout references missing label {id.GetString()}.");
  foreach(var child in value.EnumerateObject())ValidateLabels(child.Value,labels);
 }
 private static HueyImportResult Failure(List<ImportDiagnostic> diagnostics)=>new(null,[],[],[],[],new Dictionary<string,JsonElement>(),new Dictionary<string,string>(),[],diagnostics);
 private const string Scaffold="config/UH-1H.dcs-scaffold.json";
 private const string UiScaffold="config/scaffold.dcs-scaffold.json";
 private const string Kneeboard="config/kneeboard.json";
 private const string Overlay="config/ui-layer-overlay.json";
 private const string Overrides="config/scaffold-device-overrides.json";
 private const string Roles="config/scaffold-instance-roles.json";
 private const string Modifiers="src/Config/Input/UH-1H/modifiers.lua";
 private const string ProfileRoot="src/Config/Input/UH-1H/joystick/";
 private static readonly string[] Required=[Scaffold,UiScaffold,Kneeboard,Overlay,Overrides,Roles,Modifiers];
 #endregion END private helpers
}
