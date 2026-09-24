using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

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
 private void Close_Click(object sender,RoutedEventArgs e)=>Close();
 #endregion END private helpers
}
