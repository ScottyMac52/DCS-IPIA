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
  UiDiagramCanvas.Children.Clear();
  UiAssignmentList.ItemsSource=null;
  var index=UiDiagramList.SelectedIndex;
  if(index<0||index>=_viewModel.UiDiagrams.Count) {
   UiDiagramCaption.Text="Select a device to view its committed hardware image and UI Layer assignments.";
   return;
  }
  var diagram=_viewModel.UiDiagrams[index];
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
   foreach(var callout in diagram.Callouts) {
    var badge=new Border {Width=callout.Width,Height=callout.Height,BorderThickness=new Thickness(2),
     CornerRadius=new CornerRadius(4),ToolTip=callout.Description,
     Child=new TextBlock {Text=callout.Number.ToString("00"),FontWeight=FontWeights.Bold,
      HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
    badge.SetResourceReference(Border.BackgroundProperty,"SelectedBackgroundBrush");
    badge.SetResourceReference(Border.BorderBrushProperty,"AccentTextBrush");
    ((TextBlock)badge.Child).SetResourceReference(TextBlock.ForegroundProperty,"PrimaryTextBrush");
    Canvas.SetLeft(badge,callout.X);Canvas.SetTop(badge,callout.Y);
    UiDiagramCanvas.Children.Add(badge);
   }
   UiAssignmentList.ItemsSource=diagram.Callouts.Select(x=>x.Description).ToArray();
   UiDiagramCaption.Text=$"Explicit Huey UI Layer usage on {diagram.Path} · {diagram.Callouts.Count} assignments · source graphic from committed DCS-Common.";
  }
  catch(Exception ex) {
   UiDiagramCanvas.Children.Clear();
   UiDiagramCaption.Text=$"Unable to display {diagram.Path}: {ex.Message}";
  }
 }
 private void Close_Click(object sender,RoutedEventArgs e)=>Close();
 #endregion END private helpers
}
