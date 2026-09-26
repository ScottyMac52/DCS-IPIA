using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Xunit;

namespace Dcs.Ipia.Tests;

/// <summary>Checks current-draft kneeboard pages across device scopes and newly used layers.</summary>
public sealed class KneeboardDraftProjectionTests {
 #region BEGIN Public
 /// <summary>A new shift assignment gets its own page while Base and UI Layer retain their independent labels.</summary>
 [Fact] public void Renders_every_used_layer_from_pending_assignments() {
  var module=new DcsModule(Guid.NewGuid(),"TEST","Test");
  var profile=new DeviceProfile(Guid.NewGuid(),"test-device",null,"Device","Device.diff.lua");
  var configuration=new ModuleConfiguration(module,[profile],
   [new DcsModifier("SHIFT","test-device","JOY_BTN7",ModifierMode.Hold)],[],"fixture");
  var controls=new[] {
   new HardwareGraphicControl("one","One","JOY_BTN1",10,10,40,20),
   new HardwareGraphicControl("two","Two","JOY_BTN2",10,40,40,20)
  };
  var diagram=new UiLayerDiagram("Device",null,"device.svg",100,100,[],[]) {
   ProfileId=profile.Id,Controls=controls,Svg="<svg/>"
  };
  var snapshot=new EditorWorkspaceSnapshot(configuration,[],[],[diagram],[],[],[],
   new Dictionary<string,string>());
  DraftAssignment Assigned(AssignmentLayer layer,string input,string id,string chord="")=>
   new(new(module.Id,profile.Id,layer,BindingSection.Key,input,chord),
    new DraftCommand(id,id,false,layer));
  var original=Assigned(AssignmentLayer.Module,"JOY_BTN1","BASE");
  var shifted=Assigned(AssignmentLayer.Module,"JOY_BTN2","SHIFTED","SHIFT");
  var ui=Assigned(AssignmentLayer.UiLayer,"JOY_BTN1","UI");
  var labels=new Dictionary<string,string> {[shifted.Slot.Key]="New shifted label"};
  var pages=KneeboardDraftProjection.Create(snapshot,diagram,[original,shifted,ui],labels);
  Assert.Equal(["Module · Base","Module · SHIFT","UI Layer · Base"],pages.Select(x=>x.Title));
  Assert.Equal("BASE",pages[0].Captions["one"]);
  Assert.Equal("Unassigned",pages[0].Captions["two"]);
  Assert.Equal("New shifted label",pages[1].Captions["two"]);
  Assert.Equal("UI",pages[2].Captions["one"]);
  Assert.Equal("Unassigned",pages[2].Captions["two"]);
  Assert.DoesNotContain(pages,x=>x.Title=="UI Layer · SHIFT");
  var combined=KneeboardDraftProjection.CreateCombined(snapshot,diagram,[original,shifted,ui],labels);
  Assert.Equal(["BASE","UI"],combined.Labels["one"].Select(x=>x.Text));
  Assert.Equal(["New shifted label"],combined.Labels["two"].Select(x=>x.Text));
  Assert.Contains(combined.Legend,x=>x.Label=="SHIFT"&&x.Color=="#dc2626");
  Assert.Contains(combined.Legend,x=>x.Label=="UI Layer — Base"&&x.Color=="#0891b2");
 }
 /// <summary>An unsupported UI Layer produces only module pages even when a stale UI assignment exists.</summary>
 [Fact] public void Unsupported_ui_layer_is_not_rendered() {
  var module=new DcsModule(Guid.NewGuid(),"TEST","Test");
  var profile=new DeviceProfile(Guid.NewGuid(),"device",null,"Device","Device.diff.lua");
  var config=new ModuleConfiguration(module,[profile],[],[],"fixture");
  var diagram=new UiLayerDiagram("Device",null,"device.svg",100,100,[],[]) {
   ProfileId=profile.Id,SupportsUiLayer=false,Controls=[
    new HardwareGraphicControl("one","One","JOY_BTN1",10,10,40,20)]
  };
  var snapshot=new EditorWorkspaceSnapshot(config,[],[],[diagram],[],[],[],
   new Dictionary<string,string>());
  var pages=KneeboardDraftProjection.Create(snapshot,diagram,[],new Dictionary<string,string>());
  Assert.Single(pages);
  Assert.Equal("Module · Base",pages[0].Title);
 }
 /// <summary>One authored page retains static control text and category names while staged module and UI assignments share it.</summary>
 [Fact] public void Combined_device_uses_authored_page_and_current_ui_assignments() {
  var module=new DcsModule(Guid.NewGuid(),"TEST","Test");
  var profile=new DeviceProfile(Guid.NewGuid(),"test-device",null,"Device","Device.diff.lua");
  var config=new ModuleConfiguration(module,[profile],[],[],"fixture");
  var diagram=new UiLayerDiagram("Device",null,"device.svg",100,100,[],[]) {
   ProfileId=profile.Id,Controls=[
    new HardwareGraphicControl("one","One","JOY_BTN1",10,10,40,20),
    new HardwareGraphicControl("one-shifted","One shifted","JOY_BTN1",10,40,40,20),
    new HardwareGraphicControl("two","Two","JOY_BTN2",10,70,40,20)]
  };
  const string source="""
   {"profiles":{"stick":"Device.diff.lua"},"pages":[
    {"profile":"stick","title":"Authored device","kicker":"FLIGHT CONTROLS",
     "labels":{"two":"Physical control"},"categoryLabels":{"top":"Flight"},
     "layers":[{"labels":{"one":"Old authored label"},"categoryLabels":{"right":"Systems"}}]}
   ]}
   """;
  var snapshot=new EditorWorkspaceSnapshot(config,[],[],[diagram],[],[],[],
   new Dictionary<string,string>(),new Dictionary<string,string> {["config/kneeboard.json"]=source});
  DraftAssignment Assigned(AssignmentLayer layer,string id,string chord="")=>
   new(new(module.Id,profile.Id,layer,BindingSection.Key,"JOY_BTN1",chord),
    new DraftCommand(id,id,false,layer));
  var moduleBase=Assigned(AssignmentLayer.Module,"Module");
  var uiShift=Assigned(AssignmentLayer.UiLayer,"UI shifted","UI_BTN7");
  var overrides=new Dictionary<string,string> {[moduleBase.Slot.Key]="Edited module"};
  var page=KneeboardDraftProjection.CreateCombined(snapshot,diagram,[moduleBase,uiShift],overrides);
  Assert.Equal("Authored device",page.Title);
  Assert.Equal("FLIGHT CONTROLS",page.Kicker);
  Assert.Equal("Edited module",Assert.Single(page.Labels["one"]).Text);
  Assert.Equal("UI shifted",Assert.Single(page.Labels["one-shifted"]).Text);
  Assert.Equal("Physical control",Assert.Single(page.Labels["two"]).Text);
  Assert.Equal("Flight",page.CategoryLabels["mfd-category-top"]);
  Assert.Equal("Systems",page.CategoryLabels["mfd-category-right"]);
  Assert.Contains(page.Legend,x=>x.Label=="UI Layer — UI_BTN7");
 }
 #endregion END Public
}
