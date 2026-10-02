using System.IO;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
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
        private static readonly string[] CommandImportButtons = ["LoadCommandJsonButton", "LoadCommandHtmlButton"];
        private static readonly string[] CreationHardware = ["tm-mfd", "tm-mfd", "tm-mfd", "tm-mfd", "logitech-throttle-quadrant", "logitech-throttle-quadrant",
            "moza-ab9", "vkb-f14-gunfighter", "winctrl-pto2", "winctrl-icp"];
        #region BEGIN Public
        [STAThread]
        /// <summary>Checks that imported source data reaches a visible WPF preview window.</summary>
        /// <returns>Zero when both windows render their expected content.</returns>
        public static int Main()
        {
            var app = new DcsIpia.Presentation.App();
            app.InitializeComponent();
            global::Dcs.Ipia.Dcs.BundledSharedCatalog.Validate(global::Dcs.Ipia.Dcs.BundledSharedCatalog.Root);
            var defaultsWindow = new SharedUiLayerWorkspaceWindow(new SharedUiLayerWorkspace(1, new string('a', 40),
                new Dictionary<string, string> { ["assets/shared/ui-layer/functions.json"] = "{}" }));
            defaultsWindow.Show();
            defaultsWindow.UpdateLayout();
            VerifySharedDefaultBindingEditor();
            VerifyModuleDeleteButton();
            VerifyNewModuleForm();
            VerifyModuleAuthoring();
            var defaultText = (TextBox)defaultsWindow.FindName("SourceText")!;
            if (!defaultsWindow.IsVisible || !defaultText.IsReadOnly || defaultText.Text != "{}")
                throw new InvalidOperationException("Shared defaults did not reopen in the read-only source preview.");
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
            defaultsWindow.Close();
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
        #region BEGIN private helpers
        private static void VerifyModuleAuthoring()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-authoring-smoke-" + Guid.NewGuid().ToString("N") + ".db");
            var connection = "Data Source=" + path + ";Pooling=False";
            var jsonPath = Path.Combine(Path.GetTempPath(), "ipia-command-smoke-" + Guid.NewGuid().ToString("N") + ".json");
            var htmlRoot = Path.Combine(Path.GetTempPath(), "ipia-html-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(htmlRoot);
                var htmlPath = Path.Combine(htmlRoot, "Stick.html");
                File.WriteAllText(jsonPath, """{"schemaVersion":1,"moduleId":"AUTHORING","commands":[{"bindingKey":"a2001cdnil","name":"Imported pitch","categoryPath":["Flight"],"type":"axis"}]}""");
                File.WriteAllText(htmlPath, "<h1>Stick</h1><table><tr><td>JOY_BTN1</td><td>Imported button</td><td>Systems</td><td>d3011pnilu3011cd35vd1vpnilvu0</td></tr></table>");
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                var profile = new DeviceProfile(Guid.NewGuid(), "test", null, "Test", "Test.diff.lua");
                var configuration = new ModuleConfiguration(new(Guid.NewGuid(), "AUTHORING", "Authoring"), [profile], [], [], "Created in IPIA");
                var diagram = new UiLayerDiagram("Test", null, "test.svg", 100, 100, [], [])
                {
                    ProfileId = profile.Id, Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><rect width='100' height='100' fill='#07111d'/></svg>",
                    Controls = [new HardwareGraphicControl("button", "Button", "JOY_BTN1", 5, 5, 20, 20)]
                };
                var workspace = new EditorWorkspaceSnapshot(configuration, [], [], [diagram], [], [], [], new Dictionary<string, string>(),
                    new Dictionary<string, string> { ["config/kneeboard.json"] = """{"profiles":{},"pages":[],"summaryPages":[]}""",
                        ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = "local modifiers = {} return modifiers" });
                repository.SaveAsync(workspace).GetAwaiter().GetResult();
                var window = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(workspace), repository: repository,
                    commandFilePicker: html => html ? [htmlPath] : [jsonPath]);
                window.Show();
                window.UpdateLayout();
                ((DataGrid)window.FindName("ManagedDevices")!).SelectedIndex = 0;
                if (((TextBox)window.FindName("ManagedFileName")!).Text != profile.SourceName)
                    throw new InvalidOperationException("A newly created device without page metadata could not be selected.");
                ((Button)window.FindName("MissingDevicePagesButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (((DataGrid)window.FindName("AuthoredPages")!).Items.Count != 1)
                    throw new InvalidOperationException("Missing device pages did not initialize from the New Module manifest.");
                ((TextBox)window.FindName("AircraftCommandCode")!).Text = "KEY";
                ((TextBox)window.FindName("AircraftCommandName")!).Text = "Test action";
                ((TextBox)window.FindName("AircraftCommandCategory")!).Text = "Keys";
                ((Button)window.FindName("StageAircraftCommandButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var commands = (DataGrid)window.FindName("AuthoredCommands")!;
                var palette = (ListBox)window.FindName("Commands")!;
                if (commands.Items.Count != 1 || !palette.Items.Cast<CommandPaletteEntry>().Any(x => x.Id == "KEY"))
                    throw new InvalidOperationException("Authored aircraft command did not refresh the assignment palette.");
                foreach (var buttonName in CommandImportButtons)
                {
                    window.Dispatcher.BeginInvoke(new Action(() => {
                        var reviewWindow = System.Windows.Application.Current.Windows.OfType<CommandImportReviewWindow>().Single();
                        if (((DataGrid)reviewWindow.FindName("ImportChanges")!).Items.Count != 1 ||
                            !((Button)reviewWindow.FindName("StageImportButton")!).IsEnabled)
                            throw new InvalidOperationException("Command import review did not expose the validated addition.");
                        ((Button)reviewWindow.FindName("StageImportButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }));
                    ((Button)window.FindName(buttonName)!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                if (commands.Items.Count != 3 || !palette.Items.Cast<CommandPaletteEntry>().Any(x => x.Id == "a2001cdnil") ||
                    !((ComboBox)window.FindName("CommandCategory")!).Items.Cast<string>().Contains("Flight"))
                    throw new InvalidOperationException("JSON/HTML load did not refresh aircraft catalog, palette and categories.");
                window.Dispatcher.BeginInvoke(new Action(() => {
                    System.Windows.Application.Current.Windows.OfType<CommandImportReviewWindow>().Single().DialogResult = false;
                }));
                ((Button)window.FindName("LoadCommandHtmlButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (commands.Items.Count != 3) throw new InvalidOperationException("Cancelling import changed the catalog.");
                ((ComboBox)window.FindName("AuthoredPageKind")!).SelectedItem = KneeboardPageKind.Summary;
                ((TextBox)window.FindName("AuthoredPageFile")!).Text = "overview";
                ((TextBox)window.FindName("AuthoredPageTitle")!).Text = "OVERVIEW";
                ((TextBox)window.FindName("AuthoredPageSubtitle")!).Text = "CONTROLS";
                ((TextBox)window.FindName("SummaryCardKey")!).Text = "STICK";
                ((TextBox)window.FindName("SummaryCardText")!).Text = "Flight controls";
                ((Button)window.FindName("SetSummaryCardButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ((Button)window.FindName("StageAuthoredPageButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (((DataGrid)window.FindName("AuthoredPages")!).Items.Count != 2)
                    throw new InvalidOperationException("Authored summary page did not stage.");
                ((Button)window.FindName("SaveAuthoringButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (repository.GetAsync(configuration.Module.Id).GetAwaiter().GetResult()!.Pages.Count != 2)
                {
                    if (DateTime.UtcNow > deadline)
                        throw new InvalidOperationException("Commands and pages did not save to SQLite.");
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                }
                commands.SelectedItem = commands.Items.Cast<CommandPaletteEntry>().Single(x => x.Id == "KEY");
                ((TextBox)window.FindName("AircraftCommandName")!).Text = "Discarded name";
                ((Button)window.FindName("StageAircraftCommandButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ((Button)window.FindName("DiscardAuthoringButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (commands.Items.Cast<CommandPaletteEntry>().Single(x => x.Id == "KEY").Name != "Test action")
                    throw new InvalidOperationException("Discard did not restore the saved aircraft catalog.");
                if (repository.GetAsync(configuration.Module.Id).GetAwaiter().GetResult()!.ModuleCommands.Single(x => x.Id == "KEY").Name != "Test action")
                    throw new InvalidOperationException("Discard altered the saved aircraft catalog.");
                var savedCommands = repository.GetAsync(configuration.Module.Id).GetAwaiter().GetResult()!;
                if (savedCommands.ModuleCommands.Count != 3 || savedCommands.Sources!.Count(x => x.Key.StartsWith("config/ipia-command-imports/", StringComparison.Ordinal)) != 2)
                    throw new InvalidOperationException("Command imports and original sources did not reopen from SQLite.");
                File.WriteAllText(jsonPath, """{"schemaVersion":1,"moduleId":"AUTHORING","commands":[{"bindingKey":"EXTRA","name":"Discarded import","categoryPath":["Flight"],"type":"button"}]}""");
                window.Dispatcher.BeginInvoke(new Action(() => {
                    ((Button)System.Windows.Application.Current.Windows.OfType<CommandImportReviewWindow>().Single().FindName("StageImportButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }));
                ((Button)window.FindName("LoadCommandJsonButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (commands.Items.Count != 4) throw new InvalidOperationException("The discardable import did not stage.");
                ((Button)window.FindName("DiscardAuthoringButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (commands.Items.Count != 3 || repository.GetAsync(configuration.Module.Id).GetAwaiter().GetResult()!.ModuleCommands.Count != 3)
                    throw new InvalidOperationException("Discard did not restore the saved catalog after import.");
                VerifyModuleSnapshotButtons(window, repository, configuration.Module.Id);
                window.Close();
            }
            finally { File.Delete(path); File.Delete(jsonPath); if (Directory.Exists(htmlRoot)) Directory.Delete(htmlRoot, true); }
        }
        private static void VerifyModuleSnapshotButtons(ModuleWorkspaceWindow authoring, SqliteEditorWorkspaceRepository source, Guid moduleId)
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-module-output-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var snapshotPath = Path.Combine(root, "module.ipia-module.json");
                var saved = source.GetAsync(moduleId).GetAwaiter().GetResult()!;
                var editor = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(saved), repository: source,
                    outputPathPicker: snapshot => snapshot ? snapshotPath : root);
                editor.Show();
                ((TextBox)editor.FindName("PackageVersion")!).Text = "1.2.3";
                var validate = (Button)editor.FindName("ValidateModuleButton")!;
                var export = (Button)editor.FindName("ExportModuleSnapshotButton")!;
                var build = (Button)editor.FindName("BuildButton")!;
                if (!validate.IsEnabled || !export.IsEnabled || !build.IsEnabled)
                    throw new InvalidOperationException("Clean saved module output actions are unavailable.");
                validate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                WaitForOutput(editor, "Validated ");
                export.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                WaitForOutput(editor, "Exported module snapshot:");
                var snapshot = global::Dcs.Ipia.Dcs.ModuleSnapshotWorkflow.Read(File.ReadAllText(snapshotPath));
                build.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                WaitForOutput(editor, "Wrote ");
                var original = File.ReadAllBytes(Path.Combine(root, "DCS-AUTHORING-Components-1.2.3-OVGME.zip"));
                if (!original.SequenceEqual(global::Dcs.Ipia.Dcs.ModuleSnapshotWorkflow.Build(snapshot).Bytes))
                    throw new InvalidOperationException("UI build did not use the exported immutable inputs.");
                ((TextBox)editor.FindName("AircraftCommandCode")!).Text = "UNSAVED";
                ((TextBox)editor.FindName("AircraftCommandName")!).Text = "Pending";
                ((TextBox)editor.FindName("AircraftCommandCategory")!).Text = "Keys";
                ((Button)editor.FindName("StageAircraftCommandButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (validate.IsEnabled || export.IsEnabled || build.IsEnabled)
                    throw new InvalidOperationException("Pending authoring edits did not disable saved output actions.");
                ((Button)editor.FindName("DiscardAuthoringButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                editor.Close();

                var connection = "Data Source=" + Path.Combine(root, "restore.db") + ";Pooling=False";
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var restored = new SqliteEditorWorkspaceRepository(connection);
                var settings = new SqliteAppSettingsService(connection, Path.Combine(root, "defaults.json"),
                    Path.Combine(root, "settings.json"), Path.Combine(root, "theme.txt"));
                var main = new MainWindow(new global::Dcs.Ipia.Dcs.ModuleWorkspaceLoader(), new global::Dcs.Ipia.Dcs.DcsFolderImporter(),
                    settings, restored, snapshotFilePicker: () => snapshotPath);
                main.Show();
                var restore = (Button)main.FindName("RestoreModuleSnapshotButton")!;
                restore.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var deadline = DateTime.UtcNow.AddSeconds(60);
                while (!((TextBlock)main.FindName("ImportStatus")!).Text.StartsWith("Restored ", StringComparison.Ordinal))
                {
                    if (DateTime.UtcNow > deadline) throw new InvalidOperationException("Module snapshot UI restore failed: " + ((TextBlock)main.FindName("ImportStatus")!).Text);
                    main.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                }
                if (((ComboBox)main.FindName("SavedModules")!).SelectedItem is null ||
                    global::Dcs.Ipia.Dcs.EditorSnapshotSerializer.Serialize(restored.GetAsync(moduleId).GetAwaiter().GetResult()!) != snapshot.Workspace)
                    throw new InvalidOperationException("Restored snapshot did not populate the saved module selector losslessly.");
                restore.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                deadline = DateTime.UtcNow.AddSeconds(60);
                while (!((TextBlock)main.FindName("ImportStatus")!).Text.StartsWith("Module snapshot was not restored:", StringComparison.Ordinal))
                {
                    if (DateTime.UtcNow > deadline) throw new InvalidOperationException("Duplicate snapshot restore was not blocked.");
                    main.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                }
                if (restored.ListAsync().GetAwaiter().GetResult().Count != 1)
                    throw new InvalidOperationException("Duplicate restore changed saved module membership.");
                main.Close();
                ((TextBox)authoring.FindName("PackageVersion")!).Text = "1.2.3";
                if (!((Button)authoring.FindName("ExportModuleSnapshotButton")!).IsEnabled)
                    throw new InvalidOperationException("Original module became dirty during isolated restore.");
            }
            finally { Directory.Delete(root, true); }
        }
        private static void WaitForOutput(ModuleWorkspaceWindow editor, string prefix)
        {
            var deadline = DateTime.UtcNow.AddSeconds(60);
            var status = (TextBlock)editor.FindName("BuildStatus")!;
            while (!status.Text.StartsWith(prefix, StringComparison.Ordinal))
            {
                if (status.Text.StartsWith("Module output failed:", StringComparison.Ordinal) || DateTime.UtcNow > deadline)
                    throw new InvalidOperationException("Module output UI failed: " + status.Text);
                editor.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            }
            while (!((Button)editor.FindName("BuildButton")!).IsEnabled)
            {
                if (DateTime.UtcNow > deadline) throw new InvalidOperationException("Output action did not release its busy state.");
                editor.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            }
        }
        private static void VerifyNewModuleForm()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-creation-smoke-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False";
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                var defaults = new SqliteSharedUiLayerWorkspaceRepository(connection).SaveAsync(
                    global::Dcs.Ipia.Dcs.SharedUiLayerWorkspaceFactory.CreateAsync().GetAwaiter().GetResult()).GetAwaiter().GetResult();
                var sources = global::Dcs.Ipia.Dcs.ModuleWorkspaceLoader.LoadHardwareAsync(
                    global::Dcs.Ipia.Dcs.BundledSharedCatalog.Root).GetAwaiter().GetResult();
                var window = new NewModuleWindow(repository, defaults, sources, new EmptyDeviceDiscovery());
                Exception? failure = null;
                window.Dispatcher.BeginInvoke(new Action(() => {
                    try
                    {
                        ((TextBox)window.FindName("DcsIdInput")!).Text = "P47-D-30";
                        ((TextBox)window.FindName("DisplayNameInput")!).Text = "P-47D Thunderbolt";
                        var physical = (ComboBox)window.FindName("PhysicalChoices")!;
                        var offline = new DetectedDcsDevice(Guid.NewGuid(), "Captured offline unit", false);
                        physical.ItemsSource = new[] { offline };
                        physical.SelectedItem = offline;
                        ((Button)window.FindName("ManualFileButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        if (physical.SelectedItem is not null) throw new InvalidOperationException("Manual filename entry retained a stale discovered-unit GUID.");
                        var hardware = (ComboBox)window.FindName("HardwareChoices")!;
                        var rows = (ListBox)window.FindName("SelectedUnits")!;
                        var ids = CreationHardware;
                        for (var i = 0; i < ids.Length; i++)
                        {
                            hardware.SelectedItem = hardware.Items.Cast<WorkspaceHardware>().Single(x => x.DeviceId == ids[i]);
                            var choice = (WorkspaceHardware)hardware.SelectedItem;
                            ((TextBox)window.FindName("NativeFileInput")!).Text = new DetectedDcsDevice(Guid.NewGuid(), choice.Label, false).FileName;
                            ((TextBox)window.FindName("UnitInstance")!).Text = "Unit " + i;
                            if (i == 0) ((ComboBox)window.FindName("BaselineChoices")!).SelectedIndex = 0;
                            ((Button)window.FindName("AddUnitButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            if (rows.Items.Count != i + 1) throw new InvalidOperationException("Repeated physical-unit selection did not add an independent row: " + ((TextBlock)window.FindName("CreationError")!).Text);
                        }
                        hardware.SelectedItem = hardware.Items.Cast<WorkspaceHardware>().First(x => x.Label.Contains("Pendular", StringComparison.OrdinalIgnoreCase));
                        ((TextBox)window.FindName("NativeFileInput")!).Text = new DetectedDcsDevice(Guid.NewGuid(), "T-Pendular Rudder", false).FileName;
                        ((Button)window.FindName("AddUnitButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        if (rows.Items.Count != 11) throw new InvalidOperationException("The reported rudder choice was not staged.");
                        ((Button)window.FindName("CreateButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    catch (Exception ex) { failure = ex; window.Close(); }
                }));
                var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
                timeout.Tick += (_, _) => {
                    failure = new InvalidOperationException("New Module creation timed out: " + ((TextBlock)window.FindName("CreationError")!).Text);
                    window.Close();
                };
                timeout.Start();
                window.ShowDialog();
                timeout.Stop();
                if (failure is not null) throw failure;
                var created = window.CreatedWorkspace ?? throw new InvalidOperationException("Physical-unit creation did not reopen from SQLite.");
                var state = global::Dcs.Ipia.Dcs.WorkspaceStructureEditor.Describe(created);
                if (state.Devices.Count(x => x.Layer == AssignmentLayer.Module) != 11)
                    throw new InvalidOperationException("Repeated device rows were lost during creation.");
                var editor = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(created), repository: repository, deviceDiscovery: new EmptyDeviceDiscovery());
                editor.Show();
                editor.UpdateLayout();
                var dropdown = (ComboBox)editor.FindName("DeviceChoice")!;
                if (dropdown.Items.Cast<UiLayerDiagram>().Any(x => x.Title.Contains(" {", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Assignment device labels still expose duplicate native GUIDs.");
                var grid = (DataGrid)editor.FindName("ManagedDevices")!;
                foreach (var device in grid.Items.Cast<WorkspaceDevice>().ToArray())
                {
                    grid.SelectedItem = device;
                    editor.UpdateLayout();
                    if (((TextBox)editor.FindName("ManagedFileName")!).Text != device.FileName)
                        throw new InvalidOperationException("New-module physical device selection failed.");
                }
                if (editor.FindName("RebindDeviceButton") is not Button)
                    throw new InvalidOperationException("Explicit physical rebinding is missing.");
                editor.Close();
            }
            finally { File.Delete(path); }
        }
        private static void VerifyModuleDeleteButton()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-module-delete-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                var connection = "Data Source=" + Path.Combine(root, "ipia.db") + ";Pooling=False";
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                var config = new ModuleConfiguration(new DcsModule(Guid.NewGuid(), "UH-1H", "Huey"), [], [], [], "import");
                repository.SaveAsync(new EditorWorkspaceSnapshot(config, [], [], [], [], [], [],
                    new Dictionary<string, string>())).GetAwaiter().GetResult();
                var settings = new SqliteAppSettingsService(connection, Path.Combine(root, "defaults.json"),
                    Path.Combine(root, "user-settings.json"), Path.Combine(root, "theme.txt"));
                var window = new MainWindow(new global::Dcs.Ipia.Dcs.ModuleWorkspaceLoader(),
                    new global::Dcs.Ipia.Dcs.DcsFolderImporter(), settings, repository);
                var modules = (ComboBox)window.FindName("SavedModules")!;
                var delete = (Button)window.FindName("DeleteModuleButton")!;
                if (delete.IsEnabled || delete.Content?.ToString() != "Delete")
                    throw new InvalidOperationException("Delete must be labeled and disabled without a selected module.");
                window.Show();
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (modules.SelectedItem is null)
                {
                    if (DateTime.UtcNow > deadline)
                        throw new InvalidOperationException("The saved module selector did not populate.");
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                }
                window.UpdateLayout();
                if (!delete.IsEnabled || delete.TranslatePoint(new Point(), window).X <
                    modules.TranslatePoint(new Point(), window).X + modules.ActualWidth)
                    throw new InvalidOperationException("Delete must be enabled to the right of the selected module.");
                modules.SelectedIndex = -1;
                if (delete.IsEnabled)
                    throw new InvalidOperationException("Delete remained enabled after clearing the module selection.");
                window.Close();
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }
        private static void VerifySharedDefaultBindingEditor()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-default-editor-smoke-" + Guid.NewGuid().ToString("N") + ".db");
            var connection = "Data Source=" + path + ";Pooling=False";
            try
            {
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var repository = new SqliteSharedUiLayerWorkspaceRepository(connection);
                var seed = global::Dcs.Ipia.Dcs.SharedUiLayerWorkspaceFactory.CreateAsync().GetAwaiter().GetResult();
                var first = repository.SaveAsync(seed).GetAwaiter().GetResult();
                var modules = new SqliteEditorWorkspaceRepository(connection);
                var pinnedSources = new Dictionary<string, string>(first.Sources, StringComparer.Ordinal);
                var localSources = first.Sources.ToDictionary(x => x.Key, x => x.Value + "\n-- local source\n", StringComparer.Ordinal);
                EditorWorkspaceSnapshot ImpactModule(string id, IReadOnlyDictionary<string, string>? sources) =>
                    new(new ModuleConfiguration(new DcsModule(Guid.NewGuid(), id, "Impact " + id), [], [], [], "smoke"),
                        [], [], [], [], [], [], new Dictionary<string, string>(), sources);
                var pinnedModule = ImpactModule("PINNED", pinnedSources);
                var localModule = ImpactModule("LOCAL", localSources);
                modules.SaveAsync(pinnedModule).GetAwaiter().GetResult();
                modules.SaveAsync(localModule).GetAwaiter().GetResult();
                modules.SaveAsync(ImpactModule("UNKNOWN", null)).GetAwaiter().GetResult();
                var window = new SharedUiLayerWorkspaceWindow(first, repository, modules);
                window.Show();
                var impactRows = (DataGrid)window.FindName("ImpactModules")!;
                var impactChanges = (DataGrid)window.FindName("ImpactChanges")!;
                var impactButton = (Button)window.FindName("ImpactRefreshButton")!;
                void ReviewImpact()
                {
                    impactButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var reviewDeadline = DateTime.UtcNow.AddSeconds(15);
                    while (!impactButton.IsEnabled)
                    {
                        if (DateTime.UtcNow > reviewDeadline)
                            throw new InvalidOperationException("Module impact review did not complete.");
                        window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                    }
                    if (impactRows.Items.OfType<SharedUiLayerModuleImpact>().Count() != 3)
                        throw new InvalidOperationException("Module impact review did not load all saved modules.");
                }
                ReviewImpact();
                if (impactChanges.Items.Count != 0 || impactRows.Items.OfType<SharedUiLayerModuleImpact>().Any(x => x.Status != "No default changes"))
                    throw new InvalidOperationException("An unchanged default draft reports source changes.");
                void WaitModuleUpdate()
                {
                    var updateDeadline = DateTime.UtcNow.AddSeconds(15);
                    while (!impactButton.IsEnabled)
                    {
                        if (DateTime.UtcNow > updateDeadline)
                            throw new InvalidOperationException("Reviewed module update did not complete.");
                        window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                    }
                    if (((TextBlock)window.FindName("AdoptionStatus")!).Text.StartsWith("Module was not changed", StringComparison.Ordinal))
                        throw new InvalidOperationException(((TextBlock)window.FindName("AdoptionStatus")!).Text);
                }
                impactRows.SelectedItem = impactRows.Items.OfType<SharedUiLayerModuleImpact>().Single(x => x.ModuleId == pinnedModule.Configuration.Module.Id);
                ((Button)window.FindName("RecordBaselineButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                WaitModuleUpdate();
                if (modules.GetAsync(pinnedModule.Configuration.Module.Id).GetAwaiter().GetResult()!.SharedDefault?.ContentSha256 != first.ContentSha256)
                    throw new InvalidOperationException("Recording a module baseline did not reopen its verified reference.");
                ReviewImpact();
                const string smokeFile = "Smoke controller {00000001-0000-0000-8001-444553540000}.diff.lua";
                var deviceRows = (DataGrid)window.FindName("DeviceRows")!;
                var cloneChoice = (ComboBox)window.FindName("CloneProfileChoice")!;
                var template = deviceRows.Items.OfType<global::Dcs.Ipia.Dcs.SharedUiLayerDeviceProfile>().First(x => !x.Protected);
                void AddSmokeProfile(bool copy)
                {
                    ((Button)window.FindName("NewProfileButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    ((TextBox)window.FindName("DeviceFileName")!).Text = smokeFile;
                    cloneChoice.SelectedItem = copy ? cloneChoice.Items.OfType<global::Dcs.Ipia.Dcs.SharedUiLayerDeviceProfile>().First(x => x.Path == template.Path) : null;
                    ((Button)window.FindName("AddProfileButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                AddSmokeProfile(true);
                var copy = deviceRows.Items.OfType<global::Dcs.Ipia.Dcs.SharedUiLayerDeviceProfile>().Single(x => x.FileName == smokeFile);
                if (copy.Source != template.Source || !((ComboBox)window.FindName("ProfilePaths")!).Items.Contains(copy.Path) ||
                    !((ListBox)window.FindName("SourcePaths")!).Items.Contains(copy.Path))
                    throw new InvalidOperationException("Shared profile clone did not preserve its source and refresh selectors.");
                deviceRows.SelectedItem = deviceRows.Items.OfType<global::Dcs.Ipia.Dcs.SharedUiLayerDeviceProfile>().Single(x => x.Path == template.Path);
                var renamedFile = "Smoke renamed " + template.FileName;
                ((TextBox)window.FindName("DeviceFileName")!).Text = renamedFile;
                ((Button)window.FindName("RenameProfileButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var renamedProfile = deviceRows.Items.OfType<global::Dcs.Ipia.Dcs.SharedUiLayerDeviceProfile>().Single(x => x.FileName == renamedFile);
                if (renamedProfile.Source != template.Source || !((ComboBox)window.FindName("ModifierDevice")!).Items.Contains(renamedFile[..^9]) ||
                    ((DataGrid)window.FindName("ModifierRows")!).Items.OfType<DcsModifier>().Any(x => x.Device == template.FileName[..^9]))
                    throw new InvalidOperationException("Shared profile rename lost bindings or native modifier references.");
                ((Button)window.FindName("RemoveProfileButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (!deviceRows.Items.OfType<global::Dcs.Ipia.Dcs.SharedUiLayerDeviceProfile>().Any(x => x.FileName == renamedFile))
                    throw new InvalidOperationException("A bound shared profile was removed.");
                deviceRows.SelectedItem = deviceRows.Items.OfType<global::Dcs.Ipia.Dcs.SharedUiLayerDeviceProfile>().Single(x => x.Protected);
                if (((Button)window.FindName("RenameProfileButton")!).IsEnabled || ((Button)window.FindName("RemoveProfileButton")!).IsEnabled)
                    throw new InvalidOperationException("The default keyboard profile is editable.");
                ((Button)window.FindName("DiscardDefaultsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (deviceRows.Items.OfType<global::Dcs.Ipia.Dcs.SharedUiLayerDeviceProfile>().Any(x => x.FileName == smokeFile || x.FileName == renamedFile))
                    throw new InvalidOperationException("Discard did not restore the saved device profiles.");
                AddSmokeProfile(false);
                ((Button)window.FindName("RemoveProfileButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (deviceRows.Items.OfType<global::Dcs.Ipia.Dcs.SharedUiLayerDeviceProfile>().Any(x => x.FileName == smokeFile))
                    throw new InvalidOperationException("An empty unreferenced shared profile was not removed.");
                ((Button)window.FindName("DiscardDefaultsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var modifierRows = (DataGrid)window.FindName("ModifierRows")!;
                var modifierDevices = (ComboBox)window.FindName("ModifierDevice")!;
                var nativeDevice = modifierDevices.Items.OfType<string>().First(x => x != "Keyboard");
                void AddSmokeModifier()
                {
                    ((TextBox)window.FindName("ModifierName")!).Text = "SMOKE_MODIFIER";
                    modifierDevices.SelectedItem = nativeDevice;
                    ((TextBox)window.FindName("ModifierInput")!).Text = "JOY_BTN9999";
                    ((ComboBox)window.FindName("ModifierModeChoice")!).SelectedItem = ModifierMode.Hold;
                    ((Button)window.FindName("AddModifierButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                AddSmokeModifier();
                modifierRows.SelectedItem = modifierRows.Items.OfType<DcsModifier>().Single(x => x.Name == "SMOKE_MODIFIER");
                ((ComboBox)window.FindName("ModifierModeChoice")!).SelectedItem = ModifierMode.Toggle;
                ((Button)window.FindName("UpdateModifierButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (modifierRows.Items.OfType<DcsModifier>().Single(x => x.Name == "SMOKE_MODIFIER").Mode != ModifierMode.Toggle ||
                    !((ComboBox)window.FindName("ClearLayerChoice")!).Items.Contains("SMOKE_MODIFIER"))
                    throw new InvalidOperationException("Shared modifier update did not refresh modes and layer choices.");
                modifierRows.SelectedItem = modifierRows.Items.OfType<DcsModifier>().Single(x => x.Name == "LCtrl");
                if (((Button)window.FindName("UpdateModifierButton")!).IsEnabled || ((Button)window.FindName("RemoveModifierButton")!).IsEnabled)
                    throw new InvalidOperationException("Built-in keyboard defaults are editable.");
                modifierRows.SelectedItem = modifierRows.Items.OfType<DcsModifier>().Single(x => x.Name == "SMOKE_MODIFIER");
                ((Button)window.FindName("RemoveModifierButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (modifierRows.Items.OfType<DcsModifier>().Any(x => x.Name == "SMOKE_MODIFIER"))
                    throw new InvalidOperationException("Unused shared modifier was not removed.");
                AddSmokeModifier();
                ((Button)window.FindName("DiscardDefaultsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (modifierRows.Items.OfType<DcsModifier>().Any(x => x.Name == "SMOKE_MODIFIER"))
                    throw new InvalidOperationException("Discard did not restore saved shared modifiers.");
                var catalogRows = (DataGrid)window.FindName("CatalogRows")!;
                var addCommand = (Button)window.FindName("AddCommandButton")!;
                void AddCatalogCommand()
                {
                    ((TextBox)window.FindName("CatalogIdentity")!).Text = "smoke-command";
                    ((TextBox)window.FindName("CatalogCode")!).Text = "SMOKE_COMMAND";
                    ((TextBox)window.FindName("CatalogLabel")!).Text = "Smoke command";
                    ((TextBox)window.FindName("CatalogCategory")!).Text = "General";
                    addCommand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                AddCatalogCommand();
                catalogRows.SelectedItem = catalogRows.Items.OfType<global::Dcs.Ipia.Dcs.SharedUiLayerCommand>().Single(x => x.Id == "smoke-command");
                ((TextBox)window.FindName("CatalogLabel")!).Text = "Updated smoke command";
                ((TextBox)window.FindName("CatalogCategory")!).Text = "VR";
                ((Button)window.FindName("UpdateCommandButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (!((ComboBox)window.FindName("CommandChoice")!).Items.OfType<DraftCommand>()
                    .Any(x => x.Id == "SMOKE_COMMAND" && x.Name == "Updated smoke command"))
                    throw new InvalidOperationException("Catalog changes did not refresh the binding command choices.");
                ((Button)window.FindName("RemoveCommandButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (catalogRows.Items.OfType<global::Dcs.Ipia.Dcs.SharedUiLayerCommand>().Any(x => x.Id == "smoke-command"))
                    throw new InvalidOperationException("Unused catalog command was not removed.");
                ((Button)window.FindName("DiscardDefaultsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (catalogRows.Items.Count != global::Dcs.Ipia.Dcs.SharedUiLayerCommandEditor.Commands(first).Count)
                    throw new InvalidOperationException("Discard did not restore the shared command catalog.");
                var profiles = (ComboBox)window.FindName("ProfilePaths")!;
                profiles.SelectedIndex = 0;
                var profilePath = (string)profiles.SelectedItem;
                var rows = (DataGrid)window.FindName("BindingRows")!;
                var clearLayer = (ComboBox)window.FindName("ClearLayerChoice")!;
                clearLayer.SelectedItem = "Base";
                ((Button)window.FindName("ClearLayerButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (rows.Items.OfType<DcsBinding>().Any(x => x.Disposition != BindingDisposition.Removed && x.Reformers.Count == 0) ||
                    !((Button)window.FindName("SaveDefaultsButton")!).IsEnabled)
                    throw new InvalidOperationException("Shared layer clear did not stage all base keys and axes.");
                ((Button)window.FindName("DiscardDefaultsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (!rows.Items.OfType<DcsBinding>().Any(x => x.Disposition != BindingDisposition.Removed))
                    throw new InvalidOperationException("Discard did not restore cleared shared layer bindings.");
                var selected = rows.Items.OfType<DcsBinding>().First(x => x.Section == BindingSection.Key && x.Disposition != BindingDisposition.Removed);
                rows.SelectedItem = selected;
                ((TextBox)window.FindName("InputKey")!).Text = "JOY_BTN999";
                var update = (Button)window.FindName("UpdateBindingButton")!;
                update.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var save = (Button)window.FindName("SaveDefaultsButton")!;
                if (!save.IsEnabled) throw new InvalidOperationException("Shared default binding edit was not staged.");
                if (impactRows.Items.Count != 0)
                    throw new InvalidOperationException("An edit left a stale module impact report visible.");
                ReviewImpact();
                var pinnedImpact = impactRows.Items.OfType<SharedUiLayerModuleImpact>().Single(x => x.ModuleId == pinnedModule.Configuration.Module.Id);
                var localImpact = impactRows.Items.OfType<SharedUiLayerModuleImpact>().Single(x => x.ModuleId == localModule.Configuration.Module.Id);
                if (pinnedImpact.Sources.Single(x => x.Path == profilePath).Status != "Matches baseline" ||
                    localImpact.Sources.Single(x => x.Path == profilePath).Status != "Different source")
                    throw new InvalidOperationException("Module impact evidence missed pinned or local source versions.");
                impactRows.SelectedItem = localImpact;
                if (!((DataGrid)window.FindName("ImpactDetails")!).Items.OfType<SharedUiLayerModuleSourceImpact>()
                    .Any(x => x.Path == profilePath && x.Status == "Different source"))
                    throw new InvalidOperationException("Module selection did not display source evidence.");
                ((Button)window.FindName("DiscardDefaultsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (save.IsEnabled || !rows.Items.OfType<DcsBinding>().Any(x => x.InputKey == selected.InputKey && x.CommandId == selected.CommandId))
                    throw new InvalidOperationException("Discard did not restore the saved shared bindings.");
                if (impactRows.Items.Count != 0)
                    throw new InvalidOperationException("Discard left a stale impact report visible.");
                rows.SelectedItem = rows.Items.OfType<DcsBinding>().First(x => x.InputKey == selected.InputKey && x.CommandId == selected.CommandId);
                ((TextBox)window.FindName("InputKey")!).Text = "JOY_BTN999";
                update.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                AddCatalogCommand();
                AddSmokeModifier();
                AddSmokeProfile(false);
                save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var status = (TextBlock)window.FindName("EditorStatus")!;
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (!status.Text.StartsWith("Shared default revision saved", StringComparison.Ordinal))
                {
                    if (DateTime.UtcNow > deadline || status.Text.StartsWith("Defaults were not saved", StringComparison.Ordinal))
                        throw new InvalidOperationException("Shared binding save failed: " + status.Text);
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                }
                var saved = repository.GetAsync().GetAwaiter().GetResult()!;
                if (!global::Dcs.Ipia.Dcs.SharedUiLayerCommandEditor.Commands(saved).Any(x => x.Id == "smoke-command"))
                    throw new InvalidOperationException("Shared catalog command did not reopen in the saved revision.");
                if (!global::Dcs.Ipia.Dcs.SharedUiLayerModifierEditor.Modifiers(saved).Any(x =>
                    x.Name == "SMOKE_MODIFIER" && x.Device == nativeDevice && x.Key == "JOY_BTN9999" && x.Mode == ModifierMode.Hold) ||
                    global::Dcs.Ipia.Dcs.SharedUiLayerModifierEditor.Modifiers(repository.GetAsync(1).GetAwaiter().GetResult()!).Any(x => x.Name == "SMOKE_MODIFIER"))
                    throw new InvalidOperationException("Shared modifier revision did not reopen independently of its history.");
                if (!global::Dcs.Ipia.Dcs.SharedUiLayerProfileEditor.Profiles(saved).Any(x => x.FileName == smokeFile && x.ActiveBindings == 0) ||
                    global::Dcs.Ipia.Dcs.SharedUiLayerProfileEditor.Profiles(repository.GetAsync(1).GetAwaiter().GetResult()!).Any(x => x.FileName == smokeFile))
                    throw new InvalidOperationException("Shared device additions did not reopen independently of revision history.");
                if (saved.Revision != 2 || !global::Dcs.Ipia.Dcs.SharedUiLayerBindingEditor.Bindings(saved, profilePath)
                    .Any(x => x.InputKey == "JOY_BTN999" && x.CommandId == selected.CommandId) || save.IsEnabled)
                    throw new InvalidOperationException("Shared binding revision did not reopen correctly.");
                if (repository.GetAsync(1).GetAwaiter().GetResult()!.Sources[profilePath] != first.Sources[profilePath])
                    throw new InvalidOperationException("Shared binding save changed revision history.");
                if (impactRows.Items.Count != 0 || modules.GetAsync(pinnedModule.Configuration.Module.Id).GetAwaiter().GetResult()!.Sources![profilePath] != first.Sources[profilePath] ||
                    modules.GetAsync(localModule.Configuration.Module.Id).GetAwaiter().GetResult()!.Sources![profilePath] != localSources[profilePath])
                    throw new InvalidOperationException("Saving defaults changed saved modules or retained a stale impact report.");
                ReviewImpact();
                impactRows.SelectedItem = impactRows.Items.OfType<SharedUiLayerModuleImpact>().Single(x => x.ModuleId == pinnedModule.Configuration.Module.Id);
                if (!((Button)window.FindName("ExportSharedButton")!).IsEnabled ||
                    !((Button)window.FindName("RestoreSharedButton")!).IsEnabled ||
                    !((Button)window.FindName("RebuildSharedButton")!).IsEnabled)
                    throw new InvalidOperationException("Saved shared defaults did not enable independent output and restore.");
                var adoptionRows = (DataGrid)window.FindName("AdoptionChoices")!;
                if (!adoptionRows.Items.OfType<SharedUiLayerAdoptionChoice>().Any(x => x.Path == profilePath && x.TakeDefault && !x.Conflict))
                    throw new InvalidOperationException("Safe baseline source changes were not offered for adoption.");
                ((Button)window.FindName("ReviewSemanticButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (!((Button)window.FindName("AdoptDefaultsButton")!).IsEnabled)
                    throw new InvalidOperationException(((TextBlock)window.FindName("AdoptionStatus")!).Text);
                ((Button)window.FindName("AdoptDefaultsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                WaitModuleUpdate();
                var adopted = modules.GetAsync(pinnedModule.Configuration.Module.Id).GetAwaiter().GetResult()!;
                if (adopted.SharedDefault?.Revision != 2 || adopted.Sources![profilePath] != saved.Sources[profilePath] ||
                    modules.GetAsync(localModule.Configuration.Module.Id).GetAwaiter().GetResult()!.Sources![profilePath] != localSources[profilePath])
                    throw new InvalidOperationException("Reviewed adoption did not preserve module isolation.");
                ReviewImpact();
                impactRows.SelectedItem = impactRows.Items.OfType<SharedUiLayerModuleImpact>().Single(x => x.ModuleId == pinnedModule.Configuration.Module.Id);
                ((Button)window.FindName("RollbackAdoptionButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                WaitModuleUpdate();
                var rolledBack = modules.GetAsync(pinnedModule.Configuration.Module.Id).GetAwaiter().GetResult()!;
                if (rolledBack.SharedDefault?.Revision != 1 || rolledBack.Sources![profilePath] != first.Sources[profilePath])
                    throw new InvalidOperationException("Adoption rollback did not restore the prior baseline and sources.");
                ReviewImpact();
                using (var broken = new SqliteConnection(connection))
                {
                    broken.Open();
                    using var tamper = broken.CreateCommand();
                    tamper.CommandText = "UPDATE editor_workspaces SET snapshot_json='{' WHERE module_id=$id";
                    tamper.Parameters.AddWithValue("$id", localModule.Configuration.Module.Id.ToString("D"));
                    tamper.ExecuteNonQuery();
                }
                impactButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var failedReviewDeadline = DateTime.UtcNow.AddSeconds(15);
                while (!impactButton.IsEnabled)
                {
                    if (DateTime.UtcNow > failedReviewDeadline)
                        throw new InvalidOperationException("Failed impact review did not restore controls.");
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                }
                if (impactRows.Items.Count != 0 || impactChanges.Items.Count != 0 ||
                    !((TextBlock)window.FindName("ImpactStatus")!).Text.StartsWith("Unable to review module impact", StringComparison.Ordinal) ||
                    !((StackPanel)window.FindName("BindingFields")!).IsEnabled)
                    throw new InvalidOperationException("Failed impact review retained stale results or disabled editing.");
                window.Close();
                System.Console.WriteLine("Shared default binding editor staged, discarded, saved and reopened an independent revision.");
            }
            finally { File.Delete(path); }
        }
        #endregion END private helpers
    }
}
