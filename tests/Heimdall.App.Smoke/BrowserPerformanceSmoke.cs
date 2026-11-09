using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Heimdall.App.Games;
using Heimdall.App.Games.Elite;
using Heimdall.App.Games.Squadrons;
using Heimdall.App.Games.WarThunder;
using Heimdall.Application;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>Measures real mounted WPF browser searches, lifecycle and work counters in all four games.</summary>
    internal static class BrowserPerformanceSmoke
    {
        #region BEGIN Public
        internal static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "heimdall-browser-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var previous = EditionAccess.Policy; using var issuer = new OwnerLicenseTestIssuer();
            try
            {
                EditionAccess.SetForTests(issuer.Policy);
                var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
                var ed = new EliteWorkspaceWindow(Services(root, "ed"), [], pendingChoice: () => MessageBoxResult.No);
                ed.Show(); ed.ImportFile(Path.Combine(fixtures, "EliteDangerous", "Synthetic.4.2.binds"));
                Mount(ed); var edView = (EliteAssignmentsView)((ContentControl)ed.FindName("FeatureContent")).Content;
                Measure("Elite", ed, edView, edView.CommandSearch, edView.CommandsList, (Selector)ed.FindName("DevicePicker"), () => ed.Session!.SnapshotCopies);
                var sq = new SquadronsWorkspaceWindow(Services(root, "sq"), drawings: [], pendingChoice: () => MessageBoxResult.No);
                sq.Show(); sq.ImportFile(Path.Combine(fixtures, "Squadrons", "Synthetic_profile_synced"));
                Mount(sq); var sqView = (SquadronsAssignmentsView)((ContentControl)sq.FindName("FeatureContent")).Content;
                Measure("Squadrons", sq, sqView, sqView.CommandBrowser.CommandSearch, sqView.CommandBrowser.CommandsList, (Selector)sq.FindName("DevicePicker"), () => sq.Session!.SnapshotCopies);
                var wt = new WarThunderWorkspaceWindow(Services(root, "wt"), drawings: [], pendingChoice: () => MessageBoxResult.No);
                wt.Show(); wt.ImportFile(Path.Combine(fixtures, "WarThunder", "Synthetic.blk"));
                Mount(wt); var wtView = (WarThunderAssignmentsView)((ContentControl)wt.FindName("FeatureContent")).Content;
                Measure("War Thunder", wt, wtView, wtView.Browser.CommandSearch, wtView.Browser.CommandsList, (Selector)wt.FindName("Controllers"), () => wt.Session!.SnapshotCopies);
                var dcs = new DcsModuleWorkspaceWindow(new DcsModuleWorkspaceViewModel(MixedSnapshotFixture.Create()), persistLayout: false);
                dcs.Show(); var dcsView = (GameAssignmentsView)WorkspaceUi.Navigate(dcs, "Assignments");
                var manager = (Heimdall.App.Features.Commands.DcsCommandsManagementView)dcsView.FindName("CommandManager");
                Measure("DCS", dcs, dcsView, (TextBox)manager.FindName("CommandSearch"), (Selector)manager.FindName("Commands"), (Selector)dcs.FindName("ControllerPicker"), () => 0);
            }
            finally { EditionAccess.SetForTests(previous); Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static GameWorkspaceServices Services(string root, string name) => new(new FileGameWorkspaceStore(Path.Combine(root, name)), new GameSmokeDiscovery());
        private static void Mount(Window window) { ((Selector)window.FindName("FeaturePicker")).SelectedIndex = 1; ThemeControlSmoke.Pump(window); }
        private static void Measure(string game, Window window, GameAssignmentsView view, TextBox search, Selector commands, Selector controllers, Func<int> copies)
        {
            try
            {
                ThemeControlSmoke.Pump(window); search.Text = "no-such-command-9f49"; ThemeControlSmoke.Pump(window);
                search.Text = ""; ThemeControlSmoke.Pump(window);
                var browser = view.BoundBrowser ?? throw new InvalidOperationException(game + " did not bind the browser role.");
                var builds = browser.Metrics.ContentBuilds; var beforeCopies = copies();
                var frozen = ((GameWorkspaceWindow)window).WorkspaceContext.CaptureSnapshot();
                Sample(game, "search", window, view, copies, i => search.Text = i % 2 == 0 ? "a" : "no-such-command-9f49");
                search.Text = ""; ThemeControlSmoke.Pump(window);
                if (commands.Items.Count > 0) Sample(game, "command selection", window, view, copies, i => commands.SelectedIndex = i % commands.Items.Count);
                // Warm every available native controller/drawing scope before measuring repeated switches.
                for (var i = 0; i < controllers.Items.Count; i++) { controllers.SelectedIndex = i; ThemeControlSmoke.Pump(window); }
                if (controllers.Items.Count > 1) Sample(game, "controller/scope", window, view, copies, i => controllers.SelectedIndex = i % controllers.Items.Count);
                Console.WriteLine($"Browser UI {game}: projection-builds={browser.Metrics.ContentBuilds - builds}; initial UI-document-copies={beforeCopies}.");
                if (browser.Metrics.ContentBuilds != builds) throw new InvalidOperationException(game + " rebuilt native browser content during warm interaction.");
                if (((GameWorkspaceWindow)window).WorkspaceContext.CaptureSnapshot() != frozen) throw new InvalidOperationException(game + " browser interaction changed native data.");
                var refreshes = view.NativeRefreshPasses; var publications = browser.Metrics.Publications;
                for (var i = 0; i < 5; i++) { browser.Refresh(); ThemeControlSmoke.Pump(window); }
                if (view.NativeRefreshPasses != refreshes || browser.Metrics.Publications != publications)
                    throw new InvalidOperationException(game + " repeated an identical browser projection or native panel refresh.");
                // Remount the same cached view: its role must remain alive, without reconstructing native content.
                if (window is not DcsModuleWorkspaceWindow)
                {
                    ((Selector)window.FindName("FeaturePicker")).SelectedIndex = 0; ThemeControlSmoke.Pump(window);
                    ((Selector)window.FindName("FeaturePicker")).SelectedIndex = 1; ThemeControlSmoke.Pump(window);
                    if (view.BoundBrowser != browser) throw new InvalidOperationException(game + " replaced its cached browser on navigation.");
                }
            }
            finally { window.Close(); }
        }
        private static void Sample(string game, string interaction, Window window, GameAssignmentsView view, Func<int> copies, Action<int> action)
        {
            var samples = new List<double>(); var before = copies(); var allocated = GC.GetAllocatedBytesForCurrentThread();
            var refreshes = view.NativeRefreshPasses; var publications = view.BoundBrowser!.Metrics.Publications;
            for (var i = 0; i < 20; i++)
            {
                var watch = Stopwatch.StartNew(); action(i); ThemeControlSmoke.Pump(window); samples.Add(watch.Elapsed.TotalMilliseconds);
            }
            samples.Sort(); var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Console.WriteLine($"Browser UI {game} {interaction}: p50={samples[9]:F2}ms p95={samples[18]:F2}ms allocation={bytes}B UI-document-copies={copies() - before} native-refreshes={view.NativeRefreshPasses - refreshes} publications={view.BoundBrowser.Metrics.Publications - publications}.");
            if (samples[18] > 100) throw new InvalidOperationException(game + " " + interaction + " p95 exceeded 100ms; inspect the reported work before merging.");
        }
        #endregion END private helpers
    }
}
