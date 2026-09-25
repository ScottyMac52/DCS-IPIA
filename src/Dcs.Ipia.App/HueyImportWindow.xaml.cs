using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Dcs.Ipia.Application;

namespace DcsIpia.Presentation;

/// <summary>Displays imported sources and opens the original DCS-Common scaffolding workspace.</summary>
public partial class HueyImportWindow : Window
{
    private readonly HueyImportViewModel _viewModel;

    /// <summary>Creates the import preview and retains the existing top tab navigation.</summary>
    /// <param name="viewModel">The validated repository or Saved Games import.</param>
    /// <param name="settings">Existing application preferences.</param>
    public HueyImportWindow(HueyImportViewModel viewModel, IAppSettingsService? settings = null)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        KneeboardList.SelectedIndex = 0;
        if (_viewModel.HasErrors) ImportedTabs.SelectedItem = DiagnosticsTab;
    }

    #region BEGIN Public
    #endregion END Public

    #region BEGIN private helpers
    private void KneeboardList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (KneeboardPreview is null || KneeboardCaption is null) return;
        KneeboardPreview.Source = null;
        var index = KneeboardList.SelectedIndex;
        if (index < 0 || index >= _viewModel.PageKeys.Count)
        {
            KneeboardCaption.Text = "No rendered kneeboard image is available from this source.";
            return;
        }
        var asset = _viewModel.Images.FirstOrDefault(x => x.PageKey == _viewModel.PageKeys[index]);
        if (asset is null)
        {
            KneeboardCaption.Text = "No committed PNG was found for this page.";
            return;
        }
        try
        {
            using var stream = new MemoryStream(asset.Bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            KneeboardPreview.Source = bitmap;
            KneeboardCaption.Text = $"Existing rendered image: {asset.Path} · SHA-256 {asset.Sha256}.";
        }
        catch (Exception ex)
        {
            KneeboardCaption.Text = $"Unable to display {asset.Path}: {ex.Message}";
        }
    }

    private void OpenOriginalScaffold_Click(object sender, RoutedEventArgs e)
    {
        var profiles = _viewModel.SourceFolder;
        if (profiles is not null)
        {
            var repoProfiles = Path.Combine(profiles, "src", "Config", "Input", "UH-1H", "joystick");
            if (Directory.Exists(repoProfiles)) profiles = repoProfiles;
            else if (Directory.Exists(Path.Combine(profiles, "joystick"))) profiles = Path.Combine(profiles, "joystick");
        }
        new DcsConsumerScaffold.MainWindow(_viewModel.CommonRoot, profiles) { Owner = this }.ShowDialog();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    #endregion END private helpers
}
