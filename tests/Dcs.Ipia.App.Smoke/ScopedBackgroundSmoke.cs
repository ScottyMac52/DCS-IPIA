using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dcs.Ipia.Application;
using Dcs.Ipia.Infrastructure;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Exercises the real target selector, per-window and per-view backgrounds, restart and rollback.</summary>
    public static class ScopedBackgroundSmoke
    {
        #region BEGIN Public
        /// <summary>Uses two synthetic images to verify inheritance and transactional multi-image editing.</summary>
        public static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-scoped-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var original = AppearanceManager.Store;
            var store = new FileAppearanceStore(root);
            var host = new AppearanceDialog("ModuleWorkspaceWindow") { Width = 700, Height = 500 };
            var view = new UserControl { Width = 400, Height = 300 };
            AppearanceManager.SetTarget(view, "AssignmentsView"); host.Content = view;
            var other = new AppearanceDialog("MainWindow") { Width = 300, Height = 220 };
            try
            {
                AppearanceManager.Configure(store); host.Show(); other.Show();
                var first = Path.Combine(root, "first.png"); var second = Path.Combine(root, "second.png");
                MakeImage(first, Colors.Navy); MakeImage(second, Colors.DarkRed);
                var editor = new AppearanceWindow(store); editor.Show(); editor.LoadImage(first);
                var target = (ComboBox)editor.FindName("TargetChoice")!;
                target.SelectedValue = "ModuleWorkspaceWindow"; editor.LoadImage(second);
                ((Slider)editor.FindName("ArtworkOpacity")!).Value = 0.4;
                ThemeControlSmoke.Pump(host);
                if (host.Background is not DrawingBrush || other.Background is not DrawingBrush ||
                    view.Background != Brushes.Transparent || AppearanceManager.ArtworkOpacityFor(view) != 0.4 ||
                    AppearanceManager.SettingsFor(host).Image == AppearanceManager.SettingsFor(other).Image)
                    throw new InvalidOperationException("Window override did not coexist with the default or inherit into its view.");
                var inheritedPixel = ThemeControlSmoke.ReadPixel(view, 2, 2);
                target.SelectedValue = "AssignmentsView";
                ((ComboBox)editor.FindName("TargetMode")!).SelectedIndex = 2;
                ThemeControlSmoke.Pump(host);
                if (view.Background is not SolidColorBrush || AppearanceManager.HasBackground(view) || !AppearanceManager.HasBackground(host))
                    throw new InvalidOperationException("A view's No image override failed to stop window inheritance.");
                ThemeControlSmoke.AssertPixel(view, 2, 2, "WindowBackgroundBrush");
                ((ComboBox)editor.FindName("TargetMode")!).SelectedIndex = 0;
                ThemeControlSmoke.Pump(host);
                if (ThemeControlSmoke.ReadPixel(view, 2, 2) != inheritedPixel)
                    throw new InvalidOperationException("Restored inheritance did not restore the actual rendered background pixel.");
                if (AppearanceManager.SettingsFor(view).Image != AppearanceManager.SettingsFor(host).Image)
                    throw new InvalidOperationException("Use inherited background did not restore the parent selection.");
                target.SelectedValue = "MainWindow"; editor.LoadImage(second);
                ((Slider)editor.FindName("CropX")!).Value = 0.8;
                ((Button)editor.FindName("ApplyAppearance")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (editor.IsVisible || store.Current.Backgrounds?.Count != 2 || Directory.GetFiles(Path.Combine(root, "appearance"), "*.png").Length != 2)
                    throw new InvalidOperationException("Apply lost one of the independently selected images.");
                File.Delete(first); File.Delete(second);
                var reopened = new FileAppearanceStore(root); AppearanceManager.Configure(reopened);
                if (reopened.ReadImage() is null || AppearanceManager.SettingsFor(host).ArtworkOpacity != 0.4 || AppearanceManager.SettingsFor(other).Horizontal != 0.8)
                    throw new InvalidOperationException("Scoped images and adjustments did not survive restart without their source files.");
                editor = new AppearanceWindow(reopened); editor.Show();
                target = (ComboBox)editor.FindName("TargetChoice")!; target.SelectedValue = "ModuleWorkspaceWindow";
                ((Slider)editor.FindName("ArtworkOpacity")!).Value = 0.1;
                ((ComboBox)editor.FindName("TargetMode")!).SelectedIndex = 2;
                editor.Close();
                if (!AppearanceManager.HasBackground(host) || AppearanceManager.ArtworkOpacityFor(view) != 0.4)
                    throw new InvalidOperationException("Cancel failed to restore scoped backgrounds and artwork opacity.");
                var newlyOpened = new AppearanceDialog("MainWindow") { Width = 300, Height = 220 };
                newlyOpened.Show(); ThemeControlSmoke.Pump(newlyOpened);
                if (newlyOpened.Background is not DrawingBrush || AppearanceManager.SettingsFor(newlyOpened).Horizontal != 0.8)
                    throw new InvalidOperationException("New dialogs did not receive accepted target overrides.");
                ThemeControlSmoke.SaveImage(host, "scoped-background-inherited-view"); newlyOpened.Close();
            }
            finally
            {
                foreach (var editor in System.Windows.Application.Current.Windows.OfType<AppearanceWindow>().ToArray()) editor.Close();
                host.Close(); other.Close(); AppearanceManager.Configure(original); Directory.Delete(root, true);
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void MakeImage(string path, Color color)
        {
            var pixels = new byte[] { color.B, color.G, color.R, 255 };
            var image = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 4);
            File.WriteAllBytes(path, BackgroundImageLoader.Encode(image));
        }
        #endregion END private helpers
    }
}
