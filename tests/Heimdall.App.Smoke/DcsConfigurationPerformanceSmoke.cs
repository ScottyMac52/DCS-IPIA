using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>Measures an editable DCS module round-trip against its verified OVGME payload.</summary>
    internal static class DcsConfigurationPerformanceSmoke
    {
        #region BEGIN Public
        /// <summary>Restores into independent databases, exercises WPF and rebuilds through the production output buttons.</summary>
        internal static void Verify(string input, string output, int iterations, Action<string, bool, Action> step)
        {
            DcsModuleWorkspaceWindow? window = null;
            var companion = Environment.GetEnvironmentVariable("HEIMDALL_SMOKE_DCS_SNAPSHOT");
            var companionHash = companion is null ? null : SHA256.HashData(File.ReadAllBytes(companion));
            var companionUnchanged = true;
            try
            {
                if (companion is not null)
                {
                    var copy = Path.Combine(output, "input", "authoring.heimdall-module.json");
                    File.Copy(companion, copy); companion = copy;
                }
                DcsModuleBuildSnapshot? candidate = null;
                Measure("scenario.package-validation", false, () => candidate = Await(Task.Run(() => DcsPerformancePackageInput.Read(input, companion))));
                var inputs = candidate ?? throw new InvalidOperationException("Package inputs were not loaded.");
                var original = DcsEditorSnapshotSerializer.Deserialize(inputs.Workspace);
                var export = Path.Combine(output, "export"); Directory.CreateDirectory(export);
                var repository = Repository(Path.Combine(output, "creation.db"));
                Measure("scenario.creation", false, () =>
                {
                    var restored = Await(Task.Run(() => DcsModuleSnapshotWorkflow.RestoreAsync(inputs, repository)));
                    window = Open(restored, repository, export, inputs.Version);
                });
                Measure("scenario.reopen", false, () =>
                {
                    window!.Close();
                    var saved = Await(Task.Run(() => repository.GetAsync(original.Configuration.Module.Id)))
                        ?? throw new InvalidOperationException("Created module was not persisted.");
                    Require(DcsEditorSnapshotSerializer.Serialize(saved) == inputs.Workspace, "Reopening changed editable module content.");
                    window = Open(saved, repository, export, inputs.Version);
                });
                Exercise(window!, iterations, Measure);
                Require(DcsEditorSnapshotSerializer.Serialize(window!.DcsContext.Working!) == inputs.Workspace && !window.Session.HasPendingChanges,
                    "Browsing changed module assignments, modifiers, drawings or pages.");
                Measure("scenario.ovgme-export", false, () =>
                {
                    Measure("scenario.ovgme-build", false, () => Output(window, "BuildButton", "Wrote "));
                    Measure("scenario.snapshot-export", false, () => Output(window, "ExportModuleSnapshotButton", "Exported module snapshot:"));
                    Measure("scenario.ovgme-verification", false, () =>
                    {
                        var generated = Directory.GetFiles(export, "*-OVGME.zip").Single();
                        Await(Task.Run(() =>
                        {
                            DcsPerformancePackageInput.Compare(DcsModuleSnapshotWorkflow.Build(inputs), DcsPerformancePackageInput.Package(generated));
                            return true;
                        }));
                    });
                });
                Measure("scenario.reimport", false, () =>
                {
                    window.Close();
                    var generated = Directory.GetFiles(export, "*-OVGME.zip").Single();
                    var second = Await(Task.Run(() => DcsPerformancePackageInput.Read(generated, Path.Combine(export, "module.heimdall-module.json"))));
                    Require(second.Workspace == inputs.Workspace, "Re-import changed editable module data.");
                    var fresh = Repository(Path.Combine(output, "reimport.db"));
                    var restored = Await(Task.Run(() => DcsModuleSnapshotWorkflow.RestoreAsync(second, fresh)));
                    window = Open(restored, fresh, Path.Combine(output, "second-export"), second.Version);
                });
                Exercise(window!, iterations, Measure);
                Require(DcsEditorSnapshotSerializer.Serialize(window.DcsContext.Working!) == inputs.Workspace && !window.Session.HasPendingChanges,
                    "Browsing changed the re-imported module.");
            }
            finally
            {
                window?.Close();
                var source = Environment.GetEnvironmentVariable("HEIMDALL_SMOKE_DCS_SNAPSHOT");
                if (source is not null) companionUnchanged = companionHash!.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source)));
            }
            Require(companionUnchanged, "The original module snapshot changed.");

            void Measure(string name, bool interactive, Action action) => step(name, interactive, () =>
            {
                action(); if (window is not null) ThemeControlSmoke.Pump(window);
            });
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DcsSqliteEditorWorkspaceRepository Repository(string file)
        {
            var connection = "Data Source=" + file + ";Pooling=False";
            Await(Task.Run(async () => { await new SqliteMigrationRunner(connection).MigrateAsync(); return true; }));
            return new(connection);
        }
        private static DcsModuleWorkspaceWindow Open(DcsEditorWorkspaceSnapshot saved, DcsSqliteEditorWorkspaceRepository repository, string export, string version)
        {
            Directory.CreateDirectory(export);
            var window = new DcsModuleWorkspaceWindow(new(saved), repository: repository, deviceDiscovery: new EmptyDeviceDiscovery(),
                outputPathPicker: snapshot => snapshot ? Path.Combine(export, "module.heimdall-module.json") : export, persistLayout: false);
            window.Show(); ((TextBox)WorkspaceUi.Find(window, "PackageVersion")!).Text = version;
            ThemeControlSmoke.Pump(window); return window;
        }
        private static void Exercise(DcsModuleWorkspaceWindow window, int iterations, Action<string, bool, Action> step)
        {
            foreach (var feature in new[] { "Devices", "Commands", "Assignments", "Kneeboards", "Pages" })
                step("scenario.navigation", true, () => WorkspaceUi.Navigate(window, feature));
            var assignments = WorkspaceUi.Navigate(window, "Assignments");
            var manager = (Heimdall.App.Features.Commands.DcsCommandsManagementView)assignments.FindName("CommandManager");
            var search = (TextBox)manager.FindName("CommandSearch");
            var commands = (Selector)manager.FindName("Commands");
            var controllers = (Selector)window.FindName("ControllerPicker");
            var visitedControllers = new HashSet<int> { controllers.SelectedIndex };
            for (var i = 0; i < iterations; i++)
            {
                if (controllers.Items.Count > 0)
                    ConfigurationPerformanceSmoke.SelectController(controllers, i % controllers.Items.Count, visitedControllers, step);
                var text = i % 2 == 0 ? "a" : "no-such-command-performance-smoke";
                step("scenario.search", true, () => search.Text = text);
                step("scenario.search", true, () => search.Text = "");
                if (commands.Items.Count > 0)
                {
                    var index = i % commands.Items.Count;
                    step("scenario.command-selection", true, () => commands.SelectedIndex = index);
                }
            }
        }
        private static void Output(DcsModuleWorkspaceWindow window, string buttonName, string success)
        {
            var button = (Button)WorkspaceUi.Find(window, buttonName)!;
            Require(button.IsEnabled, "DCS output is blocked: " + buttonName);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var watch = Stopwatch.StartNew();
            var status = (TextBlock)WorkspaceUi.Find(window, "BuildStatus")!;
            while (!status.Text.StartsWith(success, StringComparison.Ordinal) || !button.IsEnabled)
            {
                if (status.Text.StartsWith("Module output failed:", StringComparison.Ordinal) || watch.Elapsed > TimeSpan.FromSeconds(120))
                    throw new InvalidOperationException("DCS output did not complete: " + status.Text);
                ThemeControlSmoke.Pump(window); Thread.Sleep(1);
            }
        }
        private static T Await<T>(Task<T> task)
        {
            var watch = Stopwatch.StartNew();
            while (!task.IsCompleted)
            {
                if (watch.Elapsed > TimeSpan.FromSeconds(120)) throw new TimeoutException("DCS package operation did not complete.");
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Thread.Sleep(1);
            }
            return task.GetAwaiter().GetResult();
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        #endregion END private helpers
    }
}
