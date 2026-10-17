using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dcs.Ipia.Application;
using Dcs.Ipia.Infrastructure;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Exercises real appearance previews, rollback, image portability, palette imports and failed saves.</summary>
    public static class AppearanceEditorSmoke
    {
        #region BEGIN Public
        /// <summary>Checks Apply/Cancel and every skin with a synthetic image rather than redistributing user photographs.</summary>
        public static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-appearance-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var settings = new FileAppSettingsService(Path.Combine(root, "defaults.json"), Path.Combine(root, "appsettings.json"), Path.Combine(root, "theme.txt"));
            var store = new FileAppearanceStore(root);
            var chooser = new SettingsWindow(settings);
            MainWindow? main = null;
            var source = Path.Combine(root, "source.png");
            var original = AppearanceManager.Store;
            try
            {
                AppearanceManager.Configure(store); chooser.Show();
                var connection = "Data Source=" + Path.Combine(root, "preview.db") + ";Pooling=False";
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                main = new MainWindow(new global::Dcs.Ipia.Dcs.ModuleWorkspaceLoader(),
                    new global::Dcs.Ipia.Dcs.DcsFolderImporter(), settings,
                    new SqliteEditorWorkspaceRepository(connection), deviceDiscovery: new EmptyDeviceDiscovery());
                main.Show();
                var theme = (ComboBox)chooser.FindName("ThemeChoice")!;
                foreach (var skin in new[] { 0, 1, 2, 3 })
                {
                    theme.SelectedIndex = skin; MakeImage(source);
                    var editor = new AppearanceWindow(store) { Owner = chooser }; editor.Show();
                    editor.LoadImage(source); File.Delete(source);
                    ((Slider)editor.FindName("ImageOpacity")!).Value = 0.2;
                    ((Slider)editor.FindName("ImageTint")!).Value = 0.1;
                    ((Slider)editor.FindName("CropX")!).Value = 0.8;
                    ((Slider)editor.FindName("CropY")!).Value = 0.2;
                    ((Slider)editor.FindName("PanelOpacity")!).Value = 0.35;
                    ((ComboBox)editor.FindName("ImageFit")!).SelectedIndex = skin % 2;
                    ((TextBox)editor.FindName("AccentColor")!).Text = skin == 0 ? "#17263D" : "#F4F7FB";
                    ThemeControlSmoke.Pump(editor);
                    if (!AppearanceManager.Active.Enabled || AppearanceManager.Image is null || chooser.Background is not DrawingBrush)
                        throw new InvalidOperationException("Appearance preview did not reach another open window.");
                    VerifySurfaces(main, skin);
                    var previous = store.Current;
                    ((TextBox)editor.FindName("AccentColor")!).Text = "not-a-color";
                    ((Button)editor.FindName("ApplyAppearance")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    if (!editor.IsVisible || store.Current != previous) throw new InvalidOperationException("Invalid accent was committed.");
                    ((TextBox)editor.FindName("AccentColor")!).Text = skin == 0 ? "#17263D" : "#F4F7FB";
                    ThemeControlSmoke.SaveImage(editor, "appearance-editor-" + skin);
                    ((Button)editor.FindName("CancelAppearance")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    if (AppearanceManager.Active != store.Current) throw new InvalidOperationException("Cancel did not restore the accepted appearance.");
                    MakeImage(source); editor = new AppearanceWindow(store) { Owner = chooser }; editor.Show(); editor.LoadImage(source);
                    File.Delete(source);
                    ((Slider)editor.FindName("ImageOpacity")!).Value = 0.2;
                    ((Button)editor.FindName("ApplyAppearance")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    if (editor.IsVisible || !store.Current.Enabled || store.Current.Image is null || store.ReadImage() is null)
                        throw new InvalidOperationException("Apply did not commit a portable managed image.");
                    var reopened = new FileAppearanceStore(root);
                    if (reopened.Current != store.Current || reopened.ReadImage() is null) throw new InvalidOperationException("Appearance did not survive restart.");
                    editor = new AppearanceWindow(store); editor.Show();
                    ((Slider)editor.FindName("ImageOpacity")!).Value = 0.1; editor.Close();
                    if (AppearanceManager.Active != store.Current) throw new InvalidOperationException("Window close did not cancel preview.");
                    ThemeControlSmoke.SaveImage(chooser, "appearance-applied-" + skin);
                    if (!store.TrySave(new AppearanceSettings())) throw new InvalidOperationException(store.LastError);
                    AppearanceManager.Restore();
                }
                if (!store.TrySave(new AppearanceSettings(Accent: "#F4F7FB"))) throw new InvalidOperationException(store.LastError);
                AppearanceManager.Restore();
                theme.SelectedIndex = 0;
                if (AppearanceManager.Active.Accent != "#F4F7FB" || AppearanceManager.LastError is null)
                    throw new InvalidOperationException("An incompatible skin discarded the accepted accent instead of using a temporary fallback.");
                theme.SelectedIndex = 1;
                if (((SolidColorBrush)System.Windows.Application.Current.FindResource("AccentTextBrush")).Color != Color.FromRgb(244, 247, 251))
                    throw new InvalidOperationException("The saved accent was not reapplied on a compatible skin.");
                if (!store.TrySave(new AppearanceSettings())) throw new InvalidOperationException(store.LastError);
                AppearanceManager.Restore(); theme.SelectedIndex = 3;
                var colors = AppearanceValidation.ColorKeys.ToDictionary(key => key, key =>
                    "#" + ((SolidColorBrush)System.Windows.Application.Current.FindResource(key)).Color.ToString(System.Globalization.CultureInfo.InvariantCulture)[3..]);
                var palettePath = Path.Combine(root, "palette.json");
                File.WriteAllText(palettePath, JsonSerializer.Serialize(new { SchemaVersion = 1, Colors = colors }));
                var paletteEditor = new AppearanceWindow(store); paletteEditor.Show(); paletteEditor.ImportPalette(palettePath);
                ((Button)paletteEditor.FindName("ApplyAppearance")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (new FileAppearanceStore(root).Current.Palette?.Count != 19) throw new InvalidOperationException("Imported palette was not saved by value.");
                if (!store.TrySave(new AppearanceSettings())) throw new InvalidOperationException(store.LastError);
                AppearanceManager.Restore();
                var failureEditor = new AppearanceWindow(store); failureEditor.Show();
                ((TextBox)failureEditor.FindName("AccentColor")!).Text = "#F4F7FB";
                var path = Path.Combine(root, "appearance", "settings.json"); File.Delete(path); Directory.CreateDirectory(path);
                ((Button)failureEditor.FindName("ApplyAppearance")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (!failureEditor.IsVisible || AppearanceManager.Active != store.Current ||
                    !((TextBlock)failureEditor.FindName("AppearanceStatus")!).Text.Contains("not saved", StringComparison.Ordinal))
                    throw new InvalidOperationException("A failed save did not restore accepted appearance and report failure.");
                failureEditor.Close(); Directory.Delete(path);
                AssertBadImage(root);
            }
            finally
            {
                foreach (var window in System.Windows.Application.Current.Windows.OfType<AppearanceWindow>().ToArray()) window.Close();
                ((ComboBox)chooser.FindName("ThemeChoice")!).SelectedIndex = 0;
                main?.Close(); chooser.Close(); AppearanceManager.Configure(original); Directory.Delete(root, true);
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void VerifySurfaces(MainWindow main, int skin)
        {
            ThemeControlSmoke.Pump(main);
            var surfaces = ThemeControlSmoke.Children(main).OfType<Border>().Where(border =>
                border.Background is SolidColorBrush brush && (ReferenceEquals(brush, main.FindResource("SurfaceBrush")) ||
                    ReferenceEquals(brush, main.FindResource("StatusBackgroundBrush")))).ToArray();
            if (surfaces.Length < 2 || surfaces.Any(border => ((SolidColorBrush)border.Background).Color.A >= 255) || main.Background is not DrawingBrush)
                throw new InvalidOperationException("Main-window cards or status surfaces hide the continuous photograph.");
            ThemeControlSmoke.SaveImage(main, "appearance-main-" + skin);
            AppearanceManager.Preview(AppearanceManager.Active with { PanelOpacity = 1 }, AppearanceManager.Image);
            if (surfaces.Any(border => ((SolidColorBrush)border.Background).Color.A != 255))
                throw new InvalidOperationException("Opaque panel preference was not applied.");
            AppearanceManager.Preview(AppearanceManager.Active with { PanelOpacity = 0.35 }, AppearanceManager.Image);
        }
        private static void MakeImage(string path)
        {
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.DrawRectangle(new LinearGradientBrush(Colors.Navy, Colors.SteelBlue, 35), null, new Rect(0, 0, 640, 360));
                drawing.DrawEllipse(Brushes.Gold, null, new Point(460, 140), 85, 85);
                drawing.DrawRectangle(Brushes.DarkSlateGray, null, new Rect(0, 270, 640, 90));
            }
            var bitmap = new RenderTargetBitmap(640, 360, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
            File.WriteAllBytes(path, BackgroundImageLoader.Encode(bitmap));
        }
        private static void AssertBadImage(string root)
        {
            foreach (var name in new[] { "invalid.png", "invalid.gif" })
            {
                var path = Path.Combine(root, name); File.WriteAllText(path, "not an image");
                try { BackgroundImageLoader.LoadFile(path); throw new InvalidOperationException("Invalid image was accepted."); }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or FileFormatException) { }
            }
        }
        #endregion END private helpers
    }
}
