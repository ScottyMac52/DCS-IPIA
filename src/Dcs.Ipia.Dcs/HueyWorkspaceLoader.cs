using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.Dcs;

/// <summary>Reads committed source files from selected local Git checkouts for a Huey preview.</summary>
public sealed class HueyWorkspaceLoader:IHueyWorkspaceLoader {
 #region BEGIN Public
 /// <summary>Imports the committed repository heads without changing either checkout.</summary>
 /// <param name="commonRoot">DCS-Common checkout root.</param>
 /// <param name="hueyRoot">Huey checkout root.</param>
 /// <param name="cancellationToken">Cancellation token for Git reads.</param>
 /// <returns>A validated Huey import including original committed source text.</returns>
 public async Task<HueyWorkspacePreview> LoadAsync(string commonRoot,string hueyRoot,CancellationToken cancellationToken=default) {
  var common=await RepositoryRootAsync(commonRoot,cancellationToken);
  var huey=await RepositoryRootAsync(hueyRoot,cancellationToken);
  var commonCommit=(await GitAsync(common,cancellationToken,"rev-parse","HEAD")).Trim();
  var hueyCommit=(await GitAsync(huey,cancellationToken,"rev-parse","HEAD")).Trim();
  var catalogSources=await ReadSourcesAsync(common,SharedCatalogPaths,cancellationToken);
  var catalog=new DcsCommonCatalogImporter().Import(new(commonCommit,
   SharedCatalogPaths[0],catalogSources[SharedCatalogPaths[0]],
   SharedCatalogPaths[1],catalogSources[SharedCatalogPaths[1]],
   SharedCatalogPaths[2],catalogSources[SharedCatalogPaths[2]],
   SharedCatalogPaths[3],catalogSources[SharedCatalogPaths[3]]));
  if(!catalog.Succeeded)throw new InvalidDataException($"DCS-Common catalog: {string.Join("; ",catalog.Diagnostics.Where(x=>x.IsError).Select(x=>x.Message))}");
  var sharedPaths=(await TrackedPathsAsync(common,"assets/shared/hardware/lua","assets/shared/ui-layer/input/UiLayer",cancellationToken)).Where(x=>x.EndsWith(".lua",StringComparison.Ordinal)).ToArray();
  var sharedSources=await ReadSourcesAsync(common,sharedPaths,cancellationToken);
  var shared=new DcsCommonLuaImporter().Import(new(commonCommit,catalog,sharedSources.Select(x=>new SharedLuaSource(x.Key,x.Value)).ToArray()));
  if(!shared.Succeeded)throw new InvalidDataException($"DCS-Common Lua: {string.Join("; ",shared.Diagnostics.Where(x=>x.IsError).Select(x=>x.Message))}");
  var hueyPaths=(await TrackedPathsAsync(huey,"config","src/Config/Input/UH-1H",cancellationToken))
   .Where(x=>x.StartsWith("config/",StringComparison.Ordinal)&&x.EndsWith(".json",StringComparison.Ordinal)
    ||x=="src/Config/Input/UH-1H/modifiers.lua"
    ||x.StartsWith("src/Config/Input/UH-1H/joystick/",StringComparison.Ordinal)&&x.EndsWith(".diff.lua",StringComparison.Ordinal))
   .ToArray();
  var hueySources=await ReadSourcesAsync(huey,hueyPaths,cancellationToken);
  var imported=new HueyConsumerImporter().Import(new(hueyCommit,catalog,shared,hueySources.Select(x=>new ConsumerSource(x.Key,x.Value)).ToArray()));
  var (moduleCommands,commandSource,commandDiagnostics)=LoadLinkedCommands(huey,hueySources,imported.Configuration?.Module.DcsId);
  if(!imported.Succeeded)return new(imported,[],[]) {UiCommands=catalog.Commands,ModuleCommands=moduleCommands,ModuleCommandSource=commandSource,CommandDiagnostics=commandDiagnostics};

  var available=(await TrackedPathsAsync(huey,"kneeboard/UH-1H","kneeboard/UH-1H",cancellationToken)).ToHashSet(StringComparer.Ordinal);
  var images=new List<KneeboardImageAsset>();
  foreach(var page in imported.Pages) {
   var path=$"kneeboard/UH-1H/{page.StableKey}.png";
   if(!available.Contains(path))continue;
   var bytes=await GitBytesAsync(huey,cancellationToken,"show",$"HEAD:{path}");
   images.Add(new(page.StableKey,path,bytes,Convert.ToHexStringLower(SHA256.HashData(bytes))));
  }
  IReadOnlyList<UiLayerDiagram> diagrams=[];
  IReadOnlyList<ImportDiagnostic> graphicDiagnostics=[];
  try {
   var graphicPaths=imported.Configuration!.Profiles.Select(x=>catalog.Devices.Single(y=>y.StableKey==x.DeviceId||y.Aliases.Contains(x.DeviceId,StringComparer.Ordinal)))
    .Where(x=>catalog.Templates.Any(y=>y.DeviceKey==x.StableKey))
    .Select(x=>$"assets/shared/hardware/svg/{x.StableKey}.svg").Distinct(StringComparer.Ordinal).ToArray();
   var graphics=await ReadSourcesAsync(common,graphicPaths,cancellationToken);
   diagrams=UiLayerDiagramFactory.Create(imported,catalog,graphics,shared);
  }
  catch(Exception ex) when(ex is InvalidDataException or FormatException or System.Xml.XmlException or ArgumentException or InvalidOperationException or KeyNotFoundException) {
   graphicDiagnostics=[new("ui-graphic-unavailable",$"DCS-Common UI Layer graphic: {ex.Message}",true)];
  }
  return new(imported,images,diagrams) {GraphicDiagnostics=graphicDiagnostics,UiCommands=catalog.Commands,ModuleCommands=moduleCommands,ModuleCommandSource=commandSource,CommandDiagnostics=commandDiagnostics};
 }
 #endregion END Public
 #region BEGIN private helpers
 private static (IReadOnlyList<CommandPaletteEntry> Commands,string Source,IReadOnlyList<ImportDiagnostic> Diagnostics)
  LoadLinkedCommands(string hueyRoot,IReadOnlyDictionary<string,string> sources,string? moduleId) {
  var solution=sources.FirstOrDefault(x=>x.Key=="config/UH-1H.dcs-scaffold.json");
  if(string.IsNullOrEmpty(solution.Value))
   solution=sources.FirstOrDefault(x=>x.Key=="config/scaffold.dcs-scaffold.json");
  if(string.IsNullOrEmpty(solution.Value))
   return ([],"No scaffold solution with a linked command source was found.",[]);
  try {
   using var document=JsonDocument.Parse(solution.Value);
   var import=document.RootElement.GetProperty("import");
   if(!import.TryGetProperty("commandSource",out var source)||source.ValueKind==JsonValueKind.Null)
    return ([],"No module command source is linked in the scaffold solution.",[]);
   var type=source.GetProperty("type").GetString();
   var paths=source.GetProperty("paths").EnumerateArray().Select(x=>x.GetString())
    .Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>Path.IsPathRooted(x!)?x!:Path.GetFullPath(Path.Combine(hueyRoot,x!))).ToArray();
   if(paths.Length==0)throw new InvalidDataException("The linked command source has no paths.");
   var missing=paths.Where(x=>!File.Exists(x)).ToArray();
   if(missing.Length>0)throw new FileNotFoundException($"Linked command source is missing: {string.Join(", ",missing.Select(Path.GetFileName))}. Relink it in the scaffold solution or import a catalog manually.");
   if(string.IsNullOrWhiteSpace(moduleId))throw new InvalidDataException("The imported module ID is unavailable.");
   var commands=type switch {
    "catalog-json" when paths.Length==1=>DcsCommandCatalogImporter.Parse(File.ReadAllText(paths[0]),moduleId),
    "dcs-html"=>DcsHtmlCommandCatalogImporter.Parse(paths.Select(path=>(Path.GetFileName(path),File.ReadAllText(path)))),
    _=>throw new InvalidDataException($"Unsupported linked command source '{type}'.")
   };
   return (commands,$"{commands.Count} module commands and named categories loaded from {paths.Length} linked {type} source file(s).",[]);
  }
  catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or FormatException or InvalidOperationException or KeyNotFoundException or JsonException) {
   return ([],"Linked command source could not be loaded.",[new("module-command-source",ex.Message,true)]);
  }
 }
 private static async Task<string> RepositoryRootAsync(string root,CancellationToken ct) {
  ArgumentException.ThrowIfNullOrWhiteSpace(root);
  if(!Directory.Exists(root))throw new DirectoryNotFoundException($"Selected folder does not exist: {root}");
  var full=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
  var gitRoot=Path.TrimEndingDirectorySeparator(Path.GetFullPath((await GitAsync(full,ct,"rev-parse","--show-toplevel")).Trim()));
  if(!string.Equals(full,gitRoot,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))
   throw new InvalidDataException($"Select the repository root, not a subfolder: {root}");
  return full;
 }
 private static async Task<string[]> TrackedPathsAsync(string root,string first,string second,CancellationToken ct) {
  var output=await GitAsync(root,ct,"ls-tree","-r","-z","--name-only","HEAD","--",first,second);
  return output.Split('\0',StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal).ToArray();
 }
 private static async Task<SortedDictionary<string,string>> ReadSourcesAsync(string root,IEnumerable<string> paths,CancellationToken ct) {
  var values=new SortedDictionary<string,string>(StringComparer.Ordinal);
  foreach(var path in paths)
   values.Add(path,await GitAsync(root,ct,"show",$"HEAD:{path}"));
  return values;
 }
 private static async Task<string> GitAsync(string root,CancellationToken ct,params string[] args) {
  using var process=new Process();
  process.StartInfo=new("git") {WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
  foreach(var argument in args)process.StartInfo.ArgumentList.Add(argument);
  try {
   if(!process.Start())throw new InvalidOperationException("Unable to start Git.");
   var output=process.StandardOutput.ReadToEndAsync(ct);
   var error=process.StandardError.ReadToEndAsync(ct);
   await process.WaitForExitAsync(ct);
   var stdout=await output;
   var stderr=await error;
   if(process.ExitCode!=0)throw new InvalidDataException($"Git {args[0]} failed in {root}: {stderr.Trim()}");
   return stdout;
  }
  catch(System.ComponentModel.Win32Exception ex) {throw new InvalidOperationException("Git is required to read committed source files.",ex);}
 }
 private static async Task<byte[]> GitBytesAsync(string root,CancellationToken ct,params string[] args) {
  using var process=new Process();
  process.StartInfo=new("git") {WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
  foreach(var argument in args)process.StartInfo.ArgumentList.Add(argument);
  try {
   if(!process.Start())throw new InvalidOperationException("Unable to start Git.");
   using var output=new MemoryStream();
   var copy=process.StandardOutput.BaseStream.CopyToAsync(output,ct);
   var error=process.StandardError.ReadToEndAsync(ct);
   await Task.WhenAll(copy,process.WaitForExitAsync(ct));
   if(process.ExitCode!=0)throw new InvalidDataException($"Git {args[0]} failed in {root}: {(await error).Trim()}");
   return output.ToArray();
  }
  catch(System.ComponentModel.Win32Exception ex) {throw new InvalidOperationException("Git is required to read committed kneeboard images.",ex);}
 }
 private static readonly string[] SharedCatalogPaths=[
  "assets/shared/hardware/manifest.json",
  "assets/shared/ui-layer/functions.json",
  "assets/shared/ui-layer/hardware-overlays.json",
  "assets/shared/hardware/scaffold-device-map.json"];
 #endregion END private helpers
}
