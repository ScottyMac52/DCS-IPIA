using System.Xml.Linq;
using Dcs.Ipia.Application;
using Dcs.Ipia.Rendering;
using Xunit;

namespace Dcs.Ipia.Tests;

/// <summary>Checks a single final-style device image with live labels and modifier legend.</summary>
public sealed class CompositeKneeboardRendererTests {
 #region BEGIN Public
 /// <summary>One page contains Base, shifted, and UI Layer variants plus the DCS-Common-style legend.</summary>
 [Fact] public void Combines_all_scope_labels_on_one_png() {
  const string svg="<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100' viewBox='0 0 100 100'><rect width='100' height='100' fill='black'/><text id='lbl-one' x='50' y='50' fill='white'>Old</text><text id='meta-mfd-category-top' x='50' y='15'>Old</text></svg>";
  var diagram=new UiLayerDiagram("Device",null,"device.svg",100,100,[],[]) {Svg=svg};
  var page=new KneeboardComposite("Authored title","CURRENT DRAFT",
   new Dictionary<string,IReadOnlyList<KneeboardLabelVariant>> {
    ["one"]=[
     new("Base label","#ffffff",AssignmentLayer.Module,""),
     new("Shift label","#dc2626",AssignmentLayer.Module,"SHIFT"),
     new("UI label","#0891b2",AssignmentLayer.UiLayer,"")
    ]
   },new Dictionary<string,string> {["mfd-category-top"]="Flight"},
   [new("Base (no modifier)","#ffffff"),new("SHIFT","#dc2626"),new("UI Layer — Base","#0891b2")]);
  var device=CompositeKneeboardRenderer.BuildDeviceSvg(diagram,page);
  var document=XDocument.Parse(device);
  var label=Assert.Single(document.Descendants().Where(x=>x.Attribute("id")?.Value=="lbl-one"));
  Assert.Equal(["Base label","Shift label","UI label"],label.Elements().Select(x=>x.Value));
  Assert.Equal("Flight",Assert.Single(document.Descendants().Where(x=>
   x.Attribute("id")?.Value=="meta-mfd-category-top")).Value);
  var outer=CompositeKneeboardRenderer.BuildPageSvg(diagram,page);
  Assert.Contains("data-modifier-legend",outer,StringComparison.Ordinal);
  Assert.Contains("UI Layer — Base",outer,StringComparison.Ordinal);
  Assert.Contains("data:image/png;base64,",outer,StringComparison.Ordinal);
  var png=CompositeKneeboardRenderer.Render(diagram,page);
  Assert.Equal(new byte[] {137,80,78,71,13,10,26,10},png.Take(8));
 }
 /// <summary>Final-style labels wrap without losing the authored base color; modifier entries stay on one callout.</summary>
 [Fact] public void Keeps_native_base_color_and_wraps_long_single_labels() {
  const string svg="<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100' viewBox='0 0 100 100'><rect width='100' height='100' fill='white'/><text id='lbl-one' x='50' y='50' fill='black'>Old</text></svg>";
  var diagram=new UiLayerDiagram("Device",null,"device.svg",100,100,[],[]) {Svg=svg};
  var page=new KneeboardComposite("Device","",
   new Dictionary<string,IReadOnlyList<KneeboardLabelVariant>> {
    ["one"]=[new("Long function name for a control","#ffffff",AssignmentLayer.Module,"")]
   },new Dictionary<string,string>(),[]);
  var document=XDocument.Parse(CompositeKneeboardRenderer.BuildDeviceSvg(diagram,page));
  var label=Assert.Single(document.Descendants().Where(x=>x.Attribute("id")?.Value=="lbl-one"));
  Assert.Equal("black",label.Attribute("fill")?.Value);
  Assert.Equal(2,label.Elements().Count());
  Assert.DoesNotContain("data-modifier-legend",CompositeKneeboardRenderer.BuildPageSvg(diagram,page));
 }
 #endregion END Public
}
