using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Rendering;

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
  PaletteScope.SelectedIndex=1;
  EditorStatus.Text=_viewModel.DraftNote;
  RefreshReview();
  if(_viewModel.HasErrors)ImportedTabs.SelectedItem=DiagnosticsTab;
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
  RenderUiDiagram();
 }
 private void UiModifierChoice_SelectionChanged(object sender,SelectionChangedEventArgs e) {
  _pendingSource=null;_pendingAction=null;RenderUiDiagram();
 }
 private void PaletteScope_SelectionChanged(object sender,SelectionChangedEventArgs e) {
  _pendingSource=null;_pendingAction=null;
  UpdatePalette();RenderUiDiagram();
 }
 private void PaletteSearch_TextChanged(object sender,TextChangedEventArgs e)=>UpdatePalette();
 private void UpdatePalette() {
  if(_viewModel is null||CommandPaletteList is null||PaletteScope is null||PaletteSearch is null||PaletteCaption is null)return;
  var isUi=PaletteScope.SelectedIndex==1;
  var all=isUi?_viewModel.UiCommands:_viewModel.ModuleCommands;
  var filtered=CommandPalette.Search(all,PaletteSearch.Text);
  CommandPaletteList.ItemsSource=filtered;
  PaletteCaption.Text=$"{filtered.Count} of {all.Count} {(isUi?"UI Layer":"aircraft")} commands. {_viewModel.PaletteSourceNote} Drag a command onto a control or select it and choose Assign.";
 }
 private void RenderUiDiagram() {
  if(UiDiagramCanvas is null||UiAssignmentList is null||UiDiagramCaption is null||UiModifierChoice is null)return;
  UiDiagramCanvas.Children.Clear();_visibleControls=[];UiAssignmentList.ItemsSource=null;
  var diagram=_currentDiagram;
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
     ToolTip=$"{control.Label} · {control.Control} · {control.InputKey} · {label}",
     Child=new TextBlock {Text=label,FontSize=11,FontWeight=FontWeights.SemiBold,
      TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis,
      HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,
      TextAlignment=TextAlignment.Center}};
    badge.SetResourceReference(Border.BackgroundProperty,"SurfaceBrush");
    badge.SetResourceReference(Border.BorderBrushProperty,"AccentTextBrush");
    ((TextBlock)badge.Child).SetResourceReference(TextBlock.ForegroundProperty,"PrimaryTextBrush");
    badge.MouseLeftButtonDown+=(_,_)=>SelectControl(control);
    badge.PreviewMouseMove+=(_,args)=>Control_PreviewMouseMove(badge,control,args);
    badge.DragOver+=Control_DragOver;
    badge.Drop+=(_,args)=>Control_Drop(control,args);
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
  if(_viewModel.Draft is null||_currentDiagram is null)return null;
  return new(_viewModel.ModuleId,
   _currentDiagram.ProfileId,Layer(),control.IsAxis?BindingSection.Axis:BindingSection.Key,
   control.InputKey,Chord()=="Base"?"":Chord());
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
 }
 private void UiAssignmentList_SelectionChanged(object sender,SelectionChangedEventArgs e) {
  if(UiAssignmentList.SelectedIndex>=0&&UiAssignmentList.SelectedIndex<_visibleControls.Count)
   SelectControl(_visibleControls[UiAssignmentList.SelectedIndex]);
 }
 private void UpdateInspector() {
  if(SelectedControlTitle is null||SelectedAssignment is null)return;
  SelectedControlTitle.Text=_selected is null?"Select a control":$"{_selected.Label} · {_selected.Control}";
  var assignment=_selected is null?null:CurrentAssignment(_selected);
  SelectedAssignment.Text=_selected is null?"Choose an authored callout or keyboard list row.":
   $"{assignment?.Command.Name??"Empty · choose a command"}\n{_selected.InputKey} · {Layer()} / {Chord()}";
 }
 private void CommandPaletteList_PreviewMouseMove(object sender,MouseEventArgs e) {
  if(e.LeftButton==MouseButtonState.Pressed&&CommandPaletteList.SelectedItem is CommandPaletteEntry command&&_viewModel.Draft is not null)
   DragDrop.DoDragDrop(CommandPaletteList,new DataObject(typeof(CommandPaletteEntry),command),DragDropEffects.Copy);
 }
 private void Control_PreviewMouseMove(Border badge,HardwareGraphicControl control,MouseEventArgs e) {
  if(e.LeftButton==MouseButtonState.Pressed&&CurrentAssignment(control) is not null)
   DragDrop.DoDragDrop(badge,new DataObject(typeof(HardwareGraphicControl),control),DragDropEffects.Move|DragDropEffects.Copy);
 }
 private void Control_DragOver(object sender,DragEventArgs e) {
  e.Effects=_viewModel.Draft is not null&&(e.Data.GetDataPresent(typeof(CommandPaletteEntry))||
   e.Data.GetDataPresent(typeof(HardwareGraphicControl)))?DragDropEffects.Copy:DragDropEffects.None;
  e.Handled=true;
 }
 private void Control_Drop(HardwareGraphicControl control,DragEventArgs e) {
  if(e.Data.GetData(typeof(CommandPaletteEntry)) is CommandPaletteEntry command)AssignTo(control,command);
  else if(e.Data.GetData(typeof(HardwareGraphicControl)) is HardwareGraphicControl source)
   ApplyTransfer(source,control,Keyboard.Modifiers.HasFlag(ModifierKeys.Control)?"Copy":"Move");
  e.Handled=true;
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
  _viewModel.Draft?.Discard();RenderUiDiagram();RefreshReview();SetStatus("Draft discarded; imported assignments restored.");
 }
 private void EditorSettings_Click(object sender,RoutedEventArgs e) {
  if(_settings is null) {SetStatus("Open Settings from the main workspace.");return;}
  new SettingsWindow(_settings) {Owner=this}.ShowDialog();
  SetStatus($"Occupied destination: {_settings.Current.OccupiedDestinationBehavior}.");
 }
 private void Apply(DraftResult? result) {
  if(result is null) {SetStatus(_viewModel.DraftNote);return;}
  if(result.Outcome==DraftOutcome.Applied)RenderUiDiagram();
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
