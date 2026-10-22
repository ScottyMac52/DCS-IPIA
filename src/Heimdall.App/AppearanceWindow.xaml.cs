using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Heimdall.Application;
using Heimdall.Application.Interfaces;

namespace Heimdall.App
{
    /// <summary>Edits a transactional, live preview of profile appearance with explicit Apply and Cancel.</summary>
    public partial class AppearanceWindow : Window
    {
        private readonly IAppearanceStore _store;
        private AppearanceSettings _draft;
        private BitmapSource? _image;
        private readonly Dictionary<string, byte[]> _pendingImages = new(StringComparer.Ordinal);
        private readonly Dictionary<string, BitmapSource> _images;
        private string _target = "";
        private bool _ready;
        private bool _committed;
        /// <summary>Opens an appearance session using the current profile's accepted settings.</summary>
        public AppearanceWindow(IAppearanceStore store)
        {
            EditionAccess.Policy.RequirePremium();
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _draft = store.Current;
            _image = AppearanceManager.Image;
            _images = new Dictionary<string, BitmapSource>(AppearanceManager.Images, StringComparer.Ordinal);
            if (_draft.Image is not null && _image is not null) _images[_draft.Image] = _image;
            InitializeComponent();
            TargetChoice.ItemsSource = new[] { new KeyValuePair<string, string>("", "Application default") }.Concat(AppearanceTargets.All);
            TargetChoice.SelectedIndex = 0; Populate();
            AppearanceStatus.Text = AppearanceManager.LastError ?? store.LastError ?? "Preview changes, then Apply to save.";
            Closed += (_, _) => { if (!_committed) AppearanceManager.Restore(); };
        }
        #region BEGIN Public
        /// <summary>Loads a selected local image into the preview without copying it into the profile.</summary>
        public void LoadImage(string path)
        {
            var image = BackgroundImageLoader.LoadFile(path);
            var bytes = BackgroundImageLoader.Encode(image);
            var name = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)) + ".png";
            _image = image; _pendingImages[name] = bytes; _images[name] = image;
            SetBackground(Effective() with { Enabled = true, Image = name });
            Populate(); UpdatePreview();
        }
        /// <summary>Imports and previews a bounded, complete data-only palette.</summary>
        public void ImportPalette(string path)
        {
            if (new FileInfo(path).Length > AppearanceValidation.MaximumJsonBytes) throw new ArgumentException("Palette exceeds 64 KiB.", nameof(path));
            var palette = AppearanceValidation.ParsePalette(File.ReadAllText(path));
            var candidate = _draft with { Palette = palette, Accent = null };
            AppearanceManager.Preview(candidate, DefaultImage(), _images);
            _draft = candidate; Populate();
            AppearanceStatus.Text = "Palette previewed. Apply to save.";
        }
        #endregion END Public
        #region BEGIN private helpers
        private AppearanceSettings Effective() => AppearanceTargets.Resolve(_draft, _target,
            _target.EndsWith("View", StringComparison.Ordinal) ? "ModuleWorkspaceWindow" : null);
        private BitmapSource? DefaultImage() => _draft.Image is not null ? _images.GetValueOrDefault(_draft.Image) : null;
        private void SetBackground(AppearanceSettings value)
        {
            if (_target.Length == 0) _draft = AppearanceBackground.From(value).Apply(_draft);
            else
            {
                var backgrounds = _draft.Backgrounds is null ? new Dictionary<string, AppearanceBackground>(StringComparer.Ordinal)
                    : new Dictionary<string, AppearanceBackground>(_draft.Backgrounds, StringComparer.Ordinal);
                backgrounds[_target] = AppearanceBackground.From(value);
                _draft = _draft with { Backgrounds = backgrounds };
            }
        }
        private void TargetChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready) return;
            _target = (string?)TargetChoice.SelectedValue ?? "";
            Populate();
        }
        private void ModeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready || _target.Length == 0) return;
            if (TargetMode.SelectedIndex == 0)
            {
                var backgrounds = new Dictionary<string, AppearanceBackground>(_draft.Backgrounds ?? new Dictionary<string, AppearanceBackground>(), StringComparer.Ordinal);
                backgrounds.Remove(_target);
                _draft = _draft with { Backgrounds = backgrounds.Count == 0 ? null : backgrounds };
            }
            else SetBackground(Effective() with { Enabled = TargetMode.SelectedIndex == 1 && _image is not null,
                Image = Effective().Image });
            Populate(); UpdatePreview();
        }
        private void Populate()
        {
            _ready = false;
            var background = Effective();
            _image = background.Image is not null ? _images.GetValueOrDefault(background.Image) : null;
            TargetMode.IsEnabled = _target.Length > 0;
            TargetMode.SelectedIndex = _target.Length == 0 || _draft.Backgrounds?.ContainsKey(_target) == true ? background.Enabled ? 1 : 2 : 0;
            BackgroundAdjustments.IsEnabled = _target.Length == 0 || TargetMode.SelectedIndex != 0;
            BackgroundEnabled.Visibility = _target.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            BackgroundEnabled.IsChecked = background.Enabled; ImageOpacity.Value = background.Opacity; ImageTint.Value = background.Tint;
            ArtworkOpacity.Value = background.ArtworkOpacity;
            PanelOpacity.Value = _draft.PanelOpacity; ControlOpacity.Value = _draft.ControlOpacity;
            CropX.Value = background.Horizontal; CropY.Value = background.Vertical;
            ImageFit.SelectedIndex = background.Fit == "Fill" ? 0 : 1; AccentColor.Text = _draft.Accent ?? "";
            AppearanceManager.SetTarget(TargetPreview, _target);
            PaletteStatus.Text = _draft.Palette is null ? "Using the selected skin's colors." : "Custom palette preview (19 semantic colors).";
            ImageStatus.Text = _image is null ? "No usable image selected." : $"Image ready: {_image.PixelWidth} × {_image.PixelHeight} pixels.";
            _ready = true;
        }
        private bool UpdatePreview()
        {
            if (!_ready) return false;
            try
            {
                var background = Effective() with { Enabled = BackgroundEnabled.IsChecked == true,
                    Opacity = ImageOpacity.Value, Tint = ImageTint.Value, Horizontal = CropX.Value, Vertical = CropY.Value,
                    Fit = ImageFit.SelectedIndex == 0 ? "Fill" : "Fit", ArtworkOpacity = ArtworkOpacity.Value };
                if (_target.Length == 0 || TargetMode.SelectedIndex != 0)
                {
                    if (background.Enabled && _image is null) throw new ArgumentException("Choose an image before enabling the background.");
                    SetBackground(background);
                }
                var candidate = _draft with { Accent = string.IsNullOrWhiteSpace(AccentColor.Text) ? null : AccentColor.Text.Trim(),
                    PanelOpacity = PanelOpacity.Value, ControlOpacity = ControlOpacity.Value };
                AppearanceManager.Preview(candidate, DefaultImage(), _images); _draft = candidate;
                AppearanceStatus.Text = "Preview only — Apply saves these changes.";
                return true;
            }
            catch (ArgumentException ex) { AppearanceStatus.Text = ex.Message; return false; }
        }
        private void ApplyClicked(object sender, RoutedEventArgs e)
        {
            if (!UpdatePreview()) return;
            if (!_store.TrySave(_draft, images: _pendingImages))
            {
                AppearanceManager.Restore(); AppearanceStatus.Text = _store.LastError;
                return;
            }
            _committed = true; AppearanceManager.Restore(); Close();
        }
        private void CancelClicked(object sender, RoutedEventArgs e) => Close();
        private void ResetClicked(object sender, RoutedEventArgs e)
        {
            _draft = new(); _image = null; _pendingImages.Clear(); _images.Clear(); Populate(); UpdatePreview();
        }
        private void RemoveImageClicked(object sender, RoutedEventArgs e)
        {
            _image = null; SetBackground(Effective() with { Enabled = false, Image = null }); Populate(); UpdatePreview();
        }
        private void ClearPaletteClicked(object sender, RoutedEventArgs e)
        {
            _draft = _draft with { Palette = null, Accent = null }; Populate(); UpdatePreview();
        }
        private void ChooseImageClicked(object sender, RoutedEventArgs e)
        {
            var picker = new ThemeBrowseDialog { Title = "Choose application background", Filter = "Images|*.jpg;*.jpeg;*.png", CheckFileExists = true };
            if (picker.ShowDialog(this) == true) TryImport(() => LoadImage(picker.FileName));
        }
        private void ImportPaletteClicked(object sender, RoutedEventArgs e)
        {
            var picker = new ThemeBrowseDialog { Title = "Import appearance palette", Filter = "Palette JSON|*.json", CheckFileExists = true };
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
