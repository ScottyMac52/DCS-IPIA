using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Checks rendered control states and input behavior while replacing the live theme palette.</summary>
    public static class ThemeControlSmoke
    {
        #region BEGIN Public
        /// <summary>Reproduces selected navigation, disabled actions, dropdowns and expander headers in both themes.</summary>
        public static void Verify()
        {
            var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
            var index = Enumerable.Range(0, dictionaries.Count).Single(i =>
                dictionaries[i].Source?.OriginalString.EndsWith("/Light.xaml", StringComparison.Ordinal) == true);
            var original = dictionaries[index];
            var selected = new TabItem { Header = "_Devices", Content = new TextBlock { Text = "Device settings" } };
            var other = new TabItem { Header = "Modifiers", Content = new TextBlock { Text = "Modifier settings" } };
            var tabs = new TabControl { TabStripPlacement = Dock.Left, Height = 130,
                ItemContainerStyle = (Style)System.Windows.Application.Current.FindResource("NavigationItem") };
            tabs.Items.Add(selected); tabs.Items.Add(other);
            var disabled = new Button { Content = "Export module snapshot", IsEnabled = false, Width = 240, Height = 36 };
            var enabled = new Button { Content = "_Save", Width = 240, Height = 36 };
            var scope = new ComboBox { IsEnabled = false, Width = 240, Height = 32 };
            scope.Items.Add("Module"); scope.Items.Add("Module UI Layer"); scope.SelectedIndex = 0;
            var choices = new ComboBox { Width = 240, Height = 32 };
            choices.Items.Add("First controller"); choices.Items.Add("Second controller");
            choices.Items.Add(new ComboBoxItem { Content = "Unavailable controller", IsEnabled = false });
            choices.SelectedIndex = 0;
            var details = new Expander { Header = "_Import details", Content = new TextBlock { Text = "Imported profiles" } };
            var panel = new StackPanel { Margin = new Thickness(20) };
            foreach (var child in new FrameworkElement[] { tabs, scope, choices, enabled, disabled, details })
            { child.Margin = new Thickness(0, 4, 0, 4); panel.Children.Add(child); }
            var window = new Window { Title = "Theme control regression", Width = 680, Height = 460, Content = panel };
            window.SetResourceReference(Window.BackgroundProperty, "WindowBackgroundBrush");
            window.SetResourceReference(Window.ForegroundProperty, "PrimaryTextBrush");
            try
            {
                window.Show(); window.Activate();
                foreach (var theme in new[] { "Dark", "Light", "Dark" })
                {
                    dictionaries[index] = new ResourceDictionary { Source = new Uri(
                        "pack://application:,,,/Dcs.Ipia.App;component/Themes/" + theme + ".xaml", UriKind.Absolute) };
                    tabs.SelectedIndex = 0; choices.SelectedIndex = 0;
                    Pump(window);
                    AssertPixel(selected, 6, 6, "SelectedBackgroundBrush");
                    AssertPixel(disabled, 6, 6, "DisabledBackgroundBrush");
                    AssertPixel(scope, 6, 6, "DisabledBackgroundBrush");
                    AssertPixel(choices, 6, 6, "ControlBackgroundBrush");
                    AssertText(selected, "Devices", "ControlForegroundBrush");
                    AssertText(disabled, "Export module snapshot", "DisabledTextBrush");
                    AssertText(scope, "Module", "DisabledTextBrush");
                    AssertText(details, "Import details", "ControlForegroundBrush");
                    tabs.SelectedIndex = 1; Pump(window);
                    AssertPixel(other, 6, 6, "SelectedBackgroundBrush");
                    AssertPixel(selected, 6, 6, "ControlBackgroundBrush");
                    tabs.SelectedIndex = 0;
                    Keyboard.Focus(choices); Pump(window);
                    if (!choices.IsKeyboardFocusWithin) throw new InvalidOperationException("Dropdown could not receive keyboard focus.");
                    AssertPixel(choices, 0, 12, "AccentTextBrush");
                    Press(choices, Key.Down); Pump(window);
                    if (choices.SelectedIndex != 1) throw new InvalidOperationException("Dropdown keyboard selection stopped working.");
                    Press(choices, Key.Up); Press(choices, Key.F4); Pump(window);
                    if (!choices.IsDropDownOpen) throw new InvalidOperationException("F4 did not open the themed dropdown.");
                    var first = (ComboBoxItem)choices.ItemContainerGenerator.ContainerFromIndex(0);
                    var second = (ComboBoxItem)choices.ItemContainerGenerator.ContainerFromIndex(1);
                    var unavailable = (ComboBoxItem)choices.ItemContainerGenerator.ContainerFromIndex(2);
                    AssertPixel(first, 3, 3, "SelectedBackgroundBrush");
                    AssertPixel(second, 3, 3, "ControlBackgroundBrush");
                    AssertPixel(unavailable, 3, 3, "DisabledBackgroundBrush");
                    AssertText(first, "First controller", "ControlForegroundBrush");
                    AssertText(unavailable, "Unavailable controller", "DisabledTextBrush");
                    SaveImage(window, theme); SaveImage(first, theme + "-dropdown-selected");
                    choices.IsDropDownOpen = false;
                    details.IsExpanded = true; Pump(window);
                    AssertText(details, "Import details", "ControlForegroundBrush");
                    details.IsExpanded = false;
                }
                choices.IsEditable = true; Pump(window);
                var editor = (TextBox)choices.Template.FindName("PART_EditableTextBox", choices);
                editor.Text = "Typed controller"; Pump(window);
                if (choices.Text != "Typed controller") throw new InvalidOperationException("Editable dropdown text binding stopped working.");
                AssertText(enabled, "Save", "ControlForegroundBrush");
            }
            finally { choices.IsDropDownOpen = false; window.Close(); dictionaries[index] = original; }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Pump(Window window)
        {
            window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
        }
        private static void Press(UIElement control, Key key) => control.RaiseEvent(new KeyEventArgs(
            Keyboard.PrimaryDevice, PresentationSource.FromVisual(control)!, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
        private static Color Brush(string key) => ((SolidColorBrush)System.Windows.Application.Current.FindResource(key)).Color;
        private static RenderTargetBitmap Render(FrameworkElement control)
        {
            var image = new RenderTargetBitmap((int)Math.Ceiling(control.ActualWidth), (int)Math.Ceiling(control.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            image.Render(control); return image;
        }
        private static void AssertPixel(FrameworkElement control, int x, int y, string key)
        {
            var pixels = new byte[4]; Render(control).CopyPixels(new Int32Rect(x, y, 1, 1), pixels, 4, 0);
            var actual = Color.FromArgb(pixels[3], pixels[2], pixels[1], pixels[0]);
            if (actual != Brush(key)) throw new InvalidOperationException(control.GetType().Name + " rendered " + actual + " instead of " + key + ".");
        }
        private static void AssertText(DependencyObject control, string text, string key)
        {
            var labels = Children(control).OfType<TextBlock>().Where(x => x.Text.Contains(text, StringComparison.Ordinal)).ToArray();
            if (labels.Length == 0 || labels.Any(x => x.Foreground is not SolidColorBrush brush || brush.Color != Brush(key)))
                throw new InvalidOperationException("Rendered text '" + text + "' did not use " + key + ".");
        }
        private static IEnumerable<DependencyObject> Children(DependencyObject root)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i); yield return child;
                foreach (var descendant in Children(child)) yield return descendant;
            }
        }
        private static void SaveImage(FrameworkElement control, string name)
        {
            var folder = Environment.GetEnvironmentVariable("IPIA_MAPPING_PREVIEW");
            if (string.IsNullOrWhiteSpace(folder)) return;
            Directory.CreateDirectory(folder);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(Render(control)));
            using var file = File.Create(Path.Combine(folder, "theme-" + name + ".png")); encoder.Save(file);
        }
        #endregion END private helpers
    }
}
