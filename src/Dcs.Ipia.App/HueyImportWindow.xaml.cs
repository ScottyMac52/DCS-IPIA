using System.IO;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Rendering;
using Microsoft.Win32;
using System.Text.Json;

namespace DcsIpia.Presentation;

/// <summary>Edits locally persisted module controls on canonical draw.io generated SVGs.</summary>
public partial class HueyImportWindow : Window {
 private readonly HueyImportViewModel _viewModel;
 private readonly IAppSettingsService? _settings;
 private readonly IEditorWorkspaceRepository? _repository;
 private EditorWorkspaceSnapshot? _saved;
 private DraftAssignmentService? _draft;
 private readonly Dictionary<string,string> _labels=new(StringComparer.Ordinal);
 private readonly Dictionary<string,TextBlock> _calloutText=new(StringComparer.Ordinal);
 private HardwareGraphicControl? _selected;
 private Point? _dragStart;
 private bool _updatingInspector;
 private bool _updatingFilters;
 /// <summary>Opens a saved module editor with a SQLite persistence boundary.</summary>
 /// <param name="viewModel">Persisted editor data or a source preview.</param>
 /// <param name="settings">Occupied destination preference.</param>
 /// <param name="repository">Local editor database.</param>
 public HueyImportWindow(HueyImportViewModel viewModel,IAppSettingsService? settings=null,IEditorWorkspaceRepository? repository=null) {
  _viewModel=viewModel??throw new ArgumentNullException(nameof(viewModel));
  _settings=settings;
  _repository=repository;
  _saved=viewModel.Workspace;
  _draft=viewModel.Draft;
  foreach(var pair in viewModel.Labels)_labels[pair.Key]=pair.Value;
  InitializeComponent();
  DataContext=_viewModel;
  KneeboardList.SelectedIndex=0;
  DeviceChoice.ItemsSource=_viewModel.UiDiagrams;
  Layers.ItemsSource=_viewModel.ModuleModifierChoices;
  Layers.SelectedIndex=0;
  Scope.SelectedIndex=0;
  CommandType.SelectedIndex=0;
  DeviceChoice.SelectedIndex=_viewModel.UiDiagrams.Count>0?0:-1;
  if(_viewModel.HasErrors)ImportedTabs.SelectedItem=DiagnosticsTab;
  Closing+=ConfirmClose;
  RefreshEditor();
 }
 #region BEGIN Public
 #endregion END Public
 #region BEGIN private helpers
 private UiLayerDiagram? CurrentDiagram=>DeviceChoice.SelectedItem as UiLayerDiagram;
 private AssignmentLayer CurrentLayer=>Scope.SelectedIndex==1?AssignmentLayer.UiLayer:AssignmentLayer.Module;
 private DraftAssignmentSlot? Slot(HardwareGraphicControl control) {
  var diagram=CurrentDiagram;
  if(diagram is null||_saved is null||_draft is null)return null;
  if(CurrentLayer==AssignmentLayer.UiLayer&&!diagram.SupportsUiLayer)return null;
  var layer=Layers.SelectedItem as string??"Base";
  var shifted=control.Control.EndsWith("-shifted",StringComparison.Ordinal);
  if(shifted&&layer=="Base")return null;
  var hasShiftedPair=!shifted&&diagram.Controls.Any(x=>x.Control==control.Control+"-shifted");
  if(CurrentLayer==AssignmentLayer.UiLayer&&hasShiftedPair&&layer!="Base")return null;
  var chord=layer=="Base"||hasShiftedPair?"":layer.Replace(" + ","+",StringComparison.Ordinal);
  var section=control.IsAxis?BindingSection.Axis:BindingSection.Key;
  var input=DcsInputKey.Resolve(_saved.Configuration,diagram.ProfileId,section,control.InputKey);
  return new DraftAssignmentSlot(_saved.Configuration.Module.Id,diagram.ProfileId,CurrentLayer,
   section,input,chord);
 }
 private DraftAssignment? Assignment(DraftAssignmentSlot slot)=>_draft?.Snapshot.FirstOrDefault(x=>x.Slot.Key==slot.Key);
 private void DeviceChanged(object sender,SelectionChangedEventArgs e)=>RefreshEditor();
 private void LayerChanged(object sender,SelectionChangedEventArgs e)=>RefreshEditor();
 private void ScopeChanged(object sender,SelectionChangedEventArgs e) {
  if(Layers is null)return;
  var previous=Layers.SelectedItem as string??"Base";
  var choices=CurrentLayer==AssignmentLayer.Module?_viewModel.ModuleModifierChoices:_viewModel.UiModifierChoices;
  Layers.ItemsSource=choices;
  Layers.SelectedItem=choices.Contains(previous,StringComparer.Ordinal)?previous:
   previous!="Base"&&choices.Count==2?choices[1]:"Base";
  RefreshEditor();
 }
 private void ZoomChanged(object sender,RoutedPropertyChangedEventArgs<double> e)=>RefreshEditor();
 private void FiltersChanged(object sender,EventArgs e) {
  if(_updatingFilters)return;
  FilterCommands();
 }
 private void RefreshEditor() {
  if(Diagram is null||Commands is null||DeviceTitle is null)return;
  var diagram=CurrentDiagram;
  Diagram.Children.Clear();
  _calloutText.Clear();
  _selected=null;
  DeviceTitle.Text=diagram is null?"Select a stored device":diagram.Title+" — "+(CurrentLayer==AssignmentLayer.Module?"Module controls":"Module UI Layer controls");
  EditorMessage.Text=_saved is null?"Import a module to SQLite or open a saved module to edit its controls.":"";
  _updatingFilters=true;
  var available=CurrentLayer==AssignmentLayer.Module?_viewModel.ModuleCommands:_viewModel.UiCommands;
  CommandCategory.ItemsSource=Enumerable.Repeat("All",1).Concat(available.Select(x=>x.Category).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)).ToArray();
  CommandCategory.SelectedIndex=0;
  _updatingFilters=false;
  FilterCommands();
  if(diagram is null||_saved is null||_draft is null||string.IsNullOrWhiteSpace(diagram.Svg)) {
   RefreshInspector();return;
  }
  try {
   var png=CanonicalSvgRenderer.Render(diagram.Svg,diagram.Width,diagram.Height);
   using var stream=new MemoryStream(png);
   var bitmap=new BitmapImage();
   bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
   Diagram.Width=diagram.Width*Zoom.Value;Diagram.Height=diagram.Height*Zoom.Value;
   var surface=new Canvas {Width=diagram.Width,Height=diagram.Height};
   surface.Children.Add(new Image {Source=bitmap,Width=diagram.Width,Height=diagram.Height});
   CoverAuthoredLabels(surface,diagram.Svg);
   foreach(var control in diagram.Controls) {
    var slot=Slot(control);
    if(slot is null)continue;
    var assignment=Assignment(slot);
    var caption=assignment is null?"Unassigned":_labels.GetValueOrDefault(slot.Key)??assignment.Command.Name;
    var text=new TextBlock {Text=caption,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,
     VerticalAlignment=VerticalAlignment.Center,FontSize=11,Foreground=Brushes.Black};
    _calloutText[slot.Key]=text;
    var original=_saved.Assignments.FirstOrDefault(x=>x.Slot.Key==slot.Key);
    var stateBrush=assignment is null?Brushes.SlateGray:original is null?Brushes.ForestGreen:
     original.Command==assignment.Command?Brushes.White:Brushes.Goldenrod;
    var target=new Border {Width=control.Width,Height=control.Height,Background=Brushes.White,
     BorderBrush=stateBrush,BorderThickness=new Thickness(1),
     Child=text,AllowDrop=true,Cursor=Cursors.Hand,Tag=control};
    Canvas.SetLeft(target,control.X);Canvas.SetTop(target,control.Y);
    target.PreviewMouseLeftButtonDown+=(_,args)=>{_dragStart=args.GetPosition(this);_selected=control;RefreshInspector();};
    target.PreviewMouseMove+=(_,args)=>ControlDrag(target,control,args);
    target.DragOver+=(_,args)=>DragOverControl(control,args);
    target.Drop+=(_,args)=>DropOnControl(control,args);
    var menu=new ContextMenu();
    var assign=new MenuItem {Header="Assign selected command"};assign.Click+=(_,_)=>{_selected=control;AssignClicked(target,new RoutedEventArgs());};menu.Items.Add(assign);
    var clear=new MenuItem {Header="Clear assignment"};clear.Click+=(_,_)=>{_selected=control;ClearClicked(target,new RoutedEventArgs());};menu.Items.Add(clear);
    target.ContextMenu=menu;
    surface.Children.Add(target);
   }
   surface.LayoutTransform=new ScaleTransform(Zoom.Value,Zoom.Value);
   Diagram.Children.Add(surface);
  }
  catch(Exception ex) {EditorMessage.Text="Unable to render saved SVG: "+ex.Message;}
  RefreshInspector();
 }
 private static void CoverAuthoredLabels(Canvas surface,string svg) {
  using var reader=XmlReader.Create(new StringReader(svg),new XmlReaderSettings {
   DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4_000_000
  });
  var root=XDocument.Load(reader,LoadOptions.PreserveWhitespace).Root;
  if(root is null)return;
  foreach(var comment in root.Nodes().OfType<XComment>()) {
   var marker=comment.Value.Trim();
   if(!marker.StartsWith("box:",StringComparison.Ordinal))continue;
   XNode? next=comment.NextNode;
   while(next is XText whitespace&&string.IsNullOrWhiteSpace(whitespace.Value))next=next.NextNode;
   if(next is not XElement box||box.Name.LocalName!="rect")continue;
   var x=double.Parse(box.Attribute("x")?.Value??"0",CultureInfo.InvariantCulture);
   var y=double.Parse(box.Attribute("y")?.Value??"0",CultureInfo.InvariantCulture);
   var width=double.Parse(box.Attribute("width")?.Value??"0",CultureInfo.InvariantCulture);
   var height=double.Parse(box.Attribute("height")?.Value??"0",CultureInfo.InvariantCulture);
   if(width<=0||height<=0)continue;
   var mask=new Border {Width=width,Height=height,Background=Brushes.White,
    BorderBrush=Brushes.SlateGray,BorderThickness=new Thickness(1),Tag=marker["box:".Length..].Trim(),
    Child=new TextBlock {Text="Unassigned",TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,
     VerticalAlignment=VerticalAlignment.Center,FontSize=11,Foreground=Brushes.Black}};
   Canvas.SetLeft(mask,x);Canvas.SetTop(mask,y);
   surface.Children.Add(mask);
  }
 }
 private void FilterCommands() {
  if(Commands is null||CommandCategory is null||CommandCount is null)return;
  var list=CurrentLayer==AssignmentLayer.Module?_viewModel.ModuleCommands:_viewModel.UiCommands;
  var category=CommandCategory.SelectedItem as string;
  var filtered=CommandPalette.Filter(list,CommandSearch.Text,category=="All"?null:category)
   .Where(x=>CommandType.SelectedIndex==0||(CommandType.SelectedIndex==2)==x.IsAxis).ToArray();
  Commands.ItemsSource=filtered;
  CommandCount.Text=$"Showing {filtered.Length} of {list.Count}";
 }
 private void DragStart(object sender,MouseButtonEventArgs e)=>_dragStart=e.GetPosition(this);
 private bool Dragging(MouseEventArgs e)=>_dragStart is Point start&&e.LeftButton==MouseButtonState.Pressed&&
  (Math.Abs(e.GetPosition(this).X-start.X)>SystemParameters.MinimumHorizontalDragDistance||
   Math.Abs(e.GetPosition(this).Y-start.Y)>SystemParameters.MinimumVerticalDragDistance);
 private void CommandDrag(object sender,MouseEventArgs e) {
  if(!Dragging(e)||Commands.SelectedItem is not CommandPaletteEntry command||_draft is null)return;
  _dragStart=null;
  DragDrop.DoDragDrop(Commands,new DataObject(typeof(CommandPaletteEntry),command),DragDropEffects.Copy);
 }
 private void ControlDrag(Border source,HardwareGraphicControl control,MouseEventArgs e) {
  if(!Dragging(e)||_draft is null)return;
  var slot=Slot(control);
  if(slot is null||Assignment(slot) is null)return;
  _dragStart=null;
  DragDrop.DoDragDrop(source,new DataObject(typeof(EditorControlDrag),new EditorControlDrag(slot)),
   DragDropEffects.Move|DragDropEffects.Copy);
 }
 private void DragOverControl(HardwareGraphicControl target,DragEventArgs e) {
  var destination=Slot(target);
  var source=e.Data.GetData(typeof(EditorControlDrag)) as EditorControlDrag;
  var catalog=e.Data.GetData(typeof(CommandPaletteEntry)) as CommandPaletteEntry;
  var compatible=destination is not null&&((catalog is not null&&catalog.IsAxis==target.IsAxis)||
   (source is not null&&source.Slot.ProfileId==destination.ProfileId&&source.Slot.Layer==destination.Layer&&
    source.Slot.Section==destination.Section&&source.Slot.Key!=destination.Key));
  e.Effects=!compatible?DragDropEffects.None:source is null?DragDropEffects.Copy:
   (Keyboard.Modifiers&ModifierKeys.Control)!=0?DragDropEffects.Copy:DragDropEffects.Move;
  e.Handled=true;
 }
 private void DropOnControl(HardwareGraphicControl target,DragEventArgs e) {
  var destination=Slot(target);
  if(destination is null||_draft is null)return;
  var source=e.Data.GetData(typeof(EditorControlDrag)) as EditorControlDrag;
  var command=e.Data.GetData(typeof(CommandPaletteEntry)) as CommandPaletteEntry;
  if(source is not null)Apply((choice)=>(Keyboard.Modifiers&ModifierKeys.Control)!=0?
   _draft.Copy(source.Slot,destination,Behavior,choice):_draft.Move(source.Slot,destination,Behavior,choice),source.Slot);
  else if(command is not null)Apply(choice=>_draft.Assign(destination,
   new DraftCommand(command.Id,command.Name,command.IsAxis,CurrentLayer),Behavior,choice));
  e.Handled=true;
 }
 private OccupiedDestinationBehavior Behavior=>_settings?.Current.OccupiedDestinationBehavior??OccupiedDestinationBehavior.Ask;
 private void Apply(Func<DraftConflictChoice?,DraftResult> operation,DraftAssignmentSlot? source=null) {
  var result=operation(null);
  if(result.Outcome==DraftOutcome.ConfirmationRequired) {
   var decision=MessageBox.Show(this,"Destination is occupied. Yes: replace; No: swap assigned controls; Cancel: keep both.",
    "Occupied control",MessageBoxButton.YesNoCancel,MessageBoxImage.Question);
   var choice=decision==MessageBoxResult.Yes?DraftConflictChoice.Replace:
    decision==MessageBoxResult.No&&source is not null?DraftConflictChoice.Swap:DraftConflictChoice.Cancel;
   result=operation(choice);
  }
  EditorMessage.Text=result.Message;
  if(result.Outcome==DraftOutcome.Applied)RefreshEditor();
 }
 private void AssignClicked(object sender,RoutedEventArgs e) {
  if(_selected is null||_draft is null||Commands.SelectedItem is not CommandPaletteEntry command)return;
  var slot=Slot(_selected);
  if(slot is null)return;
  Apply(choice=>_draft.Assign(slot,new DraftCommand(command.Id,command.Name,command.IsAxis,CurrentLayer),Behavior,choice));
 }
 private void ClearClicked(object sender,RoutedEventArgs e) {
  if(_selected is null||_draft is null)return;
  var slot=Slot(_selected);
  if(slot is not null)Apply(_=>_draft.Remove(slot));
 }
 private void UndoClicked(object sender,RoutedEventArgs e) {
  if(_draft is null)return;
  EditorMessage.Text=_draft.Undo().Message;
  RefreshEditor();
 }
 private void ResetClicked(object sender,RoutedEventArgs e) {
  if(_selected is null)return;
  var slot=Slot(_selected);
  if(slot is null)return;
  _labels.Remove(slot.Key);
  RefreshEditor();
 }
 private void LabelChanged(object sender,TextChangedEventArgs e) {
  if(_updatingInspector||_selected is null||_saved is null)return;
  var slot=Slot(_selected);
  if(slot is null)return;
  if(string.IsNullOrWhiteSpace(LabelEditor.Text))_labels.Remove(slot.Key);
  else _labels[slot.Key]=LabelEditor.Text;
  if(_calloutText.TryGetValue(slot.Key,out var label))label.Text=LabelEditor.Text;
  PendingSummary.Text="Pending label or assignment changes";
 }
 private void RefreshInspector() {
  if(PhysicalInput is null)return;
  var slot=_selected is null?null:Slot(_selected);
  var assignment=slot is null?null:Assignment(slot);
  PhysicalInput.Text=_selected is null?"Select a callout":_selected.Label+" · "+_selected.InputKey;
  Chord.Text=slot is null?"":slot.ModifierChord.Length==0?"Base":slot.ModifierChord;
  CurrentCommand.Text=slot is null?"":_saved?.Assignments.FirstOrDefault(x=>x.Slot.Key==slot.Key)?.Command.Name??"Unassigned";
  PendingCommand.Text=assignment?.Command.Name??"Unassigned";
  DefaultLabel.Text=_selected?.Label??"";
  _updatingInspector=true;
  LabelEditor.IsEnabled=assignment is not null;
  LabelEditor.Text=slot is null?"":assignment is null?"Unassigned":_labels.GetValueOrDefault(slot.Key)??assignment.Command.Name;
  _updatingInspector=false;
  PendingSummary.Text=_draft?.HasChanges==true?"Pending assignments: "+_draft.History.Count:"No pending assignment changes";
 }
 private async void SaveClicked(object sender,RoutedEventArgs e) {
  if(_repository is null||_saved is null||_draft is null) {EditorMessage.Text="Open a saved database workspace to edit.";return;}
  try {
   var updated=_saved with {Assignments=_draft.Snapshot,Labels=new Dictionary<string,string>(_labels,StringComparer.Ordinal)};
   await _repository.SaveAsync(updated);
   _saved=updated;
   var allowed=updated.ModuleCommands.Select(x=>new DraftCommand(x.Id,x.Name,x.IsAxis,AssignmentLayer.Module))
    .Concat(updated.UiCommands.Select(x=>new DraftCommand(x.DcsCommandId,x.Name,x.IsAxis,AssignmentLayer.UiLayer)));
   _draft=new DraftAssignmentService(updated.Configuration,allowed,updated.Assignments,
    updated.Diagrams.SelectMany(x=>x.Callouts).SelectMany(x=>x.Modifier.Split('+',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries)));
   RefreshEditor();
   EditorMessage.Text="Assignments and labels saved to the local database.";
  }
  catch(Exception ex) {EditorMessage.Text="Database save failed: "+ex.Message;}
 }
 private void LoadAuthoredLabelsClicked(object sender,RoutedEventArgs e) {
  if(_saved is null||_draft is null)return;
  var picker=new OpenFileDialog {Title="Select a module config/kneeboard.json file",
   Filter="kneeboard.json|kneeboard.json|JSON files|*.json"};
  if(picker.ShowDialog(this)!=true)return;
  try {
   using var document=JsonDocument.Parse(File.ReadAllText(picker.FileName));
   var metadata=new Dictionary<string,JsonElement> {["config/kneeboard.json"]=document.RootElement};
   var imported=KneeboardLabelImporter.Import(metadata,_saved.Configuration);
   if(imported.Count==0) {EditorMessage.Text="No authored labels matched this saved module.";return;}
   foreach(var pair in imported)_labels[pair.Key]=pair.Value;
   RefreshEditor();
   EditorMessage.Text=$"{imported.Count} authored Module labels staged. Save to database to keep them, or Discard draft.";
   PendingSummary.Text="Pending label changes";
  }
  catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) {
   EditorMessage.Text="Unable to load authored labels: "+ex.Message;
  }
 }
 private void DiscardClicked(object sender,RoutedEventArgs e) {
  _draft?.Discard();_labels.Clear();
  if(_saved is not null)foreach(var pair in _saved.Labels)_labels[pair.Key]=pair.Value;
  RefreshEditor();
  EditorMessage.Text="Pending edits discarded.";
 }
 private void RenderClicked(object sender,RoutedEventArgs e)=>ImportedTabs.SelectedIndex=2;
 private void KneeboardList_SelectionChanged(object sender,SelectionChangedEventArgs e) {
  if(KneeboardPreview is null||KneeboardCaption is null)return;
  KneeboardPreview.Source=null;
  var index=KneeboardList.SelectedIndex;
  if(index<0||index>=_viewModel.PageKeys.Count) {KneeboardCaption.Text="No rendered kneeboard image is available.";return;}
  var asset=_viewModel.Images.FirstOrDefault(x=>x.PageKey==_viewModel.PageKeys[index]);
  if(asset is null) {KneeboardCaption.Text="No stored PNG for this page.";return;}
  try {
   using var stream=new MemoryStream(asset.Bytes);
   var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;
   bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
   KneeboardPreview.Source=bitmap;KneeboardCaption.Text="Stored kneeboard: "+asset.Path;
  }
  catch(Exception ex) {KneeboardCaption.Text=ex.Message;}
 }
 private void ConfirmClose(object? sender,CancelEventArgs e) {
  var labelsChanged=_saved is not null&&(_labels.Count!=_saved.Labels.Count||
   _labels.Any(x=>!_saved.Labels.TryGetValue(x.Key,out var original)||original!=x.Value));
  if((_draft?.HasChanges==true||labelsChanged)&&MessageBox.Show(this,
   "Discard unsaved assignment and label changes?","Pending changes",
   MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)e.Cancel=true;
 }
 private void Close_Click(object sender,RoutedEventArgs e)=>Close();
 #endregion END private helpers
}
