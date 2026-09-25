using System.Text.Json.Nodes;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dcs.Ipia.Tests;

/// <summary>Checks selector data in workspaces saved before shifted controls were mapped.</summary>
public sealed class LegacyEditorWorkspaceUpgradeTests {
 #region BEGIN Public
 /// <summary>Restores legacy shifted callouts, preserves edits, and does not resurrect later deletions.</summary>
 /// <returns>Completion of the SQLite round trip.</returns>
 [Fact] public async Task Legacy_saved_workspace_reopens_distinct_scope_and_modifier_assignments() {
  var path=Path.Combine(Path.GetTempPath(),"ipia-legacy-"+Guid.NewGuid().ToString("N")+".db");
  var connectionString="Data Source="+path+";Pooling=False";
  try {
   await new SqliteMigrationRunner(connectionString).MigrateAsync();
   var module=new DcsModule(Guid.NewGuid(),"UH-1H","Huey");
   var profile=new DeviceProfile(Guid.NewGuid(),"tm-mfd",null,"MFD","MFD.diff.lua");
   var configuration=new ModuleConfiguration(module,[profile],
    [new DcsModifier("Shift","MFD","JOY_BTN7",ModifierMode.Hold)],[],"import");
   await new SqliteConfigurationRepository(connectionString).ReplaceAsync(configuration);
   const string svg="<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'>"+
    "<!-- box:osb-1 --><rect x='10' y='10' width='25' height='20'/>"+
    "<!-- box:osb-1-shifted --><rect x='40' y='10' width='25' height='20'/>"+
    "<text id='lbl-osb-1-shifted'>OSB 1 shifted</text></svg>";
   var diagram=new UiLayerDiagram("MFD",null,"assets/shared/hardware/svg/tm-mfd.svg",100,100,[],[
    new UiLayerCallout(1,"osb-1-shifted","UI shifted","Shift",40,10,25,20) {CommandId="UI_SHIFT"}
   ]) {Svg=svg,ProfileId=profile.Id,Controls=[new HardwareGraphicControl("osb-1","OSB 1","JOY_BTN1",10,10,25,20)]};
   var baseSlot=new DraftAssignmentSlot(module.Id,profile.Id,AssignmentLayer.Module,BindingSection.Key,"JOY_BTN1");
   var shiftSlot=baseSlot with {ModifierChord="Shift"};
   var baseCommand=new DraftCommand("BASE","Module base",false,AssignmentLayer.Module);
   var shiftCommand=new DraftCommand("SHIFT","Module shifted",false,AssignmentLayer.Module);
   var uiCommand=new DcsCommandDefinition(Guid.NewGuid(),Guid.NewGuid(),"UI_SHIFT","UI shifted","General",false);
   var initial=new EditorWorkspaceSnapshot(configuration,[],[],[diagram],
    [new CommandPaletteEntry("BASE","Module base","General",false),
     new CommandPaletteEntry("SHIFT","Module shifted","General",false)],
    [uiCommand],[new DraftAssignment(baseSlot,baseCommand),new DraftAssignment(shiftSlot,shiftCommand)],
    new Dictionary<string,string> {[baseSlot.Key]="Custom base"});
   var repository=new SqliteEditorWorkspaceRepository(connectionString);
   await repository.SaveAsync(initial);
   await using(var connection=new SqliteConnection(connectionString)) {
    await connection.OpenAsync();
    var read=connection.CreateCommand();
    read.CommandText="SELECT snapshot_json FROM editor_workspaces WHERE module_id=$id";
    read.Parameters.AddWithValue("$id",module.Id.ToString("D"));
    var node=JsonNode.Parse((string)(await read.ExecuteScalarAsync())!)!.AsObject();
    node.Remove("Version");
    var write=connection.CreateCommand();
    write.CommandText="UPDATE editor_workspaces SET snapshot_json=$json WHERE module_id=$id";
    write.Parameters.AddWithValue("$json",node.ToJsonString());
    write.Parameters.AddWithValue("$id",module.Id.ToString("D"));
    await write.ExecuteNonQueryAsync();
   }
   var restored=(await repository.GetAsync(module.Id))!;
   Assert.Equal(2,restored.Diagrams[0].Controls.Count);
   Assert.Contains(restored.Diagrams[0].Controls,x=>x.Control=="osb-1-shifted"&&x.InputKey=="JOY_BTN1"&&x.X==40);
   Assert.Equal("Module base",restored.Assignments.Single(x=>x.Slot.Key==baseSlot.Key).Command.Name);
   Assert.Equal("Module shifted",restored.Assignments.Single(x=>x.Slot.Key==shiftSlot.Key).Command.Name);
   var uiSlot=shiftSlot with {Layer=AssignmentLayer.UiLayer};
   Assert.Equal("UI shifted",restored.Assignments.Single(x=>x.Slot.Key==uiSlot.Key).Command.Name);
   Assert.Equal("Custom base",restored.Labels[baseSlot.Key]);
   var draft=new DraftAssignmentService(configuration,
    [baseCommand,shiftCommand,new DraftCommand("UI_SHIFT","UI shifted",false,AssignmentLayer.UiLayer)],restored.Assignments);
   Assert.Equal(DraftOutcome.Applied,draft.Remove(uiSlot).Outcome);
   await repository.SaveAsync(restored with {Assignments=draft.Snapshot});
   var reopened=(await repository.GetAsync(module.Id))!;
   Assert.DoesNotContain(reopened.Assignments,x=>x.Slot.Key==uiSlot.Key);
  }
  finally {File.Delete(path);}
 }
 #endregion END Public
}
