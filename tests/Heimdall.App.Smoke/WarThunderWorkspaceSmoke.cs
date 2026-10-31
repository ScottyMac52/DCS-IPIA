using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Heimdall.App.Games;
using Heimdall.Application;
using Heimdall.Domain.MultiSim;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;
using Heimdall.WarThunder;

namespace Heimdall.App.Smoke
{
    /// <summary>Deterministic desktop contract checks; real-game acceptance remains a separate local requirement.</summary>
    internal static class WarThunderWorkspaceSmoke
    {
        #region BEGIN Public
        /// <summary>Exercises import, native editing, link ownership, drawings, raw input, draft guards and isolated export.</summary>
        internal static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "war-thunder-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var previous = EditionAccess.Policy; using var issuer = new OwnerLicenseTestIssuer();
            try
            {
                EditionAccess.SetForTests(new EditionPolicy()); var cards = new GameSelectionView();
                if (Box<Button>(cards, "WarThunderCard").IsEnabled) throw new InvalidOperationException("Free edition exposed War Thunder.");
                EditionAccess.SetForTests(issuer.Policy); cards.SetRecent(new("war-thunder"));
                if (!Box<Button>(cards, "WarThunderCard").IsEnabled || !Box<TextBlock>(cards, "RecentText").Text.Contains("War Thunder", StringComparison.Ordinal))
                    throw new InvalidOperationException("War Thunder card or recent selection is unavailable.");
                var device = new DetectedDcsDevice(Guid.NewGuid(), "Synthetic controller", true, ProductGuid: Guid.Parse("00020001-0000-0000-0000-504944564944"));
                var discovery = new GameSmokeDiscovery(device);
                var services = new GameWorkspaceServices(new FileGameWorkspaceStore(Path.Combine(root, "store")), discovery);
                var capture = Path.Combine(root, "capture"); Directory.CreateDirectory(capture);
                var path = Path.Combine(capture, "Synthetic.blk");
                File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "WarThunder", "Synthetic.blk"), path);
                var original = File.ReadAllText(path);
                var diagram = new UiLayerDiagram("Synthetic", null, "", 1200, 1600, [], [])
                {
                    Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1200\" height=\"1600\" viewBox=\"0 0 1200 1600\"><rect x=\"10\" y=\"10\" width=\"1180\" height=\"1580\" fill=\"#304050\"/></svg>",
                    Controls = [new("button", "Button", "JOY_BTN1", 20, 20, 100, 30)]
                };
                WorkspaceHardware[] drawings = [new("drawing-a", "Drawing A", "a.diff.lua", diagram), new("drawing-b", "Drawing B", "b.diff.lua", diagram)];
                IdentificationInputSource? input = null; var choice = MessageBoxResult.Cancel;
                using var window = new WarThunderWorkspaceWindow(services, pendingChoice: () => choice, reviewChange: _ => true,
                    drawings: drawings, inputFactory: (_, ids) => input = new(ids));
                WorkspaceLayout.SetPersistenceEnabled(window, false);
                window.Show(); window.Activate(); ThemeControlSmoke.Pump(window); window.ImportFile(path);
                if (!Box<Button>(window, "Save").IsEnabled || Box<Button>(window, "Export").IsEnabled) throw new InvalidOperationException("Import did not become an unsaved draft.");
                var controllers = Box<ListBox>(window, "Controllers"); controllers.SelectedIndex = 1;
                var devices = window.DeviceView;
                devices.Physical.SelectedItem = device; Click(devices.Link);
                if (devices.Physical.IsEnabled || !devices.Unlink.IsEnabled) throw new InvalidOperationException("Physical link was not locked.");
                devices.Drawing.SelectedItem = drawings[0];
                if (devices.Drawing.IsEnabled || !devices.UnlinkDrawing.IsEnabled) throw new InvalidOperationException("Drawing link was not locked.");
                Click(devices.UnlinkDrawing); devices.Drawing.SelectedItem = drawings[1];
                if (window.Session!.DrawingId("war-thunder.joystick.0") != "drawing-b") throw new InvalidOperationException("Reviewed drawing replacement failed.");
                Box<ListBox>(window, "FeaturePicker").SelectedIndex = 1; ThemeControlSmoke.Pump(window);
                var assignments = window.AssignmentView;
                assignments.Zoom.Value = 1.4; Click(assignments.Fit); ThemeControlSmoke.Pump(window);
                if (assignments.DrawingCanvas.Children.Count == 0) throw new InvalidOperationException("Physical drawing did not render.");
                foreach (var (width, height) in new[] { (1340d, 860d), (1050d, 650d) })
                {
                    window.Width = width; window.Height = height; assignments.Zoom.Value = 2;
                    ThemeControlSmoke.Pump(window);
                    var scroll = assignments.DrawingScroll;
                    if (scroll.ViewportHeight <= 0 || scroll.ScrollableHeight <= 0 || scroll.ScrollableWidth <= 0)
                        throw new InvalidOperationException("War Thunder drawing has clipped content without reachable scroll extent.");
                    scroll.ScrollToTop(); ThemeControlSmoke.Pump(window);
                    var down = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
                    assignments.DrawingCanvas.RaiseEvent(down); ThemeControlSmoke.Pump(window);
                    var afterDown = scroll.VerticalOffset;
                    if (!down.Handled || afterDown <= 0)
                        throw new InvalidOperationException("Plain wheel did not scroll the loaded War Thunder drawing down.");
                    var up = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, 120) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
                    assignments.DrawingCanvas.RaiseEvent(up); ThemeControlSmoke.Pump(window);
                    if (!up.Handled || scroll.VerticalOffset >= afterDown)
                        throw new InvalidOperationException("Plain wheel did not scroll the loaded War Thunder drawing up.");
                    scroll.ScrollToRightEnd(); ThemeControlSmoke.Pump(window);
                    if (scroll.HorizontalOffset <= 0) throw new InvalidOperationException("Height bounds removed horizontal drawing extent.");
                }
                Click(assignments.Start);
                if (input is null || input.Disposed) throw new InvalidOperationException("Shared input test did not acquire the explicit controller.");
                input.Values[device.PhysicalGuid]["JOY_BTN1"] = 1; window.SampleInput();
                if (!assignments.Activity.Text.Contains("JOY_BTN1", StringComparison.Ordinal)) throw new InvalidOperationException("Raw input activity was not displayed.");
                Box<ListBox>(window, "FeaturePicker").SelectedIndex = 0;
                if (!input.Disposed || assignments.Activity.Text.Length > 0) throw new InvalidOperationException("Navigation retained native input handles.");
                Click(devices.UnlinkDrawing); Box<ListBox>(window, "FeaturePicker").SelectedIndex = 1; ThemeControlSmoke.Pump(window);
                if (assignments.DrawingCanvas.Children.Count != 0 || !assignments.Start.IsEnabled) throw new InvalidOperationException("No-drawing input fallback is unavailable.");
                Click(assignments.Start);
                var foreground = new Window { Width = 150, Height = 100, ShowInTaskbar = false }; foreground.Show(); foreground.Activate(); ThemeControlSmoke.Pump(foreground); foreground.Close();
                if (input is null || !input.Disposed) throw new InvalidOperationException("Focus loss retained native input handles.");
                window.Activate(); controllers.SelectedIndex = 0;
                var browser = assignments.Browser;
                browser.CommandSearch.Text = "pitch";
                browser.CommandsList.SelectedItem = browser.CommandsList.Items.Cast<SimCommand>().Single();
                assignments.Editor.Inverse.IsChecked = true; assignments.Editor.Submit(WarThunderEditKind.Inverse);
                assignments.Editor.NativeDetails.IsExpanded = true;
                assignments.Editor.Parameter.SelectedItem = assignments.Editor.Parameter.Items.Cast<WarThunderBlkScalar>().Single(scalar => scalar.Path.EndsWith("/kMul", StringComparison.Ordinal));
                assignments.Editor.Literal.Text = "1.5"; Click(assignments.Editor.SetParameter);
                AssertNative(window, "kMul:r=1.5");
                var edited = AssertNative(window, "inverse:b=yes");
                if (edited == original) throw new InvalidOperationException("Native inverse action did not stage an edit.");
                Click(Box<Button>(window, "Discard"));
                if (AssertNative(window, "inverse:b=no") != original) throw new InvalidOperationException("Discard did not restore exact native text.");
                browser.CommandSearch.Text = "FIRE";
                browser.CommandsList.SelectedItem = browser.CommandsList.Items.Cast<SimCommand>().Single();
                assignments.CapturedAssignments.IsExpanded = true;
                assignments.Records.SelectedItem = assignments.Records.Items.Cast<WarThunderWorkspaceRow>().Single(row => row.Binding.Fields.Any(scalar => scalar.Path.EndsWith("/joyButton", StringComparison.Ordinal)));
                assignments.Editor.Index.Text = "3"; Click(assignments.Editor.Apply);
                AssertNative(window, "joyButton:i=3");
                ((MenuItem)assignments.Records.ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                if (!((WarThunderWorkspaceRow)assignments.Records.SelectedItem).Binding.Empty) throw new InvalidOperationException("Context clear did not empty the selected joystick alternative.");
                Click(assignments.Editor.Add); AssertNative(window, "joyButton:i=3");
                var keyboard = assignments.Records.Items.Cast<WarThunderWorkspaceRow>().Single(row => row.Binding.Fields.Any(scalar => scalar.Path.EndsWith("/keyboardKey", StringComparison.Ordinal)));
                assignments.Records.SelectedItem = keyboard;
                if (assignments.Editor.Apply.IsEnabled || assignments.Editor.Clear.IsEnabled) throw new InvalidOperationException("A preserved system record exposed unsupported mutation.");
                if (!window.SaveCurrent()) throw new InvalidOperationException("Native draft did not save.");
                var id = window.Session.Snapshot.Id; var saved = AssertNative(window, "joyButton:i=3");
                var other = Heimdall.Application.Games.GameConfigurations.Create(window.Session.Snapshot, "Second profile", false);
                services.Store.Save(other, null); window.LoadWorkspace(id);
                Box<TextBox>(window, "ProfileName").Text = "Pending switch";
                var profiles = Box<ComboBox>(window, "Profiles");
                profiles.SelectedItem = profiles.Items.Cast<Heimdall.Application.Games.GameWorkspaceEntry>().Single(entry => entry.Id == other.Id);
                if (window.Session.Snapshot.Id != id || Box<TextBox>(window, "ProfileName").Text != "Pending switch") throw new InvalidOperationException("Cancelled profile switch lost its draft.");
                choice = MessageBoxResult.No;
                profiles.SelectedItem = profiles.Items.Cast<Heimdall.Application.Games.GameWorkspaceEntry>().Single(entry => entry.Id == other.Id);
                if (window.Session.Snapshot.Id != other.Id) throw new InvalidOperationException("Discard did not permit profile switching.");
                profiles.SelectedItem = profiles.Items.Cast<Heimdall.Application.Games.GameWorkspaceEntry>().Single(entry => entry.Id == id);
                choice = MessageBoxResult.Cancel;
                var output = window.ExportReview(root);
                if (File.ReadAllText(Path.Combine(output, "Synthetic.blk")) != saved || File.ReadAllText(path) != original) throw new InvalidOperationException("Review export changed native evidence or the source file.");
                Box<TextBox>(window, "ProfileName").Text = "Pending name";
                Box<ComboBox>(window, "GamePicker").SelectedIndex = 1;
                if (!window.IsVisible || window.RequestedGame is not null || Box<ComboBox>(window, "GamePicker").SelectedIndex != 3) throw new InvalidOperationException("Cancel lost a pending War Thunder draft.");
                Click(Box<Button>(window, "Discard"));
                Box<ListBox>(window, "FeaturePicker").SelectedIndex = 2; ThemeControlSmoke.Pump(window);
                if (Box<ContentControl>(window, "FeatureContent").Content is not WarThunderReferenceView) throw new InvalidOperationException("Reference sheet was not mounted.");
                window.Close();
                using var reopened = new WarThunderWorkspaceWindow(services, id, () => MessageBoxResult.No, drawings: []);
                reopened.Show(); ThemeControlSmoke.Pump(reopened);
                if (AssertNative(reopened, "joyButton:i=3") != saved || reopened.Session!.Snapshot.SourceDirectory != capture) throw new InvalidOperationException("Saved evidence or source protection did not survive reopening.");
                Box<ComboBox>(reopened, "GamePicker").SelectedIndex = 0;
                if (reopened.IsVisible || reopened.RequestedGame != "dcs") throw new InvalidOperationException("Cross-game navigation failed.");
            }
            finally { EditionAccess.SetForTests(previous); Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static T Box<T>(FrameworkElement element, string name) where T : FrameworkElement => (T)element.FindName(name);
        private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        private static string AssertNative(WarThunderWorkspaceWindow window, string expected)
        {
            var text = window.Session!.Export().Files.Single().Value;
            if (!text.Contains(expected, StringComparison.Ordinal)) throw new InvalidOperationException("Missing expected native edit: " + expected);
            return text;
        }
        #endregion END private helpers
    }
}
