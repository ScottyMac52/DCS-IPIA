using System.IO;
using System.Windows;
using System.Windows.Controls;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Dcs;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;
using Heimdall.App.Features.InputTest;
using Heimdall.App;

namespace Heimdall.App.Smoke
{
    /// <summary>Exercises real Free UI, blocked direct commands and signed Owner recovery before unrestricted regression smoke.</summary>
    public static class EditionSmoke
    {
        #region BEGIN Public
        /// <summary>Verifies both editions without storing or distributing a production owner credential.</summary>
        public static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-edition-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            using var issuer = new OwnerLicenseTestIssuer();
            try
            {
                EditionAccess.SetForTests(new EditionPolicy());
                var appearance = new FileAppearanceStore(root);
                var saved = new AppearanceSettings(Opacity: 0.1, ControlOpacity: 0.4);
                if (!appearance.TrySave(saved)) throw new InvalidOperationException(appearance.LastError);
                var settings = new FileAppSettingsService(Path.Combine(root, "defaults.json"), Path.Combine(root, "settings.json"), Path.Combine(root, "theme.txt"));
                settings.TryUpdateTheme(AppearanceTheme.CockpitGreen);
                AppearanceManager.Configure(appearance); ThemeManager.Apply("Light");
                if (AppearanceManager.Active != FreeEditionAppearance.Settings || !ReferenceEquals(AppearanceManager.Image, FreeEditionAppearance.Image))
                    throw new InvalidOperationException("Free Edition did not load its embedded fixed photograph.");
                var chooser = new SettingsWindow(settings); chooser.Show(); ThemeControlSmoke.Pump(chooser);
                try
                {
                    var skin = (ComboBox)chooser.FindName("ThemeChoice")!;
                    if (skin.IsEnabled || ((Button)chooser.FindName("CustomizeAppearance")!).IsEnabled || ((Button)chooser.FindName("ResetAppearance")!).IsEnabled || skin.SelectedIndex != 2)
                        throw new InvalidOperationException("Free Edition exposed appearance controls.");
                    skin.SelectedIndex = 0;
                    ((Button)chooser.FindName("ResetAppearance")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    if (settings.Current.Theme != AppearanceTheme.CockpitGreen || appearance.Current != saved)
                        throw new InvalidOperationException("Free appearance wrote over saved owner preferences.");
                    try { var unexpected = new AppearanceWindow(appearance); unexpected.Close(); throw new InvalidOperationException("Free direct appearance constructor was allowed."); }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("reserved", StringComparison.Ordinal)) { }
                    try { using var unexpected = new WindowsDeviceInputSource(0, []); throw new InvalidOperationException("Free native acquisition was allowed."); }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("reserved", StringComparison.Ordinal)) { }
                    ThemeControlSmoke.SaveImage(chooser, "free-edition-settings");
                }
                finally { chooser.Close(); }
                VerifyHardware(root, settings);
                VerifyFreeTraining(root);
                foreach (var policy in new[] { issuer.PremiumPolicy, issuer.Policy })
                {
                    EditionAccess.SetForTests(policy); ThemeManager.Apply(settings.Current.Theme.ToString()); AppearanceManager.Restore();
                    if (AppearanceManager.Active != saved) throw new InvalidOperationException("Paid activation did not restore saved appearance.");
                    var unlocked = new SettingsWindow(settings); unlocked.Show(); ThemeControlSmoke.Pump(unlocked);
                    if (!((ComboBox)unlocked.FindName("ThemeChoice")!).IsEnabled || !((Button)unlocked.FindName("CustomizeAppearance")!).IsEnabled
                        || ((TextBlock)unlocked.FindName("EditionStatus")!).Text != policy.Label)
                        throw new InvalidOperationException("Signed Premium/Owner did not expose its own edition and unlock appearance controls.");
                    unlocked.Close();
                }
            }
            finally
            {
                EditionAccess.SetForTests(issuer.Policy); AppearanceManager.Configure(null); ThemeManager.Apply("Light");
                AppearanceManager.ResetForTests();
                Directory.Delete(root, true);
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void VerifyFreeTraining(string root)
        {
            var policy = EditionAccess.Policy;
            var before = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
            Console.WriteLine("Smoke: both full training lessons in Free, with isolation and failure/restart/exit.");
            TrainingSmoke.Verify();
            FreeTrainingScopeSmoke.Verify();
            if (!ReferenceEquals(policy, EditionAccess.Policy) || EditionAccess.Policy.IsOwner)
                throw new InvalidOperationException("Training changed the process edition.");
            if (!before.Keys.Order(StringComparer.Ordinal).SequenceEqual(Directory.GetFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)) ||
                before.Any(pair => !pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key))))
                throw new InvalidOperationException("Free training changed the normal module/settings files.");
            try { using var unexpected = new WindowsDeviceInputSource(0, []); throw new InvalidOperationException("Training unlocked native input."); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("reserved", StringComparison.Ordinal)) { }
        }
        private static void VerifyHardware(string root, FileAppSettingsService settings)
        {
            var connection = "Data Source=" + Path.Combine(root, "edition.db") + ";Pooling=False";
            new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
            var repository = new DcsSqliteEditorWorkspaceRepository(connection);
            var sources = DcsModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root).GetAwaiter().GetResult();
            var defaults = DcsSharedUiLayerWorkspaceFactory.CreateAsync().GetAwaiter().GetResult();
            var created = false;
            var form = new DcsNewModuleWindow(repository, defaults, sources, new EmptyDeviceDiscovery(), completed: () => created = true, savedGamesRoot: root);
            form.Show(); ThemeControlSmoke.Pump(form);
            try
            {
                var hardware = (ComboBox)form.FindName("HardwareChoices")!;
                if (hardware.Items.Cast<WorkspaceHardware>().Any(item => !EditionAccess.Policy.AllowsHardware(item.DeviceId)))
                    throw new InvalidOperationException("Free hardware selector exposed reserved layouts.");
                ((TextBox)form.FindName("DcsIdInput")!).Text = "FreeEditionSmoke";
                ((TextBox)form.FindName("DisplayNameInput")!).Text = "Free Edition smoke";
                hardware.SelectedItem = hardware.Items.Cast<WorkspaceHardware>().Single(item => item.DeviceId == "tm-mfd");
                ((TextBox)form.FindName("NativeFileInput")!).Text = "F16 MFD 3 {12345678-1111-2222-3333-444455556666}.diff.lua";
                ((Button)form.FindName("AddUnitButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ((Button)form.FindName("CreateButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ThemeControlSmoke.Pump(form);
                if (!created || form.CreatedWorkspace is null) throw new InvalidOperationException("Free Thrustmaster module could not be saved: " + ((TextBlock)form.FindName("CreationError")!).Text);
                var allowed = form.CreatedWorkspace;
                var window = new DcsModuleWorkspaceWindow(new DcsModuleWorkspaceViewModel(allowed), settings, repository, new EmptyDeviceDiscovery());
                window.Show(); ThemeControlSmoke.Pump(window);
                try
                {
                    var view = new DcsInputTestView(); view.Initialize(window.DcsContext);
                    ((Button)view.FindName("StartInputTestButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    if (!((TextBlock)view.FindName("InputTestStatus")!).Text.Contains("reserved", StringComparison.Ordinal))
                        throw new InvalidOperationException("Free direct input-test handler escaped the edition boundary.");
                    view.Dispose();
                    var reserved = DcsWorkspaceStructureEditor.Hardware(allowed).Single(item => item.DeviceId == "moza-ab9");
                    var before = window.Session.Working;
                    window.RunStructureEdit(current => DcsWorkspaceStructureEditor.AddDevice(current, reserved, AssignmentLayer.Module,
                        "Other base {23456789-1111-2222-3333-444455556666}.diff.lua", "other", "other"));
                    if (!ReferenceEquals(before, window.Session.Working)) throw new InvalidOperationException("Reserved hardware edit mutated a Free workspace.");
                    ThemeControlSmoke.SaveImage(window, "free-edition-workspace");
                }
                finally { window.Close(); }
                var other = DcsWorkspaceStructureEditor.Hardware(allowed).Single(item => item.DeviceId == "moza-ab9");
                var legacy = DcsWorkspaceStructureEditor.AddDevice(allowed, other, AssignmentLayer.Module,
                    "Legacy base {34567890-1111-2222-3333-444455556666}.diff.lua", "legacy", "legacy");
                repository.SaveAsync(legacy).GetAwaiter().GetResult();
                var locked = new DcsModuleWorkspaceWindow(new DcsModuleWorkspaceViewModel(legacy), settings, repository, new EmptyDeviceDiscovery());
                locked.Show(); ThemeControlSmoke.Pump(locked);
                try
                {
                    if (((ContentControl)locked.FindName("FeatureContent")!).IsEnabled || ((Button)locked.FindName("BuildButton")!).IsEnabled)
                        throw new InvalidOperationException("Existing reserved hardware was editable in Free.");
                    string? nativeReason = null;
                    try { EditionAccess.RequireWorkspace(legacy); }
                    catch (InvalidOperationException error) { nativeReason = error.Message; }
                    var invoked = false;
                    var denied = locked.WorkspaceContext.RunOperation(WorkspaceOperationKind.Save, _ =>
                    { invoked = true; return new(WorkspaceOperationOutcome.Applied, "Unexpected persistence"); });
                    if (nativeReason is null || !locked.WorkspaceContext.State.ReadOnly ||
                        locked.WorkspaceContext.State.Capabilities.Save.Reason != nativeReason ||
                        denied.Outcome != WorkspaceOperationOutcome.Blocked || denied.Message != nativeReason || invoked)
                        throw new InvalidOperationException("The shared context lost the native reserved-hardware denial or invoked persistence.");
                    try { locked.SaveWorkspaceAsync().GetAwaiter().GetResult(); throw new InvalidOperationException("Unlicensed save unexpectedly succeeded."); }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("reserved", StringComparison.OrdinalIgnoreCase)) { }
                    if (repository.GetAsync(legacy.Configuration.Module.Id).GetAwaiter().GetResult()!.Configuration.Profiles.Count != legacy.Configuration.Profiles.Count)
                        throw new InvalidOperationException("Free fallback removed existing hardware data.");
                }
                finally { locked.Close(); }
                var main = new MainWindow(new DcsModuleWorkspaceLoader(), new DcsFolderImporter(), settings, repository, deviceDiscovery: new EmptyDeviceDiscovery());
                main.Show(); ThemeControlSmoke.Pump(main); ThemeControlSmoke.SaveImage(main, "free-edition-main"); main.Close();
            }
            finally { form.Close(); }
        }
        #endregion END private helpers
    }
}
