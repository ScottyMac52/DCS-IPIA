using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
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
            using var window = new SquadronsWorkspaceWindow(new(store, new GameSmokeDiscovery()), drawings: [], pendingChoice: () => MessageBoxResult.Cancel);
            window.Show(); ThemeControlSmoke.Pump(window);
            Window host = window;
            Require(host is GameWorkspaceWindow, "Squadrons did not instantiate the common window base.");
            Require(!window.SaveProfile() && store.Writes == 0, "An unopened Squadrons workspace reported a successful save.");
            window.ImportFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var feature = (GameFeatureNavigation)window.FindName("FeaturePicker");
            feature.SelectedIndex = 1; ThemeControlSmoke.Pump(window);
            var content = (ContentControl)window.FindName("FeatureContent");
            Require(content.Content is GameAssignmentsView, "Squadrons did not mount the common assignment-view base.");
            var assignments = (SquadronsAssignmentsView)content.Content;
            Require(window.Session!.AccessMessage.Length == 0, "The Squadrons inheritance fixture is not writable.");
            window.Width = 1050; window.Height = 650;
            var devices = (ListBox)window.FindName("DevicePicker");
            devices.SelectedItem = devices.Items.Cast<GameDeviceChoice>().Single(item => item.Id == "system.2");
            assignments.CommandBrowser.CommandsList.SelectedIndex = 0;
            assignments.CapturedAssignments.IsExpanded = true;
            ThemeControlSmoke.Pump(window);
            Require(assignments.DrawingScroll.Visibility == Visibility.Visible && assignments.DrawingScroll.ActualHeight >= 150 &&
                assignments.CommandBrowser.TranslatePoint(new Point(), window).X < assignments.DrawingScroll.TranslatePoint(new Point(), window).X,
                "The inherited Squadrons layout did not preserve usable controller space beside the commands.");
            assignments.ReverseAxisButton.BringIntoView(); ThemeControlSmoke.Pump(window);
            var inputBounds = assignments.ReverseAxisButton.TransformToAncestor(window).TransformBounds(new Rect(assignments.ReverseAxisButton.RenderSize));
            var inspectorBounds = assignments.InspectorViewport.TransformToAncestor(window).TransformBounds(new Rect(assignments.InspectorViewport.RenderSize));
            Require(inspectorBounds.Contains(inputBounds), "The inherited Squadrons inspector could not scroll to its final native action.");
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
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        #endregion END private helpers
    }
}
