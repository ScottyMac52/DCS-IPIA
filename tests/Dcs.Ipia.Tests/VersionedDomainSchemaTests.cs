using Dcs.Ipia.Domain;
using Xunit;

namespace Dcs.Ipia.Tests;

public sealed class VersionedDomainSchemaTests {
 private static readonly Guid ModuleId=Guid.Parse("10000000-0000-0000-0000-000000000001");
 private static readonly Guid DeviceId=Guid.Parse("20000000-0000-0000-0000-000000000001");
 private static readonly Guid ControlId=Guid.Parse("30000000-0000-0000-0000-000000000001");
 private static readonly Guid InstanceId=Guid.Parse("40000000-0000-0000-0000-000000000001");
 private static readonly Guid ProfileId=Guid.Parse("50000000-0000-0000-0000-000000000001");
 private static readonly Guid CatalogId=Guid.Parse("60000000-0000-0000-0000-000000000001");
 private static readonly Guid CommandId=Guid.Parse("70000000-0000-0000-0000-000000000001");
 private static readonly Guid CategoryId=Guid.Parse("80000000-0000-0000-0000-000000000001");

 [Fact] public void V1_graph_defines_every_workstream_relationship() {
  var value=Create();
  Assert.Equal("1.0",value.SchemaVersion.ToString()); Assert.Equal(2,value.MigrationVersion); Assert.Equal(1,value.Snapshot.SchemaVersion);
  Assert.Equal("UH-1H",Assert.Single(value.Modules).DcsId); Assert.Equal("tm-mfd",Assert.Single(value.Devices).StableKey);
  Assert.Equal(ControlKind.Button,Assert.Single(value.Controls).Kind); Assert.Equal("pilot-left",Assert.Single(value.Instances).Role);
  Assert.Equal("iCommandPlaneFire",Assert.Single(value.Commands).DcsCommandId); Assert.Equal(["SHIFT"],Assert.Single(value.Bindings).ModifierChord);
  Assert.True(Assert.Single(value.UiLayerUsage).Enabled); Assert.Equal(CommandId,Assert.Single(value.UiLayerOverlays).CommandId);
  Assert.Equal("legacy-command",Assert.Single(value.UiLayerTombstones).StableKey); Assert.Equal(KneeboardPageKind.Device,Assert.Single(value.Pages).Kind);
  Assert.Equal("docs/controls.md",Assert.Single(value.Documentation).OutputPath); Assert.Equal("UH-1H Complete",Assert.Single(value.Packages).PackageId);
  Assert.Equal("abc",Assert.Single(value.Provenance).Commit); Assert.Equal("v1.2.3.4",value.Release.ToString());
 }
 [Fact] public void Collections_are_canonically_ordered_and_copied() {
  var aliases=new List<string>{"MFD"}; var value=Create(devices:[new(DeviceId,"tm-mfd","MFD",aliases)]);
  aliases.Add("changed"); Assert.Single(value.Devices[0].Aliases);
  Assert.Equal("v1.2.3",new ReleaseVersion(1,2,3).ToString());
 }
 [Fact] public void Duplicate_stable_identity_is_rejected()=>Assert.Contains("Duplicate device stable key",Assert.Throws<ArgumentException>(()=>Create(devices:[new(DeviceId,"tm-mfd","MFD",[]),new(Guid.NewGuid(),"TM-MFD","Other",[])])).Message,StringComparison.Ordinal);
 [Theory]
 [InlineData("control")][InlineData("instance")][InlineData("command")][InlineData("binding")][InlineData("axis")][InlineData("usage")][InlineData("overlay")][InlineData("tombstone")][InlineData("page")][InlineData("documentation")][InlineData("package")]
 public void Dangling_relationships_are_rejected(string relationship) {
  var unknown=Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
  var error=relationship switch {
   "control"=>Record.Exception(()=>Create(controls:[new(ControlId,unknown,"osb-1","OSB 1",ControlKind.Button,null)])),
   "instance"=>Record.Exception(()=>Create(instances:[new(InstanceId,unknown,DeviceId,"mfd-1","pilot-left")])),
   "command"=>Record.Exception(()=>Create(commands:[new(CommandId,unknown,"cmd","Command","Flight",false)])),
   "binding"=>Record.Exception(()=>Create(bindings:[new(Guid.NewGuid(),ProfileId,ControlId,unknown,BindingDisposition.Added,"JOY_BTN1",[],null)])),
   "axis"=>Record.Exception(()=>Create(bindings:[new(Guid.NewGuid(),ProfileId,ControlId,CommandId,BindingDisposition.Added,"JOY_X",[],new(Invert:true))])),
   "usage"=>Record.Exception(()=>Create(usage:[new(ModuleId,unknown,true)])),
   "overlay"=>Record.Exception(()=>Create(overlays:[new(ModuleId,DeviceId,ControlId,unknown,"override")])),
   "tombstone"=>Record.Exception(()=>Create(tombstones:[new(unknown,"old","removed")])),
   "page"=>Record.Exception(()=>Create(pages:[new(Guid.NewGuid(),ModuleId,unknown,KneeboardPageKind.Device,"mfd","MFD",[CategoryId],0)])),
   "documentation"=>Record.Exception(()=>Create(documentation:[new(unknown,"docs/x.md","X")])),
   "package"=>Record.Exception(()=>Create(packages:[new(unknown,"p","complete","ovgme",[])])),
   _=>throw new InvalidOperationException()
  };
  Assert.IsType<ArgumentException>(error);
 }
 [Fact] public void Snapshot_version_and_required_collections_are_validated() {
  Assert.Throws<ArgumentException>(()=>Create(snapshot:new(99,"hash")));
  Assert.Throws<ArgumentNullException>(()=>new VersionedDomainSchema(null!,[],[],[],[],[],[],[],[],[],[],[],[],[],[],[],new(1,"hash"),new(1,0,0)));
 }
 private static VersionedDomainSchema Create(
  IEnumerable<PhysicalDevice>? devices=null,IEnumerable<DeviceControl>? controls=null,IEnumerable<DeviceInstance>? instances=null,
  IEnumerable<DcsCommandDefinition>? commands=null,IEnumerable<BindingRelationship>? bindings=null,IEnumerable<UiLayerModuleUsage>? usage=null,
  IEnumerable<UiLayerOverlay>? overlays=null,IEnumerable<UiLayerTombstone>? tombstones=null,IEnumerable<KneeboardPage>? pages=null,
  IEnumerable<DocumentationMetadata>? documentation=null,IEnumerable<PackagingMetadata>? packages=null,SnapshotIdentity? snapshot=null) {
  var module=new DcsModule(ModuleId,"UH-1H","Huey");
  var profile=new DeviceProfile(ProfileId,"tm-mfd","MFD1","pilot-left","MFD.diff.lua");
  return new([module],
   devices??[new(DeviceId,"tm-mfd","MFD",["Thrustmaster MFD"])],
   controls??[new(ControlId,DeviceId,"osb-1","OSB 1",ControlKind.Button,new(0.1,0.2,0.3,0.4,"normalized"))],
   instances??[new(InstanceId,ModuleId,DeviceId,"mfd-1","pilot-left")],[profile],
   [new(CatalogId,"dcs-common-ui","DCS-Common","1")],
   commands??[new(CommandId,CatalogId,"iCommandPlaneFire","Fire","Flight",false)],
   bindings??[new(Guid.Parse("90000000-0000-0000-0000-000000000001"),ProfileId,ControlId,CommandId,BindingDisposition.Added,"JOY_BTN1",["SHIFT"],null)],
   usage??[new(ModuleId,CommandId,true)],overlays??[new(ModuleId,DeviceId,ControlId,CommandId,"Fire")],
   tombstones??[new(ModuleId,"legacy-command","Removed by module")],
   [new(CategoryId,"flight","Flight",0)],
   pages??[new(Guid.Parse("a0000000-0000-0000-0000-000000000001"),ModuleId,InstanceId,KneeboardPageKind.Device,"mfd","MFD",[CategoryId],0)],
   documentation??[new(ModuleId,"docs/controls.md","Huey controls")],
   packages??[new(ModuleId,"UH-1H Complete","complete","ovgme",["**/*"])],
   [new("ScottyMac52/DCS-Common","abc","assets/ui-layer/functions.json","hash")],
   snapshot??new(1,"hash"),new(1,2,3,4));
 }
}
