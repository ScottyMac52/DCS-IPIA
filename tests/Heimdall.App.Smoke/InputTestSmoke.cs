using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.App;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises generated-profile Start/Stop, highlighting and acquisition-loss behavior.</summary>
    public static class InputTestSmoke
    {
        #region BEGIN Public
        /// <summary>Runs Windows presentation acceptance with a deterministic input source.</summary>
        public static void Verify()
        {
            var physical = Guid.NewGuid();
            var workspace = Workspace(physical);
            var otherPhysical = Guid.NewGuid();
            var otherProfile = new DcsDeviceProfile(Guid.NewGuid(), "aux-test", "SECOND", "Modifier", $"Test {{{otherPhysical:D}}}.diff.lua");
            var config = workspace.Configuration;
            workspace = workspace with { Configuration = new(config.Module, [.. config.Profiles, otherProfile],
                [new DcsModifier("S3", $"Test {{{otherPhysical:D}}}", "JOY_BTN1", ModifierMode.Hold)], config.Bindings, config.Provenance),
                Diagrams = [.. workspace.Diagrams, workspace.Diagrams[0] with { ProfileId = otherProfile.Id, Device = "Other controller",
                    Controls = workspace.Diagrams[0].Controls }],
                Assignments = [.. workspace.Assignments, new DcsDraftAssignment(
                    new(config.Module.Id, config.Profiles[0].Id, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1", "S3"),
                    new("SHIFTED", "Shifted action", false, AssignmentLayer.Module))] };
            var before = DcsEditorSnapshotSerializer.Serialize(workspace);
            FakeDeviceInputSource? source = null;
            InputTestLogClipboard? clipboard = null;
            var clipboardFailure = false;
            var acquisitionFailure = false;
            var window = new DcsModuleWorkspaceWindow(new DcsModuleWorkspaceViewModel(workspace), deviceDiscovery: new EmptyDeviceDiscovery(),
                inputSourceFactory: (_, devices) => source = acquisitionFailure
                    ? throw new InvalidOperationException("Injected acquisition failure.") : devices.Contains(physical) && devices.Contains(otherPhysical)
                    ? new FakeDeviceInputSource(physical, otherPhysical) { ButtonValue = 0 } : throw new InvalidOperationException("Test must still acquire the modifier controller."),
                inputLogClipboardWriter: (html, text) =>
                {
                    if (clipboardFailure) throw new InvalidOperationException("Injected clipboard busy.");
                    clipboard = new(html, text);
                });
            window.Show();
            var view = WorkspaceUi.Navigate(window, "InputTest");
            window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var device = (ComboBox)view.FindName("FeedbackDevice")!;
            device.SelectedItem = device.Items.Cast<DcsWorkspaceDevice>().Single(x => x.ProfileId == workspace.Diagrams[0].ProfileId);
            window.UpdateLayout();
            var start = (Button)WorkspaceUi.Find(window, "StartInputTestButton")!;
            var stop = (Button)WorkspaceUi.Find(window, "StopInputTestButton")!;
            var log = (ListBox)WorkspaceUi.Find(window, "InputTestLog")!;
            var choice = (ComboBox)WorkspaceUi.Find(window, "InputTestSnapshotChoice")!;
            var status = (TextBlock)WorkspaceUi.Find(window, "InputTestStatus")!;
            start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (log.Items.Count != 0 || ((Canvas)view.FindName("Diagram")!).Children.OfType<Border>().Any(x => x.BorderBrush != Brushes.Transparent))
                throw new InvalidOperationException("The initial sample reported or highlighted existing input states.");
            source!.ButtonValue = 1;
            Wait(window, () => log.Items.Count > 0);
            if (start.IsEnabled || !stop.IsEnabled || choice.IsEnabled || !status.Text.Contains("frozen current edits", StringComparison.Ordinal) ||
                !log.Items.Cast<InputTestLogEntry>().Select(x => x.Display).Any(x => x.Contains("Test action (KEY)", StringComparison.Ordinal)))
                throw new InvalidOperationException("Live test did not resolve its frozen generated profile.");
            if (log.Items.Cast<InputTestLogEntry>().Select(x => x.Display).Any(x => x.Contains(otherPhysical.ToString("D"), StringComparison.Ordinal) || x.Contains("JOY_X", StringComparison.Ordinal) || x.Contains("JOY_BTN2", StringComparison.Ordinal)))
                throw new InvalidOperationException("Unchanged baseline inputs or another controller leaked into feedback.");
            var diagram = (Canvas)view.FindName("Diagram")!;
            if (!diagram.Children.OfType<Border>().Any(x => x.BorderBrush != Brushes.Transparent))
                throw new InvalidOperationException("Physical activity did not highlight its control.");
            if (diagram.Children.OfType<Border>().Any(x =>
                x.Tag is string input && !input.StartsWith("JOY_BTN1|", StringComparison.Ordinal) &&
                x.BorderBrush != Brushes.Transparent))
                throw new InvalidOperationException("Initially held buttons or stationary axes were highlighted.");
            var activeOutline = diagram.Children.OfType<Border>()
                .Single(x => x.Tag is string key && key == "JOY_BTN1|button");
            var shiftedOutline = diagram.Children.OfType<Border>()
                .Single(x => x.Tag is string key && key == "JOY_BTN1|button-shifted");
            var baseColor = (Color)ColorConverter.ConvertFromString(DcsKneeboardLiveProjection.BaseHighlightColor);
            if ((activeOutline.BorderBrush as SolidColorBrush)?.Color != baseColor ||
                shiftedOutline.BorderBrush != Brushes.Transparent)
                throw new InvalidOperationException("Base input highlight was not blue.");
            var legend = (ItemsControl)view.FindName("ModifierLegend")!;
            var shift = legend.Items.Cast<Heimdall.App.Features.InputTest.InputTestLegendItem>()
                .Single(x => x.Entry.Layer == AssignmentLayer.Module && x.Entry.Chord == "S3");
            if (shift.IsActive || diagram.Children.OfType<Border>().Any(x => x.Background != Brushes.Transparent))
                throw new InvalidOperationException("The input test obscured the finished device drawing or activated a neutral shift.");
            source.ModifierValue = 1;
            var shiftedColor = (Color)ColorConverter.ConvertFromString(shift.Color);
            Wait(window, () => shift.IsActive &&
                (shiftedOutline.BorderBrush as SolidColorBrush)?.Color == shiftedColor &&
                activeOutline.BorderBrush == Brushes.Transparent);
            if (!shift.IsActive)
                throw new InvalidOperationException("A modifier on another controller did not light its legend entry.");
            source.ModifierValue = 0;
            Wait(window, () => !shift.IsActive &&
                (activeOutline.BorderBrush as SolidColorBrush)?.Color == baseColor &&
                shiftedOutline.BorderBrush == Brushes.Transparent);
            if (shift.IsActive)
                throw new InvalidOperationException("A released Hold modifier remained lit.");
            if (activeOutline is not ContrastOutlineBorder || shiftedOutline is not ContrastOutlineBorder)
                throw new InvalidOperationException("Live highlights lost their contrasting edges.");
            var uiBase = new Heimdall.App.Features.InputTest.InputTestLegendItem(new("UI Base", "#0891b2", AssignmentLayer.UiLayer));
            if (uiBase.Color != "#0891b2") throw new InvalidOperationException("UI Base swatch lost its cyan identity.");
            VerifyLegendLayout(window, legend);
            var panes = (Grid)view.FindName("InputTestPanes")!;
            var splitter = (GridSplitter)WorkspaceUi.Find(window, "InputTestSplitter")!;
            var left = panes.ColumnDefinitions[0];
            var right = panes.ColumnDefinitions[2];
            var leftBefore = left.ActualWidth;
            var rightBefore = right.ActualWidth;
            var direction = leftBefore - left.MinWidth >= 10 ? Key.Left : Key.Right;
            splitter.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(splitter), 0, direction)
                { RoutedEvent = Keyboard.KeyDownEvent });
            window.UpdateLayout();
            if (splitter.ResizeDirection != GridResizeDirection.Columns || splitter.ResizeBehavior != GridResizeBehavior.PreviousAndNext ||
                Math.Abs(left.ActualWidth - leftBefore) < 1 || Math.Abs(right.ActualWidth - rightBefore) < 1 ||
                Math.Abs(left.ActualWidth + right.ActualWidth - leftBefore - rightBefore) > 1)
                throw new InvalidOperationException($"Input-test divider did not redistribute pane width: {leftBefore}/{rightBefore} -> {left.ActualWidth}/{right.ActualWidth}.");
            InputTestLogSmoke.VerifyLive(window, source!, () => clipboard, fail => clipboardFailure = fail);
            stop.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (legend.Items.Cast<Heimdall.App.Features.InputTest.InputTestLegendItem>().Any(x => x.IsActive))
                throw new InvalidOperationException("Stop did not turn off live modifier swatches.");
            if (source?.Disposed != true || !start.IsEnabled || stop.IsEnabled || !choice.IsEnabled)
                throw new InvalidOperationException("Stop did not release device input.");
            InputTestLogSmoke.VerifyStopped(window, () => clipboard);
            choice.SelectedIndex = 1;
            start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (!status.Text.Contains("frozen saved profile", StringComparison.Ordinal))
                throw new InvalidOperationException("Saved profile test was not identified.");
            typeof(Window).GetMethod("OnDeactivated", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [EventArgs.Empty]);
            if (source?.Disposed != true || !status.Text.Contains("focus loss", StringComparison.Ordinal))
                throw new InvalidOperationException("Focus loss did not clear native input state.");
            start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            source!.Fail = true;
            Wait(window, () => source.Disposed);
            if (!status.Text.Contains("disconnect", StringComparison.Ordinal))
                throw new InvalidOperationException("Device loss was not reported.");
            var disconnectStatus = status.Text;
            typeof(Window).GetMethod("OnDeactivated", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [EventArgs.Empty]);
            if (status.Text != disconnectStatus)
                throw new InvalidOperationException("Idle focus loss hid the disconnect diagnostic.");
            acquisitionFailure = true;
            start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var startupStatus = status.Text;
            if (!startupStatus.Contains("Input test could not start: Injected acquisition failure.", StringComparison.Ordinal) ||
                !start.IsEnabled || stop.IsEnabled || !choice.IsEnabled)
                throw new InvalidOperationException("Acquisition failure did not release the test activity and report the original error.");
            typeof(Window).GetMethod("OnDeactivated", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [EventArgs.Empty]);
            if (status.Text != startupStatus)
                throw new InvalidOperationException("Idle focus loss hid the startup diagnostic.");
            acquisitionFailure = false;
            if (DcsEditorSnapshotSerializer.Serialize(workspace) != before)
                throw new InvalidOperationException("Input testing mutated the original workspace.");
            start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            WorkspaceUi.Navigate(window, "Devices");
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            if (source?.Disposed != true) throw new InvalidOperationException("Navigation did not release device input.");
            window.Close();
            VerifyUnassignedController();
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void VerifyUnassignedController()
        {
            var physical = Guid.NewGuid();
            var linkedWorkspace = Workspace(physical);
            var linkedDiagram = linkedWorkspace.Diagrams[0];
            var workspace = linkedWorkspace with
            {
                Assignments = [],
                Diagrams = [linkedDiagram with { DrawingUnlinked = true, Svg = "" }]
            };
            if (DcsGeneratedInputTestPlan.Create(workspace).Bindings.Count != 0)
                throw new InvalidOperationException("The raw-input fixture must have no generated assignments.");
            FakeDeviceInputSource? source = null;
            var window = new DcsModuleWorkspaceWindow(new DcsModuleWorkspaceViewModel(workspace),
                deviceDiscovery: new EmptyDeviceDiscovery(), inputSourceFactory: (_, devices) =>
                    source = devices.Contains(physical) ? new FakeDeviceInputSource(physical) { ButtonValue = 0 }
                        : throw new InvalidOperationException("An unassigned module controller was excluded from input acquisition."));
            try
            {
                window.Show();
                var view = WorkspaceUi.Navigate(window, "InputTest");
                ThemeControlSmoke.Pump(window);
                WorkspaceUi.Navigate(window, "Devices");
                window.RunStructureEdit(current => current with { Diagrams = [linkedDiagram] });
                WorkspaceUi.Navigate(window, "InputTest");
                ThemeControlSmoke.Pump(window);
                var start = (Button)WorkspaceUi.Find(window, "StartInputTestButton")!;
                var log = (ListBox)WorkspaceUi.Find(window, "InputTestLog")!;
                start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (source is null) throw new InvalidOperationException("The unassigned controller could not start testing.");
                source.ButtonValue = 1;
                Wait(window, () => log.Items.Count > 0);
                if (!log.Items.Cast<InputTestLogEntry>().Any(x => x.Display.Contains("Unassigned in generated profiles", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Raw input feedback was missing for an unassigned controller.");
                var diagram = (Canvas)view.FindName("Diagram")!;
                var baseOutline = diagram.Children.OfType<Border>().Single(x => Equals(x.Tag, "JOY_BTN1|button"));
                var shiftedOutline = diagram.Children.OfType<Border>().Single(x => Equals(x.Tag, "JOY_BTN1|button-shifted"));
                var baseColor = (Color)ColorConverter.ConvertFromString(DcsKneeboardLiveProjection.BaseHighlightColor);
                if ((baseOutline.BorderBrush as SolidColorBrush)?.Color != baseColor ||
                    shiftedOutline.BorderBrush != Brushes.Transparent)
                    throw new InvalidOperationException("Unassigned controller input did not highlight only the active base callout.");
            }
            finally { window.Close(); }
            if (source?.Disposed != true) throw new InvalidOperationException("Closing the raw-input test leaked its input source.");
        }

        private static void VerifyLegendLayout(Window window, ItemsControl legend)
        {
            var originalWidth = window.Width;
            foreach (var width in new[] { 1180, 1024, 900 })
            {
                window.Width = width;
                ThemeControlSmoke.Pump(window);
                var clear = (Button)WorkspaceUi.Find(window, "ClearInputTestLogButton")!;
                var copy = (Button)WorkspaceUi.Find(window, "CopyInputTestLogButton")!;
                var clearOrigin = clear.TranslatePoint(new Point(), window);
                var copyOrigin = copy.TranslatePoint(new Point(), window);
                if (Math.Abs(clearOrigin.Y - copyOrigin.Y) > 1 || Math.Abs(clear.ActualHeight - copy.ActualHeight) > 1 ||
                    clearOrigin.X + clear.ActualWidth > copyOrigin.X)
                    throw new InvalidOperationException($"Event buttons overlap or are misaligned at window width {width}.");
                var labels = ThemeControlSmoke.Children(legend).OfType<TextBlock>()
                    .Where(text => text.DataContext is Heimdall.App.Features.InputTest.InputTestLegendItem item && text.Text == item.Label)
                    .ToArray();
                if (labels.Length != legend.Items.Count)
                    throw new InvalidOperationException("The live modifier legend did not render its real entries.");
                foreach (var label in labels)
                {
                    var row = (Grid)label.Parent;
                    var swatch = row.Children.OfType<Border>().Single();
                    if (label.ActualWidth < 120 ||
                        label.TranslatePoint(new Point(), row).X < swatch.TranslatePoint(new Point(), row).X + swatch.ActualWidth)
                        throw new InvalidOperationException($"Modifier legend text overlaps its color swatch or is squeezed at window width {width}.");
                    var entry = (Heimdall.App.Features.InputTest.InputTestLegendItem)label.DataContext;
                    if (swatch is not ContrastOutlineBorder || (swatch.Background as SolidColorBrush)?.Color != (Color)ColorConverter.ConvertFromString(entry.Color))
                        throw new InvalidOperationException("Modifier legend lost its real color key.");
                }
            }
            window.Width = originalWidth;
            ThemeControlSmoke.Pump(window);
            ThemeControlSmoke.SaveImage(window, "input-test-readable-modifier-legend");
        }

        private static void Wait(Window window, Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!ready())
            {
                var status = ((TextBlock)WorkspaceUi.Find(window, "InputTestStatus")!).Text;
                if (status.StartsWith("Input test could not start:", StringComparison.Ordinal) || DateTime.UtcNow > deadline)
                    throw new InvalidOperationException("Input test UI failed: " + status);
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            }
        }
        private static DcsEditorWorkspaceSnapshot Workspace(Guid physical)
        {
            var profile = new DcsDeviceProfile(Guid.NewGuid(), "test", null, "Test", $"Test {{{physical:D}}}.diff.lua");
            var module = new DcsModule(Guid.NewGuid(), "INPUT_TEST", "Input test");
            var command = new DcsDraftCommand("KEY", "Test action", false, AssignmentLayer.Module);
            var assignment = new DcsDraftAssignment(new(module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1", ""), command);
            var configuration = new DcsModuleConfiguration(module, [profile], [], [], "Test fixture");
            var diagram = new UiLayerDiagram("Test", null, "test.svg", 100, 100, [], [])
            {
                ProfileId = profile.Id,
                Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><rect width='100' height='100' fill='#07111d'/></svg>",
                Controls = [new HardwareGraphicControl("button", "Button", "JOY_BTN1", 5, 5, 20, 20),
                    new("button-shifted", "Shifted button", "JOY_BTN1", 5, 30, 20, 20),
                    new("held", "Initially held", "JOY_BTN2", 35, 5, 20, 20), new("axis", "Stationary axis", "JOY_X", 65, 5, 20, 20, true)]
            };
            return new(configuration, [], [], [diagram], [new("KEY", "Test action", "Keys", false)], [], [assignment], new Dictionary<string, string>(),
                new Dictionary<string, string>
                {
                    ["config/kneeboard.json"] = """{"profiles":{},"pages":[],"summaryPages":[]}""",
                    ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = "local modifiers = {} return modifiers"
                });
        }
        #endregion END private helpers
    }
}
