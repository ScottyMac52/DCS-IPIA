using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Infrastructure;
using DcsIpia.Features.Commands;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Checks shared collections, editors, code-created dialogs and modal confirmations in every palette.</summary>
    public static class ApplicationThemeSmoke
    {
        #region BEGIN Public
        /// <summary>Switches skins with representative real windows open and verifies rendered controls and editing.</summary>
        public static void Verify()
        {
            var folder = Path.Combine(Path.GetTempPath(), "ipia-all-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var palettes = System.Windows.Application.Current.Resources.MergedDictionaries;
            var paletteIndex = Enumerable.Range(0, palettes.Count).Single(i => palettes[i].Source?.OriginalString.EndsWith("/Light.xaml", StringComparison.Ordinal) == true);
            var original = palettes[paletteIndex];
            var table = new DataTable(); table.Columns.Add("Name", typeof(string)); table.Columns.Add("Enabled", typeof(bool));
            table.Rows.Add("First", true); table.Rows.Add("Second", false);
            var grid = new DataGrid { ItemsSource = table.DefaultView, AutoGenerateColumns = true, CanUserAddRows = false, Height = 110 };
            var list = new ListBox { Height = 90, ItemsSource = new[] { "First device", "Second device" }, SelectedIndex = 0 };
            var child = new TreeViewItem { Header = "Child control", IsSelected = true };
            var root = new TreeViewItem { Header = "Controller", IsExpanded = true }; root.Items.Add(child);
            var tree = new TreeView { Height = 90 }; tree.Items.Add(root);
            var slider = new Slider { Minimum = 0, Maximum = 10, Value = 4, Height = 30 };
            var scroll = new ScrollViewer { Height = 70, VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                Content = new TextBlock { Text = string.Join("\n", Enumerable.Repeat("Scrollable help", 30)) } };
            var panel = new StackPanel { Margin = new Thickness(20) };
            foreach (var element in new UIElement[] { grid, list, tree, slider, scroll }) panel.Children.Add(element);
            var fixture = new Window { Title = "Shared UI theme regression", Width = 620, Height = 560, Content = panel };
            var settings = new SettingsWindow(new FileAppSettingsService(Path.Combine(folder, "defaults.json"),
                Path.Combine(folder, "settings.json"), Path.Combine(folder, "theme.txt")));
            var axis = new AxisTuningWindow(null);
            var config = new ModuleConfiguration(new DcsModule(Guid.NewGuid(), "THEME", "Theme"), [], [], [], "smoke");
            var workspace = new EditorWorkspaceSnapshot(config, [], [], [], [], [], [], new Dictionary<string, string>(), new Dictionary<string, string>());
            var usage = new CommandUsageWindow("Theme command", "General", [], workspace, new Dictionary<string, string>());
            var support = new SupportReportWindow("{\"themePreview\":true}");
            var help = new WorkflowHelpWindow();
            var windows = new Window[] { fixture, settings, axis, usage, support, help };
            try
            {
                foreach (var window in windows) window.Show();
                ThemeControlSmoke.Pump(fixture);
                var picker = (ComboBox)settings.FindName("ThemeChoice")!;
                foreach (var skin in new[] { 0, 1, 2, 3 })
                {
                    picker.SelectedIndex = skin;
                    fixture.Activate(); ThemeControlSmoke.Pump(fixture);
                    foreach (var window in windows)
                    {
                        AssertBrush(window.Background, "WindowBackgroundBrush");
                        AssertBrush(window.Foreground, "PrimaryTextBrush");
                    }
                    ThemeControlSmoke.AssertPixel(grid, (int)grid.ActualWidth - 25, (int)grid.ActualHeight - 8, "SurfaceBrush");
                    var header = ThemeControlSmoke.Children(grid).OfType<DataGridColumnHeader>().First(x => x.Column is not null);
                    if (header.Template.FindName("PART_RightHeaderGripper", header) is not Thumb)
                        throw new InvalidOperationException("Table resize grip missing.");
                    var item = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
                    ThemeControlSmoke.AssertPixel(item, 6, 6, "SelectedBackgroundBrush");
                    ThemeControlSmoke.AssertText(item, "First device", "PrimaryTextBrush");
                    AssertBrush(child.Foreground, "PrimaryTextBrush");
                    root.IsExpanded = false; root.IsExpanded = true;
                    grid.SelectedIndex = 0; grid.CurrentCell = new DataGridCellInfo(grid.Items[0], grid.Columns[0]);
                    if (!grid.BeginEdit()) throw new InvalidOperationException("Themed grid would not enter edit mode.");
                    ThemeControlSmoke.Pump(fixture);
                    var editor = ThemeControlSmoke.Children(grid).OfType<TextBox>().Single();
                    AssertBrush(editor.Background, "ControlBackgroundBrush");
                    editor.Text = "Edited " + skin;
                    grid.CommitEdit(DataGridEditingUnit.Cell, true); grid.CommitEdit();
                    if ((string)table.Rows[0]["Name"] != "Edited " + skin)
                        throw new InvalidOperationException("Table edits were not committed.");
                    var check = ThemeControlSmoke.Children(grid).OfType<CheckBox>().First();
                    if (check.Template.FindName("Check", check) is null || check.IsHitTestVisible)
                        throw new InvalidOperationException("Generated table checkbox lost its themed display behavior.");
                    Keyboard.Focus(slider); var before = slider.Value; ThemeControlSmoke.Press(slider, Key.Right);
                    if (slider.Value <= before) throw new InvalidOperationException("Themed slider lost keyboard adjustment.");
                    scroll.ScrollToVerticalOffset(80); ThemeControlSmoke.Pump(fixture);
                    if (scroll.VerticalOffset == 0 || !ThemeControlSmoke.Children(scroll).OfType<ScrollBar>().Any(x => x.Template.FindName("PART_Track", x) is Track))
                        throw new InvalidOperationException("Themed scrollbar lost scrolling or thumb tracking.");
                    var deadzone = (TextBox)axis.FindName("Deadzone")!; deadzone.Text = "invalid";
                    ((Button)axis.FindName("ApplyToDraftButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    AssertBrush(((TextBlock)axis.FindName("ValidationMessage")!).Foreground, "ErrorTextBrush");
                    AssertBrush(((TreeView)usage.FindName("Usage")!).Background, "SurfaceBrush");
                    ThemeControlSmoke.SaveImage(fixture, "all-controls-" + skin);
                    ThemeControlSmoke.SaveImage(axis, "axis-dialog-" + skin);
                    VerifyMessage(fixture, MessageBoxButton.OK, MessageBoxResult.OK, MessageBoxResult.OK);
                    VerifyMessage(fixture, MessageBoxButton.YesNo, MessageBoxResult.No, MessageBoxResult.No);
                    VerifyMessage(fixture, MessageBoxButton.YesNoCancel, MessageBoxResult.Cancel, MessageBoxResult.Cancel);
                    VerifyMessage(fixture, MessageBoxButton.OKCancel, MessageBoxResult.Cancel, MessageBoxResult.Cancel);
                    ModifierReservationSmoke.Verify();
                }
            }
            finally
            {
                foreach (var window in windows.Reverse()) window.Close();
                palettes[paletteIndex] = original; Directory.Delete(folder, true); table.Dispose();
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void AssertBrush(Brush brush, string key)
        {
            var expected = ((SolidColorBrush)System.Windows.Application.Current.FindResource(key)).Color;
            if (brush is not SolidColorBrush actual || actual.Color != expected)
                throw new InvalidOperationException("UI brush does not match " + key);
        }
        private static void VerifyMessage(Window owner, MessageBoxButton buttons, MessageBoxResult choice, MessageBoxResult expectedDefault)
        {
            Exception? failure = null;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            timer.Tick += (_, _) =>
            {
                var dialog = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.Name == "ThemeMessageDialog");
                if (dialog is null) return;
                timer.Stop();
                try
                {
                    AssertBrush(dialog.Background, "WindowBackgroundBrush");
                    var actions = ThemeControlSmoke.Children(dialog).OfType<Button>().ToArray();
                    if (actions.Single(x => x.IsDefault).Tag is not MessageBoxResult defaultChoice || defaultChoice != expectedDefault)
                        throw new InvalidOperationException("Confirmation changed its explicit default action.");
                    var target = actions.Single(x => x.Tag is MessageBoxResult result && result == choice);
                    target.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                catch (Exception ex) { failure = ex; dialog.Close(); }
            };
            timer.Start();
            try
            {
                var result = ThemeMessageBox.Show(owner, "Theme-aware confirmation", "UI theme smoke", buttons, MessageBoxImage.Warning, expectedDefault);
                if (failure is not null) throw new InvalidOperationException("Themed confirmation failed.", failure);
                if (result != choice) throw new InvalidOperationException("Themed confirmation returned the wrong decision.");
            }
            finally { timer.Stop(); }
        }
        #endregion END private helpers
    }
}
