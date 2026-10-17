using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dcs.Ipia.Application;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Exercises the actual themed picker with temporary files, keyboard navigation, modal results and safe save confirmation.</summary>
    public static class BrowseDialogSmoke
    {
        #region BEGIN Public
        /// <summary>Checks file/folder selection, filtering, multiple imports, cancellation and live skin/background propagation.</summary>
        public static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-browse-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "child"));
            File.WriteAllText(Path.Combine(root, "first.json"), "{}");
            File.WriteAllText(Path.Combine(root, "second.JSON"), "{}");
            File.WriteAllText(Path.Combine(root, "notes.md"), "Notes");
            var original = AppearanceManager.Store;
            try
            {
                var open = new ThemeBrowseDialog { InitialDirectory = root, Filter = "JSON|*.json|Markdown|*.md", Multiselect = true };
                if (Run(open, async window =>
                {
                    var grid = (DataGrid)window.FindName("Entries");
                    if (grid.Items.Count != 3) throw new InvalidOperationException("JSON filter hid folders or included unrelated files.");
                    var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgr32, null, new byte[] { 80, 100, 120, 0 }, 4);
                    bitmap.Freeze();
                    foreach (var skin in Enum.GetValues<AppearanceTheme>())
                    {
                        ThemeManager.Apply(skin.ToString());
                        AppearanceManager.Preview(new AppearanceSettings { Enabled = true, Opacity = 0.2, ControlOpacity = 0.65 }, bitmap);
                        ThemeControlSmoke.Pump(window);
                        if (window.Background is not DrawingBrush) throw new InvalidOperationException("Browse background did not follow appearance.");
                        ThemeControlSmoke.SaveImage(window, "browse-" + skin);
                    }
                    ((ComboBox)window.FindName("FileTypes")).SelectedIndex = 1;
                    await Ready(window);
                    if (grid.Items.Count != 2) throw new InvalidOperationException("Changing file type did not refresh the list.");
                    ((ComboBox)window.FindName("FileTypes")).SelectedIndex = 0;
                    await Ready(window);
                    foreach (var entry in grid.Items.Cast<BrowseEntry>().Where(x => !x.IsDirectory).ToArray()) grid.SelectedItems.Add(entry);
                    Click(window, "AcceptSelection");
                }) != true || open.FileNames.Length != 2 || open.FileNames.Any(x => !File.Exists(x)))
                    throw new InvalidOperationException("Multiple-file browse did not return existing absolute paths.");

                var folder = new ThemeBrowseDialog { SelectFolder = true, InitialDirectory = root };
                if (Run(folder, async window =>
                {
                    ((TextBox)window.FindName("Location")).Text = Path.Combine(root, "child");
                    ThemeControlSmoke.Press((TextBox)window.FindName("Location"), System.Windows.Input.Key.Enter);
                    await Ready(window);
                    Click(window, "AcceptSelection");
                }) != true || folder.FolderName != Path.Combine(root, "child"))
                    throw new InvalidOperationException("Folder keyboard navigation/selection failed.");

                var save = new ThemeBrowseDialog { Save = true, InitialDirectory = root, Filter = "Snapshot|*.ipia-module.json", FileName = "new" };
                if (Run(save, window => { Click(window, "AcceptSelection"); return Task.CompletedTask; }) != true ||
                    save.FileName != Path.Combine(root, "new.ipia-module.json") || File.Exists(save.FileName))
                    throw new InvalidOperationException("Save did not append the compound extension or wrote a file prematurely.");

                var cancel = new ThemeBrowseDialog { InitialDirectory = root, FileName = "original.json", Filter = "JSON|*.json" };
                if (Run(cancel, async window =>
                {
                    ((TextBox)window.FindName("SelectionName")).Text = "missing.json";
                    Click(window, "AcceptSelection");
                    if (!window.IsVisible || cancel.FileNames.Length != 0) throw new InvalidOperationException("Missing file was accepted.");
                    ((TextBox)window.FindName("Location")).Text = Path.Combine(root, "missing-folder");
                    ThemeControlSmoke.Press((TextBox)window.FindName("Location"), System.Windows.Input.Key.Enter);
                    await Ready(window);
                    if (!((TextBlock)window.FindName("Status")).Text.StartsWith("Cannot open folder", StringComparison.Ordinal))
                        throw new InvalidOperationException("Unreadable folder did not report an inline error.");
                    Click(window, "CancelSelection");
                }) == true || cancel.FileName != "original.json" || cancel.FileNames.Length != 0)
                    throw new InvalidOperationException("Cancelled picker modified its result.");

                var overwrite = new ThemeBrowseDialog { Save = true, InitialDirectory = root, FileName = "first.json", Filter = "JSON|*.json" };
                Run(overwrite, window =>
                {
                    window.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var confirmation = System.Windows.Application.Current.Windows.OfType<Window>().Single(x => x.Name == "ThemeMessageDialog");
                        var no = ThemeControlSmoke.Children(confirmation).OfType<Button>().Single(x => Equals(x.Tag, MessageBoxResult.No));
                        no.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }));
                    Click(window, "AcceptSelection");
                    if (!window.IsVisible || overwrite.FileNames.Length != 0 || File.ReadAllText(Path.Combine(root, "first.json")) != "{}")
                        throw new InvalidOperationException("Declining overwrite accepted or changed an existing file.");
                    return Task.CompletedTask;
                });
            }
            finally
            {
                AppearanceManager.Configure(original); ThemeManager.Apply("Light");
                Directory.Delete(root, true);
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Click(Window window, string name) => ((Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        private static async Task Ready(Window window)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (((TextBlock)window.FindName("Status")).Text == "Reading folder…")
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Browse navigation did not complete.");
                await Task.Delay(20);
            }
            ThemeControlSmoke.Pump(window);
        }
        private static bool? Run(ThemeBrowseDialog options, Func<BrowseWindow, Task> action)
        {
            Exception? failure = null;
            var window = new BrowseWindow(options);
            window.ContentRendered += async (_, _) =>
            {
                try { await Ready(window); await action(window); }
                catch (Exception ex) { failure = ex; }
                finally { if (window.IsVisible) window.DialogResult = false; }
            };
            var result = window.ShowDialog();
            if (failure is not null) throw new InvalidOperationException("Themed browse smoke failed.", failure);
            return result;
        }
        #endregion END private helpers
    }
}
