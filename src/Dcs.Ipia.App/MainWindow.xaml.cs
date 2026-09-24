using System.Windows;
using System.Windows.Controls;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using Microsoft.Win32;

namespace DcsIpia.Presentation;

/// <summary>Displays the theme-aware module workspace and opens read-only Huey imports.</summary>
public partial class MainWindow : Window {
 private readonly IHueyWorkspaceLoader _loader=new HueyWorkspaceLoader();
 private HueyImportResult? _lastImport;
 /// <summary>Initializes the workspace and restores the saved appearance preference.</summary>
 public MainWindow() {
  InitializeComponent();
  ThemePicker.SelectedIndex=ThemeManager.LoadPreference()=="Dark"?1:0;
 }
 #region BEGIN Public
 #endregion END Public
 #region BEGIN private helpers
 private async void ImportHuey_Click(object sender,RoutedEventArgs e) {
  var huey=new OpenFolderDialog {Title="Select the DCS-UH-1H-Huey repository root"};
  if(huey.ShowDialog(this)!=true)return;
  var common=new OpenFolderDialog {Title="Select the DCS-Common repository root"};
  if(common.ShowDialog(this)!=true)return;
  ImportHueyButton.IsEnabled=false;
  ImportStatus.Text="Reading committed Huey and DCS-Common source files…";
  try {
   var result=await _loader.LoadAsync(common.FolderName,huey.FolderName);
   if(!result.Succeeded) {
    ImportStatus.Text="Import needs attention: "+string.Join("; ",result.Diagnostics.Where(x=>x.IsError).Select(x=>x.Message));
    return;
   }
   _lastImport=result;
   ViewImportedButton.IsEnabled=true;
   ImportStatus.Text=$"Imported {result.Configuration!.Profiles.Count} Huey profiles and {result.Pages.Count} kneeboard pages from committed sources. No files were changed.";
   ShowImported();
  }
  catch(Exception ex) when(ex is IOException or InvalidOperationException or UnauthorizedAccessException or ArgumentException) {
   ImportStatus.Text=$"Unable to import: {ex.Message}";
  }
  finally {ImportHueyButton.IsEnabled=true;}
 }
 private void ViewImported_Click(object sender,RoutedEventArgs e)=>ShowImported();
 private void ShowImported() {
  if(_lastImport is null)return;
  var window=new HueyImportWindow(new HueyImportViewModel(_lastImport)){Owner=this};
  window.ShowDialog();
 }
 private void ThemePicker_SelectionChanged(object sender,SelectionChangedEventArgs e) {
  if(ThemePicker.SelectedItem is not ComboBoxItem {Tag:string theme}) return;
  var saved=ThemeManager.Apply(theme);
  ThemeSaveStatus.Text=saved?string.Empty:"Theme changed for this session, but the preference could not be saved.";
 }
 #endregion END private helpers
}
