using System.IO;
using System.Text.Json;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Infrastructure;
using Heimdall.App;
using Microsoft.Data.Sqlite;

namespace Heimdall.App.Smoke
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
                    <mxCell id="label-trigger" parent="1" vertex="1" value="Trigger"><mxGeometry x="10" y="100" width="300" height="50"/></mxCell>
                    <mxCell id="footer" parent="1" vertex="1" value="Custom smoke"><mxGeometry x="0" y="575" width="900" height="25"/></mxCell>
                    </root></mxGraphModel></diagram></mxfile>
                    """);
                var library = new FileUserMappingLibrary(root);
                var empty = new UserMappingsWindow(library, [], canDiscoverHardware: true);
                empty.Show();
                Pump(empty, () => ((TextBlock)WorkspaceUi.Find(empty, "MappingStatus")!).Text.Contains("No controllers are available", StringComparison.Ordinal));
                if (((Button)WorkspaceUi.Find(empty, "PreviewMappingButton")!).IsEnabled ||
                    ((Button)WorkspaceUi.Find(empty, "SaveMappingButton")!).IsEnabled ||
                    !((Button)WorkspaceUi.Find(empty, "DiscoverMappingHardwareButton")!).IsEnabled)
                    throw new InvalidOperationException("Empty drawing import does not explain and expose hardware discovery.");
                Click((Button)WorkspaceUi.Find(empty, "DiscoverMappingHardwareButton")!);
                if (!empty.DiscoverHardwareRequested || empty.IsVisible ||
                    library.ListAsync().GetAwaiter().GetResult().Count != 0)
                    throw new InvalidOperationException("Discovery handoff changed saved drawings or failed to close the import.");
                var tools = new UserMappingsWindow(library, [definition], () => source, bundledDrawingsDirectory: root,
                    bundledInputs: new Dictionary<string, IReadOnlyDictionary<string, string>>
                    { ["custom.drawio"] = new Dictionary<string, string> { ["trigger"] = "JOY_BTN1" } });
                tools.Show();
                Pump(tools, () => ((Button)WorkspaceUi.Find(tools, "PreviewMappingButton")!).IsEnabled);
                if (!((Button)WorkspaceUi.Find(tools, "BundledMappingButton")!).IsEnabled)
                    throw new InvalidOperationException("Bundled drawing selection is unavailable for a saved controller.");
                Click((Button)WorkspaceUi.Find(tools, "BundledMappingButton")!);
                Pump(tools, () => ((TextBlock)WorkspaceUi.Find(tools, "MappingStatus")!).Text.StartsWith("Verified preview:", StringComparison.Ordinal));
                if (library.ListAsync().GetAwaiter().GetResult().Count != 0) throw new InvalidOperationException("Preview mutated the library.");
                Click((Button)WorkspaceUi.Find(tools, "SaveMappingButton")!);
                Pump(tools, () => ((TextBlock)WorkspaceUi.Find(tools, "MappingStatus")!).Text.StartsWith("Saved drawing:", StringComparison.Ordinal));
                tools.Close(); File.Delete(source);
                var revision = library.ListAsync().GetAwaiter().GetResult().Single();
                if (!revision.Source.Contains("inputKey=\"JOY_BTN1\"", StringComparison.Ordinal))
                    throw new InvalidOperationException("Bundled control keys were not captured in the portable editable source.");
                var bundle = Path.Combine(root, "editable.zip");
                var starter = Path.Combine(root, "starter.drawio");
                var reopened = new UserMappingsWindow(library, [definition], exportPicker: () => bundle, starterPicker: () => starter);
                reopened.Show();
                var revisions = (ListBox)WorkspaceUi.Find(reopened, "MappingRevisions")!;
                Pump(reopened, () => revisions.Items.Count == 1);
                revisions.SelectedIndex = 0; Click((Button)WorkspaceUi.Find(reopened, "ExportMappingButton")!);
                if (!File.Exists(bundle)) throw new InvalidOperationException("Saved mapping did not export without its source file.");
                Click((Button)WorkspaceUi.Find(reopened, "ExportMappingStarterButton")!);
                if (!File.Exists(starter) || DrawioMappingFileImport.Load(starter, definition, "smoke").MappedInputs.Count != 4)
                    throw new InvalidOperationException("The starter export lost native inputs.");
                reopened.Close();
                var defaults = new SharedUiLayerWorkspace(0, new string('a', 40), new Dictionary<string, string>
                { ["assets/shared/ui-layer/functions.json"] = """{"functions":[{"id":"pause","command":"d3001pnilu3001cdnilvd1vpnilvunil","label":"Pause","category":"General"}]}""" });
                var workspace = NewModuleWorkspaceFactory.Create("MAPPING-SMOKE", "Mapping smoke", defaults,
                    new Dictionary<string, string> { ["config/ipia-hardware.json"] = "[]" }, true);
                workspace = InventoryHardwareAuthoring.Add(workspace, new(new(Guid.NewGuid(), "Panel", definition.Id, "Panel", "", true, []), definition), AssignmentLayer.Module);
                var repository = new SqliteEditorWorkspaceRepository(connection);
                repository.SaveAsync(workspace).GetAwaiter().GetResult();
                var editor = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(workspace), repository: repository, userMappings: library, mappingReviewConfirmation: _ => true);
                editor.Show();
                var choices = (ComboBox)WorkspaceUi.Find(editor, "CustomMappingRevisions")!;
                Pump(editor, () => choices.Items.Count == 1);
                StageMapping(editor, workspace.Diagrams.Single().ProfileId, revision.Id);
                if (repository.GetAsync(workspace.Configuration.Module.Id).GetAwaiter().GetResult()!.Diagrams.Any(x => x.CustomMapping is not null))
                    throw new InvalidOperationException("Staged mapping silently changed saved module data.");
                Click((Button)WorkspaceUi.Find(editor, "DiscardAuthoringButton")!);
                StageMapping(editor, workspace.Diagrams.Single().ProfileId, revision.Id);
                Click((Button)WorkspaceUi.Find(editor, "SaveAuthoringButton")!);
                Pump(editor, () => ((TextBlock)WorkspaceUi.Find(editor, "PendingSummary")!).Text == "No pending assignment changes");
                editor.Close();
                if (repository.GetAsync(workspace.Configuration.Module.Id).GetAwaiter().GetResult()!.Diagrams.Single().CustomMapping?.Id != revision.Id)
                    throw new InvalidOperationException("Saved custom mapping lost its immutable revision.");
                var next = DrawioMappingImporter.Import(revision.Source.Replace("fillColor=#ffffff", "fillColor=#eeeeee", StringComparison.Ordinal),
                    definition, revision.SourceName, revision.ApplicationVersion);
                library.SaveAsync(next).GetAwaiter().GetResult();
                var reviewWindow = new MappingRevisionReviewWindow(next, repository);
                reviewWindow.Show();
                Pump(reviewWindow, () => WorkspaceUi.Find(reviewWindow, "RevisionTarget_0") is CheckBox);
                Click((Button)WorkspaceUi.Find(reviewWindow, "PreviewRevisionTarget_0")!);
                if (((Image)WorkspaceUi.Find(reviewWindow, "RevisionBeforePreview")!).Source is null ||
                    ((Image)WorkspaceUi.Find(reviewWindow, "RevisionAfterPreview")!).Source is null)
                    throw new InvalidOperationException("Revision comparison did not show current and proposed artwork.");
                var target = (CheckBox)WorkspaceUi.Find(reviewWindow, "RevisionTarget_0")!;
                Click((Button)WorkspaceUi.Find(reviewWindow, "SelectAllRevisionTargetsButton")!);
                if (target.IsChecked != true) throw new InvalidOperationException("Select all compatible omitted an eligible device.");
                Click((Button)WorkspaceUi.Find(reviewWindow, "ClearRevisionTargetsButton")!);
                if (target.IsChecked != false) throw new InvalidOperationException("Clear selection retained a selected device.");
                target.IsChecked = true;
                Click((Button)WorkspaceUi.Find(reviewWindow, "StageRevisionTargetsButton")!);
                if (repository.GetAsync(workspace.Configuration.Module.Id).GetAwaiter().GetResult()!.Diagrams.Single().CustomMapping!.Id != revision.Id)
                    throw new InvalidOperationException("Revision review staging changed saved data.");
                Click((Button)WorkspaceUi.Find(reviewWindow, "DiscardRevisionTargetsButton")!);
                target.IsChecked = true;
                Click((Button)WorkspaceUi.Find(reviewWindow, "StageRevisionTargetsButton")!);
                Click((Button)WorkspaceUi.Find(reviewWindow, "SaveRevisionTargetsButton")!);
                Pump(reviewWindow, () => ((TextBlock)WorkspaceUi.Find(reviewWindow, "RevisionReviewStatus")!).Text.StartsWith("Saved reviewed changes", StringComparison.Ordinal));
                reviewWindow.Close();
                var revised = repository.GetAsync(workspace.Configuration.Module.Id).GetAwaiter().GetResult()!.Diagrams.Single();
                if (revised.CustomMapping!.Id != next.Id || revised.MappingHistory?.Single().Id != revision.Id)
                    throw new InvalidOperationException("Reviewed adoption did not retain the prior pin.");
                var portableLibrary = new FileUserMappingLibrary(Path.Combine(root, "portable-empty-library"));
                var portableBundle = Path.Combine(root, "portable-pin.zip");
                var pinnedTools = new UserMappingsWindow(portableLibrary, [definition], exportPicker: () => portableBundle, modulePins: [revision]);
                pinnedTools.Show();
                var pinnedChoices = (ListBox)WorkspaceUi.Find(pinnedTools, "MappingRevisions")!;
                Pump(pinnedTools, () => pinnedChoices.Items.Count == 1);
                pinnedChoices.SelectedIndex = 0; Click((Button)WorkspaceUi.Find(pinnedTools, "ExportMappingButton")!);
                if (!File.Exists(portableBundle) || portableLibrary.ListAsync().GetAwaiter().GetResult().Count != 0)
                    throw new InvalidOperationException("A portable module pin could not export without silently publishing a library entry.");
                pinnedTools.Close();
                var bundledSources = ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root).GetAwaiter().GetResult();
                var mfd = JsonSerializer.Deserialize<WorkspaceHardware[]>(bundledSources["config/ipia-hardware.json"])!.First(x => x.DeviceId == "tm-mfd");
                var mfdCapabilities = ControllerCapabilityMapper.Map(Enumerable.Range(0, 28).Select(x =>
                    new ControllerObject(Guid.Parse("a36d02f0-c9f3-11cf-bfc7-444553540000"), 12u | ((uint)x << 8), "Button " + (x + 1))));
                var mfdDefinition = new HardwareDefinition(ControllerCapabilityMapper.Fingerprint(mfdCapabilities), mfdCapabilities);
                var mfdPath = Path.Combine(BundledSharedCatalog.Root, "assets/shared/hardware/drawio/tm-mfd.drawio");
                var mfdTools = new UserMappingsWindow(library, [mfdDefinition], () => mfdPath,
                    bundledInputs: new Dictionary<string, IReadOnlyDictionary<string, string>>
                    { ["tm-mfd.drawio"] = mfd.Diagram.Controls.ToDictionary(x => x.Control, x => x.InputKey, StringComparer.Ordinal) });
                mfdTools.Show();
                Pump(mfdTools, () => ((Button)WorkspaceUi.Find(mfdTools, "PreviewMappingButton")!).IsEnabled);
                Click((Button)WorkspaceUi.Find(mfdTools, "PreviewMappingButton")!); // Normal file picker also accepts bundled sources.
                Pump(mfdTools, () => ((TextBlock)WorkspaceUi.Find(mfdTools, "MappingStatus")!).Text.StartsWith("Verified preview:", StringComparison.Ordinal));
                Click((Button)WorkspaceUi.Find(mfdTools, "SaveMappingButton")!);
                Pump(mfdTools, () => ((TextBlock)WorkspaceUi.Find(mfdTools, "MappingStatus")!).Text.StartsWith("Saved drawing:", StringComparison.Ordinal));
                mfdTools.Close();
                var savedMfd = library.ListAsync().GetAwaiter().GetResult().Single(x => x.SourceName == "tm-mfd.drawio");
                if (savedMfd.Controls.Count != 48 || savedMfd.MappedInputs.Count != 28)
                    throw new InvalidOperationException("Real bundled MFD preview lost its base/shifted label pairs.");
                var editedPath = Path.Combine(root, "tm-mfd(1).drawio");
                var editedGraph = XDocument.Parse(File.ReadAllText(mfdPath));
                var imageCell = editedGraph.Descendants("mxCell").First(x => x.Attribute("id")?.Value.StartsWith("hardware-image-", StringComparison.Ordinal) == true);
                imageCell.Element("mxGeometry")!.SetAttributeValue("x", "1.5");
                editedGraph.Descendants("root").Single().Add(new XElement("mxCell", new XAttribute("id", "drawio-cover"),
                    new XAttribute("parent", "1"), new XAttribute("vertex", "1"), new XAttribute("style", "fillColor=#ffffff;strokeColor=none;"),
                    new XElement("mxGeometry", new XAttribute("x", "15"), new XAttribute("y", "15"), new XAttribute("width", "40"), new XAttribute("height", "20"))));
                File.WriteAllText(editedPath, editedGraph.ToString());
                var editedTools = new UserMappingsWindow(library, [mfdDefinition], () => editedPath,
                    bundledInputs: new Dictionary<string, IReadOnlyDictionary<string, string>>
                    { ["tm-mfd.drawio"] = mfd.Diagram.Controls.ToDictionary(x => x.Control, x => x.InputKey, StringComparer.Ordinal) });
                editedTools.Show();
                Pump(editedTools, () => ((Button)WorkspaceUi.Find(editedTools, "PreviewMappingButton")!).IsEnabled);
                Click((Button)WorkspaceUi.Find(editedTools, "PreviewMappingButton")!);
                Pump(editedTools, () => ((TextBlock)WorkspaceUi.Find(editedTools, "MappingStatus")!).Text.StartsWith("Verified preview:", StringComparison.Ordinal));
                Click((Button)WorkspaceUi.Find(editedTools, "SaveMappingButton")!);
                Pump(editedTools, () => ((TextBlock)WorkspaceUi.Find(editedTools, "MappingStatus")!).Text.StartsWith("Saved drawing:", StringComparison.Ordinal));
                editedTools.Close();
                var savedEdited = library.ListAsync().GetAwaiter().GetResult().Single(x => x.SourceName == "tm-mfd(1).drawio");
                if (savedEdited.ExporterVersion != "ipia-drawio-4" || savedEdited.Controls.Count != 48 || savedEdited.MappedInputs.Count != 28)
                    throw new InvalidOperationException("Renamed edited MFD preview did not preserve its native controls.");
                var mfdWorkspace = NewModuleWorkspaceFactory.Create("GLOBAL-MFD-SMOKE", "Global MFD smoke", defaults,
                    new Dictionary<string, string> { ["config/ipia-hardware.json"] = "[]" }, true);
                mfdWorkspace = WorkspaceStructureEditor.AddDevice(mfdWorkspace, mfd, AssignmentLayer.Module,
                    "MFD {22222222-2222-2222-2222-222222222222}.diff.lua", "Left", "Pilot");
                repository.SaveAsync(mfdWorkspace).GetAwaiter().GetResult();
                var globalReview = new MappingRevisionReviewWindow(savedMfd, repository);
                globalReview.Show();
                Pump(globalReview, () => WorkspaceUi.Find(globalReview, "RevisionTarget_1") is CheckBox);
                var targets = Enumerable.Range(0, 2).Select(i => (Index: i, Choice: (CheckBox)WorkspaceUi.Find(globalReview, "RevisionTarget_" + i)!)).ToArray();
                Click((Button)WorkspaceUi.Find(globalReview, "SelectAllRevisionTargetsButton")!);
                if (targets.Count(x => x.Choice.IsChecked == true) != 1 ||
                    !targets.Single(x => x.Choice.IsChecked == true).Choice.Content.ToString()!.Contains("Global MFD smoke", StringComparison.Ordinal))
                    throw new InvalidOperationException("Global drawing selection included an incompatible device or omitted a built-in MFD.");
                Click((Button)WorkspaceUi.Find(globalReview, "StageRevisionTargetsButton")!);
                Click((Button)WorkspaceUi.Find(globalReview, "SaveRevisionTargetsButton")!);
                Pump(globalReview, () => ((TextBlock)WorkspaceUi.Find(globalReview, "RevisionReviewStatus")!).Text.StartsWith("Saved reviewed changes", StringComparison.Ordinal));
                globalReview.Close();
                var changedMfd = repository.GetAsync(mfdWorkspace.Configuration.Module.Id).GetAwaiter().GetResult()!;
                if (changedMfd.Diagrams.Single().OriginalDrawing is null || changedMfd.Diagrams.Single().CustomMapping?.Id != savedMfd.Id)
                    throw new InvalidOperationException("Global MFD replacement did not retain the original built-in artwork.");
                var rollbackReview = new MappingRevisionReviewWindow(savedMfd, repository);
                rollbackReview.Show();
                Pump(rollbackReview, () => WorkspaceUi.Find(rollbackReview, "RevisionTarget_1") is CheckBox);
                var originalButton = Enumerable.Range(0, 2).Select(i => WorkspaceUi.Find(rollbackReview, "RestoreOriginalDrawing_" + i)).OfType<Button>().Single();
                Click(originalButton);
                if (repository.GetAsync(mfdWorkspace.Configuration.Module.Id).GetAwaiter().GetResult()!.Diagrams.Single().CustomMapping is null)
                    throw new InvalidOperationException("Staging an original drawing changed saved data.");
                Click((Button)WorkspaceUi.Find(rollbackReview, "SaveRevisionTargetsButton")!);
                Pump(rollbackReview, () => ((TextBlock)WorkspaceUi.Find(rollbackReview, "RevisionReviewStatus")!).Text.StartsWith("Saved reviewed changes", StringComparison.Ordinal));
                rollbackReview.Close();
                var restoredMfd = repository.GetAsync(mfdWorkspace.Configuration.Module.Id).GetAwaiter().GetResult()!.Diagrams.Single();
                if (restoredMfd.CustomMapping is not null || restoredMfd.Svg != mfdWorkspace.Diagrams.Single().Svg || restoredMfd.MappingHistory?.Count != 1)
                    throw new InvalidOperationException("Original built-in drawing restoration lost artwork or custom history.");
                Console.WriteLine("Custom mapping upload, selective/global replacement and original built-in rollback passed.");
            }
            finally { SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void StageMapping(ModuleWorkspaceWindow editor, Guid profileId, string revisionId)
        {
            var view = WorkspaceUi.Navigate(editor, "Devices");
            var devices = (DataGrid)view.FindName("ManagedDevices");
            devices.SelectedItem = devices.Items.Cast<WorkspaceDevice>().Single(device => device.ProfileId == profileId);
            var unlink = (Button)view.FindName("UnlinkDrawingButton");
            var apply = (Button)view.FindName("ApplyCustomDrawingButton");
            var status = (TextBlock)view.FindName("StatusMessage");
            if (!unlink.IsEnabled || apply.IsEnabled)
                throw new InvalidOperationException("The selected controller must start with its drawing linked. " + status.Text);
            Click(unlink);
            if (!apply.IsEnabled || unlink.IsEnabled || editor._working!.Diagrams.Single(diagram => diagram.ProfileId == profileId).DrawingUnlinked != true)
                throw new InvalidOperationException("Drawing unlink did not enable reviewed replacement. " + status.Text);
            var revisions = (ComboBox)view.FindName("CustomMappingRevisions");
            revisions.SelectedItem = revisions.Items.Cast<UserMappingRevision>().Single(revision => revision.Id == revisionId);
            Click(apply);
            var staged = editor._working!.Diagrams.Single(diagram => diagram.ProfileId == profileId);
            if (staged.CustomMapping?.Id != revisionId || staged.DrawingUnlinked || !status.Text.Contains("staged", StringComparison.Ordinal))
                throw new InvalidOperationException("Custom mapping adoption was not staged. " + status.Text);
            if (apply.IsEnabled || !unlink.IsEnabled)
                throw new InvalidOperationException("Adopted drawing did not restore the linked selector state. " + status.Text);
        }
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
                if (DateTime.UtcNow >= deadline) throw new InvalidOperationException("Custom mapping UI timed out." + (window is ModuleWorkspaceWindow editor ? " " + ((TextBlock)WorkspaceUi.Find(editor, "DevicesMessage")).Text : ""));
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            }
        }
        #endregion END private helpers
    }
}
