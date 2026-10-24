using System.IO;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.App.Games;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises the Squadrons game card, real WPF editor, saved profiles, filtering, exports and pending-change guards.</summary>
    public static class SquadronsWorkspaceSmoke
    {
        #region BEGIN Public
        /// <summary>Runs only against synthetic captures and temporary stores; no installed game or physical controller is required.</summary>
        public static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "squadrons-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var previous = EditionAccess.Policy; using var issuer = new OwnerLicenseTestIssuer();
            try
            {
                EditionAccess.SetForTests(new EditionPolicy());
                var cards = new GameSelectionView();
                var card = (Button)cards.FindName("SquadronsCard");
                if (card.IsEnabled) throw new InvalidOperationException("Free edition exposed Squadrons authoring.");
                var requested = ""; cards.GameRequested += (_, game) => requested = game;
                card.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (requested.Length != 0) throw new InvalidOperationException("Disabled game card bypassed edition policy.");
                EditionAccess.SetForTests(issuer.Policy); cards.SetRecent(new("star-wars-squadrons", DisplayName: "Space Controls"));
                card.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (!card.IsEnabled || requested != "star-wars-squadrons" || !((TextBlock)cards.FindName("RecentText")).Text.Contains("Squadrons", StringComparison.Ordinal))
                    throw new InvalidOperationException("Squadrons card or recent workspace label is not activated.");
                var services = new GameWorkspaceServices(new FileGameWorkspaceStore(root), new GameSmokeDiscovery());
                var choice = MessageBoxResult.Cancel;
                var source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced");
                var window = new SquadronsWorkspaceWindow(services, pendingChoice: () => choice);
                window.Show(); ThemeControlSmoke.Pump(window); window.ImportFile(source);
                if (!Box<Button>(window, "SaveButton").IsEnabled || Box<Button>(window, "ExportButton").IsEnabled)
                    throw new InvalidOperationException("An imported capture must be saved before export.");
                var grid = Box<DataGrid>(window, "BindingsGrid");
                grid.SelectedItem = grid.Items.Cast<SquadronsWorkspaceRow>().Single(x => x.Native.Type == 2);
                if (Box<Button>(window, "ApplyButton").IsEnabled) throw new InvalidOperationException("Preserved keyboard record became editable.");
                grid.SelectedItem = grid.Items.Cast<SquadronsWorkspaceRow>().Single(x => x.RecordId == "IncomStarshipInputConcepts.ConceptThrottle.4");
                var targets = Box<ComboBox>(window, "TargetDevice"); targets.SelectedItem = targets.Items.Cast<GameDeviceChoice>().Single(x => x.Id == "squadrons.joystick.10");
                var inputs = Box<ComboBox>(window, "InputPicker"); inputs.SelectedItem = inputs.Items.Cast<SquadronsInput>().Single(x => x.Key == "JOY_Z" && x.Direction == 1);
                Click(window, "ApplyButton");
                if (new SquadronsProfileBackend().ReadBindings(window.Session!.Snapshot.Profile).Single(x => x.RecordId.EndsWith("Throttle.4", StringComparison.Ordinal)).DeviceId != 9)
                    throw new InvalidOperationException("The UI did not stage the explicit native controller assignment.");
                Box<ComboBox>(window, "TypePicker").SelectedItem = "Axes";
                Box<TextBox>(window, "Search").Text = "Throttle";
                if (grid.Items.Count != 2) throw new InvalidOperationException("Axis/search filters lost native direction records.");
                Box<TextBox>(window, "ProfileName").Text = "Smoke Space Controls";
                if (!window.SaveProfile()) throw new InvalidOperationException("Squadrons profile did not save.");
                var id = window.Session.Snapshot.Id;
                var exported = window.ExportReview(root);
                if (!File.Exists(Path.Combine(exported, SquadronsProfileBackend.FileName))) throw new InvalidOperationException("Review export is missing its exact native filename.");
                Box<TextBox>(window, "ProfileName").Text = "Pending name";
                Box<ComboBox>(window, "GamePicker").SelectedIndex = 1;
                if (window.RequestedGame is not null || !window.IsVisible || Box<ComboBox>(window, "GamePicker").SelectedIndex != 2)
                    throw new InvalidOperationException("Cancel failed to retain the Squadrons workspace.");
                Click(window, "DiscardButton");
                ThemeControlSmoke.Pump(window); ThemeControlSmoke.SaveImage(window, "squadrons-workspace");
                window.Close();
                var reopened = new SquadronsWorkspaceWindow(services, id, () => MessageBoxResult.No);
                reopened.Show(); ThemeControlSmoke.Pump(reopened);
                if (reopened.Session!.Snapshot.Name != "Smoke Space Controls" || services.Recent().BackendId != "star-wars-squadrons")
                    throw new InvalidOperationException("Saved Squadrons workspace or recent game failed to reopen.");
                Box<ComboBox>(reopened, "GamePicker").SelectedIndex = 0;
                if (reopened.RequestedGame != "dcs" || reopened.IsVisible) throw new InvalidOperationException("Saved game transition did not return to DCS.");
            }
            finally { EditionAccess.SetForTests(previous); if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static T Box<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
        private static void Click(FrameworkElement window, string name) => Box<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        #endregion END private helpers
    }
}
