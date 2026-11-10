using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Heimdall.App.Games;
using Heimdall.App.Games.Elite;
using Heimdall.App.Games.Squadrons;
using Heimdall.App.Games.WarThunder;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises a supplied native configuration through real WPF creation, reopen, native review export and re-import.</summary>
    internal static class ConfigurationPerformanceSmoke
    {
        private static readonly JsonSerializerOptions ReportJson = new() { WriteIndented = true };
        #region BEGIN Public
        /// <summary>Runs one isolated Profile-build workflow and preserves local telemetry and failure evidence.</summary>
        internal static int Run(string game, string input, string parent)
        {
#if !PERFORMANCE_TRACE
            throw new InvalidOperationException("Configuration performance smoke requires -c Profile.");
#else
            if (game is not "elite" and not "squadrons" and not "war-thunder" and not "dcs") throw new ArgumentException("Game must be dcs, elite, squadrons or war-thunder.");
            input = Path.GetFullPath(input);
            var originalHash = SHA256.HashData(File.ReadAllBytes(input));
            var output = Path.Combine(Path.GetFullPath(parent), "configuration-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output);
            var copied = Path.Combine(output, "input", Path.GetFileName(input));
            Directory.CreateDirectory(Path.GetDirectoryName(copied)!); File.Copy(input, copied);
            var previousOutput = Environment.GetEnvironmentVariable("HEIMDALL_PERFORMANCE_DIRECTORY");
            Environment.SetEnvironmentVariable("HEIMDALL_PERFORMANCE_DIRECTORY", output);
            var previousPolicy = EditionAccess.Policy;
            var previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            using var issuer = new OwnerLicenseTestIssuer();
            EditionAccess.SetForTests(issuer.Policy);
            var steps = new List<ConfigurationPerformanceStep>();
            var budget = ReadLimit("HEIMDALL_SMOKE_UI_BUDGET_MS", 1000, 1, 60000);
            var iterations = ReadLimit("HEIMDALL_SMOKE_ITERATIONS", 20, 1, 1000);
            string? failure = null;
            GameWorkspaceWindow? window = null;
            Func<GameProfileSession?> session = () => null;
            Action<string> import = _ => { };
            using var capture = new PerformanceSession(Dispatcher.CurrentDispatcher);
            try
            {
                if (game == "dcs") DcsConfigurationPerformanceSmoke.Verify(copied, output, iterations, Step);
                else
                {
                    var store = new FileGameWorkspaceStore(Path.Combine(output, "store"));
                    var services = new GameWorkspaceServices(store, new GameSmokeDiscovery());
                    Step("scenario.creation", false, () =>
                    {
                        (window, session, import) = Open(game, services, null);
                        import(copied); Wait(window, () => session() is not null);
                        Require(store.List().Count == 1 && !session()!.HasChanges, "Import did not create one saved HEIMDALL configuration.");
                    });
                    var initial = session()!.ContentFingerprint;
                    var id = session()!.Id;
                    Step("scenario.reopen", false, () =>
                    {
                        Close(window!);
                        (window, session, import) = Open(game, services, id);
                        Wait(window, () => session() is not null);
                        Require(session()!.ContentFingerprint == initial, "Reopening changed the saved configuration.");
                    });
                    Exercise(window!, iterations, Step);
                    Require(session()!.ContentFingerprint == initial && !session()!.HasChanges, "Browsing changed assignments or controller identity.");
                    IReadOnlyDictionary<string, string> exported = new Dictionary<string, string>();
                    string? exportedFile = null;
                    Step("scenario.native-export", false, () =>
                    {
                        var plan = session()!.Export();
                        Require(plan.Succeeded, "Native export refused: " + string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
                        exported = plan.Files;
                        var folder = GameReviewExport.Write(Path.Combine(output, "export"), exported, [Path.GetDirectoryName(copied)!], session()!.Snapshot.Profile.BackendId);
                        exportedFile = Directory.GetFiles(folder).Single();
                    });
                    Step("scenario.reimport", false, () =>
                    {
                        Close(window!);
                        var freshStore = new FileGameWorkspaceStore(Path.Combine(output, "reimport-store"));
                        (window, session, import) = Open(game, new(freshStore, new GameSmokeDiscovery()), null);
                        import(exportedFile!); Wait(window, () => session() is not null);
                        Require(freshStore.List().Count == 1 && !session()!.HasChanges, "Re-import did not create an independent saved configuration.");
                        var second = session()!.Export();
                        Require(second.Succeeded && exported.OrderBy(x => x.Key, StringComparer.Ordinal).SequenceEqual(second.Files.OrderBy(x => x.Key, StringComparer.Ordinal)),
                            "Native export/re-import/export changed the payload, including bindings or unknown native fields.");
                    });
                    var reimported = session()!.ContentFingerprint;
                    Exercise(window!, iterations, Step);
                    Require(session()!.ContentFingerprint == reimported && !session()!.HasChanges, "Browsing changed the re-imported configuration.");
                }
                Require(steps.Where(x => x.Interactive).All(x => x.DurationMs <= budget), "One or more UI interactions exceeded the configured budget.");
                Require(!PerformanceTrace.Snapshot().Any(sample => sample.Operation == "dispatcher.heartbeat-delay" && sample.DurationMs > budget),
                    "Dispatcher heartbeat delay exceeded the configured UI budget during the workflow.");
                Require(PerformanceTrace.Dropped == 0, "Telemetry was truncated; shorten the scenario.");
            }
            catch (Exception error) { failure = error.ToString(); Console.Error.WriteLine(error); }
            finally
            {
                try { if (window is not null) Close(window); }
                catch (Exception error) { failure ??= error.ToString(); }
                if (!originalHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(input)))) failure = "The provided source file changed during the scenario.";
                // Capture is written before restoring its output destination, including on scenario failure.
                capture.Dispose();
                if (Directory.GetFiles(output, "capture-*.json").Length != 1) failure ??= "Performance capture was not written.";
                EditionAccess.SetForTests(previousPolicy);
                SynchronizationContext.SetSynchronizationContext(previousContext);
                Environment.SetEnvironmentVariable("HEIMDALL_PERFORMANCE_DIRECTORY", previousOutput);
                File.WriteAllText(Path.Combine(output, "scenario.json"), JsonSerializer.Serialize(new
                {
                    Game = game, Passed = failure is null, Failure = failure, UiBudgetMs = budget, Iterations = iterations,
                    ExportMode = game == "dcs" ? "OVGME build plus editable snapshot round-trip; no installation" : "Native identity-preserving review copy; no live hardware remapping or installation preflight", Steps = steps
                }, ReportJson));
                Console.WriteLine("Configuration performance artifacts: " + output);
            }
            Console.WriteLine(failure is null ? "CONFIGURATION-PERFORMANCE-PASS" : "CONFIGURATION-PERFORMANCE-FAIL");
            return failure is null ? 0 : 1;

            void Step(string name, bool interactive, Action action)
            {
                if (!interactive) Console.WriteLine("Performance phase: " + name);
                var started = Stopwatch.GetTimestamp(); var allocated = GC.GetTotalAllocatedBytes();
                using var timing = PerformanceTrace.Measure(name, typeof(ConfigurationPerformanceSmoke));
                try { action(); if (window is not null) ThemeControlSmoke.Pump(window); }
                finally { steps.Add(new(name, Stopwatch.GetElapsedTime(started).TotalMilliseconds, GC.GetTotalAllocatedBytes() - allocated, interactive)); }
            }
#endif
        }
        #endregion END Public
        #region BEGIN private helpers
        private static (GameWorkspaceWindow, Func<GameProfileSession?>, Action<string>) Open(string game, GameWorkspaceServices services, Guid? initial)
        {
            switch (game)
            {
                case "elite":
                    var ed = new EliteWorkspaceWindow(services, [], initial, pendingChoice: () => MessageBoxResult.No);
                    ed.Show(); return (ed, () => ed.Session, ed.ImportFile);
                case "squadrons":
                    var sq = new SquadronsWorkspaceWindow(services, initial, pendingChoice: () => MessageBoxResult.No, drawings: []);
                    sq.Show(); return (sq, () => sq.Session, sq.ImportFile);
                default:
                    var wt = new WarThunderWorkspaceWindow(services, initial, pendingChoice: () => MessageBoxResult.No, drawings: []);
                    wt.Show(); return (wt, () => wt.Session, wt.ImportFile);
            }
        }
        private static void Exercise(GameWorkspaceWindow window, int iterations, Action<string, bool, Action> step)
        {
            step("scenario.navigation", true, () => ((Selector)window.FindName("FeaturePicker")).SelectedIndex = 1);
            var view = (GameAssignmentsView)((ContentControl)window.FindName("FeatureContent")).Content;
            var search = ThemeControlSmoke.Children(view).OfType<TextBox>().First(x => x.Name == "CommandSearch");
            var commands = ThemeControlSmoke.Children(view).OfType<Selector>().First(x => x.Name == "CommandsList");
            var controllers = (Selector)(window.FindName("DevicePicker") ?? window.FindName("Controllers"));
            for (var i = 0; i < iterations; i++)
            {
                if (controllers.Items.Count > 0)
                {
                    var controllerIndex = i % controllers.Items.Count;
                    step("scenario.controller-selection", true, () => controllers.SelectedIndex = controllerIndex);
                }
                var term = i % 2 == 0 ? "a" : "no-such-command-performance-smoke";
                step("scenario.search", true, () => search.Text = term);
                step("scenario.search", true, () => search.Text = "");
                if (commands.Items.Count > 0)
                {
                    var index = i % commands.Items.Count;
                    step("scenario.command-selection", true, () => commands.SelectedIndex = index);
                }
            }
        }
        private static void Wait(Window window, Func<bool> done)
        {
            var deadline = Stopwatch.StartNew();
            while (!done())
            {
                if (deadline.Elapsed > TimeSpan.FromSeconds(120)) throw new TimeoutException("Configuration operation did not complete within 120 seconds.");
                ThemeControlSmoke.Pump(window); Thread.Sleep(1);
            }
            ThemeControlSmoke.Pump(window);
        }
        private static void Close(GameWorkspaceWindow window) { window.Close(); (window as IDisposable)?.Dispose(); }
        private static int ReadLimit(string name, int fallback, int minimum, int maximum)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (value is null) return fallback;
            if (!int.TryParse(value, out var parsed) || parsed < minimum || parsed > maximum) throw new ArgumentException("Invalid " + name);
            return parsed;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        #endregion END private helpers
    }
}
