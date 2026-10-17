using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Microsoft.Win32;

namespace DcsIpia.Presentation
{
    /// <summary>Edits a transactional, live preview of profile appearance with explicit Apply and Cancel.</summary>
    public partial class AppearanceWindow : Window
    {
        private readonly IAppearanceStore _store;
        private AppearanceSettings _draft;
        private BitmapSource? _image;
        private byte[]? _pendingImage;
        private bool _ready;
        private bool _committed;
        /// <summary>Opens an appearance session using the current profile's accepted settings.</summary>
        public AppearanceWindow(IAppearanceStore store)
        {
            EditionAccess.Policy.RequireOwner();
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _draft = store.Current;
            _image = AppearanceManager.Image;
            InitializeComponent(); Populate();
            AppearanceStatus.Text = AppearanceManager.LastError ?? store.LastError ?? "Preview changes, then Apply to save.";
            Closed += (_, _) => { if (!_committed) AppearanceManager.Restore(); };
        }
        #region BEGIN Public
        /// <summary>Loads a selected local image into the preview without copying it into the profile.</summary>
        public void LoadImage(string path)
        {
            var image = BackgroundImageLoader.LoadFile(path);
            var bytes = BackgroundImageLoader.Encode(image);
            _image = image; _pendingImage = bytes;
            _draft = _draft with { Enabled = true, Image = null };
            Populate(); UpdatePreview();
        }
        /// <summary>Imports and previews a bounded, complete data-only palette.</summary>
        public void ImportPalette(string path)
        {
            if (new FileInfo(path).Length > AppearanceValidation.MaximumJsonBytes) throw new ArgumentException("Palette exceeds 64 KiB.", nameof(path));
            var palette = AppearanceValidation.ParsePalette(File.ReadAllText(path));
            var candidate = _draft with { Palette = palette, Accent = null };
            AppearanceManager.Preview(candidate, _image);
            _draft = candidate; Populate();
            AppearanceStatus.Text = "Palette previewed. Apply to save.";
        }
        #endregion END Public
        #region BEGIN private helpers
        private void Populate()
        {
            _ready = false;
            BackgroundEnabled.IsChecked = _draft.Enabled; ImageOpacity.Value = _draft.Opacity; ImageTint.Value = _draft.Tint;
            PanelOpacity.Value = _draft.PanelOpacity; ControlOpacity.Value = _draft.ControlOpacity;
            CropX.Value = _draft.Horizontal; CropY.Value = _draft.Vertical;
            ImageFit.SelectedIndex = _draft.Fit == "Fill" ? 0 : 1; AccentColor.Text = _draft.Accent ?? "";
            PaletteStatus.Text = _draft.Palette is null ? "Using the selected skin's colors." : "Custom palette preview (19 semantic colors).";
            ImageStatus.Text = _image is null ? "No usable image selected." : $"Image ready: {_image.PixelWidth} × {_image.PixelHeight} pixels.";
            _ready = true;
        }
        private bool UpdatePreview()
        {
            if (!_ready) return false;
            try
            {
                var candidate = _draft with { Accent = string.IsNullOrWhiteSpace(AccentColor.Text) ? null : AccentColor.Text.Trim(),
                    Enabled = BackgroundEnabled.IsChecked == true, Opacity = ImageOpacity.Value, Tint = ImageTint.Value,
                    PanelOpacity = PanelOpacity.Value, ControlOpacity = ControlOpacity.Value, Horizontal = CropX.Value, Vertical = CropY.Value,
                    Fit = ImageFit.SelectedIndex == 0 ? "Fill" : "Fit" };
                if (candidate.Enabled && _image is null) throw new ArgumentException("Choose an image before enabling the background.");
                AppearanceManager.Preview(candidate, _image); _draft = candidate;
                AppearanceStatus.Text = "Preview only — Apply saves these changes.";
                return true;
            }
            catch (ArgumentException ex) { AppearanceStatus.Text = ex.Message; return false; }
        }
        private void ApplyClicked(object sender, RoutedEventArgs e)
        {
            if (!UpdatePreview()) return;
            if (!_store.TrySave(_draft, _pendingImage))
            {
                AppearanceManager.Restore(); AppearanceStatus.Text = _store.LastError;
                return;
            }
            _committed = true; AppearanceManager.Restore(); Close();
        }
        private void CancelClicked(object sender, RoutedEventArgs e) => Close();
        private void ResetClicked(object sender, RoutedEventArgs e)
        {
            _draft = new(); _image = null; _pendingImage = null; Populate(); UpdatePreview();
        }
        private void RemoveImageClicked(object sender, RoutedEventArgs e)
        {
            _image = null; _pendingImage = null; _draft = _draft with { Enabled = false, Image = null }; Populate(); UpdatePreview();
        }
        private void ClearPaletteClicked(object sender, RoutedEventArgs e)
        {
            _draft = _draft with { Palette = null, Accent = null }; Populate(); UpdatePreview();
        }
        private void ChooseImageClicked(object sender, RoutedEventArgs e)
        {
            var picker = new OpenFileDialog { Title = "Choose application background", Filter = "Images|*.jpg;*.jpeg;*.png", CheckFileExists = true };
            if (picker.ShowDialog(this) == true) TryImport(() => LoadImage(picker.FileName));
        }
        private void ImportPaletteClicked(object sender, RoutedEventArgs e)
        {
            var picker = new OpenFileDialog { Title = "Import appearance palette", Filter = "Palette JSON|*.json", CheckFileExists = true };
            if (picker.ShowDialog(this) == true) TryImport(() => ImportPalette(picker.FileName));
        }
        private void TryImport(Action import)
        {
            try { import(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or JsonException or NotSupportedException or InvalidOperationException or System.IO.FileFormatException)
            { AppearanceStatus.Text = "Appearance import failed. " + ex.Message; }
        }
        private void AdjustmentChanged(object sender, RoutedEventArgs e) => UpdatePreview();
        private void SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdatePreview();
        private void FitChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();
        private void AccentChanged(object sender, TextChangedEventArgs e) => UpdatePreview();
        #endregion END private helpers
    }
}
