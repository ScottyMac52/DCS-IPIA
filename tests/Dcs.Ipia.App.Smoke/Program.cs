using System.IO;
using System.Windows.Controls;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Microsoft.Data.Sqlite;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke;

/// <summary>Opens both read-only WPF preview modes on a Windows CI runner.</summary>
public static class Program {
 #region BEGIN Public
 [STAThread]
 /// <summary>Checks that imported source data reaches a visible WPF preview window.</summary>
 /// <returns>Zero when both windows render their expected content.</returns>
 public static int Main() {
  var app=new DcsIpia.Presentation.App();
  app.InitializeComponent();
  var profile=new DeviceProfile(Guid.NewGuid(),"F16 MFD 3",null,"unresolved device","F16 MFD 3.diff.lua");
  var folder=new DcsFolderImportResult("Saved Games/Input/UH-1H/joystick",[profile],[],[],
   [new("bad-profile","One other device profile could not be parsed.",true)]);
  var folderWindow=new HueyImportWindow(new HueyImportViewModel(folder));
  folderWindow.Show();
  folderWindow.UpdateLayout();
  var folderTabs=(TabControl)folderWindow.FindName("ImportedTabs")!;
  if(!folderWindow.IsVisible||folderTabs.SelectedIndex!=4)
   throw new InvalidOperationException("Saved Games preview did not show its diagnostics.");
  var module=new DcsModule(Guid.NewGuid(),"UH-1H","Huey");
  var mfd=new DeviceProfile(Guid.NewGuid(),"tm-mfd","MFD3","MFD","MFD3.diff.lua");
  var configuration=new ModuleConfiguration(module,[mfd],
   [new DcsModifier("MOZA_MODIFIER_BTN3","tm-mfd","JOY_BTN5",ModifierMode.Hold)],
   [new DcsBinding(mfd.Id,BindingSection.Key,"MODULE_BASE","Base action",BindingDisposition.Added,"JOY_BTN1"),
    new DcsBinding(mfd.Id,BindingSection.Key,"MODULE_SHIFTED","Modified action",BindingDisposition.Added,"JOY_BTN1",["MOZA_MODIFIER_BTN3"])],
   "smoke fixture");
  var imported=new HueyImportResult(configuration,[],[],[],[],
   new Dictionary<string,System.Text.Json.JsonElement>(),new Dictionary<string,string>(),
   [new SourceProvenance("Huey",new string('a',40),"config/kneeboard.json","hash")],[]);
  var image=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/ZQAAAABJRU5ErkJggg==");
  var svg=$"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 150'><image href='data:image/png;base64,{Convert.ToBase64String(image)}' width='200' height='150'/><!-- box:mfd-osb-t1 --><rect x='10' y='60' width='60' height='40' fill='white'/><text id='lbl-mfd-osb-t1' x='20' y='70'>OSB01</text><!-- box:mfd-osb-t1-shifted --><rect x='10' y='10' width='60' height='40' fill='white'/><text id='lbl-mfd-osb-t1-shifted' x='20' y='20'>OSB01</text><!-- box:mfd-osb-t2 --><rect x='80' y='10' width='60' height='40' fill='white'/><text id='lbl-mfd-osb-t2' x='90' y='20'>OSB02</text><!-- box:mfd-osb-t3 --><rect x='145' y='10' width='45' height='40' fill='white'/><text id='lbl-mfd-osb-t3' x='160' y='20'>OSB03</text></svg>";
  var diagram=new UiLayerDiagram("TM MFD","MFD3","assets/shared/hardware/svg/tm-mfd.svg",200,150,image,
   [new UiLayerCallout(1,"mfd-osb-t1-shifted","VR Zoom","MOZA_MODIFIER_BTN3",10,10,60,40) {CommandId="UI_ZOOM"}]) {
   Svg=svg,ProfileId=mfd.Id,Controls=[new("mfd-osb-t1","OSB01","JOY_BTN1",10,60,60,40),
    new("mfd-osb-t1-shifted","OSB01","JOY_BTN1",10,10,60,40),new("mfd-osb-t2","OSB02","JOY_BTN2",80,10,60,40),
    new("mfd-osb-t3","OSB03","JOY_BTN3",145,10,45,40)]
  };
  var preview=new HueyWorkspacePreview(imported,[],[diagram]) {
   UiCommands=[new(Guid.NewGuid(),Guid.NewGuid(),"UI_ZOOM","VR Zoom","VR",false),
    new(Guid.NewGuid(),Guid.NewGuid(),"UI_PAUSE","Active Pause","General",false)]
  };
  var importedView=new HueyImportViewModel(preview);
  if(importedView.Draft is null||!importedView.Draft.Snapshot.Any(x=>x.Slot.Layer==AssignmentLayer.Module&&x.Slot.ModifierChord=="MOZA_MODIFIER_BTN3")||
     !importedView.Draft.Snapshot.Any(x=>x.Slot.Layer==AssignmentLayer.UiLayer&&x.Slot.ModifierChord=="MOZA_MODIFIER_BTN3"))
   throw new InvalidOperationException("The import dropped shifted module or UI Layer assignments.");
  var repoWindow=new HueyImportWindow(importedView);
  repoWindow.Show();
  repoWindow.UpdateLayout();
  var scratch=Path.Combine(Path.GetTempPath(),"ipia-editor-smoke-"+Guid.NewGuid().ToString("N")+".db");
  var db=new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder {DataSource=scratch,ForeignKeys=true}.ToString();
  new SqliteMigrationRunner(db).MigrateAsync().GetAwaiter().GetResult();
  new SqliteConfigurationRepository(db).ReplaceAsync(configuration).GetAwaiter().GetResult();
  var snapshot=new EditorWorkspaceSnapshot(configuration,[],[],[diagram],
   [new CommandPaletteEntry("MODULE_BASE","Base action","Flight Control",false),
    new CommandPaletteEntry("MODULE_SHIFTED","Modified action","Flight Control",false)],preview.UiCommands,importedView.Draft.Snapshot,
   new Dictionary<string,string>());
  var repository=new SqliteEditorWorkspaceRepository(db);
  repository.SaveAsync(snapshot).GetAwaiter().GetResult();
  var loaded=repository.GetAsync(module.Id).GetAwaiter().GetResult()!;
  if(loaded.Diagrams[0].Svg!=svg||loaded.ModuleCommands[0].Category!="Flight Control")
   throw new InvalidOperationException("Saved SVG or category was not recovered from SQLite.");
  var savedWindow=new HueyImportWindow(new HueyImportViewModel(loaded),null,repository);
  savedWindow.Show();
  savedWindow.UpdateLayout();
  if(!savedWindow.IsVisible||((Canvas)savedWindow.FindName("Diagram")!).Children.Count==0)
   throw new InvalidOperationException("Database-backed graphical assignment editor did not display.");
  var layers=(ComboBox)savedWindow.FindName("Layers")!;
  var scope=(ComboBox)savedWindow.FindName("Scope")!;
  string Caption(string control) {
   var canvas=(Canvas)savedWindow.FindName("Diagram")!;
   var surface=canvas.Children.OfType<Canvas>().Single();
   var target=surface.Children.OfType<Border>().Single(x=>x.Tag is HardwareGraphicControl item&&item.Control==control);
   return ((TextBlock)target.Child).Text;
  }
  if(Caption("mfd-osb-t1")!="Base action"||
     ((Canvas)savedWindow.FindName("Diagram")!).Children.OfType<Canvas>().Single()
       .Children.OfType<Border>().Any(x=>x.Tag is HardwareGraphicControl item&&item.Control=="mfd-osb-t1-shifted"))
   throw new InvalidOperationException("Base layer did not show only its own assignment.");
  layers.SelectedIndex=1;
  savedWindow.UpdateLayout();
  if(Caption("mfd-osb-t1-shifted")!="Modified action")
   throw new InvalidOperationException("Modifier selection did not show the shifted module assignment.");
  scope.SelectedIndex=1;
  savedWindow.UpdateLayout();
  if(Caption("mfd-osb-t1-shifted")!="VR Zoom")
   throw new InvalidOperationException("Scope selection did not show the shifted UI Layer assignment.");
  savedWindow.Close();
  SqliteConnection.ClearAllPools();
  File.Delete(scratch);
  repoWindow.Close();
  folderWindow.Close();
  var preferencesRoot=Path.Combine(Path.GetTempPath(),"dcs-ipia-settings-smoke-"+Guid.NewGuid().ToString("N"));
  try {
   Directory.CreateDirectory(preferencesRoot);
   var preferenceConnection="Data Source="+Path.Combine(preferencesRoot,"ipia.db")+";Pooling=False";
   new SqliteMigrationRunner(preferenceConnection).MigrateAsync().GetAwaiter().GetResult();
   var settings=new SqliteAppSettingsService(preferenceConnection,Path.Combine(preferencesRoot,"defaults.json"),
    Path.Combine(preferencesRoot,"user-settings.json"),Path.Combine(preferencesRoot,"theme.txt"));
   var preferencesWindow=new SettingsWindow(settings);
   preferencesWindow.Show();
   preferencesWindow.UpdateLayout();
   ((ComboBox)preferencesWindow.FindName("ThemeChoice")!).SelectedIndex=1;
   ((ComboBox)preferencesWindow.FindName("ConflictChoice")!).SelectedIndex=2;
   if(settings.Current.Theme!=AppearanceTheme.Dark || settings.Current.OccupiedDestinationBehavior!=OccupiedDestinationBehavior.Reject ||
      new SqliteAppSettingsService(preferenceConnection,Path.Combine(preferencesRoot,"defaults.json"),
       Path.Combine(preferencesRoot,"user-settings.json"),Path.Combine(preferencesRoot,"theme.txt")).Current.OccupiedDestinationBehavior!=OccupiedDestinationBehavior.Reject)
    throw new InvalidOperationException("Settings window did not apply and save theme and destination preferences.");
   preferencesWindow.Close();
  }
  finally {SqliteConnection.ClearAllPools();if(Directory.Exists(preferencesRoot))Directory.Delete(preferencesRoot,true);}
  System.Console.WriteLine("Both WPF import previews opened and displayed their expected content.");
  return 0;
 }
 #endregion END Public
}
