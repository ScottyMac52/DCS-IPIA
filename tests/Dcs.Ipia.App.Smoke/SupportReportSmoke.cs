using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Tests visible report review/export and offline help keyboard/version contracts.</summary>
    public static class SupportReportSmoke
    {
        #region BEGIN Public
        /// <summary>Checks exact preview export, cancellation, overwrite protection and help topics in WPF.</summary>
        public static void Verify()
        {
            var folder = Path.Combine(Path.GetTempPath(), "ipia-report-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                const string preview = "{\"FormatVersion\":1,\"ApplicationVersion\":\"1.2.3.4\"}";
                var path = Path.Combine(folder, "support.json");
                var report = new SupportReportWindow(preview, () => path);
                report.Show(); report.UpdateLayout();
                var text = (TextBox)report.FindName("SupportReportPreview")!;
                if (!text.IsReadOnly || text.Text != preview || !text.IsKeyboardFocusWithin)
                    throw new InvalidOperationException("Support preview must be read-only, complete and keyboard-focused.");
                var save = (Button)report.FindName("SaveSupportReportButton")!;
                save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (!File.ReadAllBytes(path).SequenceEqual(Encoding.UTF8.GetBytes(preview)))
                    throw new InvalidOperationException("Support save differs from the reviewed preview.");
                save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (!((TextBlock)report.FindName("SupportReportStatus")!).Text.StartsWith("Report was not saved", StringComparison.Ordinal) || File.ReadAllText(path) != preview)
                    throw new InvalidOperationException("Support export overwrote an existing destination.");
                report.Close();
                var cancelled = new SupportReportWindow(preview, () => null);
                cancelled.Show();
                ((Button)cancelled.FindName("SaveSupportReportButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (((TextBlock)cancelled.FindName("SupportReportStatus")!).Text != "Export cancelled.")
                    throw new InvalidOperationException("Support cancellation was not explicit.");
                cancelled.Close();
                var help = new WorkflowHelpWindow();
                help.Show(); help.UpdateLayout();
                var tabs = ThemeControlSmoke.Children(help).OfType<TabControl>().Single();
                var topics = tabs.Items.Cast<TabItem>().Select(x => x.Content).OfType<TextBox>().Select(x => x.Text).ToArray();
                if (!help.Title.Contains(ApplicationVersion.Label, StringComparison.Ordinal) || tabs.Items.Count != 5 ||
                    !topics[0].Contains(ApplicationVersion.Label, StringComparison.Ordinal) ||
                    !topics[1].Contains("--test-profile", StringComparison.Ordinal) ||
                    !topics[2].Contains("GUIDs", StringComparison.Ordinal) || !topics[3].Contains("user-owned", StringComparison.Ordinal))
                    throw new InvalidOperationException("Offline help lacks version/workflow/privacy/ownership guidance.");
                if (!MainWindowCommands.About.InputGestures.OfType<KeyGesture>().Any(x => x.Key == Key.F1) ||
                    !MainWindowCommands.SupportReport.InputGestures.OfType<KeyGesture>().Any(x => x.Key == Key.D && x.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)))
                    throw new InvalidOperationException("Help/support keyboard commands are missing.");
                help.Close();
            }
            finally { Directory.Delete(folder, true); }
        }
        #endregion END Public
    }
}

