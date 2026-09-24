using System.Windows;
using System.Windows.Controls;

namespace DcsIpia.Presentation;

/// <summary>Displays the theme-aware module workspace preview.</summary>
public partial class MainWindow : Window {
 /// <summary>Initializes the workspace and restores the saved appearance preference.</summary>
 public MainWindow() {
  InitializeComponent();
  ThemePicker.SelectedIndex=ThemeManager.LoadPreference()=="Dark"?1:0;
 }
 #region BEGIN Public
 #endregion END Public
 #region BEGIN private helpers
 private void ThemePicker_SelectionChanged(object sender,SelectionChangedEventArgs e) {
  if(ThemePicker.SelectedItem is not ComboBoxItem {Tag:string theme}) return;
  var saved=ThemeManager.Apply(theme);
  ThemeSaveStatus.Text=saved?string.Empty:"Theme changed for this session, but the preference could not be saved.";
 }
 #endregion END private helpers
}
