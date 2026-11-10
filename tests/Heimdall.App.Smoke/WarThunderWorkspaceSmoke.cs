using Heimdall.App.Games.WarThunder;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Heimdall.App.Games;
using Heimdall.Application;
using Heimdall.Application.Games;
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
                EditionAccess.SetForTests(issuer.Policy);
                WarThunderInheritanceSmoke.Verify(root); cards.SetRecent(new("war-thunder"));
                if (!Box<Button>(cards, "WarThunderCard").IsEnabled || !Box<TextBlock>(cards, "RecentText").Text.Contains("War Thunder", StringComparison.Ordinal))
                    throw new InvalidOperationException("War Thunder card or recent selection is unavailable.");
                VerifyControllerAddition(root);
                var device = new DetectedDcsDevice(Guid.NewGuid(), "Synthetic controller", true, ProductGuid: Guid.Parse("00020001-0000-0000-0000-504944564944"));
                var discovery = new GameSmokeDiscovery(device);
                discovery.Inputs[device.PhysicalGuid] = ControllerCapabilityMapper.Map([
                    new(Guid.Parse("a36d02e1-c9f3-11cf-bfc7-444553540000"), 258u, "Y"),
                    new(Guid.Parse("a36d02e0-c9f3-11cf-bfc7-444553540000"), 2u, "X"),
                    new(Guid.NewGuid(), 4u, "Button 1"), new(Guid.NewGuid(), 260u, "Button 2"),
                    new(Guid.NewGuid(), 516u, "Button 3"), new(Guid.NewGuid(), 772u, "Button 4"),
                    new(Guid.Parse("a36d02e2-c9f3-11cf-bfc7-444553540000"), 513u, "Recognized relative axis")]);
                var store = new InterceptedGameWorkspaceStore(new FileGameWorkspaceStore(Path.Combine(root, "store")));
                var services = new GameWorkspaceServices(store, discovery);
                var capture = Path.Combine(root, "capture"); Directory.CreateDirectory(capture);
                var path = Path.Combine(capture, "Synthetic.blk");
                File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "WarThunder", "Synthetic.blk"), path);
                var original = File.ReadAllText(path).Replace("hotkeys{",
                    "hotkeys{ID_CLEAR_CHORD{joyButton:i=0;joyButton:i=2;}ID_CLEAR_BUTTON{joyButton:i=0;}", StringComparison.Ordinal);
                File.WriteAllText(path, original);
                var diagram = new UiLayerDiagram("Synthetic", null, "", 1200, 1600, [], [])
                {
                    Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1200\" height=\"1600\" viewBox=\"0 0 1200 1600\"><rect x=\"10\" y=\"10\" width=\"1180\" height=\"1580\" fill=\"#304050\"/></svg>",
                    Controls = [new("button", "Button 1", "JOY_BTN1", 20, 20, 100, 30), new("button2", "Button 2", "JOY_BTN2", 140, 20, 100, 30), new("axis", "X Axis", "JOY_X", 260, 20, 100, 30, true)]
                };
                WorkspaceHardware[] drawings = [new("drawing-a", "Drawing A", "a.diff.lua", diagram), new("drawing-b", "Drawing B", "b.diff.lua", diagram)];
                IdentificationInputSource? input = null; var choice = MessageBoxResult.Cancel;
                using var window = new WarThunderWorkspaceWindow(services, pendingChoice: () => choice, reviewChange: _ => true,
                    drawings: drawings, inputFactory: (_, ids) => input = new(ids));
                var workflowCompletions = 0;
                window.AssignmentWorkflow.Completed += (_, _) => workflowCompletions++;
                WorkspaceLayout.SetPersistenceEnabled(window, false);
                window.Show(); window.Activate(); ThemeControlSmoke.Pump(window);
                if (Box<ContentControl>(window, "FeatureContent").Content is not WarThunderDevicesView || Box<Expander>(window, "ErrorPanel").Visibility == Visibility.Visible)
                    throw new InvalidOperationException("Empty startup did not mount Devices without a loading error.");
                window.ImportFile(path);
                if (!Box<Button>(window, "Save").IsEnabled || Box<Button>(window, "Export").IsEnabled) throw new InvalidOperationException("Import did not become an unsaved draft.");
                var controllers = Box<ListBox>(window, "Controllers"); controllers.SelectedIndex = 1;
                var devices = window.DeviceView;
                devices.Physical.SelectedItem = device; Click(devices.Link);
                if (devices.Physical.IsEnabled || !devices.Unlink.IsEnabled) throw new InvalidOperationException("Physical link was not locked.");
                devices.Drawing.SelectedItem = drawings[0];
                if (devices.Drawing.IsEnabled || !devices.UnlinkDrawing.IsEnabled) throw new InvalidOperationException("Drawing link was not locked.");
                Click(devices.UnlinkDrawing); devices.Drawing.SelectedItem = drawings[1];
                if (window.Session!.DrawingId("war-thunder.joystick.0") != "drawing-b") throw new InvalidOperationException("Reviewed drawing replacement failed.");
                // Hot path: switching the selected controller updates the device panel from cached native records (no re-parse per selection).
                var thunder = (WarThunderWorkspaceContext)window.WorkspaceContext;
                var parsed = thunder.NativeDeviceProjections; var panels = new Dictionary<int, string>();
                for (var i = 0; i < 6; i++)
                {
                    controllers.SelectedIndex = i % 2 == 0 ? 0 : 1; ThemeControlSmoke.Pump(window);
                    var panel = devices.NativeDetails.Text + "|" + devices.Unlink.IsEnabled;
                    if (panels.TryGetValue(controllers.SelectedIndex, out var seen) && seen != panel) throw new InvalidOperationException("Device panel went stale after a selection change.");
                    panels[controllers.SelectedIndex] = panel;
                }
                if (!devices.Unlink.IsEnabled || panels[0] == panels[1]) throw new InvalidOperationException("Device panel did not follow the selected controller.");
                if (thunder.NativeDeviceProjections != parsed) throw new InvalidOperationException("Selection re-parsed native controller records.");
                Box<ListBox>(window, "FeaturePicker").SelectedIndex = 1; ThemeControlSmoke.Pump(window);
                var assignments = window.AssignmentView;
                if (!devices.NativeDetails.Text.Contains("Axis mapping blocked", StringComparison.Ordinal)
                    || !assignments.Artwork.Feedback.Text.Contains("Translated 4 native button slots", StringComparison.Ordinal))
                    throw new InvalidOperationException("A mismatched axis group did not retain verified button mapping and visible diagnostics.");
                assignments.Zoom.Value = 1.4; Click(assignments.Fit); ThemeControlSmoke.Pump(window);
                if (assignments.DrawingCanvas.Children.Count == 0) throw new InvalidOperationException("Physical drawing did not render.");
                VerifyBlockedAxis(window);
                var fireCommand = window.Session!.Catalog.Commands.Single(command => command.Metadata["war-thunder.command"] == "ID_FIRE");
                if (!window.SaveCurrent() || window.Session.HasChanges) throw new InvalidOperationException("Saved baseline was not clean before the drawing edit.");
                var beforeDrop = workflowCompletions;
                window.DropPhysicalCommand(diagram.Controls[0], fireCommand.Id);
                if (workflowCompletions != beforeDrop + 1) throw new InvalidOperationException("Drawing drop bypassed the shared assignment workflow.");
                AssertNative(window, "joyButton:i=0;activationType:i=9");
                if (!window.Session.HasChanges || !Box<Button>(window, "Save").IsEnabled)
                    throw new InvalidOperationException("A changed drawing assignment did not enable Save on a saved profile.");
                var clearBlockedDuringSave = false;
                store.BeforeSave = () =>
                {
                    var callout = assignments.DrawingCanvas.Children.OfType<Border>()
                        .FirstOrDefault(border => border.Tag is HardwareGraphicControl control && control.InputKey == "JOY_BTN1");
                    var clear = callout?.ContextMenu?.Items.OfType<MenuItem>().FirstOrDefault(item => item.Header as string == "Clear assignment");
                    clearBlockedDuringSave = clear is { IsEnabled: false } && clear.Items.Count == 0;
                };
                var drawingSaved = window.SaveCurrent();
                store.BeforeSave = null;
                if (!drawingSaved) throw new InvalidOperationException("Drawing assignment could not be saved.");
                if (!clearBlockedDuringSave) throw new InvalidOperationException("Shared clear retained executable alternatives while persistence was running.");
                var savedDrawingText = AssertNative(window, "joyButton:i=0;activationType:i=9");
                window.DropPhysicalCommand(diagram.Controls[0], fireCommand.Id);
                if (window.Session.HasChanges || Box<Button>(window, "Save").IsEnabled
                    || !Box<TextBlock>(window, "Status").Text.Contains("unchanged", StringComparison.Ordinal))
                    throw new InvalidOperationException("An unchanged assignment was reported as a staged change.");
                var firstControl = assignments.DrawingCanvas.Children.OfType<Border>().Single(border => border.Tag is HardwareGraphicControl control && control.InputKey == "JOY_BTN1");
                var menu = firstControl.ContextMenu ?? throw new InvalidOperationException("Assigned physical callout has no right-click menu.");
                var clearChoice = menu.Items.OfType<MenuItem>().Single(item => item.Header as string == "Clear assignment");
                if (!clearChoice.IsEnabled || clearChoice.Items.Count != 3)
                    throw new InvalidOperationException($"Shared clear did not enable choices for a joystick chord and multiple alternatives on one input. " +
                        $"Enabled={clearChoice.IsEnabled}, alternatives={clearChoice.Items.Count}, reason={clearChoice.ToolTip}.");
                clearChoice.Items.OfType<MenuItem>().Single(item => (item.Header as string)?.StartsWith("CLEAR CHORD", StringComparison.Ordinal) == true)
                    .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                AssertNative(window, "ID_CLEAR_CHORD{}"); AssertNative(window, "ID_CLEAR_BUTTON{joyButton:i=0;}");
                AssertNative(window, "keyboardKey:i=30;keyboardKey:i=31");
                if (!window.Session.HasChanges || !Box<Button>(window, "Save").IsEnabled)
                    throw new InvalidOperationException("A callout clear did not stage an independently saveable change.");
                Click(Box<Button>(window, "Discard"));
                if (AssertNative(window, "ID_CLEAR_CHORD{joyButton:i=0;joyButton:i=2;}") != savedDrawingText)
                    throw new InvalidOperationException("Discard did not restore the cleared joystick chord and other saved native records.");
                menu.Items.OfType<MenuItem>().Single(item => item.Header as string == "View assignments").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                if (!assignments.CapturedAssignments.IsExpanded || assignments.Records.Items.Count == 0)
                    throw new InvalidOperationException("The physical context action did not display its captured assignments.");
                assignments.Browser.CommandsList.SelectedItem = assignments.Browser.CommandsList.Items.Cast<SimCommand>().Single(command => command.Id == fireCommand.Id);
                var secondControl = assignments.DrawingCanvas.Children.OfType<Border>().Single(border => border.Tag is HardwareGraphicControl control && control.InputKey == "JOY_BTN2");
                secondControl.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
                var applyAction = Box<FeatureActionBar>(window, "FeatureActions").Actions.Single(action => action.Id == "Apply");
                if (!applyAction.Command.CanExecute(null)) throw new InvalidOperationException("A verified physical input did not enable Apply assignment.");
                applyAction.Command.Execute(null); AssertNative(window, "joyButton:i=1;activationType:i=9");
                if (!window.Session.HasChanges || !Box<Button>(window, "Save").IsEnabled)
                    throw new InvalidOperationException("Public Apply did not refresh Save after changing a saved drawing assignment.");
                foreach (var (width, height) in new[] { (1340d, 860d), (1050d, 650d) })
                {
                    window.Width = width; window.Height = height; assignments.Zoom.Value = 2;
                    ThemeControlSmoke.Pump(window);
                    var scroll = assignments.DrawingScroll;
                    if (scroll.ActualHeight < 150 || scroll.ViewportHeight <= 0 || scroll.ScrollableHeight <= 0 || scroll.ScrollableWidth <= 0 ||
                        scroll.TranslatePoint(new Point(0, scroll.ActualHeight), assignments.DrawingPanel).Y > assignments.DrawingPanel.ActualHeight + 1)
                        throw new InvalidOperationException($"War Thunder drawing has clipped content or insufficient controller space. " +
                            $"Viewport height={scroll.ActualHeight:F1}, panel={assignments.DrawingPanel.ActualHeight:F1}, view={assignments.ActualHeight:F1}, " +
                            $"reserve={assignments.ControllerReserveHeight:F1}, scroll extents={scroll.ScrollableWidth:F1}x{scroll.ScrollableHeight:F1}.");
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
                if (assignments.Editor.Apply.IsEnabled || assignments.Editor.Clear.IsEnabled || !assignments.Stop.IsEnabled ||
                    assignments.Editor.Clear.ToolTip is not string reason || !reason.Contains("Stop input", StringComparison.Ordinal))
                    throw new InvalidOperationException("Live input did not apply the shared action policy to the native editor and toolbar.");
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
                browser.CommandSearch.Text = "__no_matching_command__";
                if (browser.CommandsList.Items.Count != 0 || assignments.Records.Items.Count != 0 || assignments.Editor.Apply.IsEnabled)
                    throw new InvalidOperationException("An empty command filter retained a stale assignment or failed to clear selection.");
                browser.CommandSearch.Clear();
                if (browser.CommandsList.Items.Count == 0) throw new InvalidOperationException("Empty filtering left refresh suppression stuck.");
                window.DropPhysicalCommand(diagram.Controls[0], "missing-command");
                var operationError = Box<TextBlock>(window, "Status").Text;
                var details = Box<TextBox>(window, "ErrorDetails").Text;
                var statusWindow = new Window { Width = 150, Height = 100, ShowInTaskbar = false };
                statusWindow.Show(); statusWindow.Activate(); ThemeControlSmoke.Pump(statusWindow); statusWindow.Close();
                if (Box<TextBlock>(window, "Status").Text != operationError || Box<TextBox>(window, "ErrorDetails").Text != details || details.Length == 0)
                    throw new InvalidOperationException("Focus loss replaced the operation error or its retained diagnostic details.");
                window.Activate();
                browser.CommandSearch.Text = "pitch";
                browser.CommandsList.SelectedItem = browser.CommandsList.Items.Cast<SimCommand>().Single();
                var beforeNativeEdit = workflowCompletions;
                window.AxisTuningOpened = dialog =>
                {
                    try
                    {
                        if (dialog.NativeFields.Content is not StackPanel fields)
                            throw new InvalidOperationException("The shared dialog did not display native tuning fields.");
                        fields.Children.OfType<CheckBox>().Single().IsChecked = true;
                        var label = fields.Children.OfType<Label>().Single(item => Equals(item.Content, "kMul"));
                        if (label.Target is not TextBox editor)
                            throw new InvalidOperationException("The native multiplier has no editor.");
                        editor.Text = "1.5";
                        Click(dialog.ApplyToDraftButton);
                    }
                    finally
                    {
                        if (dialog.IsVisible) dialog.Close();
                    }
                };
                try { Click(assignments.Editor.Tune); }
                finally { window.AxisTuningOpened = null; }
                if (workflowCompletions != beforeNativeEdit + 1)
                    throw new InvalidOperationException("Shared tuning did not stage both fields in one operation.");
                AssertNative(window, "kMul:r=1.5");
                var edited = AssertNative(window, "inverse:b=yes");
                if (edited == savedDrawingText) throw new InvalidOperationException("Native inverse action did not stage an edit.");
                Click(Box<Button>(window, "Discard"));
                if (AssertNative(window, "inverse:b=no") != savedDrawingText || window.Session.HasChanges || Box<Button>(window, "Save").IsEnabled)
                    throw new InvalidOperationException("Discard did not restore the exact latest saved native text and clean state.");
                browser.CommandSearch.Text = "FIRE";
                browser.CommandsList.SelectedItem = browser.CommandsList.Items.Cast<SimCommand>().Single();
                assignments.CapturedAssignments.IsExpanded = true;
                assignments.Records.SelectedItem = assignments.Records.Items.Cast<WarThunderWorkspaceRow>().Single(row => row.Binding.Fields.Any(scalar => scalar.Path.EndsWith("/joyButton", StringComparison.Ordinal)));
                var sourceController = window.WorkspaceContext.State.Selection.ControllerId;
                var unchangedNative = window.Session.Export().Files.Single().Value;
                assignments.Editor.Index.SelectedValue = 3;
                assignments.Editor.ActionState(WorkspaceAssignmentAction.Apply);
                assignments.Editor.ActionState(WorkspaceAssignmentAction.AddAlternative);
                var reviewsBefore = assignments.Editor.NativeReviewComputations;
                var copiesBefore = window.Session.SnapshotCopies;
                for (var i = 0; i < 20; i++)
                {
                    assignments.Editor.ActionState(WorkspaceAssignmentAction.Apply);
                    assignments.Editor.ActionState(WorkspaceAssignmentAction.AddAlternative);
                }
                if (assignments.Editor.NativeReviewComputations != reviewsBefore || window.Session.SnapshotCopies != copiesBefore)
                    throw new InvalidOperationException("Repeated native draft availability revalidated or copied the unchanged document.");
                var profileName = Box<TextBox>(window, "ProfileName"); var originalName = profileName.Text;
                profileName.Text = originalName + " pending";
                store.FailSave = true;
                try
                {
                    if (window.SaveCurrent()) throw new InvalidOperationException("The pending-index fixture accepted a failed save.");
                }
                finally { store.FailSave = false; }
                ThemeControlSmoke.Pump(window);
                if (!Equals(assignments.Editor.Index.SelectedValue, 3) || !assignments.Editor.Apply.IsEnabled ||
                    profileName.Text != originalName + " pending" || window.WorkspaceContext.State.EditedName != profileName.Text ||
                    window.WorkspaceContext.State.Selection.ControllerId != sourceController ||
                    window.Session.Export().Files.Single().Value != unchangedNative)
                    throw new InvalidOperationException("Context notifications reset the pending native index, changed its browser controller, or edited data before Apply.");
                profileName.Text = originalName;
                var editsBeforeIndex = workflowCompletions;
                Click(assignments.Editor.Apply);
                AssertNative(window, "joyButton:i=3");
                if (workflowCompletions != editsBeforeIndex + 1)
                    throw new InvalidOperationException("The native index edit did not execute exactly once through the shared workflow.");
                if (!Equals(assignments.Editor.Index.SelectedValue, 3) || assignments.Editor.Apply.IsEnabled)
                    throw new InvalidOperationException("The applied native index was replaced by an old physical selection or remained falsely actionable.");
                ((MenuItem)assignments.Records.ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                if (!((WarThunderWorkspaceRow)assignments.Records.SelectedItem).Binding.Empty) throw new InvalidOperationException("Context clear did not empty the selected joystick alternative.");
                assignments.Editor.Index.SelectedValue = 3;
                Click(assignments.Editor.Add); AssertNative(window, "joyButton:i=3");
                var keyboard = assignments.Records.Items.Cast<WarThunderWorkspaceRow>().Single(row => row.Binding.Fields.Any(scalar => scalar.Path.EndsWith("/keyboardKey", StringComparison.Ordinal)));
                assignments.Records.SelectedItem = keyboard;
                if (assignments.Editor.Clear.IsEnabled) throw new InvalidOperationException("A preserved system record exposed unsupported mutation.");
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
                WarThunderAxisAssignmentSmoke.Verify(root, original);
                VerifyRemoval(window);
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
        private static void VerifyControllerAddition(string root)
        {
            var device = new DetectedDcsDevice(Guid.NewGuid(), "Added controller", true,
                ProductGuid: Guid.Parse("56781234-0000-0000-0000-504944564944"));
            var discovery = new GameSmokeDiscovery(device);
            discovery.Inputs[device.PhysicalGuid] = ControllerCapabilityMapper.Map([
                new(Guid.Parse("a36d02e0-c9f3-11cf-bfc7-444553540000"), 2u, "X") { Details = new(0, 256) },
                new(Guid.Parse("a36d02f0-c9f3-11cf-bfc7-444553540000"), 4u, "Button") { Details = new(48, 0) }]);
            var services = new GameWorkspaceServices(new FileGameWorkspaceStore(Path.Combine(root, "addition-store")), discovery);
            var path = Path.Combine(root, "addition.blk");
            const string source = "controls{version:i=5;hotkeys{ID_FIRE{keyboardKey:i=30;}}}";
            File.WriteAllText(path, source);
            var accept = false;
            var reviews = 0;
            using var window = new WarThunderWorkspaceWindow(services, pendingChoice: () => MessageBoxResult.No,
                reviewChange: _ => { reviews++; return accept; }, drawings: []);
            WorkspaceLayout.SetPersistenceEnabled(window, false);
            window.Show(); ThemeControlSmoke.Pump(window);
            window.ImportFile(path);
            var devices = window.DeviceView;
            if (!devices.Physical.IsEnabled)
                throw new InvalidOperationException("Adding a controller required an existing War Thunder mapping.");
            devices.Physical.SelectedItem = device;
            if (!devices.Add.IsEnabled) throw new InvalidOperationException("The connected controller could not be added.");
            Click(devices.Add);
            if (reviews != 1 || window.Session!.Snapshot.Profile.Devices.Count != 0 || window.Session.Export().Files.Values.Single() != source)
                throw new InvalidOperationException("Canceling Add controller changed the War Thunder profile.");
            accept = true; Click(devices.Add); ThemeControlSmoke.Pump(window);
            var created = WarThunderProfileBackend.ReadDevices(window.Session.Snapshot.Profile);
            if (reviews != 2 || created.Count != 1)
                throw new InvalidOperationException("Add controller did not complete its review and create one mapping. "
                    + Box<TextBox>(window, "ErrorDetails").Text);
            var native = created[0];
            if (native.Name != device.Name || native.AxesCount != 1 || native.ButtonsCount != 1
                || window.Session.Snapshot.Hardware[native.Id].Device.PhysicalGuid != device.PhysicalGuid
                || !Box<Button>(window, "Save").IsEnabled)
                throw new InvalidOperationException("Add controller failed to stage the native mapping and physical link.");
            if (!window.SaveCurrent()) throw new InvalidOperationException("The added controller could not be saved.");
            Click(devices.Add);
            devices.Physical.SelectedItem = device;
            if (devices.Add.IsEnabled) throw new InvalidOperationException("The same physical controller could be added twice.");
            window.Close();
        }

        private static void VerifyRemoval(WarThunderWorkspaceWindow window)
        {
            var before = window.Session!.Export().Files.Single().Value;
            Box<ListBox>(window, "FeaturePicker").SelectedIndex = 0;
            Box<ListBox>(window, "Controllers").SelectedIndex = 1;
            if (!window.DeviceView.Remove.IsEnabled) throw new InvalidOperationException("Selected native controller cannot be removed.");
            Click(window.DeviceView.Remove);
            if (window.Session.Snapshot.Profile.Devices.Count != 1 || !window.Session.HasChanges || !Box<Button>(window, "Save").IsEnabled)
                throw new InvalidOperationException("Reviewed controller removal was not staged and saveable.");
            Click(Box<Button>(window, "Discard"));
            if (window.Session.Snapshot.Profile.Devices.Count != 2 || window.Session.Export().Files.Single().Value != before)
                throw new InvalidOperationException("Discard did not restore the removed controller and its native assignments.");
        }
        private static void VerifyBlockedAxis(WarThunderWorkspaceWindow window)
        {
            var assignments = window.AssignmentView;
            assignments.Browser.CommandsList.SelectedItem = assignments.Browser.CommandsList.Items.Cast<SimCommand>()
                .Single(command => command.Metadata["war-thunder.command"] == "pitch");
            if (assignments.Editor.Target.SelectedItem is not WarThunderDevice) throw new InvalidOperationException("Native axis inspection did not select its saved owner.");
            var control = assignments.DrawingCanvas.Children.OfType<Border>().Single(border => border.Tag is HardwareGraphicControl graphic && graphic.InputKey == "JOY_X");
            control.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
            if (assignments.Editor.Target.SelectedItem is not null || assignments.Editor.Index.SelectedItem is not null || assignments.Editor.Apply.IsEnabled
                || assignments.Editor.Tune.IsEnabled)
                throw new InvalidOperationException("Unmapped axis retained a stale editable native target.");
            assignments.Records.SelectedItem = null;
            assignments.Records.SelectedIndex = 0;
            if (assignments.Editor.Target.SelectedItem is not WarThunderDevice || !assignments.Editor.Tune.IsEnabled ||
                assignments.Editor.Apply.IsEnabled || assignments.Editor.Apply.ToolTip is not string explanation ||
                !explanation.Contains("unchanged", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Explicit native record inspection did not recover its target and truthful unchanged-action state.");
        }
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
