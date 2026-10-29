using System.IO;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
using Heimdall.Application;
using Heimdall.Domain;
using Heimdall.Dcs;
using Heimdall.Infrastructure;
using Microsoft.Data.Sqlite;
using Heimdall.App;

namespace Heimdall.App.Smoke
{
    /// <summary>Opens both read-only WPF preview modes on a Windows CI runner.</summary>
    public static class Program
    {
        private static readonly string[] NavigationHeaders = ["Devices", "Assignments & Testing", "Reference Sheets"];
        private static readonly string[] CommandImportButtons = ["LoadCommandJsonButton", "LoadCommandHtmlButton"];
        private static readonly string[] CreationHardware = ["tm-mfd", "tm-mfd", "tm-mfd", "tm-mfd", "logitech-throttle-quadrant", "logitech-throttle-quadrant",
            "moza-ab9", "vkb-f14-gunfighter", "winctrl-pto2", "winctrl-icp"];
        #region BEGIN Public
        [STAThread]
        /// <summary>Checks that imported source data reaches a visible WPF preview window.</summary>
        /// <returns>Zero when both windows render their expected content.</returns>
        public static int Main()
        {
            WorkspaceLayoutStore.FilePath = Path.Combine(Path.GetTempPath(),
                "ipia-layout-smoke-" + Guid.NewGuid().ToString("N"), "workspace-layout.json");
            Console.WriteLine("Smoke: JSON-configured physical logging.");
            LoggingSmoke.Verify();
            // A plain host loads the real themes without queuing production App.OnStartup during dispatcher pumping.
            var app = new System.Windows.Application();
            foreach (var theme in new[] { "Light", "Styles", "Primitives", "Collections", "Controls", "Menus" })
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Heimdall;component/Themes/" + theme + ".xaml", UriKind.Absolute)
                });
            ApplicationTheme.Initialize();
            Console.WriteLine("Smoke: Free Edition and signed Owner entitlement.");
            EditionSmoke.Verify();
            FreeSnapshotImportSmoke.Verify();
            BrandingSmoke.Verify();
            global::Heimdall.Dcs.BundledSharedCatalog.Validate(global::Heimdall.Dcs.BundledSharedCatalog.Root);
            SolRHardwareSmoke.Verify();
            Console.WriteLine("Smoke: game selection and Elite profile authoring.");
            GameWorkspaceSmoke.Verify();
            SharedConsumerHostSmoke.Verify();
            DcsConsumerMigrationSmoke.Verify();
            Console.WriteLine("Smoke: Squadrons selection, native authoring and saved profiles.");
            SquadronsWorkspaceSmoke.Verify();
            SquadronsDeviceLinkSmoke.Verify();
            SquadronsControllerInputSmoke.Verify();
            SquadronsAssignmentViewSmoke.Verify();
            SquadronsDrawingWorkspaceSmoke.Verify();
            SquadronsFreshAssignmentSmoke.Verify();
            SquadronsFeatureNavigationSmoke.Verify();
            var defaultsWindow = new SharedUiLayerWorkspaceWindow(new SharedUiLayerWorkspace(1, new string('a', 40),
                new Dictionary<string, string> { ["assets/shared/ui-layer/functions.json"] = "{}" }));
            defaultsWindow.Show();
            defaultsWindow.UpdateLayout();
            UserGuideCapture.Save(defaultsWindow, "shared-ui-layer");
            Console.WriteLine("Smoke: rendered theme control states and dropdown keyboard behavior.");
            ThemeControlSmoke.Verify();
            MainWindowThemeSmoke.Verify();
            ApplicationThemeSmoke.Verify();
            AppearanceEditorSmoke.Verify();
            ScopedBackgroundSmoke.Verify();
            BrowseDialogSmoke.Verify();
            VerifySharedDefaultBindingEditor();
            Console.WriteLine("Smoke: module deletion.");
            VerifyModuleDeleteButton();
            Console.WriteLine("Smoke: user data recovery.");
            UserDataRecoverySmoke.Verify();
            SupportReportSmoke.Verify();
            UserMappingsSmoke.Verify();
            Console.WriteLine("Smoke: new module form.");
            VerifyNewModuleForm();
            Console.WriteLine("Smoke: Saved Games import.");
            VerifySavedGamesImportForm();
            Console.WriteLine("Smoke: module authoring.");
            VerifyModuleAuthoring();
            Console.WriteLine("Smoke: reserved modifier callouts.");
            ModifierReservationSmoke.Verify();
            AssignmentsLabelSmoke.Verify();
            SearchReplaceLabelsSmoke.Verify();
            Console.WriteLine("Smoke: shared feature action bar.");
            FeatureActionBarSmoke.Verify();
            CompactWorkspaceSmoke.Verify();
            UnifiedWorkspaceSmoke.VerifyDcs();
            Console.WriteLine("Smoke: draft notifications across cached features.");
            DraftNotificationsSmoke.Verify();
            Console.WriteLine("Smoke: device page deletion.");
            VerifyDevicePageDeletion();
            Console.WriteLine("Smoke: summary order.");
            SummaryCardOrderSmoke.Verify();
            Console.WriteLine("Smoke: page row ordering.");
            PageOrderSmoke.Verify();
            Console.WriteLine("Smoke: interactive training.");
            TrainingSmoke.Verify();
            Console.WriteLine("Smoke: input testing.");
            ContrastOutlineSmoke.Verify();
            InputTestSmoke.Verify();
            var defaultText = (TextBox)WorkspaceUi.Find(defaultsWindow, "SourceText")!;
            if (!defaultsWindow.IsVisible || !defaultText.IsReadOnly || defaultText.Text != "{}")
                throw new InvalidOperationException("Shared defaults did not reopen in the read-only source preview.");
            Console.WriteLine("Smoke: hardware inventory.");
            HardwareInventorySmoke.Verify();
            FreeInventorySetupSmoke.Verify();
            SetupSmoke.Verify();
            GenericHardwareSmoke.Verify();
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
            if (!System.Text.RegularExpressions.Regex.IsMatch(folderWindow.Title, @" · version \d+\.\d+\.\d+\.\d+$"))
                throw new InvalidOperationException("Workspace title must show only a four-part application version.");
            defaultsWindow.Close();
            folderWindow.UpdateLayout();
            var folderNav = (ListBox)folderWindow.FindName("FeatureNav")!;
            var diagnostics = (ListBox)folderWindow.FindName("ImportDiagnostics")!;
            if (!folderWindow.IsVisible || !diagnostics.Items.Cast<string>().Any(x => x.Contains("bad-profile", StringComparison.Ordinal)))
                throw new InvalidOperationException("Saved Games preview did not show its diagnostics.");
            if (!folderNav.Items.Cast<ListBoxItem>().Where(x => x.Visibility == Visibility.Visible).Take(3).Select(x => x.Content.ToString()).SequenceEqual(NavigationHeaders))
                throw new InvalidOperationException("Common workspace tasks must precede DCS-specific tools.");
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
            if (!savedWindow.IsVisible || ((Canvas)WorkspaceUi.Find(savedWindow, "Diagram")!).Children.Count == 0)
                throw new InvalidOperationException("Database-backed graphical assignment editor did not display.");
            var managedDevices = (DataGrid)WorkspaceUi.Find(savedWindow, "ManagedDevices")!;
            managedDevices.SelectedIndex = 0;
            if (managedDevices.Items.Count != 1 || ((TextBox)WorkspaceUi.Find(savedWindow, "ManagedFileName")!).Text != "MFD3.diff.lua" ||
                !((GroupBox)WorkspaceUi.Find(savedWindow, "MfdLabelFields")!).IsEnabled)
                throw new InvalidOperationException("The Devices editor did not show the stored module MFD and its editable fields.");
            var managedModifiers = (DataGrid)WorkspaceUi.Find(savedWindow, "ManagedModifiers")!;
            if (managedModifiers.Items.Count != 1 || ((WorkspaceModifier)managedModifiers.Items[0]).Name != "MOZA_F16_F18_BTN3")
                throw new InvalidOperationException("The Modifiers editor did not show the stored module definitions.");
            var layers = (ComboBox)WorkspaceUi.Find(savedWindow, "Layers")!;
            var scope = (ComboBox)WorkspaceUi.Find(savedWindow, "Scope")!;
            string Caption(string control)
            {
                var canvas = (Canvas)WorkspaceUi.Find(savedWindow, "Diagram")!;
                var surface = canvas.Children.OfType<Canvas>().Single();
                var target = surface.Children.OfType<Border>().Single(x => x.Tag is HardwareGraphicControl item &&
                                                                           item.Control == control);
                return ((TextBlock)target.Child).Text;
            }
            if (Caption("mfd-osb-t1") != "Base authored" || Caption("trim-up") != "Pilot" ||
                Caption("mfd-osb-t2") != "Unassigned" ||
                ((Canvas)WorkspaceUi.Find(savedWindow, "Diagram")!)
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
                Caption("mfd-osb-t2") != "Unassigned")
                throw new InvalidOperationException(
                    "UI Layer displayed an assignment or authored label from another layer.");
            layers.SelectedIndex = 0;
            savedWindow.UpdateLayout();
            if (Caption("mfd-osb-t1") != "Unassigned" || Caption("mfd-osb-t2") != "Unassigned")
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
            ((ComboBox)WorkspaceUi.Find(clearedWindow, "Scope")!).SelectedIndex = 1;
            ((ComboBox)WorkspaceUi.Find(clearedWindow, "Layers")!).SelectedIndex = 1;
            clearedWindow.UpdateLayout();
            var clearedSurface = ((Canvas)WorkspaceUi.Find(clearedWindow, "Diagram")!).Children.OfType<Canvas>().Single();
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
                ((ComboBox)WorkspaceUi.Find(preferencesWindow, "ThemeChoice")!).SelectedIndex = 1;
                ((ComboBox)WorkspaceUi.Find(preferencesWindow, "ConflictChoice")!).SelectedIndex = 2;
                if (settings.Current.Theme != AppearanceTheme.Dark ||
                    settings.Current.OccupiedDestinationBehavior != OccupiedDestinationBehavior.Reject ||
                    new SqliteAppSettingsService(preferenceConnection, Path.Combine(preferencesRoot, "defaults.json"),
                                                 Path.Combine(preferencesRoot, "user-settings.json"),
                                                 Path.Combine(preferencesRoot, "theme.txt"))
                            .Current.OccupiedDestinationBehavior != OccupiedDestinationBehavior.Reject)
                    throw new InvalidOperationException(
                        "Settings window did not apply and save theme and destination preferences.");
                var skinChoice = (ComboBox)WorkspaceUi.Find(preferencesWindow, "ThemeChoice")!;
                foreach (var skin in Enum.GetValues<AppearanceTheme>())
                {
                    skinChoice.SelectedIndex = (int)skin;
                    preferencesWindow.UpdateLayout();
                    var saved = new SqliteAppSettingsService(preferenceConnection,
                        Path.Combine(preferencesRoot, "defaults.json"), Path.Combine(preferencesRoot, "user-settings.json"),
                        Path.Combine(preferencesRoot, "theme.txt"));
                    if (settings.Current.Theme != skin || saved.Current.Theme != skin ||
                        saved.Current.OccupiedDestinationBehavior != OccupiedDestinationBehavior.Reject)
                        throw new InvalidOperationException("Skin selection failed to apply/persist: " + skin);
                    var palette = app.Resources.MergedDictionaries.Single(x =>
                        x.Source?.OriginalString.EndsWith("/" + skin + ".xaml", StringComparison.Ordinal) == true);
                    if (palette is null) throw new InvalidOperationException("Skin was not applied immediately.");
                    var reopened = new SettingsWindow(saved);
                    reopened.Show(); reopened.UpdateLayout();
                    if (((ComboBox)WorkspaceUi.Find(reopened, "ThemeChoice")!).SelectedIndex != (int)skin)
                        throw new InvalidOperationException("Settings reopened with the wrong skin.");
                    reopened.Close();
                }
                var reset = (Button)WorkspaceUi.Find(preferencesWindow, "ResetAppearance")!;
                reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (settings.Current.Theme != AppearanceTheme.Light || skinChoice.SelectedIndex != 0 ||
                    settings.Current.OccupiedDestinationBehavior != OccupiedDestinationBehavior.Reject)
                    throw new InvalidOperationException("Appearance reset changed unrelated settings or failed.");
                preferencesWindow.Close();
                var mainSettings = new SqliteAppSettingsService(preferenceConnection,
                    Path.Combine(preferencesRoot, "defaults.json"), Path.Combine(preferencesRoot, "user-settings.json"),
                    Path.Combine(preferencesRoot, "theme.txt"));
                if (mainSettings.Current.Theme != AppearanceTheme.Light)
                    throw new InvalidOperationException("Default appearance was not persisted.");
                foreach (var skin in Enum.GetValues<AppearanceTheme>())
                {
                    mainSettings.TryUpdateTheme(skin);
                    var main = new MainWindow(new global::Heimdall.Dcs.ModuleWorkspaceLoader(),
                        new global::Heimdall.Dcs.DcsFolderImporter(), mainSettings,
                        new SqliteEditorWorkspaceRepository(preferenceConnection), deviceDiscovery: new EmptyDeviceDiscovery());
                    try
                    {
                        main.Show(); main.UpdateLayout();
                        var picker = (ComboBox)WorkspaceUi.Find(main, "ThemePicker")!;
                        if (picker.SelectedIndex != (int)skin || mainSettings.Current.Theme != skin)
                            throw new InvalidOperationException("Main window lost its saved skin on startup.");
                        picker.SelectedIndex = ((int)skin + 1) % Enum.GetValues<AppearanceTheme>().Length;
                        if ((int)mainSettings.Current.Theme != picker.SelectedIndex)
                            throw new InvalidOperationException("Main window skin selector failed to persist.");
                    }
                    finally { main.Close(); }
                }
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
        private static void VerifyDevicePageDeletion()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-page-deletion-smoke-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False";
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                var module = new DcsModule(Guid.NewGuid(), "PAGE-DELETE", "Page deletion");
                var profile = new DeviceProfile(Guid.NewGuid(), "stick", null, "Stick", "Stick.diff.lua");
                var live = new KneeboardPage(Guid.NewGuid(), module.Id, null, KneeboardPageKind.Device, "live", "Stick", [], 0);
                var orphan = new KneeboardPage(Guid.NewGuid(), module.Id, null, KneeboardPageKind.Device, "orphan", "Deleted device", [], 4);
                var summary = new KneeboardPage(Guid.NewGuid(), module.Id, null, KneeboardPageKind.Summary, "summary", "Summary", [], 9);
                var diagram = new UiLayerDiagram("Stick", null, "stick.svg", 100, 100, [], []) {
                    ProfileId = profile.Id, SupportsUiLayer = false,
                    Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'/>",
                    Controls = [new("button", "Button", "JOY_BTN1", 0, 0, 10, 10)]
                };
                var workspace = new EditorWorkspaceSnapshot(new(module, [profile], [], [], "smoke"), [live, orphan, summary], [], [diagram], [], [], [],
                    new Dictionary<string, string>(), new Dictionary<string, string> {
                        ["config/kneeboard.json"] = """{"profiles":{"p":"src/Config/Input/PAGE-DELETE/joystick/Stick.diff.lua"},"pages":[{"profile":"p","file":"live","title":"Stick"},{"profile":"missing","file":"orphan","title":"Deleted device"}],"summaryPages":[{"file":"summary","title":"Summary","kicker":"CONTROLS","items":[{"key":"STICK","text":"Controls","accent":"cyan"}]}]}""",
                        ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = "local modifiers = {} return modifiers"
                    });
                repository.SaveAsync(workspace).GetAwaiter().GetResult();
                var window = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(workspace), repository: repository, deviceDiscovery: new EmptyDeviceDiscovery());
                window.Show();
                var pages = (DataGrid)WorkspaceUi.Find(window, "AuthoredPages")!;
                var removePage = (Button)WorkspaceUi.Find(window, "RemoveAuthoredPageButton")!;
                ((Button)WorkspaceUi.Find(window, "GenerateSummaryButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (pages.Items.Count != 4 || pages.Items.Cast<KneeboardPage>().Count(x => x.Kind == KneeboardPageKind.Summary) != 2)
                    throw new InvalidOperationException("Generate Summary did not append a summary page.");
                var generated = pages.Items.Cast<KneeboardPage>().Single(x => x.StableKey == "summary-2");
                pages.SelectedItem = generated;
                var cards = (ListBox)WorkspaceUi.Find(window, "SummaryCards")!;
                if (cards.Items.Count != 2 || cards.Items.Cast<ModuleSummaryCard>().Last().Text != "Deleted device")
                    throw new InvalidOperationException("Generated summary did not include all device pages.");
                ((Button)WorkspaceUi.Find(window, "DiscardAuthoringButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (pages.Items.Count != 3) throw new InvalidOperationException("Discard did not restore pages after summary generation.");
                pages.SelectedItem = pages.Items.Cast<KneeboardPage>().Single(x => x.Id == live.Id);
                if (!removePage.IsEnabled) throw new InvalidOperationException("A live device page could not be removed.");
                removePage.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (pages.Items.Count != 2 || ((DataGrid)WorkspaceUi.Find(window, "ManagedDevices")!).Items.Count != 1)
                    throw new InvalidOperationException("Page removal did not retain its device.");
                ((Button)WorkspaceUi.Find(window, "DiscardAuthoringButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (pages.Items.Count != 3) throw new InvalidOperationException("Discard did not restore the live device page.");
                pages.SelectedItem = pages.Items.Cast<KneeboardPage>().Single(x => x.Id == orphan.Id);
                if (!removePage.IsEnabled) throw new InvalidOperationException("An orphan page could not be removed.");
                removePage.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ((Button)WorkspaceUi.Find(window, "DiscardAuthoringButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (pages.Items.Count != 3) throw new InvalidOperationException("Discard did not restore the orphan draft.");
                pages.SelectedItem = pages.Items.Cast<KneeboardPage>().Single(x => x.Id == orphan.Id);
                removePage.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ClickWithDispatcherContext((Button)WorkspaceUi.Find(window, "SaveAuthoringButton")!);
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (repository.GetAsync(module.Id).GetAwaiter().GetResult()!.Pages.Count != 2)
                {
                    if (DateTime.UtcNow > deadline) throw new InvalidOperationException("Orphan removal did not save.");
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                }
                ((DataGrid)WorkspaceUi.Find(window, "ManagedDevices")!).SelectedIndex = 0;
                ((Button)WorkspaceUi.Find(window, "RemoveManagedDeviceButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (pages.Items.Count != 1 || pages.Items.Cast<KneeboardPage>().Single().Id != summary.Id || pages.Items.Cast<KneeboardPage>().Single().Order != 0)
                    throw new InvalidOperationException("Device removal did not remove its page and compact order.");
                ClickWithDispatcherContext((Button)WorkspaceUi.Find(window, "SaveAuthoringButton")!);
                deadline = DateTime.UtcNow.AddSeconds(15);
                while (repository.GetAsync(module.Id).GetAwaiter().GetResult()!.Pages.Count != 1)
                {
                    if (DateTime.UtcNow > deadline) throw new InvalidOperationException("Device deletion did not save.");
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                }
                window.Close();
            }
            finally { File.Delete(path); }
        }

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
                var integrationPath = Path.Combine(htmlRoot, "instructions.json");
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
                    commandFilePicker: html => html ? [htmlPath] : [jsonPath], integrationFilePicker: () => integrationPath,
                    axisDefaultsFilePicker: () => Path.Combine(htmlRoot, "axes.diff.lua"));
                window.Show();
                window.UpdateLayout();
                ((DataGrid)WorkspaceUi.Find(window, "ManagedDevices")!).SelectedIndex = 0;
                if (((TextBox)WorkspaceUi.Find(window, "ManagedFileName")!).Text != profile.SourceName)
                    throw new InvalidOperationException("A newly created device without page metadata could not be selected.");
                ((Button)WorkspaceUi.Find(window, "MissingDevicePagesButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (((DataGrid)WorkspaceUi.Find(window, "AuthoredPages")!).Items.Count != 1)
                    throw new InvalidOperationException("Missing device pages did not initialize from the New Module manifest.");
                ((TextBox)WorkspaceUi.Find(window, "AircraftCommandCode")!).Text = "KEY";
                ((TextBox)WorkspaceUi.Find(window, "AircraftCommandName")!).Text = "Test action";
                ((TextBox)WorkspaceUi.Find(window, "AircraftCommandCategory")!).Text = "Keys";
                ((Button)WorkspaceUi.Find(window, "StageAircraftCommandButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var commands = (DataGrid)WorkspaceUi.Find(window, "AuthoredCommands")!;
                var palette = (DataGrid)WorkspaceUi.Find(window, "Commands")!;
                if (commands.Items.Count != 1 || !WorkspaceUi.Entries(palette).Any(x => x.Id == "KEY"))
                    throw new InvalidOperationException("Authored aircraft command did not refresh the assignment palette.");
                foreach (var buttonName in CommandImportButtons)
                {
                    window.Dispatcher.BeginInvoke(new Action(() => {
                        var reviewWindow = System.Windows.Application.Current.Windows.OfType<CommandImportReviewWindow>().Single();
                        if (((DataGrid)WorkspaceUi.Find(reviewWindow, "ImportChanges")!).Items.Count != 1 ||
                            !((Button)WorkspaceUi.Find(reviewWindow, "StageImportButton")!).IsEnabled)
                            throw new InvalidOperationException("Command import review did not expose the validated addition.");
                        ((Button)WorkspaceUi.Find(reviewWindow, "StageImportButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }));
                    ((Button)WorkspaceUi.Find(window, buttonName)!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                if (commands.Items.Count != 3 || !WorkspaceUi.Entries(palette).Any(x => x.Id == "a2001cdnil") ||
                    !((ComboBox)WorkspaceUi.Find(window, "CommandCategory")!).Items.Cast<string>().Contains("Flight"))
                    throw new InvalidOperationException("JSON/HTML load did not refresh aircraft catalog, palette and categories.");
                window.Dispatcher.BeginInvoke(new Action(() => {
                    System.Windows.Application.Current.Windows.OfType<CommandImportReviewWindow>().Single().DialogResult = false;
                }));
                ((Button)WorkspaceUi.Find(window, "LoadCommandHtmlButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (commands.Items.Count != 3) throw new InvalidOperationException("Cancelling import changed the catalog.");
                ((ComboBox)WorkspaceUi.Find(window, "AuthoredPageKind")!).SelectedItem = KneeboardPageKind.Summary;
                ((TextBox)WorkspaceUi.Find(window, "AuthoredPageFile")!).Text = "overview";
                ((TextBox)WorkspaceUi.Find(window, "AuthoredPageTitle")!).Text = "OVERVIEW";
                ((TextBox)WorkspaceUi.Find(window, "AuthoredPageSubtitle")!).Text = "CONTROLS";
                ((TextBox)WorkspaceUi.Find(window, "SummaryCardKey")!).Text = "STICK";
                ((TextBox)WorkspaceUi.Find(window, "SummaryCardText")!).Text = "Flight controls";
                ((Button)WorkspaceUi.Find(window, "SetSummaryCardButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ((Button)WorkspaceUi.Find(window, "StageAuthoredPageButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (((DataGrid)WorkspaceUi.Find(window, "AuthoredPages")!).Items.Count != 2)
                    throw new InvalidOperationException("Authored summary page did not stage.");
                ((Button)WorkspaceUi.Find(window, "SaveAuthoringButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (repository.GetAsync(configuration.Module.Id).GetAwaiter().GetResult()!.Pages.Count != 2)
                {
                    if (DateTime.UtcNow > deadline)
                        throw new InvalidOperationException("Commands and pages did not save to SQLite.");
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                }
                WorkspaceUi.SelectCommand(commands, "KEY");
                ((TextBox)WorkspaceUi.Find(window, "AircraftCommandName")!).Text = "Discarded name";
                ((Button)WorkspaceUi.Find(window, "StageAircraftCommandButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ((Button)WorkspaceUi.Find(window, "DiscardAuthoringButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (WorkspaceUi.Entries(commands).Single(x => x.Id == "KEY").Name != "Test action")
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
                ((Button)WorkspaceUi.Find(window, "LoadCommandJsonButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (commands.Items.Count != 4) throw new InvalidOperationException("The discardable import did not stage.");
                ((Button)WorkspaceUi.Find(window, "DiscardAuthoringButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (commands.Items.Count != 3 || repository.GetAsync(configuration.Module.Id).GetAwaiter().GetResult()!.ModuleCommands.Count != 3)
                    throw new InvalidOperationException("Discard did not restore the saved catalog after import.");
                VerifyIntegrationInstructions(window, repository, configuration.Module.Id, integrationPath);
                VerifyAxisDefaults(window, repository, configuration.Module.Id, Path.Combine(htmlRoot, "axes.diff.lua"));
                VerifyModuleSnapshotButtons(window, repository, configuration.Module.Id);
                UserGuideCapture.DcsFeatures(window);
                window.Close();
            }
            finally { File.Delete(path); File.Delete(jsonPath); if (Directory.Exists(htmlRoot)) Directory.Delete(htmlRoot, true); }
        }
        private static void VerifyAxisDefaults(ModuleWorkspaceWindow window, SqliteEditorWorkspaceRepository repository, Guid moduleId, string path)
        {
            const string source = "local diff = { [\"axisDiffs\"] = { [\"a2002cdnil\"] = { [\"name\"] = \"Roll\", [\"removed\"] = { [1] = { [\"key\"] = \"JOY_X\" } } } } } return diff";
            File.WriteAllText(path, source);
            void Import(bool stage)
            {
                ((DataGrid)WorkspaceUi.Find(window, "ManagedDevices")!).SelectedIndex = 0;
                window.Dispatcher.BeginInvoke(new Action(() => {
                    var review = System.Windows.Application.Current.Windows.OfType<AxisDefaultsReviewWindow>().Single();
                    if (!((TextBlock)WorkspaceUi.Find(review, "AxisDefaultsHeading")!).Text.Contains("AUTHORING", StringComparison.Ordinal) ||
                        !((TextBox)WorkspaceUi.Find(review, "AxisDefaultsChanges")!).Text.Contains("JOY_X", StringComparison.Ordinal))
                        throw new InvalidOperationException("Axis default review did not show its destination and removals.");
                    if (stage) ((Button)WorkspaceUi.Find(review, "StageAxisDefaultsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    else review.DialogResult = false;
                }));
                ((Button)WorkspaceUi.Find(window, "ImportAxisDefaultsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            Import(false);
            if (((TextBlock)WorkspaceUi.Find(window, "PendingSummary")!).Text.Contains("authoring", StringComparison.Ordinal))
                throw new InvalidOperationException("Cancelled axis import changed the draft.");
            Import(true);
            if (!((TextBlock)WorkspaceUi.Find(window, "PendingSummary")!).Text.Contains("authoring", StringComparison.Ordinal))
                throw new InvalidOperationException("Axis import did not stage.");
            ((Button)WorkspaceUi.Find(window, "DiscardIntegrationInstructionsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Import(true);
            ClickWithDispatcherContext((Button)WorkspaceUi.Find(window, "SaveIntegrationInstructionsButton")!);
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!repository.GetAsync(moduleId).GetAwaiter().GetResult()!.Sources!.Values.Contains(source, StringComparer.Ordinal))
            {
                if (DateTime.UtcNow > deadline) throw new InvalidOperationException("Axis defaults did not save to SQLite.");
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            }
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            var reopened = repository.GetAsync(moduleId).GetAwaiter().GetResult()!;
            var profile = reopened.Configuration.Profiles[0];
            if (!DcsLuaParser.ParseProfile(ModuleLuaProfileRenderer.Render(reopened, profile.Id), profile.Id)
                .Any(x => x.InputKey == "JOY_X" && x.Disposition == BindingDisposition.Removed))
                throw new InvalidOperationException("Saved axis defaults did not survive reopen/export.");
            File.WriteAllText(path, "local diff = {} return diff");
            ((DataGrid)WorkspaceUi.Find(window, "ManagedDevices")!).SelectedIndex = 0;
            ((Button)WorkspaceUi.Find(window, "ImportAxisDefaultsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (!((TextBlock)WorkspaceUi.Find(window, "DevicesMessage")!).Text.StartsWith("Axis default import failed:", StringComparison.Ordinal))
                throw new InvalidOperationException("Malformed axis import hid its error.");
        }
        private static void VerifyIntegrationInstructions(ModuleWorkspaceWindow window, SqliteEditorWorkspaceRepository repository, Guid moduleId, string path)
        {
            var load = (Button)WorkspaceUi.Find(window, "LoadIntegrationInstructionsButton")!;
            var groups = (ListBox)WorkspaceUi.Find(window, "IntegrationInstructions")!;
            var text = (TextBox)WorkspaceUi.Find(window, "IntegrationInstructionText")!;
            File.WriteAllText(path, """{"schemaVersion":1,"moduleDcsId":"AUTHORING","integrations":[{"name":"OpenKneeboard","instructions":"Use this module's generated kneeboard folder."}]}""");
            void Import(bool stage)
            {
                window.Dispatcher.BeginInvoke(new Action(() => {
                    var review = System.Windows.Application.Current.Windows.OfType<IntegrationImportReviewWindow>().Single();
                    if (!((TextBlock)WorkspaceUi.Find(review, "IntegrationImportHeading")!).Text.Contains("AUTHORING", StringComparison.Ordinal) ||
                        !((TextBox)WorkspaceUi.Find(review, "IntegrationImportChanges")!).Text.Contains("Proposed:", StringComparison.Ordinal))
                        throw new InvalidOperationException("Instruction review did not show the module and complete proposal.");
                    if (stage) ((Button)WorkspaceUi.Find(review, "StageIntegrationImportButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    else review.DialogResult = false;
                }));
                load.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            Import(false);
            if (groups.Items.Count != 0) throw new InvalidOperationException("Cancel changed module instructions.");
            Import(true);
            if (groups.Items.Count != 1 || !text.Text.Contains("generated kneeboard folder", StringComparison.Ordinal))
                throw new InvalidOperationException("Integration instructions did not stage/display.");
            ((Button)WorkspaceUi.Find(window, "DiscardIntegrationInstructionsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (groups.Items.Count != 0) throw new InvalidOperationException("Discard retained unsaved integration instructions.");
            Import(true);
            ClickWithDispatcherContext((Button)WorkspaceUi.Find(window, "SaveIntegrationInstructionsButton")!);
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!repository.GetAsync(moduleId).GetAwaiter().GetResult()!.Sources!.ContainsKey("config/integrations.json"))
            {
                if (DateTime.UtcNow > deadline) throw new InvalidOperationException("Integration instructions did not save to SQLite.");
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            }
            // Drain Save's continuation before removing the instruction from the next draft.
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            ((Button)WorkspaceUi.Find(window, "RemoveIntegrationInstructionButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (groups.Items.Count != 0) throw new InvalidOperationException("Selected instruction did not remove.");
            ((Button)WorkspaceUi.Find(window, "DiscardIntegrationInstructionsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (groups.Items.Count != 1) throw new InvalidOperationException("Discard did not restore saved instructions.");
            var reopened = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(repository.GetAsync(moduleId).GetAwaiter().GetResult()!), repository: repository);
            reopened.Show();
            if (((ListBox)WorkspaceUi.Find(reopened, "IntegrationInstructions")!).Items.Count != 1)
                throw new InvalidOperationException("Saved integration instructions did not reopen.");
            reopened.Close();
            File.WriteAllText(path, "{}");
            load.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (groups.Items.Count != 1 || !((TextBlock)WorkspaceUi.Find(window, "IntegrationStatus")!).Text.StartsWith("Integration import failed:", StringComparison.Ordinal))
                throw new InvalidOperationException("Malformed instruction import changed the draft or hid the error.");
        }
        private static void VerifyModuleSnapshotButtons(ModuleWorkspaceWindow authoring, SqliteEditorWorkspaceRepository source, Guid moduleId)
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-module-output-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var snapshotPath = Path.Combine(root, "module.heimdall-module.json");
                var saved = source.GetAsync(moduleId).GetAwaiter().GetResult()!;
                var editor = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(saved), repository: source,
                    outputPathPicker: snapshot => snapshot ? snapshotPath : root, documentationFolderPicker: () => root,
                    completeReleaseFolderPicker: () => root);
                editor.Show();
                ((TextBox)WorkspaceUi.Find(editor, "PackageVersion")!).Text = "1.2.3";
                var validate = (Button)WorkspaceUi.Find(editor, "ValidateModuleButton")!;
                var export = (Button)WorkspaceUi.Find(editor, "ExportModuleSnapshotButton")!;
                var build = (Button)WorkspaceUi.Find(editor, "BuildButton")!;
                var documentation = (Button)WorkspaceUi.Find(editor, "ExportModuleDocumentationButton")!;
                var complete = (Button)WorkspaceUi.Find(editor, "ExportCompleteReleaseButton")!;
                if (!validate.IsEnabled || !export.IsEnabled || !build.IsEnabled || !documentation.IsEnabled || !complete.IsEnabled)
                    throw new InvalidOperationException("Clean saved module output actions are unavailable.");
                ClickWithDispatcherContext(validate);
                WaitForOutput(editor, "Validated ");
                ClickWithDispatcherContext(export);
                WaitForOutput(editor, "Exported module snapshot:");
                var snapshot = global::Heimdall.Dcs.ModuleSnapshotWorkflow.Read(File.ReadAllText(snapshotPath));
                ClickWithDispatcherContext(documentation);
                WaitForOutput(editor, "Exported module documentation:");
                var guides = global::Heimdall.Dcs.ModuleDocumentationWorkflow.Generate(snapshot);
                foreach (var guide in guides)
                    if (File.ReadAllText(Path.Combine(root, "DCS-AUTHORING-Components-1.2.3-Documentation", guide.Key)) != guide.Value)
                        throw new InvalidOperationException("UI documentation differs from frozen snapshot output.");
                ClickWithDispatcherContext(complete);
                WaitForOutput(editor, "Exported Complete release:");
                var completePackage = global::Heimdall.Dcs.ModuleCompleteReleaseWorkflow.Build(snapshot);
                var completeFolder = Path.Combine(root, "DCS-AUTHORING-Components-1.2.3-Complete");
                if (!File.ReadAllBytes(Path.Combine(completeFolder, completePackage.FileName)).SequenceEqual(completePackage.Bytes) ||
                    File.ReadAllText(Path.Combine(completeFolder, "SHA256SUMS.txt")) != completePackage.ChecksumLine)
                    throw new InvalidOperationException("Complete export differs from verified frozen release.");
                ClickWithDispatcherContext(build);
                WaitForOutput(editor, "Wrote ");
                var original = File.ReadAllBytes(Path.Combine(root, "DCS-AUTHORING-Components-1.2.3-OVGME.zip"));
                if (!original.SequenceEqual(global::Heimdall.Dcs.ModuleSnapshotWorkflow.Build(snapshot).Bytes))
                    throw new InvalidOperationException("UI build did not use the exported immutable inputs.");
                ((TextBox)WorkspaceUi.Find(editor, "AircraftCommandCode")!).Text = "UNSAVED";
                ((TextBox)WorkspaceUi.Find(editor, "AircraftCommandName")!).Text = "Pending";
                ((TextBox)WorkspaceUi.Find(editor, "AircraftCommandCategory")!).Text = "Keys";
                ((Button)WorkspaceUi.Find(editor, "StageAircraftCommandButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (validate.IsEnabled || export.IsEnabled || build.IsEnabled || documentation.IsEnabled || complete.IsEnabled)
                    throw new InvalidOperationException("Pending authoring edits did not disable saved output actions.");
                ((Button)WorkspaceUi.Find(editor, "DiscardAuthoringButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                editor.Close();

                var connection = "Data Source=" + Path.Combine(root, "restore.db") + ";Pooling=False";
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var restored = new SqliteEditorWorkspaceRepository(connection);
                var settings = new SqliteAppSettingsService(connection, Path.Combine(root, "defaults.json"),
                    Path.Combine(root, "settings.json"), Path.Combine(root, "theme.txt"));
                var main = new MainWindow(new global::Heimdall.Dcs.ModuleWorkspaceLoader(), new global::Heimdall.Dcs.DcsFolderImporter(),
                    settings, restored, snapshotFilePicker: () => snapshotPath);
                main.Show();
                if (main.Title != "HEIMDALL — Modules · " + ApplicationVersion.Label)
                    throw new InvalidOperationException("Main window title did not include the application version.");
                var restore = (Button)WorkspaceUi.Find(main, "RestoreModuleSnapshotButton")!;
                ClickWithDispatcherContext(restore);
                var deadline = DateTime.UtcNow.AddSeconds(60);
                while (!((TextBlock)WorkspaceUi.Find(main, "ImportStatus")!).Text.StartsWith("Restored ", StringComparison.Ordinal))
                {
                    if (DateTime.UtcNow > deadline) throw new InvalidOperationException("Module snapshot UI restore failed: " + ((TextBlock)WorkspaceUi.Find(main, "ImportStatus")!).Text);
                    main.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                }
                if (((ComboBox)WorkspaceUi.Find(main, "SavedModules")!).SelectedItem is null ||
                    global::Heimdall.Dcs.EditorSnapshotSerializer.Serialize(restored.GetAsync(moduleId).GetAwaiter().GetResult()!) != snapshot.Workspace)
                    throw new InvalidOperationException("Restored snapshot did not populate the saved module selector losslessly.");
                ClickWithDispatcherContext(restore);
                deadline = DateTime.UtcNow.AddSeconds(60);
                while (!((TextBlock)WorkspaceUi.Find(main, "ImportStatus")!).Text.StartsWith("Module snapshot was not restored:", StringComparison.Ordinal))
                {
                    if (DateTime.UtcNow > deadline) throw new InvalidOperationException("Duplicate snapshot restore was not blocked.");
                    main.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                }
                if (restored.ListAsync().GetAwaiter().GetResult().Count != 1)
                    throw new InvalidOperationException("Duplicate restore changed saved module membership.");
                main.Close();
                FlyNowSmoke.Verify(connection, restored, settings, root, moduleId);
                ((TextBox)WorkspaceUi.Find(authoring, "PackageVersion")!).Text = "1.2.3";
                if (!((Button)WorkspaceUi.Find(authoring, "ExportModuleSnapshotButton")!).IsEnabled)
                    throw new InvalidOperationException("Original module became dirty during isolated restore.");
            }
            finally { Directory.Delete(root, true); }
        }
        private static void ClickWithDispatcherContext(Button button)
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(button.Dispatcher));
            try { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }
        private static void WaitForOutput(ModuleWorkspaceWindow editor, string prefix)
        {
            var deadline = DateTime.UtcNow.AddSeconds(60);
            var status = (TextBlock)WorkspaceUi.Find(editor, "BuildStatus")!;
            while (!status.Text.StartsWith(prefix, StringComparison.Ordinal))
            {
                if (status.Text.StartsWith("Module output failed:", StringComparison.Ordinal) || DateTime.UtcNow > deadline)
                    throw new InvalidOperationException("Module output UI failed: " + status.Text);
                editor.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            }
            while (!((Button)WorkspaceUi.Find(editor, "BuildButton")!).IsEnabled)
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
                    global::Heimdall.Dcs.SharedUiLayerWorkspaceFactory.CreateAsync().GetAwaiter().GetResult()).GetAwaiter().GetResult();
                var sources = global::Heimdall.Dcs.ModuleWorkspaceLoader.LoadHardwareAsync(
                    global::Heimdall.Dcs.BundledSharedCatalog.Root).GetAwaiter().GetResult();
                var window = new NewModuleWindow(repository, defaults, sources, new EmptyDeviceDiscovery());
                ((CheckBox)WorkspaceUi.Find(window, "EmptyDefaults")!).IsChecked = false;
                Exception? failure = null;
                window.Dispatcher.BeginInvoke(new Action(() => {
                    try
                    {
                        ((TextBox)WorkspaceUi.Find(window, "DcsIdInput")!).Text = "P47-D-30";
                        ((TextBox)WorkspaceUi.Find(window, "DisplayNameInput")!).Text = "P-47D Thunderbolt";
                        var physical = (ComboBox)WorkspaceUi.Find(window, "PhysicalChoices")!;
                        var offline = new DetectedDcsDevice(Guid.NewGuid(), "Captured offline unit", false);
                        physical.ItemsSource = new[] { offline };
                        physical.SelectedItem = offline;
                        ((Button)WorkspaceUi.Find(window, "ManualFileButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        if (physical.SelectedItem is not null) throw new InvalidOperationException("Manual filename entry retained a stale discovered-unit GUID.");
                        var hardware = (ComboBox)WorkspaceUi.Find(window, "HardwareChoices")!;
                        var rows = (ListBox)WorkspaceUi.Find(window, "SelectedUnits")!;
                        var ids = CreationHardware;
                        for (var i = 0; i < ids.Length; i++)
                        {
                            hardware.SelectedItem = hardware.Items.Cast<WorkspaceHardware>().Single(x => x.DeviceId == ids[i]);
                            var choice = (WorkspaceHardware)hardware.SelectedItem;
                            ((TextBox)WorkspaceUi.Find(window, "NativeFileInput")!).Text = new DetectedDcsDevice(Guid.NewGuid(), choice.Label, false).FileName;
                            ((TextBox)WorkspaceUi.Find(window, "UnitInstance")!).Text = "Unit " + i;
                            if (i == 0) ((ComboBox)WorkspaceUi.Find(window, "BaselineChoices")!).SelectedIndex = 0;
                            ((Button)WorkspaceUi.Find(window, "AddUnitButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            if (rows.Items.Count != i + 1) throw new InvalidOperationException("Repeated physical-unit selection did not add an independent row: " + ((TextBlock)WorkspaceUi.Find(window, "CreationError")!).Text);
                        }
                        hardware.SelectedItem = hardware.Items.Cast<WorkspaceHardware>().First(x => x.Label.Contains("Pendular", StringComparison.OrdinalIgnoreCase));
                        ((TextBox)WorkspaceUi.Find(window, "NativeFileInput")!).Text = new DetectedDcsDevice(Guid.NewGuid(), "T-Pendular Rudder", false).FileName;
                        ((Button)WorkspaceUi.Find(window, "AddUnitButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        if (rows.Items.Count != 11) throw new InvalidOperationException("The reported rudder choice was not staged.");
                        ((Button)WorkspaceUi.Find(window, "CreateButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    catch (Exception ex) { failure = ex; window.Close(); }
                }));
                var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
                timeout.Tick += (_, _) => {
                    failure = new InvalidOperationException("New Module creation timed out: " + ((TextBlock)WorkspaceUi.Find(window, "CreationError")!).Text);
                    window.Close();
                };
                timeout.Start();
                window.ShowDialog();
                timeout.Stop();
                if (failure is not null) throw failure;
                var created = window.CreatedWorkspace ?? throw new InvalidOperationException("Physical-unit creation did not reopen from SQLite.");
                var state = global::Heimdall.Dcs.WorkspaceStructureEditor.Describe(created);
                if (state.Devices.Count(x => x.Layer == AssignmentLayer.Module) != 11)
                    throw new InvalidOperationException("Repeated device rows were lost during creation.");
                if (!created.Sources!.TryGetValue("config/ipia-axis-cleanup-devices.json", out var cleanup) ||
                    state.Devices.Where(x => x.Layer == AssignmentLayer.Module).Any(x => !cleanup.Contains(x.FileName, StringComparison.Ordinal)))
                    throw new InvalidOperationException("Native controller identities were not saved for aircraft axis cleanup.");
                var editor = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(created), repository: repository, deviceDiscovery: new EmptyDeviceDiscovery());
                editor.Show();
                editor.UpdateLayout();
                var dropdown = (ComboBox)WorkspaceUi.Find(editor, "DeviceChoice")!;
                if (dropdown.Items.Cast<UiLayerDiagram>().Any(x => x.Title.Contains(" {", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Assignment device labels still expose duplicate native GUIDs.");
                var grid = (DataGrid)WorkspaceUi.Find(editor, "ManagedDevices")!;
                foreach (var device in grid.Items.Cast<WorkspaceDevice>().ToArray())
                {
                    grid.SelectedItem = device;
                    editor.UpdateLayout();
                    if (((TextBox)WorkspaceUi.Find(editor, "ManagedFileName")!).Text != device.FileName)
                        throw new InvalidOperationException("New-module physical device selection failed.");
                }
                if (WorkspaceUi.Find(editor, "RebindDeviceButton") is not Button)
                    throw new InvalidOperationException("Explicit physical rebinding is missing.");
                editor.Close();
                AxisCleanupFlyNowSmoke.Verify(connection, repository);
                ModuleOutputLoggingSmoke.Verify(connection, repository);
            }
            finally { File.Delete(path); }
        }
        private static void VerifySavedGamesImportForm()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-saved-games-smoke-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False";
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                var defaults = new SqliteSharedUiLayerWorkspaceRepository(connection).SaveAsync(
                    SharedUiLayerWorkspaceFactory.CreateAsync().GetAwaiter().GetResult()).GetAwaiter().GetResult();
                var sources = ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root).GetAwaiter().GetResult();
                var first = new DeviceProfile(Guid.NewGuid(), "F16 MFD", null, "unresolved device", new DetectedDcsDevice(Guid.NewGuid(), "F16 MFD", false).FileName);
                var second = new DeviceProfile(Guid.NewGuid(), "F16 MFD", null, "unresolved device", new DetectedDcsDevice(Guid.NewGuid(), "F16 MFD", false).FileName);
                var empty = new DeviceProfile(Guid.NewGuid(), "Unknown panel", null, "unresolved device", new DetectedDcsDevice(Guid.NewGuid(), "Unknown panel", false).FileName);
                var folder = new DcsFolderImportResult("CUSTOM-HELICOPTER/joystick", [first, second, empty], [
                    new(first.Id, BindingSection.Key, "CUSTOM_KEY", "Custom key", BindingDisposition.Added, "JOY_BTN1"),
                    new(second.Id, BindingSection.Axis, "a9200cdnil", "Captured default", BindingDisposition.Removed, "JOY_X")
                ], [], []);
                var cancelled = new SavedGamesImportWindow(folder, defaults, sources, repository);
                cancelled.Dispatcher.BeginInvoke(new Action(() => cancelled.DialogResult = false));
                cancelled.ShowDialog();
                if (repository.ListAsync().GetAwaiter().GetResult().Count != 0)
                    throw new InvalidOperationException("Cancel saved a partial Saved Games module.");
                var window = new SavedGamesImportWindow(folder, defaults, sources, repository);
                Exception? failure = null;
                window.Dispatcher.BeginInvoke(new Action(() => {
                    try
                    {
                        var rows = ((ItemsControl)WorkspaceUi.Find(window, "ProfileChoices")!).Items.Cast<SavedGamesProfileChoice>().ToArray();
                        if (rows.Length != 3 || rows[0].Hardware?.DeviceId != "tm-mfd" || rows[1].Hardware?.DeviceId != "tm-mfd" || rows[2].Hardware is not null)
                            throw new InvalidOperationException("Hardware suggestions lost repeated units or guessed an unknown panel.");
                        var import = (Button)WorkspaceUi.Find(window, "ImportSavedGamesButton")!;
                        import.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        if (!((TextBlock)WorkspaceUi.Find(window, "ImportError")!).Text.Contains("Choose hardware", StringComparison.Ordinal) || repository.ListAsync().GetAwaiter().GetResult().Count != 0)
                            throw new InvalidOperationException("Unresolved hardware saved a partial module.");
                        rows[2].Hardware = rows[2].HardwareChoices.Single(x => x.DeviceId == "tm-mfd");
                        ((TextBox)WorkspaceUi.Find(window, "DcsIdInput")!).Text = "CUSTOM-HELICOPTER";
                        ((TextBox)WorkspaceUi.Find(window, "DisplayNameInput")!).Text = "Custom helicopter";
                        ClickWithDispatcherContext(import);
                    }
                    catch (Exception ex) { failure = ex; window.Close(); }
                }));
                var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
                timeout.Tick += (_, _) => {
                    failure = new InvalidOperationException("Saved Games import timed out: " + ((TextBlock)WorkspaceUi.Find(window, "ImportError")!).Text);
                    window.Close();
                };
                timeout.Start();
                window.ShowDialog();
                timeout.Stop();
                if (failure is not null) throw failure;
                var saved = window.ImportedWorkspace ?? throw new InvalidOperationException("Saved Games import did not reopen an editable module.");
                var devices = WorkspaceStructureEditor.Describe(saved).Devices.Where(x => x.Layer == AssignmentLayer.Module).ToArray();
                if (devices.Length != 3 || !DcsLuaParser.ParseProfile(ModuleLuaProfileRenderer.Render(saved, devices.Single(x => x.FileName == second.SourceName).ProfileId), second.Id)
                    .Any(x => x.Disposition == BindingDisposition.Removed))
                    throw new InvalidOperationException("Empty or removal-only profiles did not survive import.");
                var editor = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(saved), repository: repository, deviceDiscovery: new EmptyDeviceDiscovery());
                editor.Show();
                editor.UpdateLayout();
                if (((DataGrid)WorkspaceUi.Find(editor, "ManagedDevices")!).Items.Count == 0 || ((ComboBox)WorkspaceUi.Find(editor, "DeviceChoice")!).Items.Count == 0)
                    throw new InvalidOperationException("Saved Games devices remained invisible in the editable workspace.");
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
                var window = new MainWindow(new global::Heimdall.Dcs.ModuleWorkspaceLoader(),
                    new global::Heimdall.Dcs.DcsFolderImporter(), settings, repository);
                var modules = (ComboBox)WorkspaceUi.Find(window, "SavedModules")!;
                var delete = (Button)WorkspaceUi.Find(window, "DeleteModuleButton")!;
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
                var seed = global::Heimdall.Dcs.SharedUiLayerWorkspaceFactory.CreateAsync().GetAwaiter().GetResult();
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
                var impactRows = (DataGrid)WorkspaceUi.Find(window, "ImpactModules")!;
                var impactChanges = (DataGrid)WorkspaceUi.Find(window, "ImpactChanges")!;
                var impactButton = (Button)WorkspaceUi.Find(window, "ImpactRefreshButton")!;
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
                    if (((TextBlock)WorkspaceUi.Find(window, "AdoptionStatus")!).Text.StartsWith("Module was not changed", StringComparison.Ordinal))
                        throw new InvalidOperationException(((TextBlock)WorkspaceUi.Find(window, "AdoptionStatus")!).Text);
                }
                impactRows.SelectedItem = impactRows.Items.OfType<SharedUiLayerModuleImpact>().Single(x => x.ModuleId == pinnedModule.Configuration.Module.Id);
                ((Button)WorkspaceUi.Find(window, "RecordBaselineButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                WaitModuleUpdate();
                if (modules.GetAsync(pinnedModule.Configuration.Module.Id).GetAwaiter().GetResult()!.SharedDefault?.ContentSha256 != first.ContentSha256)
                    throw new InvalidOperationException("Recording a module baseline did not reopen its verified reference.");
                ReviewImpact();
                const string smokeFile = "Smoke controller {00000001-0000-0000-8001-444553540000}.diff.lua";
                var deviceRows = (DataGrid)WorkspaceUi.Find(window, "DeviceRows")!;
                var cloneChoice = (ComboBox)WorkspaceUi.Find(window, "CloneProfileChoice")!;
                var template = deviceRows.Items.OfType<global::Heimdall.Dcs.SharedUiLayerDeviceProfile>().First(x => !x.Protected);
                void AddSmokeProfile(bool copy)
                {
                    ((Button)WorkspaceUi.Find(window, "NewProfileButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    ((TextBox)WorkspaceUi.Find(window, "DeviceFileName")!).Text = smokeFile;
                    cloneChoice.SelectedItem = copy ? cloneChoice.Items.OfType<global::Heimdall.Dcs.SharedUiLayerDeviceProfile>().First(x => x.Path == template.Path) : null;
                    ((Button)WorkspaceUi.Find(window, "AddProfileButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                AddSmokeProfile(true);
                var copy = deviceRows.Items.OfType<global::Heimdall.Dcs.SharedUiLayerDeviceProfile>().Single(x => x.FileName == smokeFile);
                if (copy.Source != template.Source || !((ComboBox)WorkspaceUi.Find(window, "ProfilePaths")!).Items.Contains(copy.Path) ||
                    !((ListBox)WorkspaceUi.Find(window, "SourcePaths")!).Items.Contains(copy.Path))
                    throw new InvalidOperationException("Shared profile clone did not preserve its source and refresh selectors.");
                deviceRows.SelectedItem = deviceRows.Items.OfType<global::Heimdall.Dcs.SharedUiLayerDeviceProfile>().Single(x => x.Path == template.Path);
                var renamedFile = "Smoke renamed " + template.FileName;
                ((TextBox)WorkspaceUi.Find(window, "DeviceFileName")!).Text = renamedFile;
                ((Button)WorkspaceUi.Find(window, "RenameProfileButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var renamedProfile = deviceRows.Items.OfType<global::Heimdall.Dcs.SharedUiLayerDeviceProfile>().Single(x => x.FileName == renamedFile);
                if (renamedProfile.Source != template.Source || !((ComboBox)WorkspaceUi.Find(window, "ModifierDevice")!).Items.Contains(renamedFile[..^9]) ||
                    ((DataGrid)WorkspaceUi.Find(window, "ModifierRows")!).Items.OfType<DcsModifier>().Any(x => x.Device == template.FileName[..^9]))
                    throw new InvalidOperationException("Shared profile rename lost bindings or native modifier references.");
                ((Button)WorkspaceUi.Find(window, "RemoveProfileButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (!deviceRows.Items.OfType<global::Heimdall.Dcs.SharedUiLayerDeviceProfile>().Any(x => x.FileName == renamedFile))
                    throw new InvalidOperationException("A bound shared profile was removed.");
                deviceRows.SelectedItem = deviceRows.Items.OfType<global::Heimdall.Dcs.SharedUiLayerDeviceProfile>().Single(x => x.Protected);
                if (((Button)WorkspaceUi.Find(window, "RenameProfileButton")!).IsEnabled || ((Button)WorkspaceUi.Find(window, "RemoveProfileButton")!).IsEnabled)
                    throw new InvalidOperationException("The default keyboard profile is editable.");
                ((Button)WorkspaceUi.Find(window, "DiscardDefaultsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (deviceRows.Items.OfType<global::Heimdall.Dcs.SharedUiLayerDeviceProfile>().Any(x => x.FileName == smokeFile || x.FileName == renamedFile))
                    throw new InvalidOperationException("Discard did not restore the saved device profiles.");
                AddSmokeProfile(false);
                ((Button)WorkspaceUi.Find(window, "RemoveProfileButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (deviceRows.Items.OfType<global::Heimdall.Dcs.SharedUiLayerDeviceProfile>().Any(x => x.FileName == smokeFile))
                    throw new InvalidOperationException("An empty unreferenced shared profile was not removed.");
                ((Button)WorkspaceUi.Find(window, "DiscardDefaultsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var modifierRows = (DataGrid)WorkspaceUi.Find(window, "ModifierRows")!;
                var modifierDevices = (ComboBox)WorkspaceUi.Find(window, "ModifierDevice")!;
                var nativeDevice = modifierDevices.Items.OfType<string>().First(x => x != "Keyboard");
                void AddSmokeModifier()
                {
                    ((TextBox)WorkspaceUi.Find(window, "ModifierName")!).Text = "SMOKE_MODIFIER";
                    modifierDevices.SelectedItem = nativeDevice;
                    ((TextBox)WorkspaceUi.Find(window, "ModifierInput")!).Text = "JOY_BTN9999";
                    ((ComboBox)WorkspaceUi.Find(window, "ModifierModeChoice")!).SelectedItem = ModifierMode.Hold;
                    ((Button)WorkspaceUi.Find(window, "AddModifierButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                AddSmokeModifier();
                modifierRows.SelectedItem = modifierRows.Items.OfType<DcsModifier>().Single(x => x.Name == "SMOKE_MODIFIER");
                ((ComboBox)WorkspaceUi.Find(window, "ModifierModeChoice")!).SelectedItem = ModifierMode.Toggle;
                ((Button)WorkspaceUi.Find(window, "UpdateModifierButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (modifierRows.Items.OfType<DcsModifier>().Single(x => x.Name == "SMOKE_MODIFIER").Mode != ModifierMode.Toggle ||
                    !((ComboBox)WorkspaceUi.Find(window, "ClearLayerChoice")!).Items.Contains("SMOKE_MODIFIER"))
                    throw new InvalidOperationException("Shared modifier update did not refresh modes and layer choices.");
                modifierRows.SelectedItem = modifierRows.Items.OfType<DcsModifier>().Single(x => x.Name == "LCtrl");
                if (((Button)WorkspaceUi.Find(window, "UpdateModifierButton")!).IsEnabled || ((Button)WorkspaceUi.Find(window, "RemoveModifierButton")!).IsEnabled)
                    throw new InvalidOperationException("Built-in keyboard defaults are editable.");
                modifierRows.SelectedItem = modifierRows.Items.OfType<DcsModifier>().Single(x => x.Name == "SMOKE_MODIFIER");
                ((Button)WorkspaceUi.Find(window, "RemoveModifierButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (modifierRows.Items.OfType<DcsModifier>().Any(x => x.Name == "SMOKE_MODIFIER"))
                    throw new InvalidOperationException("Unused shared modifier was not removed.");
                AddSmokeModifier();
                ((Button)WorkspaceUi.Find(window, "DiscardDefaultsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (modifierRows.Items.OfType<DcsModifier>().Any(x => x.Name == "SMOKE_MODIFIER"))
                    throw new InvalidOperationException("Discard did not restore saved shared modifiers.");
                var catalogRows = (DataGrid)WorkspaceUi.Find(window, "CatalogRows")!;
                var addCommand = (Button)WorkspaceUi.Find(window, "AddCommandButton")!;
                void AddCatalogCommand()
                {
                    ((TextBox)WorkspaceUi.Find(window, "CatalogIdentity")!).Text = "smoke-command";
                    ((TextBox)WorkspaceUi.Find(window, "CatalogCode")!).Text = "SMOKE_COMMAND";
                    ((TextBox)WorkspaceUi.Find(window, "CatalogLabel")!).Text = "Smoke command";
                    ((TextBox)WorkspaceUi.Find(window, "CatalogCategory")!).Text = "General";
                    addCommand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                AddCatalogCommand();
                catalogRows.SelectedItem = catalogRows.Items.OfType<global::Heimdall.Dcs.SharedUiLayerCommand>().Single(x => x.Id == "smoke-command");
                ((TextBox)WorkspaceUi.Find(window, "CatalogLabel")!).Text = "Updated smoke command";
                ((TextBox)WorkspaceUi.Find(window, "CatalogCategory")!).Text = "VR";
                ((Button)WorkspaceUi.Find(window, "UpdateCommandButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (!((ComboBox)WorkspaceUi.Find(window, "CommandChoice")!).Items.OfType<DraftCommand>()
                    .Any(x => x.Id == "SMOKE_COMMAND" && x.Name == "Updated smoke command"))
                    throw new InvalidOperationException("Catalog changes did not refresh the binding command choices.");
                ((Button)WorkspaceUi.Find(window, "RemoveCommandButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (catalogRows.Items.OfType<global::Heimdall.Dcs.SharedUiLayerCommand>().Any(x => x.Id == "smoke-command"))
                    throw new InvalidOperationException("Unused catalog command was not removed.");
                ((Button)WorkspaceUi.Find(window, "DiscardDefaultsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (catalogRows.Items.Count != global::Heimdall.Dcs.SharedUiLayerCommandEditor.Commands(first).Count)
                    throw new InvalidOperationException("Discard did not restore the shared command catalog.");
                var profiles = (ComboBox)WorkspaceUi.Find(window, "ProfilePaths")!;
                profiles.SelectedIndex = 0;
                var profilePath = (string)profiles.SelectedItem;
                var rows = (DataGrid)WorkspaceUi.Find(window, "BindingRows")!;
                var clearLayer = (ComboBox)WorkspaceUi.Find(window, "ClearLayerChoice")!;
                clearLayer.SelectedItem = "Base";
                ((Button)WorkspaceUi.Find(window, "ClearLayerButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (rows.Items.OfType<DcsBinding>().Any(x => x.Disposition != BindingDisposition.Removed && x.Reformers.Count == 0) ||
                    !((Button)WorkspaceUi.Find(window, "SaveDefaultsButton")!).IsEnabled)
                    throw new InvalidOperationException("Shared layer clear did not stage all base keys and axes.");
                ((Button)WorkspaceUi.Find(window, "DiscardDefaultsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (!rows.Items.OfType<DcsBinding>().Any(x => x.Disposition != BindingDisposition.Removed))
                    throw new InvalidOperationException("Discard did not restore cleared shared layer bindings.");
                var selected = rows.Items.OfType<DcsBinding>().First(x => x.Section == BindingSection.Key && x.Disposition != BindingDisposition.Removed);
                rows.SelectedItem = selected;
                ((TextBox)WorkspaceUi.Find(window, "InputKey")!).Text = "JOY_BTN999";
                var update = (Button)WorkspaceUi.Find(window, "UpdateBindingButton")!;
                update.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var save = (Button)WorkspaceUi.Find(window, "SaveDefaultsButton")!;
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
                if (!((DataGrid)WorkspaceUi.Find(window, "ImpactDetails")!).Items.OfType<SharedUiLayerModuleSourceImpact>()
                    .Any(x => x.Path == profilePath && x.Status == "Different source"))
                    throw new InvalidOperationException("Module selection did not display source evidence.");
                ((Button)WorkspaceUi.Find(window, "DiscardDefaultsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (save.IsEnabled || !rows.Items.OfType<DcsBinding>().Any(x => x.InputKey == selected.InputKey && x.CommandId == selected.CommandId))
                    throw new InvalidOperationException("Discard did not restore the saved shared bindings.");
                if (impactRows.Items.Count != 0)
                    throw new InvalidOperationException("Discard left a stale impact report visible.");
                rows.SelectedItem = rows.Items.OfType<DcsBinding>().First(x => x.InputKey == selected.InputKey && x.CommandId == selected.CommandId);
                ((TextBox)WorkspaceUi.Find(window, "InputKey")!).Text = "JOY_BTN999";
                update.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                AddCatalogCommand();
                AddSmokeModifier();
                AddSmokeProfile(false);
                save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var status = (TextBlock)WorkspaceUi.Find(window, "EditorStatus")!;
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (!status.Text.StartsWith("Shared default revision saved", StringComparison.Ordinal))
                {
                    if (DateTime.UtcNow > deadline || status.Text.StartsWith("Defaults were not saved", StringComparison.Ordinal))
                        throw new InvalidOperationException("Shared binding save failed: " + status.Text);
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
                }
                var saved = repository.GetAsync().GetAwaiter().GetResult()!;
                if (!global::Heimdall.Dcs.SharedUiLayerCommandEditor.Commands(saved).Any(x => x.Id == "smoke-command"))
                    throw new InvalidOperationException("Shared catalog command did not reopen in the saved revision.");
                if (!global::Heimdall.Dcs.SharedUiLayerModifierEditor.Modifiers(saved).Any(x =>
                    x.Name == "SMOKE_MODIFIER" && x.Device == nativeDevice && x.Key == "JOY_BTN9999" && x.Mode == ModifierMode.Hold) ||
                    global::Heimdall.Dcs.SharedUiLayerModifierEditor.Modifiers(repository.GetAsync(1).GetAwaiter().GetResult()!).Any(x => x.Name == "SMOKE_MODIFIER"))
                    throw new InvalidOperationException("Shared modifier revision did not reopen independently of its history.");
                if (!global::Heimdall.Dcs.SharedUiLayerProfileEditor.Profiles(saved).Any(x => x.FileName == smokeFile && x.ActiveBindings == 0) ||
                    global::Heimdall.Dcs.SharedUiLayerProfileEditor.Profiles(repository.GetAsync(1).GetAwaiter().GetResult()!).Any(x => x.FileName == smokeFile))
                    throw new InvalidOperationException("Shared device additions did not reopen independently of revision history.");
                if (saved.Revision != 2 || !global::Heimdall.Dcs.SharedUiLayerBindingEditor.Bindings(saved, profilePath)
                    .Any(x => x.InputKey == "JOY_BTN999" && x.CommandId == selected.CommandId) || save.IsEnabled)
                    throw new InvalidOperationException("Shared binding revision did not reopen correctly.");
                if (repository.GetAsync(1).GetAwaiter().GetResult()!.Sources[profilePath] != first.Sources[profilePath])
                    throw new InvalidOperationException("Shared binding save changed revision history.");
                if (impactRows.Items.Count != 0 || modules.GetAsync(pinnedModule.Configuration.Module.Id).GetAwaiter().GetResult()!.Sources![profilePath] != first.Sources[profilePath] ||
                    modules.GetAsync(localModule.Configuration.Module.Id).GetAwaiter().GetResult()!.Sources![profilePath] != localSources[profilePath])
                    throw new InvalidOperationException("Saving defaults changed saved modules or retained a stale impact report.");
                ReviewImpact();
                impactRows.SelectedItem = impactRows.Items.OfType<SharedUiLayerModuleImpact>().Single(x => x.ModuleId == pinnedModule.Configuration.Module.Id);
                if (!((Button)WorkspaceUi.Find(window, "ExportSharedButton")!).IsEnabled ||
                    !((Button)WorkspaceUi.Find(window, "RestoreSharedButton")!).IsEnabled ||
                    !((Button)WorkspaceUi.Find(window, "RebuildSharedButton")!).IsEnabled)
                    throw new InvalidOperationException("Saved shared defaults did not enable independent output and restore.");
                var adoptionRows = (DataGrid)WorkspaceUi.Find(window, "AdoptionChoices")!;
                if (!adoptionRows.Items.OfType<SharedUiLayerAdoptionChoice>().Any(x => x.Path == profilePath && x.TakeDefault && !x.Conflict))
                    throw new InvalidOperationException("Safe baseline source changes were not offered for adoption.");
                ((Button)WorkspaceUi.Find(window, "ReviewSemanticButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (!((Button)WorkspaceUi.Find(window, "AdoptDefaultsButton")!).IsEnabled)
                    throw new InvalidOperationException(((TextBlock)WorkspaceUi.Find(window, "AdoptionStatus")!).Text);
                ((Button)WorkspaceUi.Find(window, "AdoptDefaultsButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                WaitModuleUpdate();
                var adopted = modules.GetAsync(pinnedModule.Configuration.Module.Id).GetAwaiter().GetResult()!;
                if (adopted.SharedDefault?.Revision != 2 || adopted.Sources![profilePath] != saved.Sources[profilePath] ||
                    modules.GetAsync(localModule.Configuration.Module.Id).GetAwaiter().GetResult()!.Sources![profilePath] != localSources[profilePath])
                    throw new InvalidOperationException("Reviewed adoption did not preserve module isolation.");
                ReviewImpact();
                impactRows.SelectedItem = impactRows.Items.OfType<SharedUiLayerModuleImpact>().Single(x => x.ModuleId == pinnedModule.Configuration.Module.Id);
                ((Button)WorkspaceUi.Find(window, "RollbackAdoptionButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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
                    !((TextBlock)WorkspaceUi.Find(window, "ImpactStatus")!).Text.StartsWith("Unable to review module impact", StringComparison.Ordinal) ||
                    !((StackPanel)WorkspaceUi.Find(window, "BindingFields")!).IsEnabled)
                    throw new InvalidOperationException("Failed impact review retained stale results or disabled editing.");
                window.Close();
                System.Console.WriteLine("Shared default binding editor staged, discarded, saved and reopened an independent revision.");
            }
            finally { File.Delete(path); }
        }
        #endregion END private helpers
    }
}



