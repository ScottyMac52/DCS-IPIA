using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;
using Heimdall.Infrastructure;
using Heimdall.App;
using Microsoft.Extensions.Logging;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises the actual WPF failure handlers rather than only calling log helpers.</summary>
    internal static class ModuleOutputLoggingSmoke
    {
        #region BEGIN Public
        /// <summary>Checks successful duplicate-binding output and logging of three UI failures.</summary>
        public static void Verify(string connection, IEditorWorkspaceRepository repository)
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-output-log-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            ModuleWorkspaceWindow? editor = null;
            MainWindow? main = null;
            try
            {
                var saved = AxisCleanupFlyNowSmoke.Workspace(Guid.NewGuid(), "OUTPUT-LOG-TEST");
                repository.SaveAsync(saved).GetAwaiter().GetResult();
                var settings = new SqliteAppSettingsService(connection, Path.Combine(root, "defaults.json"),
                    Path.Combine(root, "settings.json"), Path.Combine(root, "theme.txt"));
                var destination = Path.Combine(root, "DCS");
                Directory.CreateDirectory(destination);
                settings.TryUpdateSavedGamesDcsFolder(destination);
                var output = new RecordingModuleOutputLogger<ModuleWorkspaceWindow>();
                var exportPath = Path.Combine(root, "snapshot.heimdall-module.json");
                editor = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(saved), settings, repository,
                    deviceDiscovery: new EmptyDeviceDiscovery(), outputPathPicker: _ => exportPath, logger: output);
                editor.Show();
                // Duplicate import evidence must also pass the real validation and snapshot export actions.
                ((TextBox)WorkspaceUi.Find(editor, "PackageVersion")!).Text = "1.0.0.0";
                Click((Button)WorkspaceUi.Find(editor, "ValidateModuleButton")!);
                Wait(editor, () => ((TextBlock)WorkspaceUi.Find(editor, "BuildStatus")!).Text.StartsWith("Validated", StringComparison.Ordinal));
                Click((Button)WorkspaceUi.Find(editor, "ExportModuleSnapshotButton")!);
                Wait(editor, () => ((TextBlock)WorkspaceUi.Find(editor, "BuildStatus")!).Text.StartsWith("Exported module snapshot:", StringComparison.Ordinal));
                if (!File.Exists(exportPath) || output.Events.Count != 0)
                    throw new InvalidOperationException("Duplicate imported bindings blocked saved-module output.");
                editor.Close();
                var sources = new Dictionary<string, string>(saved.Sources!) { ["config/integrations.json"] = "{" };
                var broken = saved with { Sources = sources };
                editor = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(broken), settings, repository,
                    deviceDiscovery: new EmptyDeviceDiscovery(), outputPathPicker: _ => exportPath, logger: output);
                editor.Show();
                foreach (var (button, operation) in new[] { ("ValidateModuleButton", "Validate"),
                    ("ExportModuleSnapshotButton", "Export module snapshot") })
                {
                    ((TextBox)WorkspaceUi.Find(editor, "PackageVersion")!).Text = "1.0.0.0";
                    var count = output.Events.Count;
                    Click((Button)WorkspaceUi.Find(editor, button)!);
                    Wait(editor, () => output.Events.Count > count && ((Button)WorkspaceUi.Find(editor, button)!).IsEnabled);
                    VerifyEvent(output.Events[^1], saved.Configuration.Module.Id, saved.Configuration.Module.DcsId, operation);
                    var status = ((TextBlock)WorkspaceUi.Find(editor, "BuildStatus")!).Text;
                    if (!status.StartsWith("Module output failed:", StringComparison.Ordinal) ||
                        !((TextBlock)WorkspaceUi.Find(editor, "OutputFailureDiagnostic")!).Text.StartsWith(operation + ":", StringComparison.Ordinal))
                        throw new InvalidOperationException("Output failure lost visible status or Diagnostics feedback.");
                }
                editor.Close();
                editor = null;
                var fly = new RecordingModuleOutputLogger<MainWindow>();
                main = new MainWindow(new ModuleWorkspaceLoader(), new DcsFolderImporter(), settings, repository,
                    flyNow: new FailingModuleOutputFlyNow(), deviceDiscovery: new EmptyDeviceDiscovery(), logger: fly);
                main.Show();
                var modules = (ComboBox)WorkspaceUi.Find(main, "SavedModules")!;
                Wait(main, () => modules.Items.Count > 0 && ((TextBlock)WorkspaceUi.Find(main, "FlyNowStatus")!).Text.StartsWith("No module", StringComparison.Ordinal));
                modules.SelectedItem = modules.Items.Cast<SavedModuleChoice>().Single(x => x.Id == saved.Configuration.Module.Id);
                Click((Button)WorkspaceUi.Find(main, "FlyNowButton")!);
                Wait(main, () => fly.Events.Count == 1 && ((Button)WorkspaceUi.Find(main, "FlyNowButton")!).IsEnabled);
                VerifyEvent(fly.Events[0], saved.Configuration.Module.Id, saved.Configuration.Module.DcsId, "Fly Now!");
                if (fly.Events[0].Exception?.Message != FailingModuleOutputFlyNow.Detail ||
                    !((TextBlock)WorkspaceUi.Find(main, "ImportStatus")!).Text.Contains(FailingModuleOutputFlyNow.Detail, StringComparison.Ordinal))
                    throw new InvalidOperationException("Fly Now lost exception details or visible feedback.");
            }
            finally { editor?.Close(); main?.Close(); Directory.Delete(root, true); }
        }

        #endregion END Public
        #region BEGIN private helpers
        private static void VerifyEvent(ModuleOutputLogEntry entry, Guid moduleId, string dcsId, string operation)
        {
            if (entry.Level != LogLevel.Error || entry.EventId.Id != 1015 || entry.Exception is null ||
                !entry.Message.Contains(moduleId.ToString(), StringComparison.Ordinal) ||
                !entry.Message.Contains(dcsId, StringComparison.Ordinal) || !entry.Message.Contains(operation, StringComparison.Ordinal))
                throw new InvalidOperationException("Module operation did not log its exception, operation and module identity.");
        }
        private static void Click(Button button)
        {
            if (!button.IsEnabled) throw new InvalidOperationException("Requested module operation is disabled.");
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(button.Dispatcher));
            try { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }
        private static void Wait(Window window, Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(90);
            while (!ready())
            {
                if (DateTime.UtcNow > deadline) throw new InvalidOperationException("Module output/logging smoke timed out.");
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            }
        }
        #endregion END private helpers
    }
}

