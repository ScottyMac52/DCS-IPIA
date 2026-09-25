using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Infrastructure;

/// <summary>Restores shifted callout mappings in editor snapshots saved before they were imported.</summary>
internal static class EditorWorkspaceUpgrade {
 #region BEGIN Public
 /// <summary>Upgrades a legacy snapshot using its stored SVG and catalogs, without reading source repositories.</summary>
 /// <param name="snapshot">The stored editor snapshot.</param>
 /// <returns>The snapshot with previously inaccessible shifted controls and their UI assignments.</returns>
 public static EditorWorkspaceSnapshot RestoreShiftedControls(EditorWorkspaceSnapshot snapshot) {
  var diagrams=snapshot.Diagrams.Select(RestoreDiagram).ToArray();
  var assignments=snapshot.Assignments.ToDictionary(x=>x.Slot.Key,x=>x,StringComparer.Ordinal);
  var uiCommands=snapshot.UiCommands.ToDictionary(x=>x.DcsCommandId,x=>x,StringComparer.Ordinal);
  foreach(var diagram in diagrams) {
   var original=snapshot.Diagrams.First(x=>x.ProfileId==diagram.ProfileId);
   var newControls=diagram.Controls.Where(x=>x.Control.EndsWith("-shifted",StringComparison.Ordinal)&&
    original.Controls.All(y=>y.Control!=x.Control)).ToDictionary(x=>x.Control,x=>x,StringComparer.Ordinal);
   foreach(var callout in diagram.Callouts) {
    if(!newControls.TryGetValue(callout.Control,out var control)||
       !uiCommands.TryGetValue(callout.CommandId,out var command)||command.IsAxis!=control.IsAxis)continue;
    var chord=callout.Modifier.Replace(" + ","+",StringComparison.Ordinal);
    if(chord=="Base")chord="";
    var slot=new DraftAssignmentSlot(snapshot.Configuration.Module.Id,diagram.ProfileId,AssignmentLayer.UiLayer,
     control.IsAxis?BindingSection.Axis:BindingSection.Key,control.InputKey,chord);
    assignments.TryAdd(slot.Key,new DraftAssignment(slot,new DraftCommand(command.DcsCommandId,command.Name,command.IsAxis,AssignmentLayer.UiLayer)));
   }
  }
  return snapshot with {Diagrams=diagrams,Assignments=assignments.Values.ToArray()};
 }
 #endregion END Public
 #region BEGIN private helpers
 private static UiLayerDiagram RestoreDiagram(UiLayerDiagram diagram) {
  if(string.IsNullOrWhiteSpace(diagram.Svg)||diagram.Controls.Count==0)return diagram;
  using var reader=XmlReader.Create(new StringReader(diagram.Svg),new XmlReaderSettings {
   DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4_000_000
  });
  var root=XDocument.Load(reader,LoadOptions.PreserveWhitespace).Root;
  if(root is null)return diagram;
  var controls=diagram.Controls.ToList();
  var mapped=controls.ToDictionary(x=>x.Control,x=>x,StringComparer.Ordinal);
  foreach(var comment in root.Nodes().OfType<XComment>()) {
   var marker=comment.Value.Trim();
   if(!marker.StartsWith("box:",StringComparison.Ordinal)||!marker.EndsWith("-shifted",StringComparison.Ordinal))continue;
   var id=marker["box:".Length..].Trim();
   if(mapped.ContainsKey(id)||!mapped.TryGetValue(id[..^"-shifted".Length],out var baseControl))continue;
   XNode? next=comment.NextNode;
   while(next is XText whitespace&&string.IsNullOrWhiteSpace(whitespace.Value))next=next.NextNode;
   if(next is not XElement box||box.Name.LocalName!="rect")continue;
   var label=box.ElementsAfterSelf().FirstOrDefault(x=>x.Name.LocalName=="text"&&
    x.Attribute("id")?.Value=="lbl-"+id)?.Value.Trim()??baseControl.Label;
   var control=new HardwareGraphicControl(id,label,baseControl.InputKey,
    Number(box,"x"),Number(box,"y"),Number(box,"width"),Number(box,"height"),baseControl.IsAxis);
   controls.Add(control);
   mapped.Add(id,control);
  }
  return diagram with {Controls=controls};
 }
 private static double Number(XElement box,string name)=>
  double.TryParse(box.Attribute(name)?.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)&&
  double.IsFinite(value)&&value>=0?value:throw new InvalidDataException("Invalid stored SVG box coordinate: "+name);
 #endregion END private helpers
}
