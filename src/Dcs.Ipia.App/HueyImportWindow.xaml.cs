using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Dcs.Ipia.Application;

namespace DcsIpia.Presentation;

/// <summary>Displays imported profiles and existing rendered kneeboard images without editing them.</summary>
public partial class HueyImportWindow : Window {
 private readonly HueyImportViewModel _viewModel;
 /// <summary>Initializes the read-only preview.</summary>
 /// <param name="viewModel">The validated import presentation.</param>
 public HueyImportWindow(HueyImportViewModel viewModel) {
  _viewModel=viewModel??throw new ArgumentNullException(nameof(viewModel));
  InitializeComponent();
  DataContext=_viewModel;
  KneeboardList.SelectedIndex=0;
  UiDiagramList.SelectedIndex=0;
  PaletteScope.SelectedIndex=1;
  if(_viewModel.HasErrors)ImportedTabs.SelectedItem=DiagnosticsTab;
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
  if(asset is null) {
   KneeboardCaption.Text=$"No committed PNG was found for {page}.";
   return;
  }
  try {
   using var stream=new MemoryStream(asset.Bytes,false);
   var bitmap=new BitmapImage();
   bitmap.BeginInit();
   bitmap.CacheOption=BitmapCacheOption.OnLoad;
   bitmap.StreamSource=stream;
   bitmap.EndInit();
   bitmap.Freeze();
   KneeboardPreview.Source=bitmap;
   KneeboardCaption.Text=$"Existing rendered image: {asset.Path} · SHA-256 {asset.Sha256}. This preview does not regenerate the page.";
  }
  catch(Exception ex) {
   KneeboardCaption.Text=$"Unable to display {asset.Path}: {ex.Message}";
  }
 }
 private void UiDiagramList_SelectionChanged(object sender,SelectionChangedEventArgs e) {
  if(_viewModel is null||UiDiagramCanvas is null||UiAssignmentList is null)return;
  UiModifierChoice.ItemsSource=null;
  var index=UiDiagramList.SelectedIndex;
  if(index>=0&&index<_viewModel.UiDiagrams.Count) {
   var modifiers=_viewModel.UiDiagrams[index].Callouts.Select(x=>x.Modifier).Distinct(StringComparer.Ordinal)
    .OrderBy(x=>x.Length==0?0:1).ThenBy(x=>x,StringComparer.Ordinal).ToArray();
   UiModifierChoice.ItemsSource=modifiers.Select(x=>x.Length==0?"Base":x).ToArray();
   UiModifierChoice.SelectedIndex=0;
  }
  RenderUiDiagram();
 }
 private void UiModifierChoice_SelectionChanged(object sender,SelectionChangedEventArgs e)=>RenderUiDiagram();
 private void PaletteScope_SelectionChanged(object sender,SelectionChangedEventArgs e)=>UpdatePalette();
 private void PaletteSearch_TextChanged(object sender,TextChangedEventArgs e)=>UpdatePalette();
 private void UpdatePalette() {
  if(_viewModel is null||CommandPaletteList is null||PaletteScope is null||PaletteSearch is null||PaletteCaption is null)return;
  var isUi=PaletteScope.SelectedIndex==1;
  var all=isUi?_viewModel.UiCommands:_viewModel.ModuleCommands;
  var filtered=CommandPalette.Search(all,PaletteSearch.Text);
  CommandPaletteList.ItemsSource=filtered;
  PaletteCaption.Text=$"{filtered.Count} of {all.Count} {(isUi?"UI Layer":"aircraft")} commands. {_viewModel.PaletteSourceNote} Read-only; assignment editing follows in later PRs.";
 }
 private void RenderUiDiagram() {
  if(_viewModel is null||UiDiagramCanvas is null||UiAssignmentList is null||UiModifierChoice is null)return;
  UiDiagramCanvas.Children.Clear();
  UiAssignmentList.ItemsSource=null;
  var index=UiDiagramList.SelectedIndex;
  if(index<0||index>=_viewModel.UiDiagrams.Count) {
   UiDiagramCaption.Text="Select a device to view its committed hardware image and UI Layer assignments.";
   return;
  }
  var diagram=_viewModel.UiDiagrams[index];
  var chord=UiModifierChoice.SelectedItem as string;
  if(chord is null)return;
  var callouts=diagram.Callouts.Where(x=>string.Equals(x.Modifier,chord=="Base"?"":chord,StringComparison.Ordinal)).ToArray();
  UiDiagramCanvas.Width=diagram.Width;
  UiDiagramCanvas.Height=diagram.Height;
  try {
   using var stream=new MemoryStream(diagram.Background,false);
   var bitmap=new BitmapImage();
   bitmap.BeginInit();
   bitmap.CacheOption=BitmapCacheOption.OnLoad;
   bitmap.StreamSource=stream;
   bitmap.EndInit();
   bitmap.Freeze();
   UiDiagramCanvas.Children.Add(new Image {Source=bitmap,Width=diagram.Width,Height=diagram.Height,Stretch=System.Windows.Media.Stretch.Fill});
   foreach(var callout in callouts) {
    var badge=new Border {Width=callout.Width,Height=callout.Height,BorderThickness=new Thickness(2),
     CornerRadius=new CornerRadius(4),ToolTip=callout.Description,
     Child=new TextBlock {Text=callout.Number.ToString("00",System.Globalization.CultureInfo.InvariantCulture),FontWeight=FontWeights.Bold,
      HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
    badge.SetResourceReference(Border.BackgroundProperty,"SelectedBackgroundBrush");
    badge.SetResourceReference(Border.BorderBrushProperty,"AccentTextBrush");
    ((TextBlock)badge.Child).SetResourceReference(TextBlock.ForegroundProperty,"PrimaryTextBrush");
    Canvas.SetLeft(badge,callout.X);Canvas.SetTop(badge,callout.Y);
    UiDiagramCanvas.Children.Add(badge);
   }
   UiAssignmentList.ItemsSource=callouts.Select(x=>x.Description).ToArray();
   UiDiagramCaption.Text=$"Explicit Huey UI Layer usage on {diagram.Path} · {chord} · {callouts.Length} assignments · source graphic from committed DCS-Common.";
  }
  catch(Exception ex) {
   UiDiagramCanvas.Children.Clear();
   UiDiagramCaption.Text=$"Unable to display {diagram.Path}: {ex.Message}";
  }
 }
 private void Close_Click(object sender,RoutedEventArgs e)=>Close();
 #endregion END private helpers
}
