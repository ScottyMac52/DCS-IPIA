using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
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
        /// <summary>Reproduces selected navigation, disabled actions, dropdowns and expander headers in all four skins, including legacy explicit styles.</summary>
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
            var legacyText = new TextBox { Text = "Legacy input", Width = 240, Height = 30,
                Style = (Style)System.Windows.Application.Current.FindResource("AppTextBox") };
            var legacyChoice = new ComboBox { Width = 240, Height = 30,
                Style = (Style)System.Windows.Application.Current.FindResource("AppComboBox") };
            legacyChoice.Items.Add("Legacy selection"); legacyChoice.SelectedIndex = 0;
            var legacyLabel = new TextBlock { Text = "Legacy label",
                Style = (Style)System.Windows.Application.Current.FindResource("AppLabel") };
            var legacyCheck = new CheckBox { Content = "Legacy checkbox", IsChecked = true,
                Style = (Style)System.Windows.Application.Current.FindResource("AppCheckBox") };
            // A tall neighboring footer must not stretch ordinary dialog actions.
            var footer = new DockPanel { Height = 90 };
            var compact = new Button { Content = "Settings", MinWidth = 80 };
            var compactAction = new Button { Content = "Close", MinWidth = 80,
                Style = (Style)System.Windows.Application.Current.FindResource("ActionButton") };
            DockPanel.SetDock(compact, Dock.Right); DockPanel.SetDock(compactAction, Dock.Right);
            footer.Children.Add(compact); footer.Children.Add(compactAction);
            footer.Children.Add(new TextBlock { Text = "Package actions and status", Height = 90 });
            var panel = new StackPanel { Margin = new Thickness(20) };
            foreach (var child in new FrameworkElement[] { tabs, scope, choices, enabled, disabled, details, legacyText, legacyChoice, legacyLabel, legacyCheck, footer })
            { child.Margin = new Thickness(0, 4, 0, 4); panel.Children.Add(child); }
            var window = new Window { Title = "Theme control regression", Width = 680, Height = 780, Content = panel };
            window.SetResourceReference(Window.BackgroundProperty, "WindowBackgroundBrush");
            window.SetResourceReference(Window.ForegroundProperty, "PrimaryTextBrush");
            try
            {
                window.Show(); window.Activate();
                foreach (var theme in new[] { "Dark", "NavyAmber", "CockpitGreen", "Light", "Dark" })
                {
                    dictionaries[index] = new ResourceDictionary { Source = new Uri(
                        "pack://application:,,,/Heimdall;component/Themes/" + theme + ".xaml", UriKind.Absolute) };
                    tabs.SelectedIndex = 0; choices.SelectedIndex = 0;
                    Pump(window);
                    foreach (var button in new[] { compact, compactAction })
                    {
                        if (button.ActualHeight < 28 || button.ActualHeight > 36 ||
                            button.TranslatePoint(new Point(), footer).Y < 20)
                            throw new InvalidOperationException("Dialog buttons stretched to fill a tall footer.");
                        var fontSize = button.FontSize;
                        var normalHeight = button.ActualHeight;
                        button.FontSize = 30; Pump(window);
                        if (button.ActualHeight <= normalHeight || button.ActualHeight < 40)
                            throw new InvalidOperationException("Compact buttons clipped enlarged text.");
                        button.FontSize = fontSize; Pump(window);
                    }
                    SaveImage(window, theme);
                    AssertPixel(selected, 6, 6, "SelectedBackgroundBrush");
                    AssertPixel(tabs, 220, 90, "SurfaceBrush");
                    AssertPixel(disabled, 6, 6, "DisabledBackgroundBrush");
                    AssertPixel(scope, 6, 6, "DisabledBackgroundBrush");
                    AssertPixel(choices, 6, 6, "ControlBackgroundBrush");
                    AssertPixel(legacyText, 6, 6, "ControlBackgroundBrush");
                    AssertPixel(legacyChoice, 6, 6, "ControlBackgroundBrush");
                    if (legacyText.Foreground is not SolidColorBrush foreground || foreground.Color != Brush("PrimaryTextBrush") ||
                        legacyLabel.Foreground is not SolidColorBrush label || label.Color != Brush("PrimaryTextBrush") ||
                        legacyCheck.Foreground is not SolidColorBrush check || check.Color != Brush("PrimaryTextBrush"))
                        throw new InvalidOperationException("Legacy explicit styles retained a previous palette.");
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
                    Press(choices, Key.Up);
                    ((Popup)choices.Template.FindName("PART_Popup", choices)).PopupAnimation = PopupAnimation.None;
                    Press(choices, Key.F4); Pump(window);
                    if (!choices.IsDropDownOpen) throw new InvalidOperationException("F4 did not open the themed dropdown.");
                    var first = (ComboBoxItem)choices.ItemContainerGenerator.ContainerFromIndex(0);
                    var second = (ComboBoxItem)choices.ItemContainerGenerator.ContainerFromIndex(1);
                    var unavailable = (ComboBoxItem)choices.ItemContainerGenerator.ContainerFromIndex(2);
                    AssertPixel(first, 3, 3, "SelectedBackgroundBrush");
                    AssertPixel(second, 3, 3, second.IsHighlighted ? "SelectedBackgroundBrush" : "ControlBackgroundBrush");
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
        internal static void Pump(Window window)
        {
            window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
        }
        internal static void Press(UIElement control, Key key) => control.RaiseEvent(new KeyEventArgs(
            Keyboard.PrimaryDevice, PresentationSource.FromVisual(control)!, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
        private static Color Brush(string key) => ((SolidColorBrush)System.Windows.Application.Current.FindResource(key)).Color;
        private static CroppedBitmap Render(FrameworkElement control)
        {
            var source = PresentationSource.FromVisual(control) ?? throw new InvalidOperationException("The control has no presentation surface.");
            var root = (FrameworkElement)source.RootVisual;
            var image = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            image.Render(root);
            var bounds = control.TransformToAncestor(root).TransformBounds(new Rect(0, 0, control.ActualWidth, control.ActualHeight));
            var rectangle = new Int32Rect((int)Math.Round(bounds.X), (int)Math.Round(bounds.Y),
                (int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height));
            if (rectangle.X < 0 || rectangle.Y < 0 || rectangle.X + rectangle.Width > image.PixelWidth || rectangle.Y + rectangle.Height > image.PixelHeight)
                throw new InvalidOperationException("Control capture is outside its surface: " + rectangle + " within " + image.PixelWidth + "x" + image.PixelHeight + ".");
            return new CroppedBitmap(image, rectangle);
        }
        internal static void AssertPixel(FrameworkElement control, int x, int y, string key)
        {
            var actual = ReadPixel(control, x, y);
            if (actual != Brush(key)) throw new InvalidOperationException(control.GetType().Name + " rendered " + actual + " instead of " + key + ".");
        }
        internal static Color ReadPixel(FrameworkElement control, int x, int y)
        {
            var pixels = new byte[4]; Render(control).CopyPixels(new Int32Rect(x, y, 1, 1), pixels, 4, 0);
            return Color.FromArgb(pixels[3], pixels[2], pixels[1], pixels[0]);
        }
        internal static void AssertText(DependencyObject control, string text, string key)
        {
            var labels = Children(control).OfType<TextBlock>().Where(x => new TextRange(x.ContentStart, x.ContentEnd).Text.Contains(text, StringComparison.Ordinal)).ToArray();
            if (labels.Length == 0 || labels.Any(x => x.Foreground is not SolidColorBrush brush || brush.Color != Brush(key)))
                throw new InvalidOperationException("Rendered text '" + text + "' did not use " + key + ".");
        }
        internal static IEnumerable<DependencyObject> Children(DependencyObject root)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i); yield return child;
                foreach (var descendant in Children(child)) yield return descendant;
            }
        }
        internal static void SaveImage(FrameworkElement control, string name)
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
