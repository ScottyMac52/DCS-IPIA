using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
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
            var otherProfile = new DeviceProfile(Guid.NewGuid(), "aux-test", "SECOND", "Modifier", $"Test {{{otherPhysical:D}}}.diff.lua");
            var config = workspace.Configuration;
            workspace = workspace with { Configuration = new(config.Module, [.. config.Profiles, otherProfile],
                [new DcsModifier("S3", $"Test {{{otherPhysical:D}}}", "JOY_BTN1", ModifierMode.Hold)], config.Bindings, config.Provenance),
                Diagrams = [.. workspace.Diagrams, workspace.Diagrams[0] with { ProfileId = otherProfile.Id, Device = "Other controller",
                    Controls = workspace.Diagrams[0].Controls }] };
            var before = EditorSnapshotSerializer.Serialize(workspace);
            FakeDeviceInputSource? source = null;
            InputTestLogClipboard? clipboard = null;
            var clipboardFailure = false;
            var window = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(workspace), deviceDiscovery: new EmptyDeviceDiscovery(),
                inputSourceFactory: (_, devices) => source = devices.Contains(physical) && devices.Contains(otherPhysical)
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
            device.SelectedItem = device.Items.Cast<WorkspaceDevice>().Single(x => x.ProfileId == workspace.Diagrams[0].ProfileId);
            window.UpdateLayout();
            var start = (Button)WorkspaceUi.Find(window, "StartInputTestButton")!;
            var stop = (Button)WorkspaceUi.Find(window, "StopInputTestButton")!;
            var log = (ListBox)WorkspaceUi.Find(window, "InputTestLog")!;
            var choice = (ComboBox)WorkspaceUi.Find(window, "InputTestSnapshotChoice")!;
            var status = (TextBlock)WorkspaceUi.Find(window, "InputTestStatus")!;
            start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (log.Items.Count != 0 || ((Canvas)view.FindName("Diagram")!).Children.OfType<Border>().Any(x => x.Background == Brushes.LightSkyBlue))
                throw new InvalidOperationException("The initial sample reported or highlighted existing input states.");
            source!.ButtonValue = 1;
            Wait(window, () => log.Items.Count > 0);
            if (start.IsEnabled || !stop.IsEnabled || choice.IsEnabled || !status.Text.Contains("frozen current edits", StringComparison.Ordinal) ||
                !log.Items.Cast<InputTestLogEntry>().Select(x => x.Display).Any(x => x.Contains("Test action (KEY)", StringComparison.Ordinal)))
                throw new InvalidOperationException("Live test did not resolve its frozen generated profile.");
            if (log.Items.Cast<InputTestLogEntry>().Select(x => x.Display).Any(x => x.Contains(otherPhysical.ToString("D"), StringComparison.Ordinal) || x.Contains("JOY_X", StringComparison.Ordinal) || x.Contains("JOY_BTN2", StringComparison.Ordinal)))
                throw new InvalidOperationException("Unchanged baseline inputs or another controller leaked into feedback.");
            var diagram = (Canvas)view.FindName("Diagram")!;
            if (!diagram.Children.OfType<Border>().Any(x => x.Background == Brushes.LightSkyBlue))
                throw new InvalidOperationException("Physical activity did not highlight its control.");
            if (diagram.Children.OfType<Border>().Any(x =>
                x.Tag is HardwareGraphicControl control && control.InputKey != "JOY_BTN1" && x.Background == Brushes.LightSkyBlue))
                throw new InvalidOperationException("Initially held buttons or stationary axes were highlighted.");
            var panes = (Grid)view.FindName("InputTestPanes")!;
            var splitter = (GridSplitter)WorkspaceUi.Find(window, "InputTestSplitter")!;
            var upper = panes.RowDefinitions[1];
            var lower = panes.RowDefinitions[3];
            if (upper.ActualHeight + lower.ActualHeight - upper.MinHeight - lower.MinHeight < 20)
            {
                window.Height += 160;
                window.UpdateLayout();
            }
            var upperBefore = upper.ActualHeight;
            var lowerBefore = lower.ActualHeight;
            var direction = upperBefore - upper.MinHeight >= 10 ? Key.Up : Key.Down;
            splitter.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(splitter), 0, direction)
                { RoutedEvent = Keyboard.KeyDownEvent });
            window.UpdateLayout();
            if (splitter.ResizeDirection != GridResizeDirection.Rows || splitter.ResizeBehavior != GridResizeBehavior.PreviousAndNext ||
                Math.Abs(upper.ActualHeight - upperBefore) < 1 || Math.Abs(lower.ActualHeight - lowerBefore) < 1 ||
                Math.Abs(upper.ActualHeight + lower.ActualHeight - upperBefore - lowerBefore) > 1)
                throw new InvalidOperationException($"Input-test divider did not redistribute pane height: {upperBefore}/{lowerBefore} -> {upper.ActualHeight}/{lower.ActualHeight}.");
            InputTestLogSmoke.VerifyLive(window, source!, () => clipboard, fail => clipboardFailure = fail);
            stop.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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
            if (EditorSnapshotSerializer.Serialize(workspace) != before)
                throw new InvalidOperationException("Input testing mutated the original workspace.");
            start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            WorkspaceUi.Navigate(window, "Devices");
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            if (source?.Disposed != true) throw new InvalidOperationException("Navigation did not release device input.");
            window.Close();
        }
        #endregion END Public
        #region BEGIN private helpers
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
        private static EditorWorkspaceSnapshot Workspace(Guid physical)
        {
            var profile = new DeviceProfile(Guid.NewGuid(), "test", null, "Test", $"Test {{{physical:D}}}.diff.lua");
            var module = new DcsModule(Guid.NewGuid(), "INPUT_TEST", "Input test");
            var command = new DraftCommand("KEY", "Test action", false, AssignmentLayer.Module);
            var assignment = new DraftAssignment(new(module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1", ""), command);
            var configuration = new ModuleConfiguration(module, [profile], [], [], "Test fixture");
            var diagram = new UiLayerDiagram("Test", null, "test.svg", 100, 100, [], [])
            {
                ProfileId = profile.Id,
                Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><rect width='100' height='100' fill='#07111d'/></svg>",
                Controls = [new HardwareGraphicControl("button", "Button", "JOY_BTN1", 5, 5, 20, 20),
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


