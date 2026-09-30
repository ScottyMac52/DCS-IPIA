using System.IO;
using System.Windows.Controls;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Microsoft.Data.Sqlite;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Opens both read-only WPF preview modes on a Windows CI runner.</summary>
    public static class Program
    {
        private static readonly string[] NavigationHeaders = ["Devices", "Modifiers", "Assignments"];
        #region BEGIN Public
        [STAThread]
        /// <summary>Checks that imported source data reaches a visible WPF preview window.</summary>
        /// <returns>Zero when both windows render their expected content.</returns>
        public static int Main()
        {
            var app = new DcsIpia.Presentation.App();
            app.InitializeComponent();
            var detected = new WindowsConnectedDeviceDiscovery().Discover();
            if (detected.Any(x => x.PhysicalGuid == Guid.Empty || string.IsNullOrWhiteSpace(x.Name)))
                throw new InvalidOperationException("DirectInput discovery returned an invalid physical controller identity.");
            var profile =
                new DeviceProfile(Guid.NewGuid(), "F16 MFD 3", null, "unresolved device", "F16 MFD 3.diff.lua");
            var folder =
                new DcsFolderImportResult("Saved Games/Input/UH-1H/joystick", [profile], [], [],
                                          [new("bad-profile", "One other device profile could not be parsed.", true)]);
            var folderWindow = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(folder));
            folderWindow.Show();
            folderWindow.UpdateLayout();
            var folderTabs = (TabControl)folderWindow.FindName("ImportedTabs")!;
            if (!folderWindow.IsVisible || (folderTabs.SelectedItem as TabItem)?.Header?.ToString() != "Diagnostics")
                throw new InvalidOperationException("Saved Games preview did not show its diagnostics.");
            if (!folderTabs.Items.Cast<TabItem>().Take(3).Select(x => x.Header.ToString()).SequenceEqual(NavigationHeaders))
                throw new InvalidOperationException("Devices and Modifiers must precede Assignments.");
            var module = new DcsModule(Guid.NewGuid(), "UH-1H", "Huey");
            var mfd = new DeviceProfile(Guid.NewGuid(), "tm-mfd", "MFD3", "MFD", "MFD3.diff.lua");
            var configuration = new ModuleConfiguration(
                module, [mfd], [new DcsModifier("MOZA_F16_F18_BTN3", "tm-mfd", "JOY_BTN5", ModifierMode.Hold)],
                [
                    new DcsBinding(mfd.Id, BindingSection.Key, "MODULE_BASE", "Base action", BindingDisposition.Added,
                                   "JOY_BTN1"),
                    new DcsBinding(mfd.Id, BindingSection.Key, "MODULE_SHIFTED", "Modified action",
                                   BindingDisposition.Added, "JOY_BTN1", ["MOZA_F16_F18_BTN3"]),
                    new DcsBinding(mfd.Id, BindingSection.Key, "POV_BASE", "Pilot Trimmer", BindingDisposition.Added,
                                   "JOY_BTN_POV1_U"),
                    new DcsBinding(mfd.Id, BindingSection.Key, "POV_SHIFT", "Pilot Sight Elevation Increase",
                                   BindingDisposition.Added, "JOY_BTN_POV1_U", ["MOZA_F16_F18_BTN3"])
                ],
                "smoke fixture");
            using var kneeboard = System.Text.Json.JsonDocument.Parse("""
               {"labels":{"base":"Base authored","shift":"Shift authored","pilot":"Pilot","elevation":"Elevation+"},
                "pages":[{"deviceId":"tm-mfd","deviceInstance":"MFD3","layers":[
                 {"id":"base","controls":{
                  "mfd-osb-t1":{"key":"JOY_BTN1","command":"MODULE_BASE","labelId":"base"},
                  "trim-up":{"key":"JOY_BTN_POV1_U","command":"POV_BASE","labelId":"pilot"}}},
                 {"id":"MOZA_F16_F18_BTN3","controls":{
                  "mfd-osb-t1":{"key":"JOY_BTN1","command":"MODULE_SHIFTED","labelId":"shift","modifiers":["MOZA_F16_F18_BTN3"]},
                  "trim-up":{"key":"JOY_BTN_POV1_U","command":"POV_SHIFT","labelId":"elevation","modifiers":["MOZA_F16_F18_BTN3"]}}}
                ]}]}
               """);
            var imported = new ConsumerImportResult(
                configuration, [], [], [], [],
                new Dictionary<string, System.Text.Json.JsonElement> { ["config/kneeboard.json"] =
                                                                           kneeboard.RootElement.Clone() },
                new Dictionary<string, string>(),
                [new SourceProvenance("Huey", new string('a', 40), "config/kneeboard.json", "hash")], []);
            var image = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/ZQAAAABJRU5ErkJggg==");
            var svg =
                $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 150'><image href='data:image/png;base64,{Convert.ToBase64String(image)}' width='200' height='150'/><!-- box:mfd-osb-t1 --><rect x='10' y='60' width='60' height='40' fill='white'/><text id='lbl-mfd-osb-t1' x='20' y='70'>OSB01</text><!-- box:mfd-osb-t1-shifted --><rect x='10' y='10' width='60' height='40' fill='white'/><text id='lbl-mfd-osb-t1-shifted' x='20' y='20'>OSB01</text><!-- box:mfd-osb-t2 --><rect x='80' y='10' width='60' height='40' fill='white'/><text id='lbl-mfd-osb-t2' x='90' y='20'>OSB02</text><!-- box:mfd-osb-t3 --><rect x='145' y='10' width='45' height='40' fill='white'/><text id='lbl-mfd-osb-t3' x='160' y='20'>OSB03</text><!-- box:unmapped --><rect x='145' y='60' width='45' height='40' fill='white'/><text id='lbl-unmapped' x='150' y='70'>Old base label</text><!-- box:trim-up --><rect x='80' y='110' width='60' height='30' fill='white'/><text id='lbl-trim-up' x='90' y='120'>Trim</text></svg>";
            var diagram = new UiLayerDiagram(
                "TM MFD", "MFD3", "assets/shared/hardware/svg/tm-mfd.svg", 200, 150, image,
                [new UiLayerCallout(1, "mfd-osb-t1-shifted", "VR Zoom", "MOZA_MODIFIER_BTN3", 10, 10, 60, 40) {
                    CommandId = "UI_ZOOM"
                }]) { Svg = svg, ProfileId = mfd.Id,
                      Controls = [
                          new("mfd-osb-t1", "OSB01", "JOY_BTN1", 10, 60, 60, 40),
                          new("mfd-osb-t1-shifted", "OSB01", "JOY_BTN1", 10, 10, 60, 40),
                          new("mfd-osb-t2", "OSB02", "JOY_BTN2", 80, 10, 60, 40),
                          new("mfd-osb-t3", "OSB03", "JOY_BTN3", 145, 10, 45, 40),
                          new("trim-up", "Trim", "JOY_POV1_U", 80, 110, 60, 30)
                      ] };
            var preview = new ModuleWorkspacePreview(imported, [], [diagram]) { UiCommands = [
                new(Guid.NewGuid(), Guid.NewGuid(), "UI_ZOOM", "VR Zoom", "VR", false),
                new(Guid.NewGuid(), Guid.NewGuid(), "UI_PAUSE", "Active Pause", "General", false)
            ] };
            var importedView = new ModuleWorkspaceViewModel(preview);
            if (importedView.Draft is null ||
                !importedView.Draft.Snapshot.Any(x => x.Slot.Layer == AssignmentLayer.Module &&
                                                      x.Slot.ModifierChord == "MOZA_F16_F18_BTN3") ||
                !importedView.Draft.Snapshot.Any(x => x.Slot.Layer == AssignmentLayer.UiLayer &&
                                                      x.Slot.ModifierChord == "MOZA_MODIFIER_BTN3"))
                throw new InvalidOperationException("The import dropped shifted module or UI Layer assignments.");
            var repoWindow = new ModuleWorkspaceWindow(importedView);
            repoWindow.Show();
            repoWindow.UpdateLayout();
            var scratch = Path.Combine(Path.GetTempPath(), "ipia-editor-smoke-" + Guid.NewGuid().ToString("N") + ".db");
            var db =
                new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = scratch, ForeignKeys = true }
                    .ToString();
            new SqliteMigrationRunner(db).MigrateAsync().GetAwaiter().GetResult();
            new SqliteConfigurationRepository(db).ReplaceAsync(configuration).GetAwaiter().GetResult();
            var staleUiSlot =
                new DraftAssignmentSlot(module.Id, mfd.Id, AssignmentLayer.UiLayer, BindingSection.Key, "JOY_BTN2");
            var snapshot = new EditorWorkspaceSnapshot(
                configuration, [], [], [diagram],
                [
                    new CommandPaletteEntry("MODULE_BASE", "Base action", "Flight Control", false),
                    new CommandPaletteEntry("MODULE_SHIFTED", "Modified action", "Flight Control", false),
                    new CommandPaletteEntry("POV_BASE", "Pilot Trimmer", "Flight Control", false),
                    new CommandPaletteEntry("POV_SHIFT", "Pilot Sight Elevation Increase", "Flight Control", false)
                ],
                preview.UiCommands, importedView.Draft.Snapshot,
                new Dictionary<string, string>(importedView.Labels) { [staleUiSlot.Key] = "Stale base label" });
            var repository = new SqliteEditorWorkspaceRepository(db);
            repository.SaveAsync(snapshot).GetAwaiter().GetResult();
            var loaded = repository.GetAsync(module.Id).GetAwaiter().GetResult()!;
            if (loaded.Diagrams[0].Svg != svg || loaded.ModuleCommands[0].Category != "Flight Control")
                throw new InvalidOperationException("Saved SVG or category was not recovered from SQLite.");
            var savedWindow = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(loaded), null, repository);
            savedWindow.Show();
            savedWindow.UpdateLayout();
            if (!savedWindow.IsVisible || ((Canvas)savedWindow.FindName("Diagram")!).Children.Count == 0)
                throw new InvalidOperationException("Database-backed graphical assignment editor did not display.");
            var managedDevices = (DataGrid)savedWindow.FindName("ManagedDevices")!;
            managedDevices.SelectedIndex = 0;
            if (managedDevices.Items.Count != 1 || ((TextBox)savedWindow.FindName("ManagedFileName")!).Text != "MFD3.diff.lua" ||
                !((GroupBox)savedWindow.FindName("MfdLabelFields")!).IsEnabled)
                throw new InvalidOperationException("The Devices editor did not show the stored module MFD and its editable fields.");
            var managedModifiers = (DataGrid)savedWindow.FindName("ManagedModifiers")!;
            if (managedModifiers.Items.Count != 1 || ((WorkspaceModifier)managedModifiers.Items[0]).Name != "MOZA_F16_F18_BTN3")
                throw new InvalidOperationException("The Modifiers editor did not show the stored module definitions.");
            var layers = (ComboBox)savedWindow.FindName("Layers")!;
            var scope = (ComboBox)savedWindow.FindName("Scope")!;
            string Caption(string control)
            {
                var canvas = (Canvas)savedWindow.FindName("Diagram")!;
                var surface = canvas.Children.OfType<Canvas>().Single();
                var target = surface.Children.OfType<Border>().Single(x => x.Tag is HardwareGraphicControl item &&
                                                                           item.Control == control);
                return ((TextBlock)target.Child).Text;
            }
            string MaskCaption(string id)
            {
                var canvas = (Canvas)savedWindow.FindName("Diagram")!;
                var surface = canvas.Children.OfType<Canvas>().Single();
                var target = surface.Children.OfType<Border>().Single(x => x.Tag is string marker && marker == id);
                return ((TextBlock)target.Child).Text;
            }
            if (Caption("mfd-osb-t1") != "Base authored" || Caption("trim-up") != "Pilot" ||
                Caption("mfd-osb-t2") != "Unassigned" || MaskCaption("mfd-osb-t1-shifted") != "Unassigned" ||
                MaskCaption("unmapped") != "Unassigned" ||
                ((Canvas)savedWindow.FindName("Diagram")!)
                    .Children.OfType<Canvas>()
                    .Single()
                    .Children.OfType<Border>()
                    .Any(x => x.Tag is HardwareGraphicControl item && item.Control == "mfd-osb-t1-shifted"))
                throw new InvalidOperationException("Base layer did not show only its own assignment.");
            layers.SelectedIndex = 1;
            savedWindow.UpdateLayout();
            if (Caption("mfd-osb-t1-shifted") != "Shift authored" || Caption("trim-up") != "Elevation+")
                throw new InvalidOperationException("Modifier selection did not show the shifted module assignment.");
            scope.SelectedIndex = 1;
            savedWindow.UpdateLayout();
            if ((string?)layers.SelectedItem != "MOZA_MODIFIER_BTN3" || Caption("mfd-osb-t1-shifted") != "VR Zoom" ||
                Caption("mfd-osb-t2") != "Unassigned" || MaskCaption("mfd-osb-t1") != "Unassigned" ||
                MaskCaption("unmapped") != "Unassigned")
                throw new InvalidOperationException(
                    "UI Layer displayed an assignment or authored label from another layer.");
            layers.SelectedIndex = 0;
            savedWindow.UpdateLayout();
            if (Caption("mfd-osb-t1") != "Unassigned" || Caption("mfd-osb-t2") != "Unassigned" ||
                MaskCaption("mfd-osb-t1-shifted") != "Unassigned")
                throw new InvalidOperationException("Base UI Layer displayed a module assignment or stale label.");
            savedWindow.Close();
            var shiftedUiSlot = new DraftAssignmentSlot(module.Id, mfd.Id, AssignmentLayer.UiLayer, BindingSection.Key,
                                                        "JOY_BTN1", "MOZA_MODIFIER_BTN3");
            repository
                .SaveAsync(
                    loaded with { Assignments =
                                      loaded.Assignments.Where(x => x.Slot.Key != shiftedUiSlot.Key).ToArray(),
                                  Labels = new Dictionary<string, string> { [shiftedUiSlot.Key] = "Old VR Zoom" } })
                .GetAwaiter()
                .GetResult();
            var clearedWindow = new ModuleWorkspaceWindow(
                new ModuleWorkspaceViewModel(repository.GetAsync(module.Id).GetAwaiter().GetResult()!), null,
                repository);
            clearedWindow.Show();
            ((ComboBox)clearedWindow.FindName("Scope")!).SelectedIndex = 1;
            ((ComboBox)clearedWindow.FindName("Layers")!).SelectedIndex = 1;
            clearedWindow.UpdateLayout();
            var clearedSurface = ((Canvas)clearedWindow.FindName("Diagram")!).Children.OfType<Canvas>().Single();
            var clearedControl = clearedSurface.Children.OfType<Border>().Single(
                x => x.Tag is HardwareGraphicControl item && item.Control == "mfd-osb-t1-shifted");
            if (((TextBlock)clearedControl.Child).Text != "Unassigned")
                throw new InvalidOperationException("Cleared UI assignment retained an old label.");
            clearedWindow.Close();
            SqliteConnection.ClearAllPools();
            File.Delete(scratch);
            repoWindow.Close();
            folderWindow.Close();
            var preferencesRoot =
                Path.Combine(Path.GetTempPath(), "dcs-ipia-settings-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(preferencesRoot);
                var preferenceConnection = "Data Source=" + Path.Combine(preferencesRoot, "ipia.db") + ";Pooling=False";
                new SqliteMigrationRunner(preferenceConnection).MigrateAsync().GetAwaiter().GetResult();
                var settings = new SqliteAppSettingsService(
                    preferenceConnection, Path.Combine(preferencesRoot, "defaults.json"),
                    Path.Combine(preferencesRoot, "user-settings.json"), Path.Combine(preferencesRoot, "theme.txt"));
                var preferencesWindow = new SettingsWindow(settings);
                preferencesWindow.Show();
                preferencesWindow.UpdateLayout();
                ((ComboBox)preferencesWindow.FindName("ThemeChoice")!).SelectedIndex = 1;
                ((ComboBox)preferencesWindow.FindName("ConflictChoice")!).SelectedIndex = 2;
                if (settings.Current.Theme != AppearanceTheme.Dark ||
                    settings.Current.OccupiedDestinationBehavior != OccupiedDestinationBehavior.Reject ||
                    new SqliteAppSettingsService(preferenceConnection, Path.Combine(preferencesRoot, "defaults.json"),
                                                 Path.Combine(preferencesRoot, "user-settings.json"),
                                                 Path.Combine(preferencesRoot, "theme.txt"))
                            .Current.OccupiedDestinationBehavior != OccupiedDestinationBehavior.Reject)
                    throw new InvalidOperationException(
                        "Settings window did not apply and save theme and destination preferences.");
                preferencesWindow.Close();
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(preferencesRoot))
                    Directory.Delete(preferencesRoot, true);
            }
            System.Console.WriteLine("Both WPF import previews opened and displayed their expected content.");
            return 0;
        }
        #endregion END Public
    }
}

