using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
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
  var svg=$"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 150'><image href='data:image/png;base64,{Convert.ToBase64String(image)}' width='200' height='150'/><!-- box:mfd-osb-t1-shifted --><rect x='10' y='10' width='60' height='40' fill='white'/><text id='lbl-mfd-osb-t1-shifted' x='20' y='20'>OSB01</text><!-- box:mfd-osb-t2 --><rect x='80' y='10' width='60' height='40' fill='white'/><text id='lbl-mfd-osb-t2' x='90' y='20'>OSB02</text><!-- box:mfd-osb-t3 --><rect x='145' y='10' width='45' height='40' fill='white'/><text id='lbl-mfd-osb-t3' x='160' y='20'>OSB03</text></svg>";
  var diagram=new UiLayerDiagram("TM MFD","MFD3","assets/shared/hardware/svg/tm-mfd.svg",200,150,image,
   [new UiLayerCallout(1,"mfd-osb-t1-shifted","VR Zoom","MOZA_MODIFIER_BTN3",10,10,60,40) {CommandId="UI_ZOOM"}]) {
   Svg=svg,ProfileId=mfd.Id,Controls=[new("mfd-osb-t1-shifted","OSB01","JOY_BTN1",10,10,60,40),new("mfd-osb-t2","OSB02","JOY_BTN2",80,10,60,40),
    new("mfd-osb-t3","OSB03","JOY_BTN3",145,10,45,40)]
  };
  var preview=new HueyWorkspacePreview(imported,[],[diagram]) {
   UiCommands=[new(Guid.NewGuid(),Guid.NewGuid(),"UI_ZOOM","VR Zoom","VR",false),
    new(Guid.NewGuid(),Guid.NewGuid(),"UI_PAUSE","Active Pause","General",false)]
  };
  var repoModel=new HueyImportViewModel(preview);
  var repoWindow=new HueyImportWindow(repoModel);
  repoWindow.Show();
  repoWindow.UpdateLayout();
  var scope=(ComboBox)repoWindow.FindName("PaletteScope")!;
  if(scope.SelectedIndex!=0||((TabControl)repoWindow.FindName("ImportedTabs")!).SelectedIndex!=0)
   throw new InvalidOperationException("Editor should open the live assignment canvas with module commands selected.");
  if(((FrameworkElement)repoWindow.FindName("ImportSummary")!).Visibility!=Visibility.Collapsed)
   throw new InvalidOperationException("Import metadata displaced the scaffolding editor layout.");
  scope.SelectedIndex=1;
  ((ComboBox)repoWindow.FindName("UiModifierChoice")!).SelectedIndex=1;
  var canvas=(Canvas)repoWindow.FindName("UiDiagramCanvas")!;
  var assignments=(ListBox)repoWindow.FindName("UiAssignmentList")!;
  var palette=(ListBox)repoWindow.FindName("CommandPaletteList")!;
  var search=(TextBox)repoWindow.FindName("PaletteSearch")!;
  var categories=(ComboBox)repoWindow.FindName("PaletteCategory")!;
  var hitControl=typeof(HueyImportWindow).GetMethod("HitControl",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
  var hit=hitControl.Invoke(repoWindow,[new System.Windows.Point(100,30)]) as HardwareGraphicControl;
  if(hit?.Control!=diagram.Controls[1].Control||!canvas.IsVisible||
     canvas.Children.OfType<Border>().Count(x=>x.AllowDrop)!=diagram.Controls.Count)
   throw new InvalidOperationException("The visible authored callouts are not WPF drop targets at their canvas coordinates.");
  if(!repoWindow.IsVisible||canvas.Children.Count!=4||assignments.Items.Count!=3||
     !assignments.Items[0]!.ToString()!.Contains("VR Zoom",StringComparison.Ordinal))

   throw new InvalidOperationException($"Committed Huey preview: visible={repoWindow.IsVisible}, canvas children={canvas.Children.Count}, assignments={assignments.Items.Count}, caption={((TextBlock)repoWindow.FindName("UiDiagramCaption")!).Text}.");
  scope.SelectedIndex=0;
  if(palette.Items.Count!=0)throw new InvalidOperationException("Empty aircraft import must have an empty aircraft palette.");
  scope.SelectedIndex=1;
  search.Text="Zoom";
  if(palette.Items.Count!=1)throw new InvalidOperationException("Canonical UI Layer command was not searchable.");
  categories.SelectedItem="General";
  if(palette.Items.Count!=0)throw new InvalidOperationException("Category and search were not combined.");
  search.Text="";
  if(palette.Items.Count!=1||!palette.Items[0]!.ToString()!.Contains("Active Pause",StringComparison.Ordinal))
   throw new InvalidOperationException("Category filter did not show only General commands.");
  categories.SelectedItem="VR";
  if(palette.Items.Count!=1)throw new InvalidOperationException("VR category did not show its command.");
  var assignedBadge=canvas.Children.OfType<Border>().Single(x=>
   ((HardwareGraphicControl)x.Tag).Control==diagram.Controls[0].Control);
  assignedBadge.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left) {
   RoutedEvent=UIElement.MouseLeftButtonDownEvent
  });
  var badgePayload=typeof(HueyImportWindow).GetMethod("PointerData",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!
   .Invoke(repoWindow,null) as System.Windows.IDataObject;
  if(badgePayload?.GetData(System.Windows.DataFormats.UnicodeText) is not string badgeText||
     badgeText!="Dcs.Ipia.Editor.HardwareControl\n"+diagram.Controls[0].Control)
   throw new InvalidOperationException("Pressing an assigned callout did not arm a control-to-control drag.");
  typeof(HueyImportWindow).GetMethod("ResetPointerDrag",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!
   .Invoke(repoWindow,null);
  if(Environment.GetEnvironmentVariable("DCS_IPIA_INTERACTIVE_MOUSE_TEST")=="1") {
  repoWindow.Left=0;repoWindow.Top=0;repoWindow.Width=1200;repoWindow.Height=720;
  repoWindow.Activate();repoWindow.UpdateLayout();
  palette.ScrollIntoView(palette.Items[0]);repoWindow.UpdateLayout();
  var dragRow=(ListBoxItem)palette.ItemContainerGenerator.ContainerFromIndex(0)!;
  DragMouse(repoWindow,dragRow,canvas,new Point(110,30));
  if(!assignments.Items[1]!.ToString()!.Contains("VR Zoom",StringComparison.Ordinal))
   throw new InvalidOperationException($"A physical mouse drag from the command row did not assign OSB02. Status: {((TextBlock)repoWindow.FindName("EditorStatus")!).Text}; row visible={dragRow.IsVisible}; canvas visible={canvas.IsVisible}; from={dragRow.PointToScreen(new Point(dragRow.ActualWidth/2,dragRow.ActualHeight/2))}; to={canvas.PointToScreen(new Point(110,30))}; screen={SystemParameters.PrimaryScreenWidth}x{SystemParameters.PrimaryScreenHeight}.");
  ((Button)repoWindow.FindName("UndoDraft")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  repoWindow.UpdateLayout();
  var sourceBadge=canvas.Children.OfType<Border>().Single(x=>((HardwareGraphicControl)x.Tag).Control==diagram.Controls[0].Control);
  DragMouse(repoWindow,sourceBadge,canvas,new Point(110,30));
  if(!assignments.Items[1]!.ToString()!.Contains("VR Zoom",StringComparison.Ordinal)||
     assignments.Items[0]!.ToString()!.Contains("VR Zoom",StringComparison.Ordinal))
   throw new InvalidOperationException("A physical mouse drag between assigned callouts did not move the command.");
  ((Button)repoWindow.FindName("UndoDraft")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  }
  var types=(ComboBox)repoWindow.FindName("PaletteType")!;
  var availability=(ComboBox)repoWindow.FindName("PaletteAvailability")!;
  var bindingState=(ComboBox)repoWindow.FindName("PaletteBindingState")!;
  types.SelectedIndex=2;
  if(palette.Items.Count!=0)throw new InvalidOperationException("Axis filter included a button command.");
  types.SelectedIndex=1;
  assignments.SelectedIndex=0;
  availability.SelectedIndex=1;
  if(palette.Items.Count!=1)throw new InvalidOperationException("Selected-control compatibility hid the button command.");
  bindingState.SelectedIndex=1;
  if(palette.Items.Count!=1)throw new InvalidOperationException("Assigned filter missed the imported command on this modifier.");
  if(((TextBlock)repoWindow.FindName("SelectedAssignment")!).Text!="VR Zoom"||
     ((TextBlock)repoWindow.FindName("PendingAssignment")!).Text!="No pending change")
   throw new InvalidOperationException("Selected control did not show imported and pending command state.");
  bindingState.SelectedIndex=0;availability.SelectedIndex=0;types.SelectedIndex=0;
  var payload=new System.Windows.DataObject();
  payload.SetData(System.Windows.DataFormats.UnicodeText,"Dcs.Ipia.Editor.PaletteCommand\nUI_ZOOM");
  var drop=typeof(HueyImportWindow).GetMethod("ApplyDrop",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
  var canDrop=typeof(HueyImportWindow).GetMethod("CanDrop",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
  if(!(bool)canDrop.Invoke(repoWindow,[payload,hit])!)
   throw new InvalidOperationException("The visible callout rejected a valid dragged palette command.");
  drop.Invoke(repoWindow,[diagram.Controls[1],payload,false]);
  if(!assignments.Items[1]!.ToString()!.Contains("VR Zoom",StringComparison.Ordinal))
   throw new InvalidOperationException("Palette drop did not update OSB02.");
  var moved=new System.Windows.DataObject();
  moved.SetData(System.Windows.DataFormats.UnicodeText,"Dcs.Ipia.Editor.HardwareControl\n"+diagram.Controls[1].Control);
  drop.Invoke(repoWindow,[diagram.Controls[2],moved,true]);
  if(!assignments.Items[1]!.ToString()!.Contains("VR Zoom",StringComparison.Ordinal)||
     !assignments.Items[2]!.ToString()!.Contains("VR Zoom",StringComparison.Ordinal))
   throw new InvalidOperationException("Ctrl-drag copy did not preserve source and fill destination.");
  ((Button)repoWindow.FindName("UndoDraft")!).RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
  ((Button)repoWindow.FindName("UndoDraft")!).RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
  if(assignments.Items[1]!.ToString()!.Contains("VR Zoom",StringComparison.Ordinal))
   throw new InvalidOperationException("Undo did not restore the empty OSB02 callout.");
  const string dcsCatalog="""{"schemaVersion":1,"moduleId":"UH-1H","commands":[{"bindingKey":"dAuto","name":"Autopilot On","categoryPath":["Autopilot"],"type":"button"},{"bindingKey":"aCollective","name":"Collective","categoryPath":["Flight Controls"],"type":"axis"}]}""";
  repoModel.LoadModuleCatalog(DcsCommandCatalogImporter.Parse(dcsCatalog,"UH-1H"));
  scope.SelectedIndex=0;
  if(palette.Items.Count!=2||!categories.Items.Contains("Autopilot")||!categories.Items.Contains("Flight Controls")||
     categories.Items.Contains("Key")||categories.Items.Contains("Axis"))
   throw new InvalidOperationException("Module catalog did not provide named categories and full command list.");
  categories.SelectedItem="Autopilot";
  repoWindow.UpdateLayout();
  var commandRow=(ListBoxItem)palette.ItemContainerGenerator.ContainerFromIndex(0)!;
  var categoryLabels=FindVisualChildren<TextBlock>(commandRow).Select(x=>x.Text).ToArray();
  if(!categoryLabels.Contains("Autopilot"))
   throw new InvalidOperationException("The visible command row omitted its authored JSON category.");
  var press=new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left) {
   RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent
  };
  commandRow.RaiseEvent(press);
  var pointerData=typeof(HueyImportWindow).GetMethod("PointerData",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
  var stagedPointer=pointerData.Invoke(repoWindow,null) as System.Windows.IDataObject;
  if(stagedPointer?.GetData(System.Windows.DataFormats.UnicodeText) is not string dragText||
     dragText!="Dcs.Ipia.Editor.PaletteCommand\ndAuto"||
     !(bool)canDrop.Invoke(repoWindow,[stagedPointer,diagram.Controls[1]])!)
   throw new InvalidOperationException($"Pressing a visible command row did not arm a compatible assignment drag: status={((TextBlock)repoWindow.FindName("EditorStatus")!).Text}; payload={stagedPointer?.GetData(System.Windows.DataFormats.UnicodeText)}; row source={commandRow.DataContext}; scope={scope.SelectedIndex}; target={diagram.Controls[1].Control}.");
  typeof(HueyImportWindow).GetMethod("ResetPointerDrag",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!
   .Invoke(repoWindow,null);
  if(palette.Items.Count!=1||!palette.Items[0]!.ToString()!.Contains("Autopilot On",StringComparison.Ordinal))
   throw new InvalidOperationException("Autopilot category did not filter aircraft commands.");
  var incompatible=new System.Windows.DataObject();
  incompatible.SetData("Dcs.Ipia.Editor.PaletteCommand","aCollective");
  if((bool)canDrop.Invoke(repoWindow,[incompatible,diagram.Controls[1]])!)
   throw new InvalidOperationException("An axis command was accepted on a button drop target.");
  categories.SelectedIndex=0;
  types.SelectedIndex=2;
  if(palette.Items.Count!=1||!palette.Items[0]!.ToString()!.Contains("Collective",StringComparison.Ordinal))
   throw new InvalidOperationException("Axis type filter was mixed into the category selector.");
  types.SelectedIndex=0;
  const string dcsHtml="<html><h1>MFD 1</h1><table><tr><td>JOY_BTN1</td><td>Canopy open</td><td>Canopy</td><td>d3011pnilu3011cd35vd1vpnilvu0</td></tr></table></html>";
  repoModel.LoadModuleCatalog(DcsHtmlCommandCatalogImporter.Parse([("MFD 1.html",dcsHtml)]));
  scope.SelectedIndex=1;scope.SelectedIndex=0;
  if(!categories.Items.Contains("Canopy")||categories.Items.Contains("Key")||categories.Items.Contains("Axis"))
   throw new InvalidOperationException("DCS HTML category did not reach the module category selector.");
  categories.SelectedItem="Canopy";
  if(palette.Items.Count!=1||!palette.Items[0]!.ToString()!.Contains("Canopy open",StringComparison.Ordinal))
   throw new InvalidOperationException("DCS HTML named category did not filter the module palette.");
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
 #region BEGIN private helpers
 private const uint MouseLeftDown=0x0002;
 private const uint MouseLeftUp=0x0004;
 [DllImport("user32.dll")]
 private static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")]
 private static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]
 private static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extraInfo);
 private static void DragMouse(HueyImportWindow window,FrameworkElement source,Canvas canvas,Point destination) {
  window.UpdateLayout();
  var from=source.PointToScreen(new Point(source.ActualWidth/2,source.ActualHeight/2));
  var to=canvas.PointToScreen(destination);
  Console.WriteLine($"Mouse gesture: foreground={GetForegroundWindow()}, window={new WindowInteropHelper(window).Handle}, from={from}, to={to}.");
  var frame=new DispatcherFrame();
  var drag=Task.Run(()=>{
   if(!SetCursorPos((int)from.X,(int)from.Y))throw new InvalidOperationException("Mouse input is unavailable on this Windows runner.");
   Thread.Sleep(100);
   mouse_event(MouseLeftDown,0,0,0,UIntPtr.Zero);
   Thread.Sleep(100);
   for(var step=1;step<=15;step++) {
    var fraction=step/15d;
    SetCursorPos((int)(from.X+(to.X-from.X)*fraction),(int)(from.Y+(to.Y-from.Y)*fraction));
    Thread.Sleep(35);
   }
   Thread.Sleep(100);
   mouse_event(MouseLeftUp,0,0,0,UIntPtr.Zero);
  });
  drag.ContinueWith(_=>window.Dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)));
  Dispatcher.PushFrame(frame);
  drag.GetAwaiter().GetResult();
  window.UpdateLayout();
 }
 private static IEnumerable<T> FindVisualChildren<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject {
  for(var index=0;index<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);index++) {
   var child=System.Windows.Media.VisualTreeHelper.GetChild(parent,index);
   if(child is T match)yield return match;
   foreach(var descendant in FindVisualChildren<T>(child))yield return descendant;
  }
 }
 #endregion END private helpers
}
