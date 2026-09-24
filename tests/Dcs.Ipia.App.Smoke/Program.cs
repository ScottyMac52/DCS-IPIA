using System.IO;
using System.Windows.Controls;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
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
   [new DcsModifier("MOZA_MODIFIER_BTN3","tm-mfd","JOY_BTN5",ModifierMode.Hold)],[],"smoke fixture");
  var imported=new HueyImportResult(configuration,[],[],[],[],
   new Dictionary<string,System.Text.Json.JsonElement>(),new Dictionary<string,string>(),
   [new SourceProvenance("Huey",new string('a',40),"config/kneeboard.json","hash")],[]);
  var image=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/ZQAAAABJRU5ErkJggg==");
  var svg=$"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 150'><image href='data:image/png;base64,{Convert.ToBase64String(image)}' width='200' height='150'/><!-- box:mfd-osb-t1-shifted --><rect x='10' y='10' width='60' height='40' fill='white'/><text id='lbl-mfd-osb-t1-shifted' x='20' y='20'>OSB01</text><!-- box:mfd-osb-t2 --><rect x='80' y='10' width='60' height='40' fill='white'/><text id='lbl-mfd-osb-t2' x='90' y='20'>OSB02</text></svg>";
  var diagram=new UiLayerDiagram("TM MFD","MFD3","assets/shared/hardware/svg/tm-mfd.svg",200,150,image,
   [new UiLayerCallout(1,"mfd-osb-t1-shifted","VR Zoom","MOZA_MODIFIER_BTN3",10,10,60,40) {CommandId="UI_ZOOM"}]) {
   Svg=svg,ProfileId=mfd.Id,Controls=[new("mfd-osb-t1-shifted","OSB01","JOY_BTN1",10,10,60,40),new("mfd-osb-t2","OSB02","JOY_BTN2",80,10,60,40)]
  };
  var preview=new HueyWorkspacePreview(imported,[],[diagram]) {
   UiCommands=[new(Guid.NewGuid(),Guid.NewGuid(),"UI_ZOOM","VR Zoom","VR",false)]
  };
  var repoWindow=new HueyImportWindow(new HueyImportViewModel(preview));
  repoWindow.Show();
  repoWindow.UpdateLayout();
  ((ComboBox)repoWindow.FindName("UiModifierChoice")!).SelectedIndex=1;
  var canvas=(Canvas)repoWindow.FindName("UiDiagramCanvas")!;
  var assignments=(ListBox)repoWindow.FindName("UiAssignmentList")!;
  var palette=(ListBox)repoWindow.FindName("CommandPaletteList")!;
  var scope=(ComboBox)repoWindow.FindName("PaletteScope")!;
  var search=(TextBox)repoWindow.FindName("PaletteSearch")!;
  if(!repoWindow.IsVisible||canvas.Children.Count!=3||assignments.Items.Count!=2||
     !assignments.Items[0]!.ToString()!.Contains("VR Zoom",StringComparison.Ordinal))

   throw new InvalidOperationException($"Committed Huey preview: visible={repoWindow.IsVisible}, canvas children={canvas.Children.Count}, assignments={assignments.Items.Count}, caption={((TextBlock)repoWindow.FindName("UiDiagramCaption")!).Text}.");
  scope.SelectedIndex=0;
  if(palette.Items.Count!=0)throw new InvalidOperationException("Empty aircraft import must have an empty aircraft palette.");
  scope.SelectedIndex=1;
  search.Text="Zoom";
  if(palette.Items.Count!=1)throw new InvalidOperationException("Canonical UI Layer command was not searchable.");
  palette.SelectedIndex=0;
  assignments.SelectedIndex=1;
  ((Button)repoWindow.FindName("AssignSelectedCommand")!).RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
  if(!assignments.Items[1]!.ToString()!.Contains("VR Zoom",StringComparison.Ordinal))
   throw new InvalidOperationException("Assign did not update the visible OSB02 callout.");
  ((Button)repoWindow.FindName("UndoDraft")!).RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
  if(assignments.Items[1]!.ToString()!.Contains("VR Zoom",StringComparison.Ordinal))
   throw new InvalidOperationException("Undo did not restore the empty OSB02 callout.");
  repoWindow.Close();
  folderWindow.Close();
  var preferencesRoot=Path.Combine(Path.GetTempPath(),"dcs-ipia-settings-smoke-"+Guid.NewGuid().ToString("N"));
  try {
   var settings=new FileAppSettingsService(Path.Combine(preferencesRoot,"defaults.json"),
    Path.Combine(preferencesRoot,"user-settings.json"),Path.Combine(preferencesRoot,"theme.txt"));
   var preferencesWindow=new SettingsWindow(settings);
   preferencesWindow.Show();
   preferencesWindow.UpdateLayout();
   ((ComboBox)preferencesWindow.FindName("ThemeChoice")!).SelectedIndex=1;
   ((ComboBox)preferencesWindow.FindName("ConflictChoice")!).SelectedIndex=2;
   if(settings.Current.Theme!=AppearanceTheme.Dark || settings.Current.OccupiedDestinationBehavior!=OccupiedDestinationBehavior.Reject ||
      !File.Exists(Path.Combine(preferencesRoot,"user-settings.json")))
    throw new InvalidOperationException("Settings window did not apply and save theme and destination preferences.");
   preferencesWindow.Close();
  }
  finally {if(Directory.Exists(preferencesRoot))Directory.Delete(preferencesRoot,true);}
  System.Console.WriteLine("Both WPF import previews opened and displayed their expected content.");
  return 0;
 }
 #endregion END Public
}
