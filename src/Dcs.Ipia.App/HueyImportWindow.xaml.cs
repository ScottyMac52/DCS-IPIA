using System.Windows;

namespace DcsIpia.Presentation;

/// <summary>Displays the imported Huey source and validation summary without editing it.</summary>
public partial class HueyImportWindow : Window {
 /// <summary>Initializes the read-only preview.</summary>
 /// <param name="viewModel">The validated import presentation.</param>
 public HueyImportWindow(HueyImportViewModel viewModel) {
  InitializeComponent();
  DataContext=viewModel??throw new ArgumentNullException(nameof(viewModel));
 }
 #region BEGIN Public
 #endregion END Public
 #region BEGIN private helpers
 private void Close_Click(object sender,RoutedEventArgs e)=>Close();
 #endregion END private helpers
}
