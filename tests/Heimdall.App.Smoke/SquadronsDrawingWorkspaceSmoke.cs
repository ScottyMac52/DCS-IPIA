using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Heimdall.App.Games;
using Heimdall.Application;
using Heimdall.Domain.MultiSim;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises shared Squadrons composition, real callout selection, native authoring and measured drawing bounds.</summary>
    internal static class SquadronsDrawingWorkspaceSmoke
    {
        #region BEGIN Public
        /// <summary>Runs with synthetic hardware and source; no installed game or live DirectInput devices are required.</summary>
        internal static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "squadrons-drawing-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var previous = EditionAccess.Policy; using var issuer = new OwnerLicenseTestIssuer();
            try
            {
                EditionAccess.SetForTests(issuer.Policy);
                var device = new DetectedDcsDevice(Guid.NewGuid(), "Drawing stick", true, ProductGuid: Guid.NewGuid());
                var discovery = new GameSmokeDiscovery(device);
                discovery.Inputs[device.PhysicalGuid] = ControllerCapabilityMapper.Map(
                [
                    new(Guid.Parse("a36d02e1-c9f3-11cf-bfc7-444553540000"), 2u, "Y axis"),
                    new(Guid.NewGuid(), 4u, "Button 1"), new(Guid.NewGuid(), 4u | (1u << 8), "Button 2")
                ]);
                var drawing = new UiLayerDiagram("Synthetic", null, "fixture", 1200, 1600, [], [])
                {
                    Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1200\" height=\"1600\" viewBox=\"0 0 1200 1600\"><rect width=\"1200\" height=\"1600\" fill=\"white\"/></svg>",
                    Controls = [new("trigger", "Trigger", "JOY_BTN1", 20, 40, 140, 60),
                        new("button2", "Button 2", "JOY_BTN2", 20, 300, 140, 60),
                        new("axis", "Y axis", "JOY_Y", 20, 1400, 140, 60, true)]
                };
                var hardware = new WorkspaceHardware("synthetic", "Synthetic drawing", "fixture.lua", drawing);
                var services = new GameWorkspaceServices(new FileGameWorkspaceStore(Path.Combine(root, "store")), discovery);
                var acceptReview = true;
                using var window = new SquadronsWorkspaceWindow(services, pendingChoice: () => MessageBoxResult.No,
                    reviewChange: _ => acceptReview, drawings: [hardware]);
                window.Show(); ThemeControlSmoke.Pump(window);
                window.ImportFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
                var devices = Box<ListBox>(window, "DevicePicker");
                SelectDevice(devices, "squadrons.joystick.3");
                Box<Expander>(window, "ControllerSetup").IsExpanded = true;
                Box<ComboBox>(window, "ConnectedPicker").SelectedItem = device;
                Click(window, "LinkControllerButton");
                Box<Expander>(window, "ControllerSetup").IsExpanded = false;
                Box<ComboBox>(window, "DrawingPicker").SelectedItem = hardware;
                if (!window.SaveProfile()) throw new InvalidOperationException("Drawing profile did not save.");
                Box<ListBox>(window, "FeaturePicker").SelectedIndex = 1;
                ThemeControlSmoke.Pump(window);
                var source = window.Session!.Export().Files[SquadronsProfileBackend.FileName];
                var id = window.Session.Snapshot.Id;
                var browser = Box<GameCommandBrowserView>(window, "CommandBrowser");
                var commands = (ListBox)browser.FindName("CommandsList");
                var editor = Box<GameAssignmentEditorView>(window, "SelectionEditor");
                if (editor.NativeEditor is not StackPanel || commands.Items.Count < 10)
                    throw new InvalidOperationException("Squadrons did not compose the shared browser/editor or retain unbound catalog commands.");
                var fire = commands.Items.Cast<SimCommand>().Single(x => x.Metadata["squadrons.concept"] == "IncomStarshipInputConcepts.ConceptFire");
                var throttle = commands.Items.Cast<SimCommand>().Single(x => x.Metadata["squadrons.concept"] == "IncomStarshipInputConcepts.ConceptThrottle");
                commands.SelectedItem = fire;
                var actions = window.DrawingInteractions();
                var canvas = Box<Canvas>(window, "DrawingCanvas");
                var button = drawing.Controls[1];
                var border = canvas.Children.OfType<Border>().Single(x => x.Tag is HardwareGraphicControl { Control: "button2" });
                border.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
                var backend = new SquadronsProfileBackend();
                if (backend.ReadBindings(window.Session.Snapshot.Profile).Single(x => x.Concept.EndsWith("ConceptFire", StringComparison.Ordinal)).Input?.Key != "JOY_BTN2")
                    throw new InvalidOperationException("Clicking a shared callout failed to stage the selected command.");
                window.Session.Discard();
                actions.Drop!(button, fire.Id);
                if (backend.ReadBindings(window.Session.Snapshot.Profile).Single(x => x.Concept.EndsWith("ConceptFire", StringComparison.Ordinal)).Input?.Key != "JOY_BTN2")
                    throw new InvalidOperationException("Dropping a command did not use the same native assignment path.");
                window.Session.Discard();
                commands.SelectedItem = commands.Items.Cast<SimCommand>().Single(x => x.Id == throttle.Id);
                actions.Drop!(drawing.Controls[2], throttle.Id);
                var axis = backend.ReadBindings(window.Session.Snapshot.Profile).Where(x => x.Concept.EndsWith("ConceptThrottle", StringComparison.Ordinal)).ToArray();
                if (axis.Length != 2 || axis.Any(x => x.DeviceId != 2 || x.Input?.Key != "JOY_Y") || axis.Select(x => x.Input!.Direction).Distinct().Count() != 2)
                    throw new InvalidOperationException("Drawing authoring lost one native axis direction.");
                window.Session.Discard();
                acceptReview = false;
                actions.Clear!(drawing.Controls[0]);
                if (window.Session.Export().Files[SquadronsProfileBackend.FileName] != source)
                    throw new InvalidOperationException("Cancelling a reviewed callout clear changed the draft.");
                acceptReview = true;
                actions.Clear!(drawing.Controls[0]);
                if (backend.ReadBindings(window.Session.Snapshot.Profile).Any(x => x.Concept.EndsWith("ConceptFire", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Pointed callout clear depended on the unrelated selected command.");
                window.Session.Discard();
                if (actions.Tune is not null) throw new InvalidOperationException("Squadrons advertised unimplemented axis tuning.");
                var frozen = JsonSerializer.Serialize(window.Session.Snapshot);
                SelectDevice(devices, "squadrons.joystick.5");
                if (actions.CanClear?.Invoke(new("z", "Z axis", "JOY_Z", 0, 0, 10, 10, true)) != false)
                    throw new InvalidOperationException("An ambiguous paired-axis callout advertised a single-record clear.");
                SelectDevice(devices, "squadrons.joystick.3");
                SelectDevice(devices, "squadrons.joystick.5");
                if (Box<DataGrid>(window, "BindingsGrid").SelectedItem is not null || Box<TextBlock>(window, "DrawingDropFeedback").Text.Length != 0 || canvas.Children.Count != 0)
                    throw new InvalidOperationException("Controller switch retained a prior control, drawing or confirmation.");
                if (JsonSerializer.Serialize(window.Session.Snapshot) != frozen)
                    throw new InvalidOperationException("Controller selection changed native source or drawing pins.");
                SelectDevice(devices, "squadrons.joystick.3");
                foreach (var (width, height) in new[] { (1340d, 860d), (1050d, 650d) })
                {
                    window.Width = width; window.Height = height;
                    Box<Slider>(window, "DrawingZoom").Value = 2;
                    ThemeControlSmoke.Pump(window);
                    var scroll = Box<ScrollViewer>(window, "DrawingScroll");
                    var panel = Box<Expander>(window, "DrawingPanel");
                    if (scroll.ViewportHeight <= 0 || scroll.ScrollableHeight <= 0 || scroll.ScrollableWidth <= 0 ||
                        scroll.TranslatePoint(new Point(0, scroll.ActualHeight), panel).Y > panel.ActualHeight + 1)
                        throw new InvalidOperationException("The measured drawing pane clipped content without reachable scroll extent.");
                    scroll.ScrollToBottom(); scroll.ScrollToRightEnd(); ThemeControlSmoke.Pump(window);
                    if (scroll.VerticalOffset <= 0 || scroll.HorizontalOffset <= 0)
                        throw new InvalidOperationException("Drawing content could not be reached by scrolling.");
                }
                Box<ListBox>(window, "FeaturePicker").SelectedIndex = 0;
                Click(window, "ClearDrawingButton");
                if (Box<Canvas>(window, "DrawingCanvas").Children.Count != 0 || Box<ComboBox>(window, "DrawingPicker").SelectedItem is not null)
                    throw new InvalidOperationException("Hiding a drawing left stale controls or prevented reselecting the same drawing.");
                Box<ComboBox>(window, "DrawingPicker").SelectedItem = hardware;
                if (!window.SaveProfile()) throw new InvalidOperationException("Drawing choice could not be saved.");
                window.Close();
                using var reopened = new SquadronsWorkspaceWindow(services, id, () => MessageBoxResult.No, drawings: [hardware]);
                reopened.Show(); ThemeControlSmoke.Pump(reopened);
                Box<ListBox>(reopened, "FeaturePicker").SelectedIndex = 1;
                SelectDevice(Box<ListBox>(reopened, "DevicePicker"), "squadrons.joystick.3");
                if (Box<Canvas>(reopened, "DrawingCanvas").Children.OfType<Border>().Count() != drawing.Controls.Count ||
                    reopened.Session!.Export().Files[SquadronsProfileBackend.FileName] != source)
                    throw new InvalidOperationException("Saved drawing pin did not reopen or presentation changed native evidence.");
                reopened.Close();
            }
            finally { EditionAccess.SetForTests(previous); Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static T Box<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
        private static void Click(FrameworkElement window, string name) => Box<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        private static void SelectDevice(ListBox picker, string id) => picker.SelectedItem = picker.Items.Cast<GameDeviceChoice>().Single(x => x.Id == id);
        #endregion END private helpers
    }
}
