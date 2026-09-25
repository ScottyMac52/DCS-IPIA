using System.IO;
using System.Windows;
using System.Windows.Controls;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using Microsoft.Win32;

namespace DcsIpia.Presentation;

/// <summary>Displays the themed module workspace and opens read-only repository or Saved Games previews.</summary>
public partial class MainWindow : Window {
 private readonly IHueyWorkspaceLoader _loader;
 private readonly IDcsFolderImporter _folderImporter;
 private readonly IAppSettingsService _settings;
 private HueyImportViewModel? _lastViewModel;
 /// <summary>Initializes the workspace with injected import and settings boundaries.</summary>
 /// <param name="loader">The committed Huey workspace loader.</param>
 /// <param name="folderImporter">The live Saved Games profile reader.</param>
 /// <param name="settings">The validated user preference service.</param>
 public MainWindow(IHueyWorkspaceLoader loader,IDcsFolderImporter folderImporter,IAppSettingsService settings) {
  _loader=loader??throw new ArgumentNullException(nameof(loader));
  _folderImporter=folderImporter??throw new ArgumentNullException(nameof(folderImporter));
  _settings=settings??throw new ArgumentNullException(nameof(settings));
  InitializeComponent();
  ThemeManager.Apply(_settings.Current.Theme.ToString());
  ThemePicker.SelectedIndex=_settings.Current.Theme==AppearanceTheme.Dark?1:0;
  ThemeSaveStatus.Text=_settings.LastError??string.Empty;
 }
 #region BEGIN Public
 #endregion END Public
 #region BEGIN private helpers
 private async void ImportHuey_Click(object sender,RoutedEventArgs e) {
  ImportHueyButton.IsEnabled=false;
  try {
   var selected=new OpenFolderDialog {Title="Select Huey repository root or Saved Games module/joystick folder"};
   if(selected.ShowDialog(this)!=true)return;
   var folder=selected.FolderName;
   if(_folderImporter.HasProfiles(folder)||string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)),"joystick",StringComparison.OrdinalIgnoreCase)||Directory.Exists(Path.Combine(folder,"joystick"))) {
    ImportStatus.Text="Reading local Saved Games joystick profiles…";
    var result=await _folderImporter.ImportAsync(folder);
    if(result.Profiles.Count==0) {
     ShowImportError("No readable joystick profiles were found. "+string.Join("; ",result.Diagnostics.Where(x=>x.IsError).Select(x=>x.Message)));
     return;
    }
    _lastViewModel=new HueyImportViewModel(result);
    ViewImportedButton.IsEnabled=true;
    ImportStatus.Text=$"Read {result.Profiles.Count} local profiles and {result.Bindings.Count} bindings. {result.Diagnostics.Count(x=>x.IsError)} issues; open Diagnostics in the preview. Use View imported to reopen.";
   } else {
    var common=new OpenFolderDialog {Title="Select the DCS-Common Git repository root"};
    if(common.ShowDialog(this)!=true)return;
    ImportStatus.Text="Reading committed Huey, DCS-Common, and kneeboard PNG sources…";
    var preview=await _loader.LoadAsync(common.FolderName,folder);
    if(!preview.Import.Succeeded) {
     ShowImportError("Huey source import needs attention: "+string.Join("; ",preview.Import.Diagnostics.Where(x=>x.IsError).Select(x=>x.Message)));
     return;
    }
    _lastViewModel=new HueyImportViewModel(preview);
    _lastViewModel.CommonRoot=common.FolderName;
    _lastViewModel.SourceFolder=folder;
    ViewImportedButton.IsEnabled=true;
    ImportStatus.Text=$"Imported {preview.Import.Configuration!.Profiles.Count} Huey profiles, {preview.Images.Count} kneeboard PNGs and {preview.Diagrams.Count} UI Layer graphics. {preview.GraphicDiagnostics.Count} graphic issues; use View imported to reopen.";
   }
   ShowImported();
  }
  catch(Exception ex) {
   ShowImportError($"Unable to open this source: {ex.Message}");
  }
  finally {ImportHueyButton.IsEnabled=true;}
 }
 private void OpenEditor_Click(object sender,RoutedEventArgs e) {
  if(_lastViewModel is null)ImportHuey_Click(sender,e);
  else ViewImported_Click(sender,e);
 }
 private void ViewImported_Click(object sender,RoutedEventArgs e) {
  try {ShowImported();}
  catch(Exception ex) {ShowImportError($"Unable to show the imported preview: {ex.Message}");}
 }
 private void ShowImportError(string message) {
  ImportStatus.Text=message;
  MessageBox.Show(this,message,"Import needs attention",MessageBoxButton.OK,MessageBoxImage.Warning);
 }
 private void ShowImported() {
  if(_lastViewModel is null)return;
  var window=new HueyImportWindow(_lastViewModel,_settings){Owner=this};
  window.ShowDialog();
 }
 private void ThemePicker_SelectionChanged(object sender,SelectionChangedEventArgs e) {
  if(ThemePicker.SelectedItem is not ComboBoxItem {Tag:string theme}) return;
  ThemeManager.Apply(theme);
  var saved=_settings.TryUpdateTheme(Enum.Parse<AppearanceTheme>(theme));
  ThemeSaveStatus.Text=saved?string.Empty:_settings.LastError??"Theme changed for this session but was not saved.";
 }
 private void Settings_Click(object sender,RoutedEventArgs e) {
  var dialog=new SettingsWindow(_settings) {Owner=this};
  dialog.ShowDialog();
  ThemePicker.SelectedIndex=_settings.Current.Theme==AppearanceTheme.Dark?1:0;
  ThemeSaveStatus.Text=_settings.LastError??string.Empty;
 }
 #endregion END private helpers
}
