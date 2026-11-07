using Heimdall.App.Games.Squadrons;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.App.Games;
using Heimdall.Infrastructure;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises Squadrons through the inherited window/view algorithms with real local profile persistence.</summary>
    internal static class SquadronsInheritanceSmoke
    {
        #region BEGIN Public
        internal static void Verify(string root)
        {
            var store = new InterceptedGameWorkspaceStore(new FileGameWorkspaceStore(Path.Combine(root, "SquadronsInheritance")));
            VerifyControllerSelection(new(store, new GameSmokeDiscovery()));
            using var window = new SquadronsWorkspaceWindow(new(store, new GameSmokeDiscovery()), drawings: [], pendingChoice: () => MessageBoxResult.Cancel);
            // Presentation changes in this fixture must not affect later native-layout scenarios.
            WorkspaceLayout.SetPersistenceEnabled(window, false);
            window.Show(); ThemeControlSmoke.Pump(window);
            Window host = window;
            Require(host is GameWorkspaceWindow, "Squadrons did not instantiate the common window base.");
            Require(!window.SaveProfile() && store.Writes == 0, "An unopened Squadrons workspace reported a successful save.");
            window.ImportFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            Require(((ListBox)window.FindName("DevicePicker")).SelectedItem is GameDeviceChoice { Id: "all" } &&
                !((Button)window.FindName("LinkControllerButton")).IsEnabled && !((Button)window.FindName("ClearSlotButton")).IsEnabled,
                "Import did not retain aggregate browsing with controller-only actions disabled.");
            var feature = (GameFeatureNavigation)window.FindName("FeaturePicker");
            feature.SelectedIndex = 1; ThemeControlSmoke.Pump(window);
            var content = (ContentControl)window.FindName("FeatureContent");
            Require(content.Content is GameAssignmentsView, "Squadrons did not mount the common assignment-view base.");
            var assignments = (SquadronsAssignmentsView)content.Content;
            Require(window.Session!.AccessMessage.Length == 0, "The Squadrons inheritance fixture is not writable.");
            window.Width = 1050; window.Height = 650;
            ThemeControlSmoke.Pump(window);
            var save = (Button)window.FindName("SaveButton");
            var export = (Button)window.FindName("ExportButton");
            var game = (ComboBox)window.FindName("GamePicker");
            Require(Math.Abs(save.TranslatePoint(new Point(), window).Y - export.TranslatePoint(new Point(), window).Y) < 1 &&
                Math.Abs(game.TranslatePoint(new Point(), window).Y - export.TranslatePoint(new Point(), window).Y) < 3,
                "The compact Squadrons header orphaned an action below the selectors.");
            var devices = (ListBox)window.FindName("DevicePicker");
            devices.SelectedItem = devices.Items.Cast<GameDeviceChoice>().Single(item => item.Id == "system.2");
            assignments.CommandBrowser.CommandsList.SelectedIndex = 0;
            assignments.CapturedAssignments.IsExpanded = true;
            assignments.DrawingPanel.IsExpanded = true;
            ThemeControlSmoke.Pump(window);
            Require(assignments.DrawingScroll.Visibility == Visibility.Visible && assignments.DrawingScroll.ActualHeight >= 150 &&
                assignments.CommandBrowser.TranslatePoint(new Point(), window).X < assignments.DrawingScroll.TranslatePoint(new Point(), window).X,
                $"The inherited Squadrons layout did not preserve usable controller space beside the commands. " +
                $"Viewport visibility={assignments.DrawingScroll.Visibility}, height={assignments.DrawingScroll.ActualHeight:F1}, " +
                $"commands X={assignments.CommandBrowser.TranslatePoint(new Point(), window).X:F1}, " +
                $"drawing X={assignments.DrawingScroll.TranslatePoint(new Point(), window).X:F1}, " +
                $"panel height={assignments.DrawingPanel.ActualHeight:F1}, view height={assignments.ActualHeight:F1}, " +
                $"reserve={assignments.ControllerReserveHeight:F1}, expanded={assignments.DrawingPanel.IsExpanded}, " +
                $"source={(devices.SelectedItem as GameDeviceChoice)?.Id}, status={((TextBlock)window.FindName("StatusText")).Text}.");
            Require(assignments.DrawingScroll.TranslatePoint(new Point(0, assignments.DrawingScroll.ActualHeight), assignments.DrawingPanel).Y
                <= assignments.DrawingPanel.ActualHeight + 1,
                "The Squadrons drawing viewport extends outside its bounded controller panel.");
            assignments.ReverseAxisButton.BringIntoView(); ThemeControlSmoke.Pump(window);
            var inputBounds = assignments.ReverseAxisButton.TransformToAncestor(window).TransformBounds(new Rect(assignments.ReverseAxisButton.RenderSize));
            var inspectorBounds = assignments.InspectorViewport.TransformToAncestor(window).TransformBounds(new Rect(assignments.InspectorViewport.RenderSize));
            Require(inspectorBounds.Contains(inputBounds), $"The inherited Squadrons inspector could not scroll to its final native action. Input={inputBounds}; inspector={inspectorBounds}.");
            devices.SelectedIndex = 0;
            assignments.CapturedAssignments.IsExpanded = false;
            ThemeControlSmoke.Pump(window);
            var before = JsonSerializer.Serialize(window.Session.Snapshot);
            var beforeProfile = JsonSerializer.Serialize(window.Session.Snapshot.Profile);
            var name = (TextBox)window.FindName("ProfileName"); name.Text = "Inherited Squadrons profile";
            store.FailSave = true;
            store.BeforeSave = () =>
            {
                Require(!window.SaveProfile(), "An overlapping Squadrons save reported success.");
                var discardBlocked = false;
                try { window.DiscardClicked(window, new()); }
                catch (InvalidOperationException) { discardBlocked = true; }
                Require(discardBlocked, "Squadrons discarded pending changes during persistence.");
                var applied = false;
                var result = assignments.ExecuteNativeAssignment("command", "input", "record", () => new(true, true, ""), () => applied = true);
                Require(result.Outcome == DraftOutcome.Blocked && !applied, "Squadrons ignored the common busy assignment gate.");
                feature.SelectedIndex = 0; ThemeControlSmoke.Pump(window);
                feature.SelectedIndex = 1; ThemeControlSmoke.Pump(window);
                Require(ReferenceEquals(content.Content, assignments), "Inherited Squadrons navigation replaced the cached assignment view.");
                window.Close();
                Require(window.IsVisible, "Squadrons closed while native persistence was running.");
            };
            Require(!window.SaveProfile() && store.Writes == 1, "Failed Squadrons persistence succeeded or performed an overlapping write.");
            Require(JsonSerializer.Serialize(window.Session.Snapshot) == before && name.Text == "Inherited Squadrons profile" &&
                ((TextBlock)window.FindName("SaveState")).Text == "Unsaved changes", "Squadrons save failure lost the reviewed pending state.");
            Require(!assignments.SaveAssignmentsAsync().GetAwaiter().GetResult() && store.Writes == 2,
                "The Squadrons assignment view reported failed persistence as successful.");
            Require(JsonSerializer.Serialize(window.Session.Snapshot) == before && name.Text == "Inherited Squadrons profile",
                "The Squadrons assignment save lost pending native state.");
            store.BeforeSave = null; store.FailSave = false;
            Require(assignments.SaveAssignmentsAsync().GetAwaiter().GetResult() && store.Writes == 3, "Squadrons could not retry saving after failure.");
            var persisted = store.Load(window.Session!.Snapshot.Id).Workspace;
            Require(persisted.Name == "Inherited Squadrons profile" && JsonSerializer.Serialize(persisted.Profile) == beforeProfile &&
                ((TextBlock)window.FindName("SaveState")).Text == "Saved", "Inherited Squadrons save changed native bindings or failed to establish its baseline.");
            name.Text = "Unsaved rename"; window.Close();
            Require(window.IsVisible, "Squadrons ignored Cancel when closing with pending changes.");
            window.DiscardClicked(window, new());
            Require(name.Text == "Inherited Squadrons profile" && ((TextBlock)window.FindName("SaveState")).Text == "Saved",
                "Inherited Squadrons discard did not restore the native baseline and edited profile name.");
            window.Close();
            Require(!window.IsVisible && content.Content is null, "Closing Squadrons did not release the inherited feature host.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void VerifyControllerSelection(GameWorkspaceServices services)
        {
            using var context = new SquadronsWorkspaceContext(services);
            var profile = context.Backend.Import("controls", new Dictionary<string, string>
            {
                [Heimdall.Squadrons.SquadronsProfileBackend.FileName] = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"))
            });
            context.Session = new(context.Backend, new(1, Guid.NewGuid(), "Selection", profile, new()), EditionAccess.Policy, [], () => []);
            var frozen = JsonSerializer.Serialize(context.Session.Snapshot);
            foreach (var id in new string?[] { null, "all", "system.0", "system.1", "system.2", "unknown", "squadrons.joystick.999" })
            {
                context.Select(context.State.Selection with { ControllerId = id });
                Require(context.SelectedController is null, "A non-controller or missing slot became a selected native controller: " + id);
                Require(context.State.Selection.ControllerId == id, "Controller eligibility changed the browser selection.");
            }
            foreach (var device in profile.Devices)
            {
                context.Select(context.State.Selection with { ControllerId = device.LogicalId });
                Require(context.SelectedController == device.LogicalId, "An existing native slot was rejected.");
            }
            Require(JsonSerializer.Serialize(context.Session.Snapshot) == frozen, "Controller selection edited the native profile.");
            context.Session = null;
            Require(context.SelectedController is null, "Closing the profile retained a selected native slot.");
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        #endregion END private helpers
    }
}
