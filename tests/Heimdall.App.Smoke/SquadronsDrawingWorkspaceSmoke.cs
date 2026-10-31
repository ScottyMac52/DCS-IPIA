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
                    new(Guid.Parse("a36d02e0-c9f3-11cf-bfc7-444553540000"), 2u | (1u << 8), "X axis"),
                    new(Guid.NewGuid(), 4u, "Button 1"), new(Guid.NewGuid(), 4u | (1u << 8), "Button 2")
                ]);
                var drawing = new UiLayerDiagram("Synthetic", null, "fixture", 1200, 1600, [], [])
                {
                    Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1200\" height=\"1600\" viewBox=\"0 0 1200 1600\"><rect width=\"1200\" height=\"1600\" fill=\"white\"/></svg>",
                    Controls = [new("trigger", "Trigger", "JOY_BTN1", 20, 40, 140, 60),
                        new("button2", "Button 2", "JOY_BTN2", 20, 300, 140, 60),
                        new("axis", "Y axis", "JOY_Y", 20, 1400, 140, 60, true),
                        new("empty", "X axis", "JOY_X", 600, 900, 140, 60, true)]
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
                var beforeTemplates = window.Session!.Export().Files[SquadronsProfileBackend.FileName];
                var beforePins = JsonSerializer.Serialize(window.Session.Snapshot.Hardware);
                window.ImportTemplatesFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
                if (window.Session.Export().Files[SquadronsProfileBackend.FileName] != beforeTemplates ||
                    JsonSerializer.Serialize(window.Session.Snapshot.Hardware) != beforePins)
                    throw new InvalidOperationException("Template recovery replaced active native bindings or hardware links.");
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
                if (window.Session.Export().Files[SquadronsProfileBackend.FileName] != source ||
                    Box<ComboBox>(window, "InputPicker").SelectedItem is not SquadronsInput { Key: "JOY_BTN2" })
                    throw new InvalidOperationException("A callout click edited the profile instead of selecting its physical input.");
                Click(window, "ApplyButton");
                if (backend.ReadBindings(window.Session.Snapshot.Profile).Single(x => x.Concept.EndsWith("ConceptFire", StringComparison.Ordinal)).Input?.Key != "JOY_BTN2")
                    throw new InvalidOperationException("Click then Apply did not stage the selected command.");
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
                commands.SelectedItem = commands.Items.Cast<SimCommand>().Single(x => x.Id == fire.Id);
                if (actions.CanClear?.Invoke(drawing.Controls[2]) != true)
                    throw new InvalidOperationException("A paired native axis had no pointed-control clear action.");
                var axisBorder = canvas.Children.OfType<Border>().Single(x => x.Tag is HardwareGraphicControl { Control: "axis" });
                var axisMenu = axisBorder.ContextMenu ?? throw new InvalidOperationException("Axis callout has no context menu.");
                HardwareDiagramPresenter.RefreshMenu(axisMenu, drawing.Controls[2], actions);
                var beforeInspect = window.Session.Export().Files[SquadronsProfileBackend.FileName];
                axisMenu.Items.OfType<MenuItem>().Single(x => x.Header as string == "View assignments")
                    .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                if (Box<DataGrid>(window, "BindingsGrid").Items.Count != 2 ||
                    editor.AssignmentCommand.Text != "Throttle" ||
                    (commands.SelectedItem as SimCommand)?.Id != throttle.Id ||
                    window.Session.Export().Files[SquadronsProfileBackend.FileName] != beforeInspect)
                    throw new InvalidOperationException("Inspection did not synchronize the command heading, selected record and action target without editing.");
                VerifyApplyAndContext(window, drawing, actions, backend, throttle, fire, editor);
                // The checks restore the same Y-axis assignment used by the remaining clear/layout smoke.
                axisBorder = canvas.Children.OfType<Border>().Single(x => x.Tag is HardwareGraphicControl { Control: "axis" });
                axisMenu = axisBorder.ContextMenu!;
                HardwareDiagramPresenter.RefreshMenu(axisMenu, drawing.Controls[2], actions);
                var emptyMenu = new ContextMenu();
                HardwareDiagramPresenter.RefreshMenu(emptyMenu, drawing.Controls[3], actions);
                if (emptyMenu.Items.OfType<MenuItem>().Any(x => x.Header as string == "Clear assignments…"))
                    throw new InvalidOperationException("An empty callout offered a clear action.");
                emptyMenu.Items.OfType<MenuItem>().Single(x => x.Header as string == "View assignments")
                    .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                if (Box<DataGrid>(window, "BindingsGrid").Items.Count != 0 ||
                    window.Session.Export().Files[SquadronsProfileBackend.FileName] != beforeInspect)
                    throw new InvalidOperationException("Inspecting an empty axis changed native assignments.");
                var clearAxis = axisMenu.Items.OfType<MenuItem>().Single(x => x.Header as string == "Clear assignments…");
                var axisSource = window.Session.Export().Files[SquadronsProfileBackend.FileName];
                acceptReview = false;
                clearAxis.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                if (window.Session.Export().Files[SquadronsProfileBackend.FileName] != axisSource)
                    throw new InvalidOperationException("Canceling axis callout clear changed native records.");
                acceptReview = true;
                clearAxis.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                if (backend.ReadBindings(window.Session.Snapshot.Profile).Any(x => x.Concept.EndsWith("ConceptThrottle", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Axis context clear retained a native direction record.");
                if (!backend.ReadBindings(window.Session.Snapshot.Profile).Any(x => x.Concept.EndsWith("ConceptFire", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Axis context clear affected an unrelated control.");
                actions.Drop!(drawing.Controls[2], throttle.Id);
                if (backend.ReadBindings(window.Session.Snapshot.Profile).Count(x => x.Concept.EndsWith("ConceptThrottle", StringComparison.Ordinal)) != 2)
                    throw new InvalidOperationException("Clearing an axis made subsequent drawing reassignment impossible.");
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
                if (actions.CanClear?.Invoke(new("z", "Z axis", "JOY_Z", 0, 0, 10, 10, true)) != true)
                    throw new InvalidOperationException("A paired-axis callout did not offer reviewed whole-control clear.");
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
        /// <summary>Checks whole-axis Apply, explicit record editing, filtered inspection and visible category ownership.</summary>
        private static void VerifyApplyAndContext(SquadronsWorkspaceWindow window, UiLayerDiagram drawing,
            HardwareDiagramInteractions actions, SquadronsProfileBackend backend, SimCommand throttle, SimCommand fire,
            GameAssignmentEditorView editor)
        {
            var browser = Box<GameCommandBrowserView>(window, "CommandBrowser");
            var commands = (ListBox)browser.FindName("CommandsList");
            var inputs = Box<ComboBox>(window, "InputPicker");
            inputs.SelectedItem = inputs.Items.Cast<SquadronsInput>().Single(x => x.Key == "JOY_X");
            Click(window, "ApplyButton");
            var rows = backend.ReadBindings(window.Session!.Snapshot.Profile).Where(x => x.Concept.EndsWith("ConceptThrottle", StringComparison.Ordinal)).ToArray();
            if (rows.Length != 2 || rows.Any(x => x.Input?.Key != "JOY_X" || x.DeviceId != 2) ||
                rows.Select(x => x.Input!.Direction).Distinct().Count() != 2)
                throw new InvalidOperationException("Normal Apply split the native axis pair or edited the previously selected Fire command.");
            window.Session.Discard();
            actions.Drop!(drawing.Controls[2], throttle.Id);

            commands.SelectedItem = commands.Items.Cast<SimCommand>().Single(x => x.Id == fire.Id);
            browser.CommandSearch.Text = "Fire";
            var before = window.Session.Export().Files[SquadronsProfileBackend.FileName];
            actions.Inspect!(drawing.Controls[2]);
            if (editor.AssignmentCommand.Text != "Throttle" || (commands.SelectedItem as SimCommand)?.Id != throttle.Id ||
                Box<DataGrid>(window, "BindingsGrid").SelectedItem is not SquadronsWorkspaceRow selected ||
                selected.Native.Concept != "IncomStarshipInputConcepts.ConceptThrottle" ||
                window.Session.Export().Files[SquadronsProfileBackend.FileName] != before)
                throw new InvalidOperationException("Filtered inspection retained another command heading or changed native assignments.");
            inputs.SelectedItem = inputs.Items.Cast<SquadronsInput>().Single(x => x.Key == "JOY_X");
            Box<MenuItem>(window, "ApplyNativeRecordItem").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            rows = backend.ReadBindings(window.Session.Snapshot.Profile).Where(x => x.Concept.EndsWith("ConceptThrottle", StringComparison.Ordinal)).ToArray();
            if (rows.Count(x => x.Input?.Key == "JOY_X") != 1 || rows.Count(x => x.Input?.Key == "JOY_Y") != 1)
                throw new InvalidOperationException("The explicitly record-scoped advanced action did not retain the other direction.");
            window.Session.Discard();
            actions.Drop!(drawing.Controls[2], throttle.Id);

            Box<ComboBox>(window, "SectionPicker").SelectedItem = "Flight";
            browser.CommandCategoryPicker.SelectedItem = "Hangar & Briefing Room";
            var camera = commands.Items.Cast<SimCommand>().Single(x => x.Metadata["squadrons.concept"] == "IncomSoldierInputConcepts.ConceptCameraPitch");
            actions.Drop!(drawing.Controls[3], camera.Id);
            var canvas = Box<Canvas>(window, "DrawingCanvas");
            var cameraBorder = canvas.Children.OfType<Border>().Single(x => x.Tag is HardwareGraphicControl { Control: "empty" });
            if (actions.CanClear!(drawing.Controls[3]) != true ||
                cameraBorder.ToolTip is not string tooltip || !tooltip.Contains("Camera Pitch", StringComparison.Ordinal))
                throw new InvalidOperationException("A hidden Flight diagnostic filter hid the Hangar drawing assignment or clear action.");
            actions.Inspect!(drawing.Controls[3]);
            if (Box<DataGrid>(window, "BindingsGrid").Items.Count != 2 || editor.AssignmentCommand.Text != camera.DisplayName)
                throw new InvalidOperationException("Hangar inspection used a hidden diagnostic context instead of the visible command context.");
            window.Session.Discard();
            browser.CommandCategoryPicker.SelectedItem = "All";
            actions.Drop!(drawing.Controls[2], throttle.Id);
        }
        private static T Box<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
        private static void Click(FrameworkElement window, string name) => Box<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        private static void SelectDevice(ListBox picker, string id) => picker.SelectedItem = picker.Items.Cast<GameDeviceChoice>().Single(x => x.Id == id);
        #endregion END private helpers
    }
}
