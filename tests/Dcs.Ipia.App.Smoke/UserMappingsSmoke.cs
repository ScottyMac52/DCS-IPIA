using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Infrastructure;
using DcsIpia.Presentation;
using Microsoft.Data.Sqlite;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Exercises installed-style preview, immutable publication, reopening and explicit staged adoption through WPF.</summary>
    public static class UserMappingsSmoke
    {
        #region BEGIN Public
        /// <summary>Verifies real file/library/UI flows without interactive file picker input.</summary>
        public static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-mappings-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var connection = "Data Source=" + Path.Combine(root, "ipia.db") + ";Pooling=False";
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var capabilities = ControllerCapabilityMapper.Map(Enumerable.Range(0, 4).Select(x =>
                    new ControllerObject(Guid.Parse("a36d02f0-c9f3-11cf-bfc7-444553540000"), 12u | ((uint)x << 8), "Button " + (x + 1))));
                var definition = new HardwareDefinition(ControllerCapabilityMapper.Fingerprint(capabilities), capabilities);
                var source = Path.Combine(root, "custom.drawio");
                File.WriteAllText(source, """
                    <mxfile><diagram><mxGraphModel pageWidth="900" pageHeight="600"><root>
                    <mxCell id="0"/><mxCell id="1" parent="0"/>
                    <mxCell id="canvas" parent="1" vertex="1" style="fillColor=#ffffff;"><mxGeometry width="900" height="600"/></mxCell>
                    <mxCell id="label-JOY_BTN1" parent="1" vertex="1" value="Trigger"><mxGeometry x="10" y="100" width="300" height="50"/></mxCell>
                    <mxCell id="footer" parent="1" vertex="1" value="Custom smoke"><mxGeometry x="0" y="575" width="900" height="25"/></mxCell>
                    </root></mxGraphModel></diagram></mxfile>
                    """);
                var library = new FileUserMappingLibrary(root);
                var tools = new UserMappingsWindow(library, [definition], () => source);
                tools.Show();
                Pump(tools, () => ((Button)tools.FindName("PreviewMappingButton")!).IsEnabled);
                Click((Button)tools.FindName("PreviewMappingButton")!);
                Pump(tools, () => ((TextBlock)tools.FindName("MappingStatus")!).Text.StartsWith("Verified preview:", StringComparison.Ordinal));
                if (library.ListAsync().GetAwaiter().GetResult().Count != 0) throw new InvalidOperationException("Preview mutated the library.");
                Click((Button)tools.FindName("SaveMappingButton")!);
                Pump(tools, () => ((TextBlock)tools.FindName("MappingStatus")!).Text.StartsWith("Saved immutable revision", StringComparison.Ordinal));
                tools.Close(); File.Delete(source);
                var revision = library.ListAsync().GetAwaiter().GetResult().Single();
                var bundle = Path.Combine(root, "editable.zip");
                var reopened = new UserMappingsWindow(library, [definition], exportPicker: () => bundle);
                reopened.Show();
                var revisions = (ListBox)reopened.FindName("MappingRevisions")!;
                Pump(reopened, () => revisions.Items.Count == 1);
                revisions.SelectedIndex = 0; Click((Button)reopened.FindName("ExportMappingButton")!);
                if (!File.Exists(bundle)) throw new InvalidOperationException("Saved mapping did not export without its source file.");
                reopened.Close();
                var defaults = new SharedUiLayerWorkspace(0, new string('a', 40), new Dictionary<string, string>
                { ["assets/shared/ui-layer/functions.json"] = """{"functions":[{"id":"pause","command":"d3001pnilu3001cdnilvd1vpnilvunil","label":"Pause","category":"General"}]}""" });
                var workspace = NewModuleWorkspaceFactory.Create("MAPPING-SMOKE", "Mapping smoke", defaults,
                    new Dictionary<string, string> { ["config/ipia-hardware.json"] = "[]" }, true);
                workspace = InventoryHardwareAuthoring.Add(workspace, new(new(Guid.NewGuid(), "Panel", definition.Id, "Panel", "", true, []), definition), AssignmentLayer.Module);
                var repository = new SqliteEditorWorkspaceRepository(connection);
                repository.SaveAsync(workspace).GetAwaiter().GetResult();
                var editor = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(workspace), repository: repository, userMappings: library);
                editor.Show();
                var choices = (ComboBox)editor.FindName("CustomMappingRevisions")!;
                Pump(editor, () => choices.Items.Count == 1);
                choices.SelectedIndex = 0;
                ((DataGrid)editor.FindName("ManagedDevices")!).SelectedIndex = 0;
                Click((Button)editor.FindName("AdoptCustomMappingButton")!);
                if (!((TextBlock)editor.FindName("DevicesMessage")!).Text.StartsWith("Change staged", StringComparison.Ordinal))
                    throw new InvalidOperationException("Custom mapping adoption was not staged.");
                if (repository.GetAsync(workspace.Configuration.Module.Id).GetAwaiter().GetResult()!.Diagrams.Any(x => x.CustomMapping is not null))
                    throw new InvalidOperationException("Staged mapping silently changed saved module data.");
                Click((Button)editor.FindName("DiscardAuthoringButton")!);
                choices.SelectedIndex = 0; ((DataGrid)editor.FindName("ManagedDevices")!).SelectedIndex = 0;
                Click((Button)editor.FindName("AdoptCustomMappingButton")!);
                Click((Button)editor.FindName("SaveAuthoringButton")!);
                Pump(editor, () => ((TextBlock)editor.FindName("PendingSummary")!).Text == "No pending assignment changes");
                editor.Close();
                if (repository.GetAsync(workspace.Configuration.Module.Id).GetAwaiter().GetResult()!.Diagrams.Single().CustomMapping?.Id != revision.Id)
                    throw new InvalidOperationException("Saved custom mapping lost its immutable revision.");
                Console.WriteLine("Custom mapping preview, save, source-free reopen/export and staged/discarded/saved adoption passed.");
            }
            finally { SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Click(Button button)
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(button.Dispatcher));
            try { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }
        private static void Pump(Window window, Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!ready())
            {
                if (DateTime.UtcNow >= deadline) throw new InvalidOperationException("Custom mapping UI timed out.");
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            }
        }
        #endregion END private helpers
    }
}
