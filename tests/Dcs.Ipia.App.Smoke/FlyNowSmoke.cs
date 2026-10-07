using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using DcsIpia.Presentation;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Drives native Fly Now, setup browsing, active locking and Clear through the Windows UI.</summary>
    public static class FlyNowSmoke
    {
        #region BEGIN Public
        /// <summary>Deploys the restored authored module and verifies restart/cleanup with a second database module.</summary>
        public static void Verify(string connection, IEditorWorkspaceRepository repository, IAppSettingsService settings,
            string root, Guid moduleId)
        {
            var destination = Path.Combine(root, "DCS");
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "unrelated.txt"), "keep");
            var other = new ModuleConfiguration(new DcsModule(Guid.NewGuid(), "FLY-OTHER", "Other aircraft"), [], [], [], "smoke");
            repository.SaveAsync(new EditorWorkspaceSnapshot(other, [], [], [], [], [], [], new Dictionary<string, string>())).GetAwaiter().GetResult();
            settings.TryUpdateSavedGamesDcsFolder("");
            var service = new SqliteFlyNowService(connection, Path.Combine(root, "FlyNowBackups"),
                new global::Dcs.Ipia.Dcs.FlyNowPayloadBuilder(repository));
            var window = new MainWindow(new global::Dcs.Ipia.Dcs.ModuleWorkspaceLoader(), new global::Dcs.Ipia.Dcs.DcsFolderImporter(),
                settings, repository, flyNow: service, savedGamesFolderPicker: () => destination);
            window.Show();
            var modules = (ComboBox)window.FindName("SavedModules")!;
            var fly = (Button)window.FindName("FlyNowButton")!;
            var clear = (Button)window.FindName("ClearFlyNowButton")!;
            Wait(window, () => modules.Items.Count == 2 && ((TextBlock)window.FindName("FlyNowStatus")!).Text.StartsWith("No module", StringComparison.Ordinal));
            if (fly.IsEnabled || clear.IsEnabled) throw new InvalidOperationException("Fly Now and Clear must be disabled before destination setup.");
            modules.SelectedItem = modules.Items.Cast<SavedModuleChoice>().Single(x => x.Id == moduleId);
            Click((Button)window.FindName("BrowseSavedGamesButton")!);
            if (!fly.IsEnabled || settings.Current.SavedGamesDcsFolder != destination)
                throw new InvalidOperationException("Setup browsing did not persist the default destination and enable Fly Now.");
            window.UpdateLayout();
            var restore = (Button)window.FindName("RestoreModuleSnapshotButton")!;
            if (clear.Content?.ToString() != "Clear" || clear.TranslatePoint(new Point(), window).X < restore.TranslatePoint(new Point(), window).X + restore.ActualWidth)
                throw new InvalidOperationException("Clear must appear immediately next to Restore module snapshot.");
            Click(fly);
            Wait(window, () => clear.IsEnabled && ((TextBlock)window.FindName("ImportStatus")!).Text.StartsWith("Fly Now! deployed", StringComparison.Ordinal));
            var active = service.GetActiveAsync().GetAwaiter().GetResult()!;
            if (active.ModuleId != moduleId || !active.Files.Any(x => x.StartsWith("Config/Input/UiLayer/", StringComparison.Ordinal)))
                throw new InvalidOperationException("Fly Now did not deploy and track the selected module and its UI Layer.");
            modules.SelectedItem = modules.Items.Cast<SavedModuleChoice>().Single(x => x.Id == other.Module.Id);
            if (fly.IsEnabled) throw new InvalidOperationException("Another module remained eligible while Fly Now was active.");
            window.Close();
            var reopened = new MainWindow(new global::Dcs.Ipia.Dcs.ModuleWorkspaceLoader(), new global::Dcs.Ipia.Dcs.DcsFolderImporter(),
                settings, repository, flyNow: service);
            reopened.Show();
            modules = (ComboBox)reopened.FindName("SavedModules")!;
            clear = (Button)reopened.FindName("ClearFlyNowButton")!;
            fly = (Button)reopened.FindName("FlyNowButton")!;
            Wait(reopened, () => modules.Items.Count == 2 && clear.IsEnabled);
            modules.SelectedItem = modules.Items.Cast<SavedModuleChoice>().Single(x => x.Id == other.Module.Id);
            if (fly.IsEnabled) throw new InvalidOperationException("The active lock did not survive reopening the Modules window.");
            Click(clear);
            Wait(reopened, () => !clear.IsEnabled && fly.IsEnabled);
            if (service.GetActiveAsync().GetAwaiter().GetResult() is not null ||
                active.Files.Any(x => File.Exists(Path.Combine(destination, x))) ||
                File.ReadAllText(Path.Combine(destination, "unrelated.txt")) != "keep" ||
                repository.GetAsync(moduleId).GetAwaiter().GetResult() is null)
                throw new InvalidOperationException("Clear did not remove exactly the deployment while preserving unrelated files and authoring data.");
            reopened.Close();
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

        private static void Wait(Window window, Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(90);
            while (!ready())
            {
                if (DateTime.UtcNow > deadline)
                    throw new InvalidOperationException("Fly Now UI smoke timed out: " + ((TextBlock)window.FindName("ImportStatus")!).Text);
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            }
        }
        #endregion END private helpers
    }
}
