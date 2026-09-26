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
 #endregion END Public
}
