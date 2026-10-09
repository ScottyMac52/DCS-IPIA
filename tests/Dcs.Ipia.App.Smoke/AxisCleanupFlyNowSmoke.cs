using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Checks direct Fly Now deployment of a pre-cleanup locally created module.</summary>
    public static class AxisCleanupFlyNowSmoke
    {
        #region BEGIN Public
        /// <summary>Captures native units without first opening the module editor.</summary>
        /// <param name="connection">Migrated smoke database.</param>
        /// <param name="repository">Saved module repository.</param>
        /// <param name="created">Locally created module with native controller identities.</param>
        public static void Verify(string connection, IEditorWorkspaceRepository repository, EditorWorkspaceSnapshot created)
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-axis-fly-" + Guid.NewGuid().ToString("N"));
            MainWindow? window = null;
            try
            {
                var sources = created.Sources!.Where(x => x.Key != "config/ipia-axis-cleanup-devices.json").ToDictionary(x => x.Key, x => x.Value);
                var saved = created with { Sources = sources, ModuleCommands = [.. created.ModuleCommands.Where(x => x.Id != "a2001cdnil"), new("a2001cdnil", "Pitch", "Axes", true)] };
                repository.SaveAsync(saved).GetAwaiter().GetResult();
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
                Wait(window, () => ((TextBlock)window.FindName("ImportStatus")!).Text.StartsWith("Fly Now! deployed", StringComparison.Ordinal));
                var reopened = repository.GetAsync(saved.Configuration.Module.Id).GetAwaiter().GetResult()!;
                var captured = JsonSerializer.Deserialize<string[]>(reopened.Sources!["config/ipia-axis-cleanup-devices.json"])!;
                if (!captured.Contains(unused) || saved.Configuration.Profiles.Any(x => !captured.Contains(x.SourceName)))
                    throw new InvalidOperationException("Direct Fly Now did not persist unused and authored controller identities.");
                var bindings = DcsLuaParser.ParseProfile(File.ReadAllText(target), Guid.NewGuid());
                if (bindings.Count(x => x.CommandId == "a2001cdnil" && x.Disposition == BindingDisposition.Removed) != 8)
                    throw new InvalidOperationException("Direct Fly Now left unused pitch axes without deployed removals.");
                if (!service.GetActiveAsync().GetAwaiter().GetResult()!.Files.Any(x => x.EndsWith('/' + unused, StringComparison.Ordinal)))
                    throw new InvalidOperationException("Fly Now did not journal the cleanup-only profile.");
                service.ClearAsync().GetAwaiter().GetResult();
                if (File.Exists(target)) throw new InvalidOperationException("Clear left the cleanup-only profile behind.");
            }
            finally { window?.Close(); if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
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
