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
  var configuration=new ModuleConfiguration(module,[],[],[],"smoke fixture");
  var imported=new HueyImportResult(configuration,[],[],[],[],
   new Dictionary<string,System.Text.Json.JsonElement>(),new Dictionary<string,string>(),
   [new SourceProvenance("Huey",new string('a',40),"config/kneeboard.json","hash")],[]);
  var image=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/ZQAAAABJRU5ErkJggg==");
  var diagram=new UiLayerDiagram("TM MFD","MFD3","assets/shared/hardware/svg/tm-mfd.svg",200,150,image,
   [new UiLayerCallout(1,"mfd-osb-t1-shifted","VR Zoom","MOZA_MODIFIER_BTN3",10,10,40,30)]);
  var preview=new HueyWorkspacePreview(imported,[],[diagram]);
  var repoWindow=new HueyImportWindow(new HueyImportViewModel(preview));
  repoWindow.Show();
  repoWindow.UpdateLayout();
  var canvas=(Canvas)repoWindow.FindName("UiDiagramCanvas")!;
  var assignments=(ListBox)repoWindow.FindName("UiAssignmentList")!;
  if(!repoWindow.IsVisible||canvas.Children.Count!=2||assignments.Items.Count!=1)
   throw new InvalidOperationException($"Committed Huey preview: visible={repoWindow.IsVisible}, canvas children={canvas.Children.Count}, assignments={assignments.Items.Count}, caption={((TextBlock)repoWindow.FindName("UiDiagramCaption")!).Text}.");
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
