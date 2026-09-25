using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Rendering;
using Microsoft.Win32;

namespace DcsIpia.Presentation;

/// <summary>Shows the canonical device SVG and stages module-owned assignments in memory.</summary>
public partial class HueyImportWindow : Window {
 private readonly HueyImportViewModel _viewModel;
 private readonly IAppSettingsService? _settings;
 private HardwareGraphicControl? _selected;
 private HardwareGraphicControl? _pendingSource;
 private HardwareGraphicControl? _contextSource;
 private string? _pendingAction;
 private IReadOnlyList<HardwareGraphicControl> _visibleControls=[];
 private UiLayerDiagram? _currentDiagram;
 private Point? _paletteDragStart;
 private CommandPaletteEntry? _dragCommand;
 private Point? _controlDragStart;
 private HardwareGraphicControl? _dragControl;
 private bool _pointerDragging;
 private Border? _hoverBadge;
 private bool _updatingCategories;
 private const string PaletteDragFormat="Dcs.Ipia.Editor.PaletteCommand";
 private const string ControlDragFormat="Dcs.Ipia.Editor.HardwareControl";
 /// <summary>Opens the imported graphical editor with optional user preferences.</summary>
 /// <param name="viewModel">The validated import and in-memory draft.</param>
 /// <param name="settings">The app's current destination preference.</param>
 public HueyImportWindow(HueyImportViewModel viewModel,IAppSettingsService? settings=null) {
  _viewModel=viewModel??throw new ArgumentNullException(nameof(viewModel));
  _settings=settings;
  InitializeComponent();
  DataContext=_viewModel;
  UiModifierChoice.ItemsSource=_viewModel.ModifierChoices;
  UiModifierChoice.SelectedIndex=0;
  KneeboardList.SelectedIndex=0;
  UiDiagramList.SelectedIndex=0;
  PaletteScope.SelectedIndex=0;
  EditorStatus.Text=_viewModel.DraftNote;
  RefreshReview();
  if(_viewModel.Draft is not null)ImportedTabs.SelectedIndex=0;
  else ImportedTabs.SelectedItem=DiagnosticsTab;
  Deactivated+=(_,_)=>ResetPointerDrag();
  Closing+=OnClosing;
 }
 #region BEGIN Public
 #endregion END Public
 #region BEGIN private helpers
 private void KneeboardList_SelectionChanged(object sender,SelectionChangedEventArgs e) {
  if(_viewModel is null||KneeboardPreview is null)return;
  KneeboardPreview.Source=null;
  var index=KneeboardList.SelectedIndex;
  if(index<0||index>=_viewModel.PageKeys.Count) {
   KneeboardCaption.Text="No rendered kneeboard image is available from this source.";
   return;
  }
  var page=_viewModel.PageKeys[index];
  var asset=_viewModel.Images.FirstOrDefault(x=>x.PageKey==page);
  if(asset is null) {KneeboardCaption.Text=$"No committed PNG was found for {page}.";return;}
  try {
   KneeboardPreview.Source=LoadBitmap(asset.Bytes);
   KneeboardCaption.Text=$"Existing rendered image: {asset.Path} · SHA-256 {asset.Sha256}. This preview does not regenerate the page.";
  }
  catch(Exception ex) {KneeboardCaption.Text=$"Unable to display {asset.Path}: {ex.Message}";}
 }
 private void UiDiagramList_SelectionChanged(object sender,SelectionChangedEventArgs e) {
  if(UiDiagramCanvas is null)return;
  _selected=null;_pendingSource=null;_pendingAction=null;
  _currentDiagram=UiDiagramList.SelectedItem as UiLayerDiagram;
  RenderUiDiagram();UpdatePalette();
 }
 private void UiModifierChoice_SelectionChanged(object sender,SelectionChangedEventArgs e) {
  _pendingSource=null;_pendingAction=null;RenderUiDiagram();UpdatePalette();
 }
 private void PaletteScope_SelectionChanged(object sender,SelectionChangedEventArgs e) {
  _pendingSource=null;_pendingAction=null;
  RefreshCategories();
  UpdatePalette();RenderUiDiagram();
 }
 private void RefreshCategories() {
  _updatingCategories=true;
  if(PaletteCategory is not null) {
   var all=PaletteScope.SelectedIndex==1?_viewModel.UiCommands:_viewModel.ModuleCommands;
   PaletteCategory.ItemsSource=Enumerable.Repeat("All",1)
    .Concat(all.Select(x=>x.Category).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)).ToArray();
   PaletteCategory.SelectedIndex=0;
  }
  _updatingCategories=false;
 }
 private void LoadModuleCatalog_Click(object sender,RoutedEventArgs e) {
  if(!CanLoadCatalog())return;
  var chooser=new OpenFileDialog {Title="Select a DCS-Common module command catalog",Filter="DCS command catalog (*.json)|*.json"};
  if(chooser.ShowDialog(this)!=true)return;
  try {
   var entries=DcsCommandCatalogImporter.Parse(File.ReadAllText(chooser.FileName),_viewModel.ModuleDcsId);
   ApplyModuleCatalog(entries,Path.GetFileName(chooser.FileName));
  }
  catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or FormatException or InvalidOperationException) {
   SetStatus($"Could not load DCS command catalog: {ex.Message}");
  }
 }
 private void LoadDcsHtml_Click(object sender,RoutedEventArgs e) {
  if(!CanLoadCatalog())return;
  var chooser=new OpenFileDialog {Title="Select DCS Controls HTML exports for this module",Filter="DCS Controls HTML (*.html;*.htm)|*.html;*.htm",Multiselect=true};
  if(chooser.ShowDialog(this)!=true)return;
  try {
   var entries=DcsHtmlCommandCatalogImporter.Parse(chooser.FileNames.Select(file=>(Path.GetFileName(file),File.ReadAllText(file))));
   ApplyModuleCatalog(entries,$"{chooser.FileNames.Length} DCS HTML export(s)");
  }
  catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or FormatException or InvalidOperationException) {
   SetStatus($"Could not load DCS HTML: {ex.Message}");
  }
 }
 private bool CanLoadCatalog() {
  if(_viewModel.Draft?.HasChanges==true) {
   SetStatus("Review or discard pending edits before loading a new command catalog.");return false;
  }
  if(string.IsNullOrWhiteSpace(_viewModel.ModuleDcsId)) {
   SetStatus("Import both committed repositories before loading a module catalog.");return false;
  }
  return true;
 }
 private void ApplyModuleCatalog(IReadOnlyList<CommandPaletteEntry> entries,string source) {
  _viewModel.LoadModuleCatalog(entries);
  PaletteScope.SelectedIndex=0;
  RefreshCategories();UpdatePalette();RenderUiDiagram();RefreshReview();
  SetStatus($"Loaded {entries.Count} assignable aircraft commands with named categories from {source}.");
 }
 private void PaletteSearch_TextChanged(object sender,TextChangedEventArgs e)=>UpdatePalette();
 private void PaletteFilter_SelectionChanged(object sender,SelectionChangedEventArgs e)=>UpdatePalette();
 private void PaletteCategory_SelectionChanged(object sender,SelectionChangedEventArgs e) {
  if(!_updatingCategories)UpdatePalette();
 }
 private void UpdatePalette() {
  if(_viewModel is null||CommandPaletteList is null||PaletteScope is null||PaletteSearch is null||PaletteCaption is null)return;
  var isUi=PaletteScope.SelectedIndex==1;
  var all=isUi?_viewModel.UiCommands:_viewModel.ModuleCommands;
  var category=PaletteCategory?.SelectedIndex>0?PaletteCategory.SelectedItem as string:null;
  var filtered=CommandPalette.Filter(all,PaletteSearch.Text,category).AsEnumerable();
  if(PaletteType?.SelectedIndex==1)filtered=filtered.Where(x=>!x.IsAxis);
  if(PaletteType?.SelectedIndex==2)filtered=filtered.Where(x=>x.IsAxis);
  if(PaletteAvailability?.SelectedIndex==1)
   filtered=filtered.Where(x=>_selected is not null&&x.IsAxis==_selected.IsAxis);
  if(PaletteBindingState?.SelectedIndex is 1 or 2) {
   var assigned=_viewModel.Draft?.Snapshot.Where(x=>_currentDiagram is not null&&
    x.Slot.ProfileId==_currentDiagram.ProfileId&&x.Slot.Layer==Layer()&&
    x.Slot.ModifierChord==(Chord()=="Base"?"":Chord())).Select(x=>x.Command.Id)
    .ToHashSet(StringComparer.Ordinal)??new HashSet<string>(StringComparer.Ordinal);
   filtered=filtered.Where(x=>assigned.Contains(x.Id)==(PaletteBindingState.SelectedIndex==1));
  }
  var shown=filtered.ToArray();
  CommandPaletteList.ItemsSource=shown;
  PaletteCaption.Text=$"Showing {shown.Length} of {all.Count} commands{(category is null?"":$" · {category}")}";
  PaletteCaption.ToolTip=_viewModel.PaletteSourceNote;
 }
 private void RenderUiDiagram() {
  if(UiDiagramCanvas is null||UiAssignmentList is null||UiDiagramCaption is null||UiModifierChoice is null)return;
  UiDiagramCanvas.Children.Clear();_visibleControls=[];UiAssignmentList.ItemsSource=null;
  ResetPointerDrag();
  var diagram=_currentDiagram;
  if(DiagramHeading is not null)DiagramHeading.Text=diagram is null?
   "Canonical device graphic · assignment labels":$"{diagram.Title} · {Layer()} / {Chord()}";
  if(diagram is null) {UiDiagramCaption.Text="Select a device and instance to inspect its authored graphic.";UpdateInspector();return;}
  UiDiagramCanvas.Width=diagram.Width;UiDiagramCanvas.Height=diagram.Height;
  if(string.IsNullOrWhiteSpace(diagram.Svg)) {
   UiDiagramCaption.Text=$"Canonical draw.io-generated SVG unavailable: {diagram.Path}. Use Diagnostics and the assignment list.";
   UiAssignmentList.ItemsSource=diagram.Callouts.Select(x=>x.Description).ToArray();
   UpdateInspector();return;
  }
  try {
   UiDiagramCanvas.Children.Add(new Image {Source=LoadBitmap(CanonicalSvgRenderer.Render(diagram.Svg,diagram.Width,diagram.Height)),
    Width=diagram.Width,Height=diagram.Height,Stretch=System.Windows.Media.Stretch.Fill});
   _visibleControls=diagram.Controls;
   foreach(var control in _visibleControls) {
    var assignment=CurrentAssignment(control);
    var label=assignment?.Command.Name??"Assign";
    var badge=new Border {Width=control.Width,Height=control.Height,BorderThickness=new Thickness(2),
     CornerRadius=new CornerRadius(4),Padding=new Thickness(2),AllowDrop=true,
     Tag=control,Cursor=Cursors.Hand,
     ToolTip=$"{control.Label} · {control.Control} · {control.InputKey} · {label}. Drag to move an assignment; Ctrl-drag to copy.",
     Child=new TextBlock {Text=label,FontSize=11,FontWeight=FontWeights.SemiBold,
      TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis,
      HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,
      TextAlignment=TextAlignment.Center}};
    badge.SetResourceReference(Border.BackgroundProperty,"SurfaceBrush");
    badge.SetResourceReference(Border.BorderBrushProperty,"AccentTextBrush");
    ((TextBlock)badge.Child).SetResourceReference(TextBlock.ForegroundProperty,"PrimaryTextBrush");
    badge.MouseLeftButtonDown+=(_,args)=>{
     _controlDragStart=args.GetPosition(this);_dragControl=control;SelectControl(control);
     args.Handled=true;
    };
    badge.DragEnter+=(_,args)=>{
     if(CanDrop(args.Data,control))badge.SetResourceReference(Border.BackgroundProperty,"SelectedBackgroundBrush");
    };
    badge.DragLeave+=(_,_)=>badge.SetResourceReference(Border.BackgroundProperty,"SurfaceBrush");
    badge.DragOver+=Control_DragOver;
    badge.Drop+=(_,args)=>{
     badge.SetResourceReference(Border.BackgroundProperty,"SurfaceBrush");Control_Drop(control,args);
    };
    badge.PreviewMouseRightButtonDown+=(_,_)=>{_contextSource=_selected;_selected=control;UpdateInspector();UpdatePalette();};
    badge.ContextMenu=BuildContextMenu(control);
    Canvas.SetLeft(badge,control.X);Canvas.SetTop(badge,control.Y);
    UiDiagramCanvas.Children.Add(badge);
   }
   UiAssignmentList.ItemsSource=_visibleControls.Select(x=>$"{x.Label} · {CurrentAssignment(x)?.Command.Name??"Assign"} · {x.InputKey}").ToArray();
   UiDiagramCaption.Text=$"{diagram.Controls.Count} authored controls · {Layer()} / {Chord()} · draw.io SVG";
   EditorHeading.Text=$"{diagram.Title} — {(Layer()==AssignmentLayer.UiLayer?"UI Layer":"Module controls")}";
   UpdateInspector();
  }
  catch(Exception ex) {
   UiDiagramCanvas.Children.Clear();_visibleControls=[];
   UiDiagramCaption.Text=$"Unable to render canonical SVG {diagram.Path}: {ex.Message}. No substitute graphic was drawn.";
   UiAssignmentList.ItemsSource=diagram.Callouts.Select(x=>x.Description).ToArray();
   UpdateInspector();
  }
 }
 private static BitmapImage LoadBitmap(byte[] bytes) {
  using var stream=new MemoryStream(bytes,false);
  var bitmap=new BitmapImage();
  bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
  return bitmap;
 }
 private AssignmentLayer Layer()=>PaletteScope.SelectedIndex==1?AssignmentLayer.UiLayer:AssignmentLayer.Module;
 private string Chord()=>UiModifierChoice.SelectedItem as string??"Base";
 private DraftAssignmentSlot? Slot(HardwareGraphicControl control) {
  if(_viewModel.Draft is null||_currentDiagram is null||
     Layer()==AssignmentLayer.UiLayer&&!_currentDiagram.SupportsUiLayer)return null;
  return new(_viewModel.ModuleId,
   _currentDiagram.ProfileId,Layer(),control.IsAxis?BindingSection.Axis:BindingSection.Key,
   control.InputKey,Chord()=="Base"?"":Chord());
 }
 private DraftAssignment? CurrentAssignment(HardwareGraphicControl control) {
  var slot=Slot(control);
  return slot is null?null:_viewModel.Draft?.Snapshot.FirstOrDefault(x=>x.Slot.Key==slot.Key);
 }
 private DraftAssignment? ImportedAssignment(HardwareGraphicControl control) {
  var slot=Slot(control);
  if(slot is null)return null;
  var first=_viewModel.Draft?.History.SelectMany(x=>x.Changes)
   .FirstOrDefault(x=>x.Slot.Key==slot.Key);
  return first is null?CurrentAssignment(control):first.Before;
 }
 private void SelectControl(HardwareGraphicControl control) {
  if(_pendingSource is not null&&_pendingAction is not null) {
   var source=_pendingSource;var action=_pendingAction;
   _pendingSource=null;_pendingAction=null;
   ApplyTransfer(source,control,action);return;
  }
  _selected=control;UpdateInspector();UpdatePalette();
 }
 private void UiAssignmentList_SelectionChanged(object sender,SelectionChangedEventArgs e) {
  if(UiAssignmentList.SelectedIndex>=0&&UiAssignmentList.SelectedIndex<_visibleControls.Count)
   SelectControl(_visibleControls[UiAssignmentList.SelectedIndex]);
 }
 private void UpdateInspector() {
  if(SelectedControlTitle is null||SelectedAssignment is null)return;
  SelectedControlTitle.Text=_selected is null?"Select a callout":$"{_selected.Label} · {_selected.Control}";
  var assignment=_selected is null?null:CurrentAssignment(_selected);
  var imported=_selected is null?null:ImportedAssignment(_selected);
  SelectedAssignment.Text=_selected is null?"Select a callout to inspect its imported command.":imported?.Command.Name??"Unassigned";
  PendingAssignment.Text=_selected is null?"—":assignment==imported?"No pending change":assignment?.Command.Name??"Clear assignment";
  DefaultControlLabel.Text=_selected?.Label??"—";
  SelectedControlIdentity.Text=_selected is null?"—":$"{_selected.InputKey} · {Layer()} / {Chord()}";
 }
 private void Editor_PreviewMouseLeftButtonUp(object sender,MouseButtonEventArgs e) {
  if(!_pointerDragging) {ResetPointerDrag();return;}
  var target=HitControl(e.GetPosition(UiDiagramCanvas));
  var data=PointerData();
  var copy=Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
  ResetPointerDrag();
  if(target is null)SetStatus("Release over an authored control label to assign or move.");
  else if(data is not null)ApplyDrop(target,data,copy);
  e.Handled=true;
 }
 private void Editor_PreviewMouseMove(object sender,MouseEventArgs e) {
  if(e.LeftButton!=MouseButtonState.Pressed) {if(_pointerDragging)ResetPointerDrag();return;}
  if(!_pointerDragging) {
   var start=_dragCommand is not null?_paletteDragStart:_controlDragStart;
   if(_viewModel.Draft is null||start is not Point origin||
      !PastDragThreshold(origin,e.GetPosition(this)))return;
   if(_dragControl is not null&&CurrentAssignment(_dragControl) is null) {
    SetStatus("Choose an assigned control to move.");ResetPointerDrag();return;
   }
   if(!Mouse.Capture(this)) {SetStatus("Could not capture the pointer; try dragging again.");ResetPointerDrag();return;}
   _pointerDragging=true;
  }
  var target=HitControl(e.GetPosition(UiDiagramCanvas));
  var valid=target is not null&&PointerData() is {} data&&CanDrop(data,target);
  Mouse.OverrideCursor=valid?Cursors.Hand:Cursors.No;
  var badge=UiDiagramCanvas.Children.OfType<Border>().FirstOrDefault(x=>ReferenceEquals(x.Tag,target));
  if(ReferenceEquals(badge,_hoverBadge))return;
  if(_hoverBadge is not null)_hoverBadge.SetResourceReference(Border.BackgroundProperty,"SurfaceBrush");
  _hoverBadge=valid?badge:null;
  if(_hoverBadge is not null)_hoverBadge.SetResourceReference(Border.BackgroundProperty,"SelectedBackgroundBrush");
  SetStatus(target is null?"Release over an authored control label.":
   valid?$"Release to {(_dragCommand is not null?"assign":Keyboard.Modifiers.HasFlag(ModifierKeys.Control)?"copy":"move")} onto {target.Label}.":
   "That control is incompatible with this assignment.");
 }
 private void CommandPaletteList_PreviewMouseLeftButtonDown(object sender,MouseButtonEventArgs e) {
  var container=ItemsControl.ContainerFromElement(CommandPaletteList,e.OriginalSource as DependencyObject) as ListBoxItem;
  _dragCommand=(e.OriginalSource as FrameworkElement)?.DataContext as CommandPaletteEntry??
   container?.DataContext as CommandPaletteEntry;
  _paletteDragStart=_dragCommand is null?null:e.GetPosition(this);
  if(_dragCommand is not null)SetStatus($"Drag {_dragCommand.Name} onto a device callout.");
 }
 private DataObject? PointerData() {
  var data=new DataObject();
  if(_dragCommand is not null)data.SetData(DataFormats.UnicodeText,$"{PaletteDragFormat}\n{_dragCommand.Id}");
  else if(_dragControl is not null)data.SetData(DataFormats.UnicodeText,$"{ControlDragFormat}\n{_dragControl.Control}");
  else return null;
  return data;
 }
 private void ResetPointerDrag() {
  if(_hoverBadge is not null)_hoverBadge.SetResourceReference(Border.BackgroundProperty,"SurfaceBrush");
  _hoverBadge=null;
  if(_pointerDragging&&ReferenceEquals(Mouse.Captured,this))Mouse.Capture(null);
  Mouse.OverrideCursor=null;
  _pointerDragging=false;
  _paletteDragStart=null;_dragCommand=null;
  _controlDragStart=null;_dragControl=null;
 }
 private static bool PastDragThreshold(Point start,Point current)=>
  Math.Abs(start.X-current.X)>=SystemParameters.MinimumHorizontalDragDistance||
  Math.Abs(start.Y-current.Y)>=SystemParameters.MinimumVerticalDragDistance;
 private static string? DragValue(IDataObject data,string format) {
  if(data.GetDataPresent(DataFormats.UnicodeText)&&
     data.GetData(DataFormats.UnicodeText) is string text&&
     text.StartsWith(format+"\n",StringComparison.Ordinal))
   return text[(format.Length+1)..];
  return data.GetData(format) as string;
 }
 private bool CanDrop(IDataObject data,HardwareGraphicControl? target) =>
  target is not null&&Slot(target) is not null&&
  (DragValue(data,PaletteDragFormat) is string commandId
    ? (Layer()==AssignmentLayer.UiLayer?_viewModel.UiCommands:_viewModel.ModuleCommands)
       .Any(command=>command.Id==commandId&&command.IsAxis==target.IsAxis)
    : DragValue(data,ControlDragFormat) is string sourceId&&
      _visibleControls.Any(source=>source.Control==sourceId&&source.IsAxis==target.IsAxis&&
       CurrentAssignment(source) is not null));
 private string DropRejection(IDataObject data,HardwareGraphicControl target) {
  if(_viewModel.Draft is null||Slot(target) is null)return _viewModel.DraftNote;
  if(DragValue(data,PaletteDragFormat) is string id) {
   var command=(Layer()==AssignmentLayer.UiLayer?_viewModel.UiCommands:_viewModel.ModuleCommands)
    .FirstOrDefault(x=>x.Id==id);
   return command is null?"That command is not in the selected Module or UI Layer catalog.":
    $"Cannot assign {(command.IsAxis?"an axis":"a button")} command to {(target.IsAxis?"an axis":"a button")} control.";
  }
  if(DragValue(data,ControlDragFormat) is string sourceId) {
   var source=_visibleControls.FirstOrDefault(x=>x.Control==sourceId);
   return source is null?"The source control is not on this device.":
    CurrentAssignment(source) is null?"The source control has no assignment on this layer and chord.":
    "Source and destination controls must have matching button or axis types.";
  }
  return "Select a catalog command or an assigned callout to drag.";
 }
 private void Control_DragOver(object sender,DragEventArgs e) {
  var target=sender is Border badge&&badge.Tag is HardwareGraphicControl control
   ?control:UiDiagramCanvas is null?null:HitControl(e.GetPosition(UiDiagramCanvas));
  e.Effects=CanDrop(e.Data,target)?DropEffect(e.Data,e.KeyStates):DragDropEffects.None;
  e.Handled=true;
 }
 private void UiDiagramCanvas_DragOver(object sender,DragEventArgs e)=>Control_DragOver(sender,e);
 private static DragDropEffects DropEffect(IDataObject data,DragDropKeyStates keys)=>
  DragValue(data,PaletteDragFormat) is not null||keys.HasFlag(DragDropKeyStates.ControlKey)
   ?DragDropEffects.Copy:DragDropEffects.Move;
 private HardwareGraphicControl? HitControl(Point point)=>_visibleControls.LastOrDefault(x=>
  point.X>=x.X&&point.X<=x.X+x.Width&&point.Y>=x.Y&&point.Y<=x.Y+x.Height);
 private void Control_Drop(HardwareGraphicControl control,DragEventArgs e) {
  ApplyDrop(control,e.Data,e.KeyStates.HasFlag(DragDropKeyStates.ControlKey));
  e.Handled=true;
 }
 private void UiDiagramCanvas_Drop(object sender,DragEventArgs e) {
  var target=HitControl(e.GetPosition(UiDiagramCanvas));
  if(target is null)SetStatus("Drop onto an authored control label on the device.");
  else ApplyDrop(target,e.Data,e.KeyStates.HasFlag(DragDropKeyStates.ControlKey));
  e.Handled=true;
 }
 private void ApplyDrop(HardwareGraphicControl target,IDataObject data,bool copy) {
  if(!CanDrop(data,target)) {SetStatus(DropRejection(data,target));return;}
  if(DragValue(data,PaletteDragFormat) is string commandId) {
   var entries=Layer()==AssignmentLayer.UiLayer?_viewModel.UiCommands:_viewModel.ModuleCommands;
   var command=entries.FirstOrDefault(x=>x.Id==commandId);
   if(command is null)SetStatus("The dragged command is not in this scope; no draft change was made.");
   else AssignTo(target,command);
  } else if(DragValue(data,ControlDragFormat) is string controlKey) {
   var source=_visibleControls.FirstOrDefault(x=>x.Control==controlKey);
   if(source is null)SetStatus("The dragged control is no longer on this device; no draft change was made.");
   else ApplyTransfer(source,target,copy?"Copy":"Move");
  } else SetStatus("Unsupported drop data; no draft change was made.");
 }
 private ContextMenu BuildContextMenu(HardwareGraphicControl target) {
  var menu=new ContextMenu();
  AddMenu(menu,"Assign selected command",()=>AssignTo(target,CommandPaletteList.SelectedItem as CommandPaletteEntry));
  AddMenu(menu,"Move selected assignment here",()=>{if(_contextSource is not null)ApplyTransfer(_contextSource,target,"Move");});
  AddMenu(menu,"Move this assignment…",()=>StartTransfer(target,"Move"));
  AddMenu(menu,"Copy this assignment…",()=>StartTransfer(target,"Copy"));
  AddMenu(menu,"Swap with selected",()=>{if(_contextSource is not null)ApplyTransfer(_contextSource,target,"Swap");});
  AddMenu(menu,"Remove",()=>Apply(_viewModel.Draft?.Remove(Slot(target)!)));
  AddMenu(menu,"Restore imported",()=>Apply(_viewModel.Draft?.Restore(Slot(target)!)));
  return menu;
 }
 private static void AddMenu(ContextMenu menu,string caption,Action action) {
  var item=new MenuItem {Header=caption};
  item.Click+=(_,_)=>action();menu.Items.Add(item);
 }
 private void Assign_Click(object sender,RoutedEventArgs e) {
  if(_selected is not null)AssignTo(_selected,CommandPaletteList.SelectedItem as CommandPaletteEntry);
 }
 private void AssignTo(HardwareGraphicControl control,CommandPaletteEntry? entry) {
  if(_viewModel.Draft is null||Slot(control) is not {} slot) {SetStatus(_viewModel.DraftNote);return;}
  if(entry is null) {SetStatus("Select a command in the palette first.");return;}
  var command=new DraftCommand(entry.Id,entry.Name,entry.IsAxis,Layer());
  var behavior=_settings?.Current.OccupiedDestinationBehavior??OccupiedDestinationBehavior.Ask;
  var result=_viewModel.Draft.Assign(slot,command,behavior);
  if(result.Outcome==DraftOutcome.ConfirmationRequired) {
   var answer=MessageBox.Show(this,$"{control.Label} currently contains {CurrentAssignment(control)?.Command.Name}. Replace it with {entry.Name} in the draft?",
    "Occupied control",MessageBoxButton.YesNo,MessageBoxImage.Question);
   result=_viewModel.Draft.Assign(slot,command,behavior,answer==MessageBoxResult.Yes?DraftConflictChoice.Replace:DraftConflictChoice.Cancel);
  }
  Apply(result);
 }
 private void Move_Click(object sender,RoutedEventArgs e)=>StartTransfer(_selected,"Move");
 private void Copy_Click(object sender,RoutedEventArgs e)=>StartTransfer(_selected,"Copy");
 private void Swap_Click(object sender,RoutedEventArgs e)=>StartTransfer(_selected,"Swap");
 private void StartTransfer(HardwareGraphicControl? source,string action) {
  if(source is null||CurrentAssignment(source) is null) {SetStatus("Select an assigned source control first.");return;}
  _pendingSource=source;_pendingAction=action;
  SetStatus($"Select the destination on this device to {action.ToLowerInvariant()} {CurrentAssignment(source)!.Command.Name}.");
 }
 private void ApplyTransfer(HardwareGraphicControl source,HardwareGraphicControl destination,string action) {
  if(_viewModel.Draft is null||Slot(source) is not {} from||Slot(destination) is not {} to) {
   SetStatus(_viewModel.DraftNote);return;
  }
  var behavior=_settings?.Current.OccupiedDestinationBehavior??OccupiedDestinationBehavior.Ask;
  var result=action switch {
   "Swap"=>_viewModel.Draft.Swap(from,to),
   "Copy"=>_viewModel.Draft.Copy(from,to,behavior),
   _=>_viewModel.Draft.Move(from,to,behavior)
  };
  if(result.Outcome==DraftOutcome.ConfirmationRequired) {
   var message=$"{source.Label}: {CurrentAssignment(source)?.Command.Name}\n{destination.Label}: {CurrentAssignment(destination)?.Command.Name}\n\nYes: Replace destination · No: Swap · Cancel: Keep both";
   var answer=MessageBox.Show(this,message,"Occupied control",MessageBoxButton.YesNoCancel,MessageBoxImage.Question);
   var choice=answer switch {
    MessageBoxResult.Yes=>DraftConflictChoice.Replace,
    MessageBoxResult.No=>DraftConflictChoice.Swap,
    _=>DraftConflictChoice.Cancel
   };
   result=action=="Copy"?_viewModel.Draft.Copy(from,to,behavior,choice):
    _viewModel.Draft.Move(from,to,behavior,choice);
  }
  _selected=destination;Apply(result);
 }
 private void Remove_Click(object sender,RoutedEventArgs e)=>Apply(_selected is null?null:_viewModel.Draft?.Remove(Slot(_selected)!));
 private void Restore_Click(object sender,RoutedEventArgs e)=>Apply(_selected is null?null:_viewModel.Draft?.Restore(Slot(_selected)!));
 private void Undo_Click(object sender,RoutedEventArgs e)=>Apply(_viewModel.Draft?.Undo());
 private void Discard_Click(object sender,RoutedEventArgs e) {
  _viewModel.Draft?.Discard();RenderUiDiagram();RefreshReview();SetStatus("Draft discarded; imported assignments restored.");
 }
 private void EditorSettings_Click(object sender,RoutedEventArgs e) {
  if(_settings is null) {SetStatus("Open Settings from the main workspace.");return;}
  new SettingsWindow(_settings) {Owner=this}.ShowDialog();
  SetStatus($"Occupied destination: {_settings.Current.OccupiedDestinationBehavior}.");
 }
 private void OpenKneeboard_Click(object sender,RoutedEventArgs e) => ImportedTabs.SelectedIndex=3;
 private void Apply(DraftResult? result) {
  if(result is null) {SetStatus(_viewModel.DraftNote);return;}
  if(result.Outcome==DraftOutcome.Applied)RenderUiDiagram();UpdatePalette();
  RefreshReview();SetStatus(result.Message);
 }
 private void RefreshReview() {
  if(DraftCount is null||DraftReviewList is null)return;
  var history=_viewModel.Draft?.History??[];
  DraftCount.Text=$"Draft · {history.Count} operations";
  DraftReviewList.ItemsSource=history.SelectMany(op=>op.Changes.Select(change=>
   $"{op.Name} · {change.Slot.InputKey} · {change.Before?.Command.Name??"Empty"} → {change.After?.Command.Name??"Empty"}")).ToArray();
 }
 private void SetStatus(string message) {if(EditorStatus is not null)EditorStatus.Text=message;}
 private void OnClosing(object? sender,System.ComponentModel.CancelEventArgs e) {
  if(_viewModel.Draft?.HasChanges!=true)return;
  if(MessageBox.Show(this,"Discard unsaved assignment draft? No source files have been changed.","Unsaved draft",
    MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)e.Cancel=true;
  else _viewModel.Draft.Discard();
 }
 private void Close_Click(object sender,RoutedEventArgs e)=>Close();
 #endregion END private helpers
}
