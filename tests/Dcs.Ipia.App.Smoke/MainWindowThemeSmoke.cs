using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Dcs.Ipia.Application;
using Dcs.Ipia.Infrastructure;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Checks the actual main window, native caption, menu popups and keyboard commands across all skins.</summary>
    public static class MainWindowThemeSmoke
    {
        #region BEGIN Public
        /// <summary>Switches palettes with menus already open and verifies rendered surfaces and native behavior.</summary>
        public static void Verify()
        {
            var folder = Path.Combine(Path.GetTempPath(), "ipia-menu-theme-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
            var paletteIndex = Enumerable.Range(0, dictionaries.Count).Single(i =>
                Enum.GetNames<AppearanceTheme>().Any(name => dictionaries[i].Source?.OriginalString.EndsWith(
                    "/" + name + ".xaml", StringComparison.Ordinal) == true));
            var original = dictionaries[paletteIndex];
            MainWindow? window = null;
            try
            {
                var connection = "Data Source=" + Path.Combine(folder, "ipia.db") + ";Pooling=False";
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var settings = new SqliteAppSettingsService(connection, Path.Combine(folder, "defaults.json"),
                    Path.Combine(folder, "user.json"), Path.Combine(folder, "theme.txt"));
                window = new MainWindow(new global::Dcs.Ipia.Dcs.ModuleWorkspaceLoader(),
                    new global::Dcs.Ipia.Dcs.DcsFolderImporter(), settings,
                    new SqliteEditorWorkspaceRepository(connection), deviceDiscovery: new EmptyDeviceDiscovery());
                window.Show(); window.Activate(); ThemeControlSmoke.Pump(window);
                var menu = (Menu)window.FindName("MainMenu")!;
                // ContextMenu's owning Popup resolves this through its placement target.
                // Capture the final colours, independent of the desktop's menu fade/slide setting.
                menu.Resources[SystemParameters.MenuPopupAnimationKey] = PopupAnimation.None;
                var file = (MenuItem)menu.Items[0];
                var disabled = (MenuItem)file.Items[0];
                var calls = 0;
                window.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, (_, _) => calls++,
                    (_, args) => args.CanExecute = true));
                var action = new MenuItem { Header = "_Theme smoke action", Command = ApplicationCommands.Copy,
                    CommandTarget = window, InputGestureText = "Ctrl+C" };
                var nested = new MenuItem { Header = "_Nested actions" };
                nested.Items.Add(new MenuItem { Header = "Checked item", IsCheckable = true, IsChecked = true });
                file.Items.Add(action); file.Items.Add(nested);
                var context = new ContextMenu { PlacementTarget = menu };
                var contextAction = new MenuItem { Header = "Context action", Command = ApplicationCommands.Copy,
                    CommandTarget = window };
                context.Items.Add(contextAction);
                window.ContextMenu = context;
                var picker = (ComboBox)window.FindName("ThemePicker")!;
                foreach (var skin in Enum.GetValues<AppearanceTheme>())
                {
                    // Keep the existing popup alive while replacing the palette.
                    file.IsSubmenuOpen = true;
                    picker.SelectedIndex = (int)skin;
                    ThemeControlSmoke.Pump(window);
                    ThemeControlSmoke.AssertPixel(window, 5, (int)menu.ActualHeight + 8, "WindowBackgroundBrush");
                    ThemeControlSmoke.AssertPixel(menu, (int)menu.ActualWidth - 10, 5, "SurfaceBrush");
                    ThemeControlSmoke.AssertPixel(file, 3, 3, "SelectedBackgroundBrush");
                    ThemeControlSmoke.AssertPixel(action, 3, 3, action.IsHighlighted ? "SelectedBackgroundBrush" : "ControlBackgroundBrush");
                    ThemeControlSmoke.AssertText(action, "Theme smoke action", "ControlForegroundBrush");
                    ThemeControlSmoke.AssertText(disabled, "New module", "DisabledTextBrush");
                    if (disabled.IsEnabled) throw new InvalidOperationException("Disabled main-menu command was enabled by styling.");
                    VerifyCaption(window);
                    ThemeControlSmoke.SaveImage(window, "main-" + skin);
                    ThemeControlSmoke.SaveImage(action, "menu-" + skin);
                    nested.IsSubmenuOpen = true; ThemeControlSmoke.Pump(window);
                    var check = (MenuItem)nested.Items[0];
                    if (((Popup)nested.Template.FindName("PART_Popup", nested)).Placement != PlacementMode.Right ||
                        ((TextBlock)check.Template.FindName("CheckMark", check)).Visibility != Visibility.Visible)
                        throw new InvalidOperationException("Nested menu direction or checked state was lost.");
                    ThemeControlSmoke.AssertText(check, "Checked item", "ControlForegroundBrush");
                    nested.IsSubmenuOpen = false; file.IsSubmenuOpen = false;
                    Keyboard.Focus(file); ThemeControlSmoke.Press(file, Key.Down); ThemeControlSmoke.Pump(window);
                    if (!file.IsSubmenuOpen) throw new InvalidOperationException("Down did not open the main menu.");
                    Keyboard.Focus(action); ThemeControlSmoke.Pump(window);
                    ThemeControlSmoke.AssertPixel(action, 0, 8, "AccentTextBrush");
                    ThemeControlSmoke.Press(action, Key.Enter); ThemeControlSmoke.Pump(window);
                    if (calls != (int)skin * 2 + 1 || file.IsSubmenuOpen)
                        throw new InvalidOperationException("Enter did not invoke the routed menu command and close the popup.");
                    context.IsOpen = true; ThemeControlSmoke.Pump(window);
                    if (LogicalTreeHelper.GetParent(context) is not Popup { PopupAnimation: PopupAnimation.None })
                        throw new InvalidOperationException("Context-menu pixel checks require an animation-free popup.");
                    ThemeControlSmoke.AssertPixel(contextAction, 3, 3, contextAction.IsHighlighted ? "SelectedBackgroundBrush" : "ControlBackgroundBrush");
                    ThemeControlSmoke.AssertText(contextAction, "Context action", "ControlForegroundBrush");
                    Keyboard.Focus(contextAction); ThemeControlSmoke.Press(contextAction, Key.Enter);
                    ThemeControlSmoke.Pump(window);
                    if (calls != (int)skin * 2 + 2 || context.IsOpen)
                        throw new InvalidOperationException("The themed context command did not execute and close.");
                }
            }
            finally
            {
                if (window?.ContextMenu is { } context) context.IsOpen = false;
                window?.Close(); dictionaries[paletteIndex] = original;
                Directory.Delete(folder, true);
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void VerifyCaption(Window window)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) || SystemParameters.HighContrast) return;
            foreach (var (attribute, key) in new[] { (35, "WindowBackgroundBrush"), (36, "PrimaryTextBrush"), (34, "ControlBorderBrush") })
            {
                var result = DwmGetWindowAttribute(new WindowInteropHelper(window).Handle, attribute, out var actual, sizeof(int));
                if (result == unchecked((int)0x80070057) || result == unchecked((int)0x80263001))
                {
                    Console.WriteLine("Native caption inspection unsupported on this desktop; rendered UI checks remain enforced.");
                    return;
                }
                if (result != 0) throw new InvalidOperationException("Unable to inspect Windows 11 caption attribute " + attribute);
                var color = ((SolidColorBrush)window.FindResource(key)).Color;
                if (actual != (color.R | color.G << 8 | color.B << 16))
                    throw new InvalidOperationException("Native caption did not follow the selected skin: " + key);
            }
        }
        [DllImport("dwmapi.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
        #endregion END private helpers
    }
}
