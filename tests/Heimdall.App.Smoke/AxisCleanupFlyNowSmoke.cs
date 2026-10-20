using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Heimdall.App;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks direct Fly Now deployment of an imported module with an unused controller.</summary>
    public static class AxisCleanupFlyNowSmoke
    {
        #region BEGIN Public
        /// <summary>Captures native units without first opening the module editor.</summary>
        /// <param name="connection">Migrated smoke database.</param>
        /// <param name="repository">Saved module repository.</param>
        public static void Verify(string connection, IEditorWorkspaceRepository repository)
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-axis-fly-" + Guid.NewGuid().ToString("N"));
            MainWindow? window = null;
            try
            {
                var saved = Workspace(Guid.NewGuid());
                repository.SaveAsync(saved).GetAwaiter().GetResult();
                // Fail before a UI error dialog if selecting this catalog-free imported module cannot reopen its draft.
                _ = new ModuleWorkspaceViewModel(repository.GetAsync(saved.Configuration.Module.Id).GetAwaiter().GetResult()!);
                var destination = Path.Combine(root, "DCS");
                var unused = new DetectedDcsDevice(Guid.NewGuid(), "Unused quadrant", false).FileName;
                var target = Path.Combine(destination, "Config", "Input", saved.Configuration.Module.DcsId, "joystick", unused);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, "local diff = {} return diff");
                var settings = new SqliteAppSettingsService(connection, Path.Combine(root, "defaults.json"), Path.Combine(root, "settings.json"), Path.Combine(root, "theme.txt"));
                settings.TryUpdateSavedGamesDcsFolder(destination);
                var service = new SqliteFlyNowService(connection, Path.Combine(root, "backups"), new FlyNowPayloadBuilder(repository));
                window = new MainWindow(new ModuleWorkspaceLoader(), new DcsFolderImporter(), settings, repository, flyNow: service, deviceDiscovery: new EmptyDeviceDiscovery());
                window.Show();
                var modules = (ComboBox)window.FindName("SavedModules")!;
                Wait(window, () => modules.Items.Count > 0 && ((TextBlock)window.FindName("FlyNowStatus")!).Text.StartsWith("No module", StringComparison.Ordinal));
                modules.SelectedItem = modules.Items.Cast<SavedModuleChoice>().Single(x => x.Id == saved.Configuration.Module.Id);
                var previous = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
                try { ((Button)window.FindName("FlyNowButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
                finally { SynchronizationContext.SetSynchronizationContext(previous); }
                // The success text precedes the awaited journal refresh. Wait for the UI operation to release its lease.
                Wait(window, () => ((TextBlock)window.FindName("ImportStatus")!).Text.StartsWith("Fly Now! deployed", StringComparison.Ordinal) &&
                    MainWindowCommands.ClearDeployment.CanExecute(null, window));
                var reopened = repository.GetAsync(saved.Configuration.Module.Id).GetAwaiter().GetResult()!;
                var captured = JsonSerializer.Deserialize<string[]>(reopened.Sources!["config/ipia-axis-cleanup-devices.json"])!;
                if (!captured.Contains(unused) || saved.Configuration.Profiles.Any(x => !captured.Contains(x.SourceName)))
                    throw new InvalidOperationException("Direct Fly Now did not persist unused and authored controller identities.");
                var bindings = DcsLuaParser.ParseProfile(File.ReadAllText(target), Guid.NewGuid());
                if (bindings.Count(x => x.CommandId == "a2001cdnil" && x.Disposition == BindingDisposition.Removed) != 8)
                    throw new InvalidOperationException("Direct Fly Now left unused pitch axes without deployed removals.");
                var assignedPath = Path.Combine(destination, "Config", "Input", saved.Configuration.Module.DcsId, "joystick", saved.Configuration.Profiles[0].SourceName);
                var assigned = DcsLuaParser.ParseProfile(File.ReadAllText(assignedPath), saved.Configuration.Profiles[0].Id);
                if (assigned.Count(x => x.CommandId == "a2001cdnil" && x.InputKey == "JOY_Y" && x.Disposition == BindingDisposition.Added) != 1 ||
                    assigned.Any(x => x.CommandId == "a2001cdnil" && x.InputKey == "JOY_Y" && x.Disposition == BindingDisposition.Removed))
                    throw new InvalidOperationException("Direct Fly Now changed the authored pitch assignment.");
                if (!assigned.Any(x => x.CommandId == "a2002cdnil" && x.InputKey == "JOY_Y" && x.Disposition == BindingDisposition.Removed) ||
                    assigned.Any(x => x.CommandId == "a2002cdnil" && x.InputKey == "JOY_Y" && x.Disposition != BindingDisposition.Removed))
                    throw new InvalidOperationException("Direct Fly Now retained a conflicting imported roll assignment.");
                if (!service.GetActiveAsync().GetAwaiter().GetResult()!.Files.Any(x => x.EndsWith('/' + unused, StringComparison.Ordinal)))
                    throw new InvalidOperationException("Fly Now did not journal the cleanup-only profile.");
                service.ClearAsync().GetAwaiter().GetResult();
                if (File.Exists(target)) throw new InvalidOperationException("Clear left the cleanup-only profile behind.");
            }
            finally { window?.Close(); if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        internal static EditorWorkspaceSnapshot Workspace(Guid moduleId, string dcsId = "AXIS-TEST")
        {
            var profileId = Guid.NewGuid();
            var file = new DetectedDcsDevice(Guid.NewGuid(), "Authored stick", false).FileName;
            var profile = new DeviceProfile(profileId, "stick", null, "Stick", file);
            var diagram = new UiLayerDiagram("Stick", null, "stick.svg", 100, 100, [], []) {
                ProfileId = profileId, Controls = [new("pitch", "Pitch", "JOY_Y", 0, 0, 10, 10, true)],
                Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><rect width='100' height='100' fill='black'/><text id='lbl-pitch' x='10' y='20' fill='white'>Pitch</text></svg>"
            };
            var sources = new Dictionary<string, string> {
                [WorkspaceManagement.SourcePath] = JsonSerializer.Serialize(new WorkspaceManagement([
                    new(profileId, AssignmentLayer.Module, file, null, "Stick")], [])),
                ["config/kneeboard.json"] = JsonSerializer.Serialize(new {
                    profiles = new Dictionary<string, string> { ["stick"] = file },
                    pages = new[] { new { profile = "stick", file = "01-STICK", title = "Stick" } },
                    summaryPages = new[] { new { type = "summary", file = "00-OVERVIEW", title = "Overview", kicker = "CONTROLS",
                        items = new[] { new { key = "STICK", text = "Flight controls", accent = "cyan" } } } }
                })
            };
            var pages = new KneeboardPage[] {
                new(Guid.NewGuid(), moduleId, null, KneeboardPageKind.Device, "01-STICK", "Stick", [], 1),
                new(Guid.NewGuid(), moduleId, null, KneeboardPageKind.Summary, "00-OVERVIEW", "Overview", [], 0)
            };
            var original = new DcsBinding[] {
                new(profileId, BindingSection.Axis, "a2001cdnil", "Pitch", BindingDisposition.Added, "JOY_Y"),
                new(profileId, BindingSection.Axis, "a2001cdnil", "Pitch", BindingDisposition.Added, "JOY_Y"),
                new(profileId, BindingSection.Axis, "a2002cdnil", "Roll", BindingDisposition.Added, "JOY_Y")
            };
            var workspace = new EditorWorkspaceSnapshot(new(new DcsModule(moduleId, dcsId, "Axis test"), [profile], [], original, "Imported from Saved Games"),
                pages, [], [diagram], [], [],
                [new(new(moduleId, profileId, AssignmentLayer.Module, BindingSection.Axis, "JOY_Y"), new("a2001cdnil", "Pitch", true, AssignmentLayer.Module))],
                new Dictionary<string, string>(), sources);
            return ModulePhysicalBindingReview.Confirm(workspace, profileId, DcsPhysicalIdentity.FromFileName(file)!.Value);
        }
        private static void Wait(MainWindow window, Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(90);
            while (!ready())
            {
                var status = ((TextBlock)window.FindName("ImportStatus")!).Text;
                if (status.StartsWith("Fly Now! did not complete:", StringComparison.Ordinal) || DateTime.UtcNow > deadline)
                    throw new InvalidOperationException("Direct axis cleanup deployment failed: " + status);
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            }
        }
        #endregion END private helpers
    }
}


