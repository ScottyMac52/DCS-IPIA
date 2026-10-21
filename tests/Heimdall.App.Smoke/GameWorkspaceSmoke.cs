using System.IO;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;
using Heimdall.Dcs;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;
using Heimdall.App;
using Heimdall.App.Games;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises real game cards, ED authoring/reopen/export and guarded transitions on Windows.</summary>
    public static class GameWorkspaceSmoke
    {
        #region BEGIN Public
        /// <summary>Completes the game UI flow using only synthetic files and ephemeral entitlement evidence.</summary>
        public static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-game-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var previous = EditionAccess.Policy;
            using var issuer = new OwnerLicenseTestIssuer();
            try
            {
                EditionAccess.SetForTests(issuer.Policy);
                var physical = new DetectedDcsDevice(Guid.NewGuid(), "Smoke SOL-R controller", true,
                    ProductGuid: Guid.Parse("1001044f-0000-0000-0000-504944564944"));
                var services = new GameWorkspaceServices(new FileGameWorkspaceStore(root), new GameSmokeDiscovery(physical, physical with { PhysicalGuid = Guid.NewGuid(), Name = "Identical controller" },
                    physical with { PhysicalGuid = Guid.NewGuid(), Name = "Throttle", ProductGuid = Guid.Parse("1002044f-0000-0000-0000-504944564944") }));
                VerifyCards(root, services);
                VerifyDcsSelectors();
                var sources = Task.Run(() => ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root)).GetAwaiter().GetResult();
                var hardware = System.Text.Json.JsonSerializer.Deserialize<WorkspaceHardware[]>(sources["config/ipia-hardware.json"])!;
                EliteControllerIdentificationSmoke.Verify(root, hardware);
                EliteControllerLifecycleSmoke.Verify(root, hardware);
                EliteExportPreflightSmoke.Verify(root, hardware);
                VerifyControllerAddition(root, hardware);
                VerifyIdenticalControllers(root, hardware);
                var choice = MessageBoxResult.Cancel;
                FakeDeviceInputSource? input = null;
                using var window = new EliteWorkspaceWindow(services, hardware,
                    exportPicker: () => Path.Combine(root, "Review"), pendingChoice: () => choice,
                    inputFactory: (_, ids) =>
                    {
                        if (ids.Count != 1 || ids[0] != physical.PhysicalGuid) throw new InvalidOperationException("Input acquisition selected a different physical unit.");
                        return input = new(physical.PhysicalGuid, Guid.NewGuid());
                    });
                window.Show(); window.Activate(); ThemeControlSmoke.Pump(window);
                var captured = Path.Combine(root, "Synthetic.4.2.binds");
                File.WriteAllText(captured, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "EliteDangerous", "Synthetic.4.2.binds"))
                    .Replace("Device=\"ExampleStick\"", "Device=\"044F1001\" DeviceIndex=\"0\"", StringComparison.Ordinal).Replace("ExampleThrottle", "044F1002", StringComparison.Ordinal));
                window.ImportFile(captured);
                var session = window.Session!;
                var stick = session.Snapshot.Profile.Devices.First(x => x.Identity!.SimSpecificIds["ed.device"] == "044F1001");
                Box<ListBox>(window, "DevicePicker").SelectedItem = Box<ListBox>(window, "DevicePicker").Items.Cast<GameDeviceChoice>().Single(x => x.Id == stick.LogicalId);
                var connected = Box<ComboBox>(window, "ConnectedPicker");
                if (connected.SelectedItem is not null) throw new InvalidOperationException("Discovery silently selected the first physical controller.");
                connected.SelectedItem = connected.Items.Cast<DetectedDcsDevice>().Single(x => x.PhysicalGuid == physical.PhysicalGuid);
                Click(window, "AddControllerButton");
                if (!session.Snapshot.Hardware.ContainsKey(stick.LogicalId)) throw new InvalidOperationException("Adding a controller did not reuse its matching captured reference.");
                Click(window, "ChangeDrawingButton");
                Box<ComboBox>(window, "HardwarePicker").SelectedItem = hardware.Single(x => x.DeviceId == "tm-sol-r-ava-r-buttons");
                Click(window, "UseDrawingButton");
                Box<ListBox>(window, "FeaturePicker").SelectedIndex = 1;
                SelectCommand(window, "Primary Fire"); Box<TextBox>(window, "InputKey").Text = "Joy_9";
                Box<ComboBox>(window, "ModePicker").SelectedIndex = 1; Click(window, "ApplyAssignmentButton");
                SelectCommand(window, "Pitch Axis Raw"); Box<TextBox>(window, "InputKey").Text = "Joy_YAxis";
                Box<CheckBox>(window, "InvertAxis").IsChecked = false; Box<TextBox>(window, "AxisDeadzone").Text = "7.5"; Click(window, "ApplyAssignmentButton");
                if (!window.Session!.HasChanges || !window.SaveProfile()) throw new InvalidOperationException("ED authoring did not save.");
                var saved = window.Session.Snapshot;
                Click(window, "ExportPresetButton");
                var exported = Directory.GetFiles(Path.Combine(root, "Review"), "*.binds", SearchOption.AllDirectories).Single();
                var native = services.Backend.Import("controls", new Dictionary<string, string> { [Path.GetFileName(exported)] = File.ReadAllText(exported) });
                if (!native.Devices.SelectMany(x => x.Buttons).Any(x => x.SourceButton == "Joy_9" && x.Mode == BindingMode.Hold)
                    || !native.Devices.SelectMany(x => x.Axes).Any(x => x.Deadzone == 7.5f && !x.Invert))
                    throw new InvalidOperationException("The WPF-authored native button/axis edits did not survive export.");
                var frozen = System.Text.Json.JsonSerializer.Serialize(window.Session.Snapshot);
                VerifyCommandFilters(window);
                Box<ListBox>(window, "SectionPicker").SelectedIndex = 3;
                if (Box<ListBox>(window, "CommandsList").Items.Count != 0) throw new InvalidOperationException("Section filtering included commands from another context.");
                Box<ListBox>(window, "SectionPicker").SelectedIndex = 0;
                if (System.Text.Json.JsonSerializer.Serialize(window.Session.Snapshot) != frozen) throw new InvalidOperationException("Section filtering changed native assignments.");
                SelectCommand(window, "Pitch Axis Raw");
                var reference = Path.Combine(root, "Elite-reference.png"); window.ExportReference(reference);
                if (new FileInfo(reference).Length < 1000) throw new InvalidOperationException("ED reference PNG did not render shared artwork and labels.");
                ThemeControlSmoke.Pump(window); ThemeControlSmoke.SaveImage(window, "elite-workspace");
                EliteReferenceSheetsSmoke.Verify(window, root, artwork: true);
                EliteInputActivitySmoke.Verify(window, physical, () => input);
                var command = window.Session.Catalog.Commands.First(x => x.DisplayName == "Primary Fire").Id;
                window.Session.AssignButton(stick.LogicalId, command, "Primary", "Joy_22", BindingMode.Press, []);
                window.Close();
                if (!window.IsVisible || !window.Session.HasChanges) throw new InvalidOperationException("Cancel lost the current ED workspace.");
                choice = MessageBoxResult.No; window.Close();
                if (window.IsVisible || services.Store.Load(saved.Id).Workspace.Profile.Devices.SelectMany(x => x.Buttons).Any(x => x.SourceButton == "Joy_22"))
                    throw new InvalidOperationException("Discard persisted pending ED edits.");
                VerifySwitch(services, hardware, saved.Id, stick.LogicalId, command);
                VerifyProfileSwitch(services, hardware, saved.Id, stick.LogicalId, command);
                EditionAccess.SetForTests(new());
                var acquisitions = 0;
                using var free = new EliteWorkspaceWindow(services, hardware, saved.Id, pendingChoice: () => MessageBoxResult.No,
                    inputFactory: (_, _) => { acquisitions++; return new FakeDeviceInputSource(physical.PhysicalGuid); });
                free.Show(); ThemeControlSmoke.Pump(free);
                if (Box<StackPanel>(free, "AssignmentEditor").IsEnabled || Box<Button>(free, "StartInputButton").IsEnabled)
                    throw new InvalidOperationException("Free game import bypassed hardware or live-input eligibility.");
                if (Box<Button>(free, "IdentifyControllerButton").IsEnabled) throw new InvalidOperationException("Free edition enabled live identification.");
                Click(free, "IdentifyControllerButton"); Click(free, "ConfirmIdentificationButton");
                Click(free, "StartInputButton");
                if (acquisitions != 0) throw new InvalidOperationException("A direct Free game command acquired input handles.");
                free.Close();
            }
            finally { EditionAccess.SetForTests(previous); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void VerifyControllerAddition(string root, WorkspaceHardware[] hardware)
        {
            var physical = new DetectedDcsDevice(Guid.NewGuid(), "Unillustrated controller", true, ProductGuid: Guid.Parse("0bac06a3-0000-0000-0000-504944564944"));
            var services = new GameWorkspaceServices(new FileGameWorkspaceStore(Path.Combine(root, "AddController")), new GameSmokeDiscovery(physical));
            var source = Path.Combine(root, "Add.4.2.binds");
            File.WriteAllText(source, "<Root PresetName='Add' MajorVersion='4' MinorVersion='2'><PrimaryFire><Primary Device='Keyboard' Key='Key_Space'/><Secondary Device='{NoDevice}' Key=''/></PrimaryFire></Root>");
            Guid savedId;
            using (var window = new EliteWorkspaceWindow(services, hardware, pendingChoice: () => MessageBoxResult.No))
            {
                window.Show(); ThemeControlSmoke.Pump(window); window.ImportFile(source);
                Box<ComboBox>(window, "ConnectedPicker").SelectedIndex = 0; Click(window, "AddControllerButton");
                var document = window.Session!.Snapshot; var pin = document.Hardware.Single(); savedId = document.Id;
                if (document.Profile.Devices.Count != 2 || pin.Value.HardwareId is not null || !Box<StackPanel>(window, "AssignmentEditor").IsEnabled
                    || Box<WrapPanel>(window, "DrawingTools").IsVisible || Box<StackPanel>(window, "ReferenceChoiceTools").IsVisible)
                    throw new InvalidOperationException("A new controller still required ED reference or drawing choices.");
                SelectCommand(window, "Primary Fire"); Box<TextBox>(window, "InputKey").Text = "Joy_4"; Click(window, "ApplyAssignmentButton");
                if (!window.SaveProfile() || !services.Backend.Import("controls", window.Session.Export().Files).Devices.Any(x => x.Identity!.SimSpecificIds["ed.device"] == "06A30BAC"))
                    throw new InvalidOperationException("A new unillustrated controller did not save and export.");
                var identity = System.Text.Json.JsonSerializer.Serialize(window.Session.Snapshot.Profile.Devices);
                Click(window, "ChangeDrawingButton"); Box<ComboBox>(window, "HardwarePicker").SelectedItem = hardware[0]; Click(window, "UseDrawingButton");
                Click(window, "ChangeDrawingButton"); Click(window, "NoDrawingButton");
                if (!window.Session.Snapshot.Hardware[pin.Key].DrawingHidden || System.Text.Json.JsonSerializer.Serialize(window.Session.Snapshot.Profile.Devices) != identity)
                    throw new InvalidOperationException("Changing artwork changed native controller identity or assignments.");
                if (!window.SaveProfile() || Box<Button>(window, "ExportSheetButton").IsEnabled) throw new InvalidOperationException("Hiding artwork did not save or still offered an illustrated export.");
                Box<ListBox>(window, "FeaturePicker").SelectedIndex = 2;
                if (!Box<TextBox>(window, "InputActivity").IsVisible) throw new InvalidOperationException("Unillustrated input testing has no text fallback.");
                EliteReferenceSheetsSmoke.Verify(window, root, artwork: false);
                ThemeControlSmoke.Pump(window); ThemeControlSmoke.SaveImage(window, "elite-add-controller"); window.Close();
            }
            using (var reopened = new EliteWorkspaceWindow(services, hardware, savedId, pendingChoice: () => MessageBoxResult.No))
            {
                reopened.Show(); ThemeControlSmoke.Pump(reopened);
                if (!reopened.Session!.Snapshot.Hardware.Single().Value.DrawingHidden) throw new InvalidOperationException("Reopening lost the explicit No drawing choice.");
                reopened.Close();
            }
            var unique = hardware.Where(x => DcsPhysicalIdentity.FromFileName(x.DefaultFileName) is not null)
                .GroupBy(x => DcsPhysicalIdentity.NameFromFileName(x.DefaultFileName), StringComparer.OrdinalIgnoreCase).First(x => x.Count() == 1).Single();
            var recognized = physical with { Name = DcsPhysicalIdentity.NameFromFileName(unique.DefaultFileName) };
            services = new(new FileGameWorkspaceStore(Path.Combine(root, "SuggestedDrawing")), new GameSmokeDiscovery(recognized));
            using var automatic = new EliteWorkspaceWindow(services, hardware, pendingChoice: () => MessageBoxResult.No);
            automatic.Show(); ThemeControlSmoke.Pump(automatic); automatic.ImportFile(source);
            Box<ComboBox>(automatic, "ConnectedPicker").SelectedIndex = 0; Click(automatic, "AddControllerButton");
            if (automatic.Session!.Snapshot.Hardware.Single().Value.HardwareId != unique.DeviceId) throw new InvalidOperationException("An exact unique drawing was not selected automatically.");
            automatic.Close();
        }
        private static void VerifyIdenticalControllers(string root, WorkspaceHardware[] hardware)
        {
            var a = new DetectedDcsDevice(Guid.NewGuid(), "Identical A", true, ProductGuid: Guid.Parse("0bac06a3-0000-0000-0000-504944564944"));
            var b = a with { PhysicalGuid = Guid.NewGuid(), Name = "Identical B" };
            var services = new GameWorkspaceServices(new FileGameWorkspaceStore(Path.Combine(root, "Identical")), new GameSmokeDiscovery(a, b));
            var source = Path.Combine(root, "Indexed.4.2.binds");
            File.WriteAllText(source, "<Root PresetName='Indexed' MajorVersion='4' MinorVersion='2'><PrimaryFire><Primary Device='06A30BAC' DeviceIndex='0' Key='Joy_1'/><Secondary Device='06A30BAC' DeviceIndex='1' Key='Joy_2'/></PrimaryFire></Root>");
            using var window = new EliteWorkspaceWindow(services, hardware, pendingChoice: () => MessageBoxResult.No);
            window.Show(); ThemeControlSmoke.Pump(window); window.ImportFile(source);
            Box<ComboBox>(window, "ConnectedPicker").SelectedItem = b; Click(window, "AddControllerButton");
            var picker = Box<ComboBox>(window, "ReferencePicker");
            if (!Box<StackPanel>(window, "ReferenceChoiceTools").IsVisible || picker.SelectedItem is not null || window.Session!.Snapshot.Hardware.Count != 0)
                throw new InvalidOperationException("Identical controllers were assigned an inferred ED ordinal.");
            picker.SelectedIndex = 1; var id = ((GameDeviceChoice)picker.SelectedItem!).Id; Click(window, "LinkReferenceButton");
            if (window.Session!.Snapshot.Hardware[id].Device.PhysicalGuid != b.PhysicalGuid) throw new InvalidOperationException("The explicit indexed ED reference linked to a different unit.");
            Box<ComboBox>(window, "ConnectedPicker").SelectedItem = a; Click(window, "AddControllerButton");
            if (window.Session.Snapshot.Hardware.Count != 2 || window.Session.Snapshot.Profile.Devices.Count != 2)
                throw new InvalidOperationException("Adding the second identical controller duplicated or replaced a native reference.");
            window.Close();
        }
        private static void VerifyCommandFilters(EliteWorkspaceWindow window)
        {
            var types = Box<ComboBox>(window, "CommandTypePicker");
            var categories = Box<ComboBox>(window, "CommandCategoryPicker");
            var commands = Box<ListBox>(window, "CommandsList");
            types.SelectedIndex = 2;
            if (commands.Items.Count != 2 || commands.Items.Cast<SimCommand>().Any(x => x.Type != CommandType.Axis))
                throw new InvalidOperationException("Axes filtering did not use the native command type.");
            categories.SelectedItem = "Weapons";
            if (commands.Items.Count != 0 || !Box<TextBlock>(window, "CommandEmptyHint").IsVisible || Box<StackPanel>(window, "AssignmentEditor").IsEnabled)
                throw new InvalidOperationException("An empty category/type intersection left a stale command editable.");
            types.SelectedIndex = 1;
            if (commands.Items.Count != 2 || commands.Items.Cast<SimCommand>().Any(x => x.Type != CommandType.Button))
                throw new InvalidOperationException("Buttons filtering did not intersect the Weapons category.");
            Box<TextBox>(window, "CommandSearch").Text = "secondaryfire";
            if (commands.Items.Count != 1 || commands.Items.Cast<SimCommand>().Single().DisplayName != "Secondary Fire")
                throw new InvalidOperationException("Command search did not match the native identifier within active filters.");
            var assignments = Box<DataGrid>(window, "AssignmentsGrid");
            assignments.SelectedItem = assignments.Items.Cast<EliteAssignmentRow>().First(x => x.IsAxis);
            if (types.SelectedIndex != 0 || categories.SelectedIndex != 0 || Box<TextBox>(window, "CommandSearch").Text.Length != 0
                || commands.SelectedItem is not SimCommand { Type: CommandType.Axis })
                throw new InvalidOperationException("Selecting a filtered-out assignment did not reveal its command.");
            categories.SelectedItem = "Weapons";
            Box<ListBox>(window, "SectionPicker").SelectedIndex = 2;
            if (categories.SelectedIndex != 0 || commands.Items.Count != 0)
                throw new InvalidOperationException("Changing activity retained an unavailable category.");
            Box<ListBox>(window, "SectionPicker").SelectedIndex = 0;
            types.SelectedIndex = 2; Box<TextBox>(window, "CommandSearch").Text = "absent";
            Click(window, "ClearCommandFiltersButton");
            if (commands.Items.Count != 5 || types.SelectedIndex != 0 || Box<TextBlock>(window, "CommandCount").Text != "5 of 5 commands")
                throw new InvalidOperationException("Clearing command filters did not restore the imported catalog.");
            if (commands.ItemsSource is not System.Windows.Data.ListCollectionView view || view.Groups?.Count != 4)
                throw new InvalidOperationException("The command palette did not group commands by display category.");
        }
        private static void VerifyDcsSelectors()
        {
            var previewProfile = new global::Heimdall.Domain.DeviceProfile(Guid.NewGuid(), "tm-mfd", null, "Preview", "Preview.diff.lua");
            var preview = new DcsFolderImportResult("Game selection smoke", [previewProfile], [], [], []);
            var choice = new SavedModuleChoice(Guid.NewGuid(), "Saved DCS aircraft");
            var gameWindow = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(preview), persistLayout: false,
                allowGameSwitch: true, moduleChoices: [choice]);
            gameWindow.Show(); ThemeControlSmoke.Pump(gameWindow);
            if (!Box<WrapPanel>(gameWindow, "GameWorkspaceSelectors").IsVisible)
                throw new InvalidOperationException("The production DCS game/profile selectors are unavailable.");
            Box<ComboBox>(gameWindow, "WorkspaceGamePicker").SelectedIndex = 1;
            if (gameWindow.IsVisible || gameWindow.RequestedGame != "elite-dangerous")
                throw new InvalidOperationException("The DCS workspace did not request the selected game after closing.");
            var profileWindow = new ModuleWorkspaceWindow(new ModuleWorkspaceViewModel(preview), persistLayout: false,
                allowGameSwitch: true, moduleChoices: [choice]);
            profileWindow.Show(); ThemeControlSmoke.Pump(profileWindow);
            Box<ComboBox>(profileWindow, "WorkspaceProfilePicker").SelectedItem = choice;
            if (profileWindow.IsVisible || profileWindow.RequestedModule != choice.Id)
                throw new InvalidOperationException("The DCS workspace did not request the selected saved aircraft.");
        }
        private static void VerifyCards(string root, GameWorkspaceServices services)
        {
            var connection = "Data Source=" + Path.Combine(root, "ipia.db") + ";Pooling=False";
            new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
            var settings = new FileAppSettingsService(Path.Combine(root, "defaults.json"), Path.Combine(root, "settings.json"), Path.Combine(root, "theme.txt"));
            var window = new MainWindow(new ModuleWorkspaceLoader(), new DcsFolderImporter(), settings,
                new SqliteEditorWorkspaceRepository(connection), deviceDiscovery: new EmptyDeviceDiscovery(), games: services);
            try
            {
                window.Show(); ThemeControlSmoke.Pump(window);
                var cards = (GameSelectionView)window.FindName("GameSelection");
                if (!cards.IsVisible || !((Button)cards.FindName("EliteCard")).IsVisible) throw new InvalidOperationException("Startup game cards are unavailable.");
                ThemeControlSmoke.SaveImage(window, "game-selection");
                ((Button)cards.FindName("DcsCard")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (!((Grid)window.FindName("DcsLandingPanel")).IsVisible || cards.IsVisible)
                    throw new InvalidOperationException("DCS card did not preserve access to the existing module workflow.");
            }
            finally { window.Close(); }
        }
        private static void VerifyProfileSwitch(GameWorkspaceServices services, IReadOnlyList<WorkspaceHardware> hardware, Guid id, string device, string command)
        {
            var other = services.Store.Load(id).Workspace with { Id = Guid.NewGuid(), Name = "Other Controls" };
            services.Store.Save(other, null);
            var choice = MessageBoxResult.Cancel;
            using var window = new EliteWorkspaceWindow(services, hardware, id, pendingChoice: () => choice);
            window.Show(); ThemeControlSmoke.Pump(window);
            window.Session!.AssignButton(device, command, "Primary", "Joy_27", BindingMode.Press, []);
            var picker = Box<ComboBox>(window, "ProfilePicker");
            var otherChoice = picker.Items.Cast<GameWorkspaceEntry>().Single(x => x.Id == other.Id);
            picker.SelectedItem = otherChoice;
            if (window.Session.Snapshot.Id != id || ((GameWorkspaceEntry)picker.SelectedItem).Id != id || !window.Session.HasChanges)
                throw new InvalidOperationException("Cancel did not retain the selected game profile and draft.");
            choice = MessageBoxResult.No; picker.SelectedItem = otherChoice;
            if (window.Session.Snapshot.Id != other.Id || window.Session.HasChanges ||
                services.Store.Load(id).Workspace.Profile.Devices.SelectMany(x => x.Buttons).Any(x => x.SourceButton == "Joy_27"))
                throw new InvalidOperationException("Profile switching persisted discarded data or selected the wrong profile.");
            window.Close();
        }
        private static void VerifySwitch(GameWorkspaceServices services, IReadOnlyList<WorkspaceHardware> hardware, Guid id, string device, string command)
        {
            var choice = MessageBoxResult.Cancel;
            using var window = new EliteWorkspaceWindow(services, hardware, id, pendingChoice: () => choice);
            window.Show(); ThemeControlSmoke.Pump(window);
            window.Session!.AssignButton(device, command, "Primary", "Joy_23", BindingMode.Press, []);
            Box<ComboBox>(window, "GamePicker").SelectedIndex = 0;
            if (!window.IsVisible || Box<ComboBox>(window, "GamePicker").SelectedIndex != 1)
                throw new InvalidOperationException("Cancel did not retain the ED game selector and draft.");
            choice = MessageBoxResult.Yes; Box<ComboBox>(window, "GamePicker").SelectedIndex = 0;
            if (window.IsVisible || !window.DcsRequested || !services.Store.Load(id).Workspace.Profile.Devices.SelectMany(x => x.Buttons).Any(x => x.SourceButton == "Joy_23"))
                throw new InvalidOperationException("Save-and-switch did not persist the ED draft before returning to DCS.");
        }
        private static T Box<T>(FrameworkElement window, string name) where T : FrameworkElement => (T)window.FindName(name);
        private static void Click(FrameworkElement window, string name) => Box<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        private static void SelectCommand(FrameworkElement window, string name)
        {
            var list = Box<ListBox>(window, "CommandsList"); list.SelectedItem = list.Items.Cast<SimCommand>().Single(x => x.DisplayName == name);
        }
        #endregion END private helpers
    }
}

