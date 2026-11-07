using Heimdall.App.Games.WarThunder;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.App.Games;
using Heimdall.Infrastructure;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises War Thunder through the inherited window/view algorithms with real local profile persistence.</summary>
    internal static class WarThunderInheritanceSmoke
    {
        #region BEGIN Public
        internal static void Verify(string root)
        {
            var store = new InterceptedGameWorkspaceStore(new FileGameWorkspaceStore(Path.Combine(root, "WarThunderInheritance")));
            using var window = new WarThunderWorkspaceWindow(new(store, new GameSmokeDiscovery()), drawings: [], pendingChoice: () => MessageBoxResult.Cancel);
            WorkspaceLayout.SetPersistenceEnabled(window, false);
            window.Width = 1050; window.Height = 650;
            window.Show(); ThemeControlSmoke.Pump(window);
            var save = (Button)window.FindName("Save");
            var export = (Button)window.FindName("Export");
            var game = (ComboBox)window.FindName("GamePicker");
            Require(Math.Abs(save.TranslatePoint(new Point(), window).Y - export.TranslatePoint(new Point(), window).Y) < 1 &&
                Math.Abs(game.TranslatePoint(new Point(), window).Y - export.TranslatePoint(new Point(), window).Y) < 3,
                "The compact War Thunder header orphaned an action below the selectors.");
            Window host = window;
            Require(host is GameWorkspaceWindow, "War Thunder did not instantiate the common window base.");
            Require(!window.SaveCurrent() && store.Writes == 0, "An unopened War Thunder workspace reported a successful save.");
            window.ImportFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "WarThunder", "Synthetic.blk"));
            var feature = (GameFeatureNavigation)window.FindName("FeaturePicker");
            feature.SelectedIndex = 1; ThemeControlSmoke.Pump(window);
            var content = (ContentControl)window.FindName("FeatureContent");
            Require(content.Content is GameAssignmentsView, "War Thunder did not mount the common assignment-view base.");
            var assignments = (WarThunderAssignmentsView)content.Content;
            Require(window.Session!.AccessMessage.Length == 0, "The War Thunder inheritance fixture is not writable.");
            var before = JsonSerializer.Serialize(window.Session.Snapshot);
            var beforeProfile = JsonSerializer.Serialize(window.Session.Snapshot.Profile);
            var name = (TextBox)window.FindName("ProfileName"); name.Text = "Inherited War Thunder profile";
            store.FailSave = true;
            store.BeforeSave = () =>
            {
                Require(!window.SaveCurrent(), "An overlapping War Thunder save reported success.");
                var discardBlocked = false;
                try { window.DiscardClicked(window, new()); }
                catch (InvalidOperationException) { discardBlocked = true; }
                Require(discardBlocked, "War Thunder discarded pending changes during persistence.");
                var applied = false;
                var result = assignments.ExecuteNativeAssignment("command", "input", "Primary", () => new(true, true, ""), () => applied = true);
                Require(result.Outcome == DraftOutcome.Blocked && !applied, "War Thunder ignored the common busy assignment gate.");
                feature.SelectedIndex = 0; ThemeControlSmoke.Pump(window);
                feature.SelectedIndex = 1; ThemeControlSmoke.Pump(window);
                Require(ReferenceEquals(content.Content, assignments), "Inherited War Thunder navigation replaced the cached assignment view.");
                window.Close();
                Require(window.IsVisible, "War Thunder closed while native persistence was running.");
            };
            Require(!window.SaveCurrent() && store.Writes == 1, "Failed War Thunder persistence succeeded or performed an overlapping write.");
            Require(JsonSerializer.Serialize(window.Session.Snapshot) == before && name.Text == "Inherited War Thunder profile" &&
                ((TextBlock)window.FindName("SavedState")).Text == "Unsaved changes", "War Thunder save failure lost the reviewed pending state.");
            Require(!assignments.SaveAssignmentsAsync().GetAwaiter().GetResult() && store.Writes == 2,
                "The War Thunder assignment view reported failed persistence as successful.");
            Require(JsonSerializer.Serialize(window.Session.Snapshot) == before && name.Text == "Inherited War Thunder profile",
                "The War Thunder assignment save lost pending native state.");
            store.BeforeSave = null; store.FailSave = false;
            Require(assignments.SaveAssignmentsAsync().GetAwaiter().GetResult() && store.Writes == 3, "War Thunder could not retry saving after failure.");
            var persisted = store.Load(window.Session!.Snapshot.Id).Workspace;
            Require(persisted.Name == "Inherited War Thunder profile" && JsonSerializer.Serialize(persisted.Profile) == beforeProfile &&
                ((TextBlock)window.FindName("SavedState")).Text == "Saved", "Inherited War Thunder save changed native bindings or failed to establish its baseline.");
            name.Text = "Unsaved rename"; window.Close();
            Require(window.IsVisible, "War Thunder ignored Cancel when closing with pending changes.");
            window.DiscardClicked(window, new());
            Require(name.Text == "Inherited War Thunder profile" && ((TextBlock)window.FindName("SavedState")).Text == "Saved",
                "Inherited War Thunder discard did not restore the native baseline and edited profile name.");
            window.Close();
            Require(!window.IsVisible && content.Content is null, "Closing War Thunder did not release the inherited feature host.");
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
