using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
 private string? _pendingAction;
 private IReadOnlyList<HardwareGraphicControl> _visibleControls=[];
 private UiLayerDiagram? _currentDiagram;
 private Point? _paletteDragStart;
 private Point? _controlDragStart;
 private HardwareGraphicControl? _dragControl;
 private readonly Dictionary<string,string> _displayLabels=new(StringComparer.Ordinal);
 private bool _updatingInspector;
 private static readonly JsonSerializerOptions CatalogJsonOptions=new() {WriteIndented=true};
 private IReadOnlyList<CommandPaletteEntry> _moduleCommands=[];
 private bool _updatingCategories;
 private const string PaletteDragFormat="Dcs.Ipia.Editor.PaletteCommand";
 private const string ControlDragFormat="Dcs.Ipia.Editor.HardwareControl";
 /// <summary>Opens the imported graphical editor with optional user preferences.</summary>
 /// <param name="viewModel">The validated import and in-memory draft.</param>
 /// <param name="settings">The app's current destination preference.</param>
 public HueyImportWindow(HueyImportViewModel viewModel,IAppSettingsService? settings=null) {
  _viewModel=viewModel??throw new ArgumentNullException(nameof(viewModel));
  _settings=settings;
  _moduleCommands=viewModel.ModuleCommands;
  InitializeComponent();
  DataContext=_viewModel;
  UiModifierChoice.ItemsSource=_viewModel.ModifierChoices;
  UiModifierChoice.SelectedIndex=0;
  KneeboardList.SelectedIndex=0;
  UiDiagramList.SelectedIndex=0;
  PaletteScope.SelectedIndex=0;
  PaletteType.SelectedIndex=0;
  PaletteAvailability.SelectedIndex=0;
  PaletteBindingState.SelectedIndex=0;
  EditorStatus.Text=_viewModel.DraftNote;
  RefreshReview();
  if(_viewModel.HasErrors)ImportedTabs.SelectedItem=DiagnosticsTab;
  Closing+=OnClosing;
 }
 #region BEGIN Public
 #endregion END Public
 #region BEGIN private helpers
 /// <summary>Launches the original DCS-Common scaffolding application window.</summary>
 private void OpenOriginalScaffold_Click(object sender,RoutedEventArgs e) {
  var profileRoot=_viewModel.SourceFolder;
  if(profileRoot is not null) {
   var repositoryProfiles=Path.Combine(profileRoot,"src","Config","Input","UH-1H","joystick");
   if(Directory.Exists(repositoryProfiles))profileRoot=repositoryProfiles;
   else if(Directory.Exists(Path.Combine(profileRoot,"joystick")))profileRoot=Path.Combine(profileRoot,"joystick");
  }
  new DcsConsumerScaffold.MainWindow(_viewModel.CommonRoot,profileRoot) {Owner=this}.ShowDialog();
 }
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
  RenderUiDiagram();
 }
 private void UiModifierChoice_SelectionChanged(object sender,SelectionChangedEventArgs e) {
  _pendingSource=null;_pendingAction=null;RenderUiDiagram();
 }
 private void PaletteScope_SelectionChanged(object sender,SelectionChangedEventArgs e) {
  _pendingSource=null;_pendingAction=null;
  _updatingCategories=true;
  if(PaletteCategory is not null) {
   var all=PaletteScope.SelectedIndex==1?_viewModel.UiCommands:_moduleCommands;
   PaletteCategory.ItemsSource=Enumerable.Repeat("All categories",1)
    .Concat(all.Select(x=>x.Category).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)).ToArray();
   PaletteCategory.SelectedIndex=0;
  }
  _updatingCategories=false;
  UpdatePalette();RenderUiDiagram();
 }
 private void PaletteSearch_TextChanged(object sender,TextChangedEventArgs e)=>UpdatePalette();
 private void PaletteCategory_SelectionChanged(object sender,SelectionChangedEventArgs e) {
  if(!_updatingCategories)UpdatePalette();
 }
 private void UpdatePalette() {
  if(_viewModel is null||CommandPaletteList is null||PaletteScope is null||PaletteSearch is null||PaletteCaption is null)return;
  var isUi=PaletteScope.SelectedIndex==1;
  var all=isUi?_viewModel.UiCommands:_moduleCommands;
  var category=PaletteCategory?.SelectedIndex>0?PaletteCategory.SelectedItem as string:null;
  var filtered=CommandPalette.Filter(all,PaletteSearch.Text,category);
  if(PaletteType?.SelectedIndex==1)filtered=filtered.Where(x=>!x.IsAxis).ToArray();
  if(PaletteType?.SelectedIndex==2)filtered=filtered.Where(x=>x.IsAxis).ToArray();
  if(PaletteBindingState?.SelectedIndex>0) {
   var assigned=_viewModel.Draft?.Snapshot.Where(x=>x.Slot.Layer==Layer()).Select(x=>x.Command.Id).ToHashSet(StringComparer.Ordinal)
    ??new HashSet<string>(StringComparer.Ordinal);
   filtered=filtered.Where(x=>assigned.Contains(x.Id)==(PaletteBindingState.SelectedIndex==1)).ToArray();
  }
  CommandPaletteList.ItemsSource=filtered;
  PaletteCaption.Text=$"{filtered.Count} of {all.Count} {(isUi?"UI Layer":"aircraft")} commands{(category is null?"":$" in {category}")}. {_viewModel.PaletteSourceNote} Drag a command onto a control or select it and choose Assign.";
 }
 private void PaletteFilter_SelectionChanged(object sender,SelectionChangedEventArgs e)=>UpdatePalette();
 private void RenderUiDiagram() {
  if(UiDiagramCanvas is null||UiAssignmentList is null||UiDiagramCaption is null||UiModifierChoice is null)return;
  UiDiagramCanvas.Children.Clear();_visibleControls=[];UiAssignmentList.ItemsSource=null;
  var diagram=_currentDiagram;
  if(diagram is null) {UiDiagramCaption.Text="Select a device and instance to inspect its authored graphic.";UpdateInspector();return;}
  DeviceTitle.Text=$"{diagram.Title} — {(Layer()==AssignmentLayer.UiLayer?"UI Layer controls":"Module controls")}";
  Instructions.Text=Layer()==AssignmentLayer.UiLayer
   ?"Drag a standard UI command onto a callout, or select a callout to assign, clear, restore, or relabel it."
   :"Drag a module command onto a callout, or select a callout to assign, clear, restore, or relabel it.";
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
    var label=assignment is null?"Unassigned":_displayLabels.GetValueOrDefault(Slot(control)!.Key,assignment.Command.Name);
    var badge=new Border {Tag=control,Width=control.Width,Height=control.Height,BorderThickness=new Thickness(2),
     CornerRadius=new CornerRadius(4),Padding=new Thickness(2),AllowDrop=true,
     ToolTip=$"{control.Label} · {control.Control} · {control.InputKey} · {label}",
     Child=new TextBlock {Text=label,FontSize=11,FontWeight=FontWeights.SemiBold,
      TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis,
      HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,
      TextAlignment=TextAlignment.Center}};
    badge.Background=assignment is null?Brushes.Gainsboro:
     _viewModel.Draft!.History.SelectMany(x=>x.Changes).Any(x=>x.Slot.Key==Slot(control)!.Key)?Brushes.LightGreen:Brushes.White;
    badge.BorderBrush=control==_selected?Brushes.RoyalBlue:Brushes.SlateGray;
    ((TextBlock)badge.Child).SetResourceReference(TextBlock.ForegroundProperty,"PrimaryTextBrush");
    badge.MouseLeftButtonDown+=(_,args)=>{
     _controlDragStart=args.GetPosition(this);_dragControl=control;SelectControl(control);args.Handled=true;
    };
    badge.MouseMove+=(_,args)=>Control_PreviewMouseMove(badge,control,args);
    badge.DragEnter+=(_,args)=>{
     if(CanDrop(args.Data,control))badge.SetResourceReference(Border.BackgroundProperty,"SelectedBackgroundBrush");
    };
    badge.DragLeave+=(_,_)=>badge.SetResourceReference(Border.BackgroundProperty,"SurfaceBrush");
    badge.DragOver+=Control_DragOver;
    badge.Drop+=(_,args)=>{
     badge.SetResourceReference(Border.BackgroundProperty,"SurfaceBrush");Control_Drop(control,args);
    };
    badge.ContextMenu=BuildContextMenu(control);
    Canvas.SetLeft(badge,control.X);Canvas.SetTop(badge,control.Y);
    UiDiagramCanvas.Children.Add(badge);
   }
   UiAssignmentList.ItemsSource=_visibleControls.Select(x=>$"{x.Label} · {CurrentAssignment(x)?.Command.Name??"Assign"} · {x.InputKey}").ToArray();
   UiDiagramCaption.Text=$"{diagram.Title} · {Layer()} / {Chord()} · {diagram.Path} · {diagram.Controls.Count} mapped controls · canonical DCS-Common SVG.";
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
   control.InputKey,Chord()=="Base"?"":Chord().Replace(" + ","+",StringComparison.Ordinal));
 }
 private DraftAssignment? CurrentAssignment(HardwareGraphicControl control) {
  var slot=Slot(control);
  return slot is null?null:_viewModel.Draft?.Snapshot.FirstOrDefault(x=>x.Slot.Key==slot.Key);
 }
 private void SelectControl(HardwareGraphicControl control) {
  if(_pendingSource is not null&&_pendingAction is not null) {
   var source=_pendingSource;var action=_pendingAction;
   _pendingSource=null;_pendingAction=null;
   ApplyTransfer(source,control,action);return;
  }
  _selected=control;UpdateInspector();
  foreach(var border in UiDiagramCanvas.Children.OfType<Border>()) {
   if(border.Tag is HardwareGraphicControl tagged)
    border.BorderBrush=tagged==control?Brushes.RoyalBlue:Brushes.SlateGray;
  }
 }
 private void UiAssignmentList_SelectionChanged(object sender,SelectionChangedEventArgs e) {
  if(UiAssignmentList.SelectedIndex>=0&&UiAssignmentList.SelectedIndex<_visibleControls.Count)
   SelectControl(_visibleControls[UiAssignmentList.SelectedIndex]);
 }
 private void UpdateInspector() {
  if(SelectedControlTitle is null||SelectedAssignment is null)return;
  SelectedControlTitle.Text=_selected is null?"Select a callout":_selected.InputKey;
  var assignment=_selected is null?null:CurrentAssignment(_selected);
  SelectedAssignment.Text=_selected is null?"Choose an authored callout or keyboard list row.":
   $"{assignment?.Command.Name??"Empty · choose a command"}\n{_selected.InputKey} · {Layer()} / {Chord()}";
  SelectedChord.Text=_selected is null?string.Empty:$"Modifier / chord: {Chord()}";
  CurrentCommand.Text=assignment?.Command.Name??string.Empty;
  PendingCommand.Text=_selected is null?string.Empty:
   _viewModel.Draft?.History.SelectMany(x=>x.Changes).LastOrDefault(x=>x.Slot.Key==Slot(_selected)?.Key) is { } last
    ?last.After?.Command.Name??"Clear assignment":"No pending assignment";
  DefaultLabel.Text=_selected?.Label??string.Empty;
  _updatingInspector=true;
  LabelEditor.IsEnabled=assignment is not null;
  LabelEditor.Text=_selected is null?string.Empty:
   _displayLabels.GetValueOrDefault(Slot(_selected)!.Key,assignment?.Command.Name??_selected.Label);
  _updatingInspector=false;
  TuneAxisButton.IsEnabled=false;
  AxisTuningSummary.Text=_selected is {IsAxis:true}
   ?"Axis tuning is unavailable in the current draft model.":string.Empty;
 }
 // The drag source and threshold match DCS-Common's InteractivePreviewWindow.
 private void DragStart(object sender,MouseButtonEventArgs e)=>_paletteDragStart=e.GetPosition(this);
 private void CommandDrag(object sender,MouseEventArgs e) {
  if(e.LeftButton!=MouseButtonState.Pressed) {_paletteDragStart=null;return;}
  if(_viewModel.Draft is null||_paletteDragStart is not Point origin||
     !PastDragThreshold(origin,e.GetPosition(this))||
     CommandPaletteList.SelectedItem is not CommandPaletteEntry command)return;
  _paletteDragStart=null;
  var data=new DataObject();
  data.SetData(PaletteDragFormat,command.Id);
  SetStatus($"Drop {command.Name} onto an authored control on the device.");
  DragDrop.DoDragDrop(CommandPaletteList,data,DragDropEffects.Copy);
 }
 private void Control_PreviewMouseMove(Border badge,HardwareGraphicControl control,MouseEventArgs e) {
  if(e.LeftButton!=MouseButtonState.Pressed) {_controlDragStart=null;_dragControl=null;return;}
  if(_viewModel.Draft is null||_dragControl!=control||_controlDragStart is not Point origin||
     !PastDragThreshold(origin,e.GetPosition(this)))return;
  if(CurrentAssignment(control) is null) {SetStatus("Choose an assigned control to move.");return;}
  _controlDragStart=null;_dragControl=null;
  var data=new DataObject();
  data.SetData(ControlDragFormat,control.Control);
  SetStatus($"Drop {control.Label} onto another control. Hold Ctrl to copy.");
  DragDrop.DoDragDrop(badge,data,DragDropEffects.Move|DragDropEffects.Copy);
 }
 private static bool PastDragThreshold(Point start,Point current)=>
  Math.Abs(start.X-current.X)>=SystemParameters.MinimumHorizontalDragDistance||
  Math.Abs(start.Y-current.Y)>=SystemParameters.MinimumVerticalDragDistance;
 private bool CanDrop(IDataObject data,HardwareGraphicControl? target) =>
  target is not null&&Slot(target) is not null&&
  (data.GetData(PaletteDragFormat) is string commandId&&
   (Layer()==AssignmentLayer.UiLayer?_viewModel.UiCommands:_moduleCommands)
    .Any(x=>x.Id==commandId&&x.IsAxis==target.IsAxis)
   ||data.GetData(ControlDragFormat) is string controlId&&
    _visibleControls.Any(x=>x.Control==controlId&&x.Control!=target.Control&&
     x.IsAxis==target.IsAxis&&CurrentAssignment(x) is not null));
 private void Control_DragOver(object sender,DragEventArgs e) {
  var target=UiDiagramCanvas is null?null:HitControl(e.GetPosition(UiDiagramCanvas));
  e.Effects=CanDrop(e.Data,target)?DropEffect(e.Data,e.KeyStates):DragDropEffects.None;
  e.Handled=true;
 }
 private void UiDiagramCanvas_DragOver(object sender,DragEventArgs e)=>Control_DragOver(sender,e);
 private static DragDropEffects DropEffect(IDataObject data,DragDropKeyStates keys)=>
  data.GetDataPresent(PaletteDragFormat)||keys.HasFlag(DragDropKeyStates.ControlKey)
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
  if(!CanDrop(data,target)) {SetStatus(_viewModel.DraftNote);return;}
  if(data.GetData(PaletteDragFormat) is string commandId) {
   var entries=Layer()==AssignmentLayer.UiLayer?_viewModel.UiCommands:_moduleCommands;
   var command=entries.FirstOrDefault(x=>x.Id==commandId);
   if(command is null)SetStatus("The dragged command is not in this scope; no draft change was made.");
   else AssignTo(target,command);
  } else if(data.GetData(ControlDragFormat) is string controlKey) {
   var source=_visibleControls.FirstOrDefault(x=>x.Control==controlKey);
   if(source is null)SetStatus("The dragged control is no longer on this device; no draft change was made.");
   else ApplyTransfer(source,target,copy?"Copy":"Move");
  } else SetStatus("Unsupported drop data; no draft change was made.");
 }
 private ContextMenu BuildContextMenu(HardwareGraphicControl target) {
  var menu=new ContextMenu();
  AddMenu(menu,"Assign selected command",()=>AssignTo(target,CommandPaletteList.SelectedItem as CommandPaletteEntry));
  AddMenu(menu,"Move selected assignment here",()=>{if(_selected is not null)ApplyTransfer(_selected,target,"Move");});
  AddMenu(menu,"Move this assignment…",()=>StartTransfer(target,"Move"));
  AddMenu(menu,"Copy this assignment…",()=>StartTransfer(target,"Copy"));
  AddMenu(menu,"Swap with selected",()=>{if(_selected is not null)ApplyTransfer(_selected,target,"Swap");});
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
  _viewModel.Draft?.Discard();_displayLabels.Clear();RenderUiDiagram();RefreshReview();
  SetStatus("Draft discarded; imported assignments and labels restored.");
 }
 private void EditorSettings_Click(object sender,RoutedEventArgs e) {
  if(_settings is null) {SetStatus("Open Settings from the main workspace.");return;}
  new SettingsWindow(_settings) {Owner=this}.ShowDialog();
  SetStatus($"Occupied destination: {_settings.Current.OccupiedDestinationBehavior}.");
 }
 private void OpenSharedUiLayer_Click(object sender,RoutedEventArgs e)=>
  new DcsConsumerScaffold.UiLayerEditorWindow(_viewModel.CommonRoot,_viewModel.Summary) {Owner=this}.ShowDialog();
 private void Back_Click(object sender,RoutedEventArgs e)=>ImportedTabs.SelectedIndex=0;
 private void EditorZoom_ValueChanged(object sender,RoutedPropertyChangedEventArgs<double> e) {
  if(UiDiagramCanvas is not null)UiDiagramCanvas.LayoutTransform=new ScaleTransform(e.NewValue,e.NewValue);
 }
 private void ChooseChord_Click(object sender,RoutedEventArgs e) {
  var modifiers=_viewModel.ModifierChoices.Skip(1).Distinct(StringComparer.Ordinal).ToArray();
  if(modifiers.Length==0) {SetStatus("Import module modifiers before creating an assignment chord.");return;}
  var panel=new StackPanel {Margin=new Thickness(12)};
  panel.Children.Add(new TextBlock {Text="Select one or more imported modifiers for the assignment chord.",
   TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)});
  var current=Chord().Split('+',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
  var choices=modifiers.Select(name=>new CheckBox {Content=name,Tag=name,IsChecked=current.Contains(name,StringComparer.Ordinal),
   Margin=new Thickness(0,5,0,5)}).ToArray();
  foreach(var choice in choices)panel.Children.Add(choice);
  var apply=new Button {Content="Use chord",Padding=new Thickness(12,6,12,6),
   Margin=new Thickness(0,10,0,0),IsDefault=true};
  panel.Children.Add(apply);
  var dialog=new Window {Owner=this,Title="Assignment chord",Width=420,Height=380,
   Content=new ScrollViewer {Content=panel},WindowStartupLocation=WindowStartupLocation.CenterOwner};
  apply.Click+=(_,_)=>{
   var selected=choices.Where(x=>x.IsChecked==true).Select(x=>(string)x.Tag).ToArray();
   if(selected.Length==0) {SetStatus("Select at least one imported modifier.");return;}
   var chord=string.Join(" + ",selected);
   var layers=UiModifierChoice.Items.Cast<string>().Append(chord).Distinct(StringComparer.Ordinal).ToArray();
   UiModifierChoice.ItemsSource=layers;
   UiModifierChoice.SelectedItem=chord;
   dialog.Close();
  };
  dialog.ShowDialog();
 }
 private void RenderKneeboard_Click(object sender,RoutedEventArgs e)=>ImportedTabs.SelectedIndex=2;
 private void LabelEditor_TextChanged(object sender,TextChangedEventArgs e) {
  if(_updatingInspector||_selected is null||Slot(_selected) is not {} slot)return;
  _displayLabels[slot.Key]=LabelEditor.Text;
  foreach(var border in UiDiagramCanvas.Children.OfType<Border>()) {
   if(border.Tag is HardwareGraphicControl control&&Slot(control)?.Key==slot.Key&&border.Child is TextBlock label)
    label.Text=LabelEditor.Text;
  }
  RefreshReview();
 }
 private void ResetLabel_Click(object sender,RoutedEventArgs e) {
  if(_selected is null||Slot(_selected) is not {} slot)return;
  _displayLabels.Remove(slot.Key);
  RenderUiDiagram();
  RefreshReview();
 }
 private void TuneAxis_Click(object sender,RoutedEventArgs e)=>
  SetStatus("Axis tuning requires an editable axis-filter model.");
 private void ImportCatalog_Click(object sender,RoutedEventArgs e) {
  if(_viewModel.ModuleDcsId is null) {SetStatus("Open a committed module source before importing its command catalog.");return;}
  var dialog=new OpenFileDialog {Title="Import DCS-Common command catalog",Filter="Command catalog|*.json"};
  if(dialog.ShowDialog(this)!=true)return;
  try {
   var catalog=DcsCommandCatalogImporter.Parse(File.ReadAllText(dialog.FileName),_viewModel.ModuleDcsId);
   _moduleCommands=CommandPalette.FromBindings(_viewModel.ImportedBindings,catalog);
   _viewModel.Draft?.AddCommands(catalog.Select(x=>new DraftCommand(x.Id,x.Name,x.IsAxis,AssignmentLayer.Module)));
   PaletteScope.SelectedIndex=0;PaletteScope_SelectionChanged(PaletteScope,new SelectionChangedEventArgs(Selector.SelectionChangedEvent,new System.Collections.ArrayList(),new System.Collections.ArrayList()));
   SetStatus($"Loaded {catalog.Count} module commands and named categories.");
  } catch(Exception ex) {SetStatus(ex.Message);}
 }
 private void LoadDcsHtml_Click(object sender,RoutedEventArgs e) {
  var dialog=new OpenFileDialog {Title="Select DCS Controls HTML exports for this module",
   Filter="DCS Controls HTML|*.html;*.htm",Multiselect=true};
  if(dialog.ShowDialog(this)!=true)return;
  try {
   var catalog=DcsHtmlCommandCatalogImporter.Parse(dialog.FileNames.Select(x=>(x,File.ReadAllText(x))));
   _moduleCommands=CommandPalette.FromBindings(_viewModel.ImportedBindings,catalog);
   _viewModel.Draft?.AddCommands(catalog.Select(x=>new DraftCommand(x.Id,x.Name,x.IsAxis,AssignmentLayer.Module)));
   PaletteScope.SelectedIndex=0;PaletteScope_SelectionChanged(PaletteScope,new SelectionChangedEventArgs(Selector.SelectionChangedEvent,new System.Collections.ArrayList(),new System.Collections.ArrayList()));
   SetStatus($"Loaded {catalog.Count} DCS HTML commands and named categories.");
  } catch(Exception ex) {SetStatus(ex.Message);}
 }
 private void SaveCatalog_Click(object sender,RoutedEventArgs e) {
  if(string.IsNullOrWhiteSpace(_viewModel.ModuleDcsId)) {SetStatus("Open a committed module source before saving its catalog.");return;}
  var dialog=new SaveFileDialog {Title="Save reloadable DCS command catalog",
   FileName=$"{_viewModel.ModuleDcsId}-commands.json",Filter="Command catalog|*.json"};
  if(dialog.ShowDialog(this)!=true)return;
  try {
   var commands=_moduleCommands.Select(x=>new {bindingKey=x.Id,name=x.Name,type=x.IsAxis?"axis":"button",
    categoryPath=x.Category=="Uncategorized"?Array.Empty<string>():x.Category.Split(" / "),isAssignable=true});
   File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(new {schemaVersion=1,moduleId=_viewModel.ModuleDcsId,commands},CatalogJsonOptions));
   SetStatus($"Saved {_moduleCommands.Count} commands to {dialog.FileName}.");
  } catch(Exception ex) {SetStatus(ex.Message);}
 }
 private void Apply(DraftResult? result) {
  if(result is null) {SetStatus(_viewModel.DraftNote);return;}
  if(result.Outcome==DraftOutcome.Applied)RenderUiDiagram();
  RefreshReview();SetStatus(result.Message);
 }
 private void RefreshReview() {
  if(DraftCount is null||DraftReviewList is null)return;
  var history=_viewModel.Draft?.History??[];
  DraftCount.Text=$"Draft · {history.Count} assignment operations · {_displayLabels.Count} displayed labels";
  DraftReviewList.ItemsSource=history.SelectMany(op=>op.Changes.Select(change=>
   $"{op.Name} · {change.Slot.InputKey} · {change.Before?.Command.Name??"Empty"} → {change.After?.Command.Name??"Empty"}")).ToArray();
 }
 private void SetStatus(string message) {if(EditorStatus is not null)EditorStatus.Text=message;}
 private void OnClosing(object? sender,System.ComponentModel.CancelEventArgs e) {
  if(_viewModel.Draft?.HasChanges!=true&&_displayLabels.Count==0)return;
  if(MessageBox.Show(this,"Discard unsaved assignment draft? No source files have been changed.","Unsaved draft",
    MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)e.Cancel=true;
  else {_viewModel.Draft?.Discard();_displayLabels.Clear();}
 }
 private void Close_Click(object sender,RoutedEventArgs e)=>Close();
 #endregion END private helpers
}
