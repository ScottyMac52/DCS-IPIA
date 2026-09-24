using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Dcs.Ipia.Application;

namespace Dcs.Ipia.Dcs;

/// <summary>Composes explicit Huey UI usage with canonical hardware template boxes.</summary>
public static class UiLayerDiagramFactory {
 #region BEGIN Public
 /// <summary>Builds diagrams for module-used devices and instances using committed SVG text.</summary>
 /// <param name="import">Validated Huey consumer import.</param>
 /// <param name="catalog">Validated DCS-Common catalog.</param>
 /// <param name="sources">Committed shared SVG texts keyed by repository path.</param>
 /// <returns>Only diagrams with explicit module usage.</returns>
 public static IReadOnlyList<UiLayerDiagram> Create(HueyImportResult import,SharedCatalogImportResult catalog,IReadOnlyDictionary<string,string> sources) {
  ArgumentNullException.ThrowIfNull(import);ArgumentNullException.ThrowIfNull(catalog);ArgumentNullException.ThrowIfNull(sources);
  if(!import.Succeeded||!catalog.Succeeded)throw new ArgumentException("Validated imports are required.");
  var usage=import.Metadata["config/kneeboard.json"].GetProperty("uiLayerUtilization").GetProperty("bindings");
  var diagrams=new List<UiLayerDiagram>();
  foreach(var profile in import.Configuration!.Profiles) {
   var rows=usage.EnumerateArray().Where(x=>x.GetProperty("deviceId").GetString()==profile.DeviceId
    &&(x.TryGetProperty("deviceInstance",out var instance)?instance.GetString():null)==profile.Instance).ToArray();
   if(rows.Length==0)continue;
   var device=catalog.Devices.Single(x=>x.StableKey==profile.DeviceId||x.Aliases.Contains(profile.DeviceId,StringComparer.Ordinal));
   var template=catalog.Templates.Single(x=>x.DeviceKey==device.StableKey);
   if(template.Instances.Count>0&&!template.Instances.Contains(profile.Instance,StringComparer.Ordinal))
    throw new InvalidDataException($"UI Layer template does not apply to {profile.DeviceId} {profile.Instance}.");
   var path=$"assets/shared/hardware/svg/{device.StableKey}.svg";
   if(!sources.TryGetValue(path,out var svg))throw new InvalidDataException($"Missing committed UI Layer hardware image: {path}");
   using var reader=XmlReader.Create(new StringReader(svg),new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4_000_000});
   var document=XDocument.Load(reader,LoadOptions.PreserveWhitespace);
   var root=document.Root??throw new InvalidDataException($"Empty SVG: {path}");
   var viewBox=(root.Attribute("viewBox")?.Value??"").Split(' ',StringSplitOptions.RemoveEmptyEntries);
   if(viewBox.Length!=4||viewBox[0]!="0"||viewBox[1]!="0")throw new InvalidDataException($"Unsupported SVG viewBox: {path}");
   var width=Number(viewBox[2]);var height=Number(viewBox[3]);
   var href=root.Descendants().FirstOrDefault(x=>x.Name.LocalName=="image")?.Attributes().FirstOrDefault(x=>x.Name.LocalName=="href")?.Value;
   var prefix=href?.StartsWith("data:image/png;base64,",StringComparison.Ordinal)==true?"data:image/png;base64,":
    href?.StartsWith("data:image/jpeg;base64,",StringComparison.Ordinal)==true?"data:image/jpeg;base64,":null;
   if(prefix is null)throw new InvalidDataException($"Missing embedded hardware image: {path}");
   var background=Convert.FromBase64String(href![prefix.Length..]);
   var boxes=new Dictionary<string,XElement>(StringComparer.Ordinal);
   foreach(var comment in root.Nodes().OfType<XComment>()) {
    var marker=comment.Value.Trim();
    if(!marker.StartsWith("box:",StringComparison.Ordinal))continue;
    XNode? next=comment.NextNode;
    while(next is XText whitespace&&string.IsNullOrWhiteSpace(whitespace.Value))next=next.NextNode;
    if(next is not XElement element||element.Name.LocalName!="rect")
     throw new InvalidDataException($"Graphic box {marker} has no adjacent rectangle in {path}.");
    boxes.Add(marker["box:".Length..].Trim(),element);
   }
   var callouts=new List<UiLayerCallout>();
   foreach(var row in rows) {
    var function=row.GetProperty("functionId").GetString()!;
    if(!template.Bindings.TryGetValue(function,out var control)||!boxes.TryGetValue(control,out var box))
     throw new InvalidDataException($"No canonical graphic control for {device.StableKey}/{function} in {path}.");
    var command=catalog.Commands.Single(x=>x.Id==global::Dcs.Ipia.Domain.StableIdentity.Create("ui-function",function));
    var modifiers=string.Join(" + ",row.GetProperty("modifiers").EnumerateArray().Select(x=>x.GetString()));
    callouts.Add(new(callouts.Count+1,control,command.Name,modifiers,Number(box.Attribute("x")?.Value),
     Number(box.Attribute("y")?.Value),Number(box.Attribute("width")?.Value),Number(box.Attribute("height")?.Value)));
   }
   diagrams.Add(new(device.DisplayName,profile.Instance,path,width,height,background,callouts));
  }
  return diagrams;
 }
 #endregion END Public
 #region BEGIN private helpers
 private static double Number(string? value)=>double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var parsed)&&parsed>0
  ?parsed:throw new InvalidDataException($"Invalid SVG coordinate: {value}");
 #endregion END private helpers
}
