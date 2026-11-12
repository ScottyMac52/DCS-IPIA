using Heimdall.App.Games.Elite;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.App.Games;
using Heimdall.Infrastructure;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises ED through the inherited window/view algorithms with real local profile persistence.</summary>
    internal static class EliteInheritanceSmoke
    {
        #region BEGIN Public
        internal static void Verify(string root)
        {
            var store = new InterceptedGameWorkspaceStore(new FileGameWorkspaceStore(Path.Combine(root, "EliteInheritance")));
            using var window = new EliteWorkspaceWindow(new(store, new GameSmokeDiscovery()), [], pendingChoice: () => MessageBoxResult.Cancel);
            window.Width = 1050; window.Height = 650;
            window.Show(); ThemeControlSmoke.Pump(window);
            var save = (Button)window.FindName("SaveProfileButton");
            var export = (Button)window.FindName("ExportPresetButton");
            var game = (ComboBox)window.FindName("GamePicker");
            Require(Math.Abs(save.TranslatePoint(new Point(), window).Y - export.TranslatePoint(new Point(), window).Y) < 1 &&
                Math.Abs(game.TranslatePoint(new Point(), window).Y - export.TranslatePoint(new Point(), window).Y) < 3,
                "The compact ED header orphaned an action below the selectors.");
            Window host = window;
            Require(host is GameWorkspaceWindow, "ED did not instantiate the common window base.");
            Require(!window.SaveProfile() && store.Writes == 0, "An unopened ED workspace reported a successful save.");
            window.ImportFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "EliteDangerous", "Synthetic.4.2.binds"));
            var feature = (GameFeatureNavigation)window.FindName("FeaturePicker");
            feature.SelectedIndex = 1; ThemeControlSmoke.Pump(window);
            var content = (ContentControl)window.FindName("FeatureContent");
            Require(content.Content is GameAssignmentsView, "ED did not mount the common assignment-view base.");
            var assignments = (EliteAssignmentsView)content.Content;
            AssignmentEditingConformanceSmoke.Verify(assignments, window.WorkspaceContext);
            Require(window.Session!.AccessMessage.Length == 0, "The ED inheritance fixture is not writable.");
            var before = JsonSerializer.Serialize(window.Session.Snapshot);
            var beforeProfile = JsonSerializer.Serialize(window.Session.Snapshot.Profile);
            var name = (TextBox)window.FindName("ProfileName"); name.Text = "Inherited ED profile";
            store.FailSave = true;
            store.BeforeSave = () =>
            {
                Require(!window.SaveProfile(), "An overlapping ED save reported success.");
                var discardBlocked = false;
                try { window.DiscardClicked(window, new()); }
                catch (InvalidOperationException) { discardBlocked = true; }
                Require(discardBlocked, "ED discarded pending changes during persistence.");
                var applied = false;
                var result = assignments.ExecuteNativeAssignment("command", "input", "Primary", () => new(true, true, ""), () => applied = true);
                Require(result.Outcome == DraftOutcome.Blocked && !applied, "ED ignored the common busy assignment gate.");
                feature.SelectedIndex = 0; ThemeControlSmoke.Pump(window);
                feature.SelectedIndex = 1; ThemeControlSmoke.Pump(window);
                Require(ReferenceEquals(content.Content, assignments), "Inherited ED navigation replaced the cached assignment view.");
                window.Close();
                Require(window.IsVisible, "ED closed while native persistence was running.");
            };
            Require(!window.SaveProfile() && store.Writes == 1, "Failed ED persistence succeeded or performed an overlapping write.");
            Require(JsonSerializer.Serialize(window.Session.Snapshot) == before && name.Text == "Inherited ED profile" &&
                ((TextBlock)window.FindName("SaveState")).Text == "Unsaved changes", "ED save failure lost the reviewed pending state.");
            store.BeforeSave = null; store.FailSave = false;
            Require(window.SaveProfile() && store.Writes == 2, "ED could not retry saving after failure.");
            var persisted = store.Load(window.Session!.Snapshot.Id).Workspace;
            Require(persisted.Name == "Inherited ED profile" && JsonSerializer.Serialize(persisted.Profile) == beforeProfile &&
                ((TextBlock)window.FindName("SaveState")).Text == "Saved", "Inherited ED save changed native bindings or failed to establish its baseline.");
            name.Text = "Unsaved rename"; window.Close();
            Require(window.IsVisible, "ED ignored Cancel when closing with pending changes.");
            window.DiscardClicked(window, new());
            Require(name.Text == "Inherited ED profile" && ((TextBlock)window.FindName("SaveState")).Text == "Saved",
                "Inherited ED discard did not restore the native baseline and edited profile name.");
            window.Close();
            Require(!window.IsVisible && content.Content is null, "Closing ED did not release the inherited feature host.");
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
