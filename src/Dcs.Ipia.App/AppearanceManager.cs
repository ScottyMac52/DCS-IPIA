using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace DcsIpia.Presentation
{
    /// <summary>Composes palette overrides and decorative window backgrounds without changing document artwork.</summary>
    public static class AppearanceManager
    {
        private static readonly ConditionalWeakTable<Window, object> Attached = new();
        private static readonly uint[] PhotoExtremes = [0, 0xFFFFFF];
        private static readonly string[] WindowTextKeys = ["PrimaryTextBrush", "SecondaryTextBrush", "AccentTextBrush", "ErrorTextBrush"];
        private static ResourceDictionary? _overlay;
        private static AppearanceSettings _active = new();
        private static BitmapSource? _image;
        private static bool _observingContrast;
        #region BEGIN Public
        /// <summary>Gets the selected profile's accepted appearance store.</summary>
        public static IAppearanceStore? Store { get; private set; }
        /// <summary>Gets the current appearance load/preview diagnostic.</summary>
        public static string? LastError { get; private set; }
        /// <summary>Gets the in-memory appearance, including unsaved preview changes.</summary>
        public static AppearanceSettings Active => _active;
        /// <summary>Gets the unlocked decoded preview image.</summary>
        public static BitmapSource? Image => _image;
        /// <summary>Loads the active profile before its windows are displayed.</summary>
        public static void Configure(IAppearanceStore? store)
        {
            Store = store;
            if (!_observingContrast)
            {
                SystemParameters.StaticPropertyChanged += (_, e) => { if (e.PropertyName == nameof(SystemParameters.HighContrast)) Reapply(); };
                _observingContrast = true;
            }
            Restore();
        }
        /// <summary>Applies a validated draft to all open windows without writing settings or files.</summary>
        public static void Preview(AppearanceSettings settings, BitmapSource? image)
        {
            AppearanceValidation.Validate(settings);
            var dictionary = BuildPalette(settings, image is not null);
            _active = settings; _image = image;
            ReplaceOverlay(dictionary);
            foreach (var window in System.Windows.Application.Current.Windows.OfType<Window>()) Paint(window);
            LastError = null;
        }
        /// <summary>Reloads the accepted appearance after Cancel, close or successful persistence.</summary>
        public static void Restore()
        {
            var settings = Store?.Current ?? new(); BitmapSource? image = null;
            string? diagnostic = Store?.LastError;
            try
            {
                var bytes = Store?.ReadImage();
                if (bytes is not null) image = BackgroundImageLoader.Decode(bytes);
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or System.IO.FileFormatException)
            { diagnostic = "Background image could not be decoded; choose it again in Appearance. " + ex.Message; }
            try { Preview(settings, image); }
            catch (ArgumentException ex) { Preview(settings with { Accent = null }, image); _active = settings; diagnostic = ex.Message; }
            LastError = diagnostic ?? Store?.LastError;
        }
        /// <summary>Recomposes active overrides after the selected built-in skin or Windows contrast mode changes.</summary>
        public static void Reapply()
        {
            if (_overlay is null && Store is null) return;
            var requested = _active;
            try { Preview(requested, _image); }
            catch (ArgumentException ex) { Preview(requested with { Accent = null }, _image); _active = requested; LastError = ex.Message; }
        }
        /// <summary>Applies the current window background and updates it on resize.</summary>
        public static void Attach(Window window)
        {
            if (!Attached.TryGetValue(window, out _))
            {
                Attached.Add(window, new object());
                window.SizeChanged += (_, _) => Paint(window);
            }
            Paint(window);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static ResourceDictionary BuildPalette(AppearanceSettings settings, bool hasImage)
        {
            var app = System.Windows.Application.Current;
            var basis = app.Resources.MergedDictionaries.First(d => d.Source is not null && Enum.GetNames<AppearanceTheme>().Any(n =>
                d.Source.OriginalString.EndsWith("/" + n + ".xaml", StringComparison.OrdinalIgnoreCase)));
            var colors = AppearanceValidation.ColorKeys.ToDictionary(key => key,
                key => ((SolidColorBrush)basis[key]).Color.ToString(System.Globalization.CultureInfo.InvariantCulture)[3..], StringComparer.Ordinal);
            foreach (var key in colors.Keys.ToArray()) colors[key] = "#" + colors[key];
            if (!SystemParameters.HighContrast)
            {
                if (settings.Palette is not null) foreach (var pair in settings.Palette) colors[pair.Key] = pair.Value;
                if (settings.Accent is not null)
                {
                    AppearanceValidation.RequireContrast(settings.Accent, colors["WindowBackgroundBrush"], "Accent on window");
                    AppearanceValidation.RequireContrast(settings.Accent, colors["SurfaceBrush"], "Accent on panel");
                    colors["AccentTextBrush"] = settings.Accent;
                }
            }
            var dictionary = new ResourceDictionary();
            foreach (var (key, value) in colors)
            {
                var color = (Color)ColorConverter.ConvertFromString(value);
                if (key == "SurfaceBrush" && settings.Enabled && hasImage && !SystemParameters.HighContrast)
                    color.A = (byte)Math.Round(255 * ProtectedPanelOpacity(colors, settings.PanelOpacity));
                var brush = new SolidColorBrush(color); brush.Freeze(); dictionary[key] = brush;
            }
            var aliases = new Dictionary<object, string> {
                [SystemColors.WindowBrushKey] = "SurfaceBrush", [SystemColors.WindowTextBrushKey] = "PrimaryTextBrush",
                [SystemColors.ControlBrushKey] = "ControlBackgroundBrush", [SystemColors.ControlTextBrushKey] = "ControlForegroundBrush",
                [SystemColors.HighlightBrushKey] = "SelectedBackgroundBrush", [SystemColors.HighlightTextBrushKey] = "PrimaryTextBrush",
                [SystemColors.GrayTextBrushKey] = "DisabledTextBrush", [SystemColors.MenuBrushKey] = "SurfaceBrush",
                [SystemColors.MenuTextBrushKey] = "PrimaryTextBrush", [SystemColors.ScrollBarBrushKey] = "SurfaceBrush",
                [SystemColors.InactiveSelectionHighlightBrushKey] = "SelectedBackgroundBrush", [SystemColors.InactiveSelectionHighlightTextBrushKey] = "PrimaryTextBrush",
                [SystemColors.ControlLightBrushKey] = "SurfaceBrush", [SystemColors.ControlLightLightBrushKey] = "SurfaceBrush",
                [SystemColors.ControlDarkBrushKey] = "ControlBorderBrush", [SystemColors.ControlDarkDarkBrushKey] = "ControlBorderBrush",
                [SystemColors.InfoBrushKey] = "ControlBackgroundBrush", [SystemColors.InfoTextBrushKey] = "ControlForegroundBrush" };
            foreach (var (key, value) in aliases) dictionary[key] = dictionary[value];
            return dictionary;
        }
        private static double ProtectedPanelOpacity(Dictionary<string, string> colors, double requested)
        {
            var panel = AppearanceColors.Parse(colors["SurfaceBrush"]);
            var foregrounds = WindowTextKeys.Select(key => AppearanceColors.Parse(colors[key])).ToArray();
            while (requested < 1 && PhotoExtremes.Any(background => foregrounds.Any(foreground => AppearanceColors.Contrast(foreground,
                AppearanceColors.Blend(panel, background, requested)) < 4.5))) requested = Math.Min(1, requested + 0.01);
            return requested;
        }
        private static void ReplaceOverlay(ResourceDictionary dictionary)
        {
            var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
            if (_overlay is not null) dictionaries.Remove(_overlay);
            dictionaries.Add(dictionary); _overlay = dictionary;
        }
        private static void Paint(Window window)
        {
            if (Store is null && !_active.Enabled && _overlay is null) return;
            if (window.Content is Panel panel && ReferenceEquals(panel.Background, window.TryFindResource("WindowBackgroundBrush")))
                panel.Background = Brushes.Transparent;
            var background = (SolidColorBrush)window.FindResource("WindowBackgroundBrush");
            if (!_active.Enabled || _image is null || SystemParameters.HighContrast)
            { window.SetResourceReference(Window.BackgroundProperty, "WindowBackgroundBrush"); return; }
            var width = Math.Max(1, window.ActualWidth); var height = Math.Max(1, window.ActualHeight);
            var opacity = _active.Opacity;
            var baseColor = AppearanceColors.Parse("#" + background.Color.ToString(System.Globalization.CultureInfo.InvariantCulture)[3..]);
            var text = WindowTextKeys.Select(key =>
                AppearanceColors.Parse("#" + ((SolidColorBrush)window.FindResource(key)).Color.ToString(System.Globalization.CultureInfo.InvariantCulture)[3..])).ToArray();
            while (opacity > 0 && text.Any(color => PhotoExtremes.Any(photo =>
                AppearanceColors.Contrast(color, AppearanceColors.Blend(photo, baseColor, opacity)) < 4.5))) opacity = Math.Max(0, opacity - 0.01);
            var bounds = new Rect(0, 0, width, height); var group = new DrawingGroup();
            using (var context = group.Open())
            {
                context.DrawRectangle(background, null, bounds);
                context.PushClip(new RectangleGeometry(bounds)); context.PushOpacity(opacity);
                var scale = _active.Fit == "Fill" ? Math.Max(width / _image.PixelWidth, height / _image.PixelHeight)
                    : Math.Min(width / _image.PixelWidth, height / _image.PixelHeight);
                var w = _image.PixelWidth * scale; var h = _image.PixelHeight * scale;
                context.DrawImage(_image, new Rect((width - w) * _active.Horizontal, (height - h) * _active.Vertical, w, h));
                context.PushOpacity(_active.Tint);
                context.DrawRectangle((Brush)window.FindResource("SurfaceBrush"), null, bounds);
                context.Pop(); context.Pop(); context.Pop();
            }
            group.Freeze(); var brush = new DrawingBrush(group) { Stretch = Stretch.Fill }; brush.Freeze(); window.Background = brush;
        }
        #endregion END private helpers
    }
}
