using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Xunit;

namespace Dcs.Ipia.Tests;

/// <summary>Checks local persistence of graphical editor data and saved assignments.</summary>
public sealed class EditorWorkspacePersistenceTests {
 #region BEGIN Public
 /// <summary>Reopens the actual SVG, named categories, and changed callout assignment without source files.</summary>
 /// <returns>Completion of the database round trip.</returns>
 [Fact] public async Task Editor_reopens_saved_svg_catalog_assignment_and_label() {
  var path=Path.Combine(Path.GetTempPath(),"ipia-workspace-"+Guid.NewGuid().ToString("N")+".db");
  var connection="Data Source="+path+";Pooling=False";
  try {
   await new SqliteMigrationRunner(connection).MigrateAsync();
   var module=new DcsModule(Guid.NewGuid(),"UH-1H","Huey");
   var profile=new DeviceProfile(Guid.NewGuid(),"tm-mfd",null,"MFD","MFD.diff.lua");
   var configuration=new ModuleConfiguration(module,[profile],[],[],"import");
   await new SqliteConfigurationRepository(connection).ReplaceAsync(configuration);
   const string svg="<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><rect width='100' height='100'/></svg>";
   var diagram=new UiLayerDiagram("MFD",null,"assets/shared/hardware/svg/tm-mfd.svg",100,100,[],[]) {
    Svg=svg,ProfileId=profile.Id,Controls=[new HardwareGraphicControl("osb-1","OSB1","JOY_BTN1",10,10,25,20)]
   };
   var command=new CommandPaletteEntry("FIRE","Fire","Weapons",false);
   var slot=new DraftAssignmentSlot(module.Id,profile.Id,AssignmentLayer.Module,BindingSection.Key,"JOY_BTN1");
   var initial=new EditorWorkspaceSnapshot(configuration,[],[],[diagram],[command],[],[],
    new Dictionary<string,string>());
   var repository=new SqliteEditorWorkspaceRepository(connection);
   await repository.SaveAsync(initial);
   var draft=new DraftAssignmentService(configuration,[new DraftCommand(command.Id,command.Name,false,AssignmentLayer.Module)],[]);
   Assert.Equal(DraftOutcome.Applied,draft.Assign(slot,new DraftCommand(command.Id,command.Name,false,AssignmentLayer.Module),
    OccupiedDestinationBehavior.Ask).Outcome);
   await repository.SaveAsync(initial with {Assignments=draft.Snapshot,Labels=new Dictionary<string,string> {[slot.Key]="Trigger"}});
   var restored=await repository.GetAsync(module.Id);
   Assert.NotNull(restored);
   Assert.Equal(svg,restored.Diagrams[0].Svg);
   Assert.Equal("Weapons",restored.ModuleCommands[0].Category);
   Assert.Equal("FIRE",Assert.Single(restored.Assignments).Command.Id);
   Assert.Equal("Trigger",restored.Labels[slot.Key]);
   var secondModule=new DcsModule(Guid.NewGuid(),"F-14BU","Tomcat");
   var secondConfig=new ModuleConfiguration(secondModule,[],[],[],"import");
   await new SqliteConfigurationRepository(connection).ReplaceAsync(secondConfig);
   await repository.SaveAsync(new EditorWorkspaceSnapshot(secondConfig,[],[],[],[],[],[],
    new Dictionary<string,string>(),new Dictionary<string,string> {["config/kneeboard.json"]="{}"}));
   Assert.Equal(2,(await repository.ListAsync()).Count);
   var second=await repository.GetAsync(secondModule.Id);
   Assert.Equal("F-14BU",second!.Configuration.Module.DcsId);
   Assert.Equal("{}",second.Sources!["config/kneeboard.json"]);
   Assert.Equal("Trigger",(await repository.GetAsync(module.Id))!.Labels[slot.Key]);
  }
  finally {File.Delete(path);}
 }
 #endregion END Public
}
