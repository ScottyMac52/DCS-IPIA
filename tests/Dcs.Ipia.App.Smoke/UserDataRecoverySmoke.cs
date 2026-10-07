using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using DcsIpia.Presentation;
using Microsoft.Data.Sqlite;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Checks shared menu commands and real backup/restore UI without file-picker interaction.</summary>
    public static class UserDataRecoverySmoke
    {
        #region BEGIN Public
        /// <summary>Verifies menu enablement, saved-state backup and database-independent restore.</summary>
        public static void Verify()
        {
            var folder = Path.Combine(Path.GetTempPath(), "ipia-data-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                var profile = UserDataProfile.Select(folder, "smoke");
                var data = profile.DataDirectory;
                Directory.CreateDirectory(data);
                var connection = "Data Source=" + Path.Combine(data, "ipia.db") + ";Pooling=False";
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                repository.SaveAsync(new EditorWorkspaceSnapshot(new ModuleConfiguration(new DcsModule(Guid.NewGuid(), "BACKUP", "Backup test"), [], [], [], "smoke"),
                    [], [], [], [], [], [], new Dictionary<string, string>())).GetAwaiter().GetResult();
                var settings = new SqliteAppSettingsService(connection, Path.Combine(data, "defaults.json"), Path.Combine(data, "user-settings.json"), Path.Combine(data, "theme.txt"));
                var recovery = new UserDataRecoveryService(data, "1.2.3.4");
                var main = new MainWindow(new ModuleWorkspaceLoader(), new DcsFolderImporter(), settings, repository,
                    flyNow: new IsolatedFlyNowService(), recovery: recovery, profile: profile,
                    profileService: new UserDataProfileService(profile, "1.2.3.4"));
                main.Show();
                var modules = (ComboBox)main.FindName("SavedModules")!;
                Pump(main, () => modules.SelectedItem is not null && MainWindowCommands.Backup.CanExecute(null, main));
                if (!((TextBlock)main.FindName("ProfileIndicator")!).Text.Contains("TEST PROFILE: smoke", StringComparison.Ordinal) ||
                    !((TextBlock)main.FindName("ProfileIndicator")!).Text.Contains(data, StringComparison.Ordinal) ||
                    MainWindowCommands.FlyNow.CanExecute(null, main) || MainWindowCommands.ClearDeployment.CanExecute(null, main) ||
                    !MainWindowCommands.Profiles.CanExecute(null, main) ||
                    ((Button)main.FindName("FlyNowButton")!).IsEnabled)
                    throw new InvalidOperationException("Test profile visibility or deployment isolation failed.");
                var menu = (Menu)main.FindName("MainMenu")!;
                if (menu.Items.Count != 6 || ((Button)main.FindName("OpenModuleButton")!).Tag != MainWindowCommands.OpenModule ||
                    !MainWindowCommands.OpenModule.CanExecute(null, main)) throw new InvalidOperationException("Main menu and quick actions did not share enabled commands.");
                modules.SelectedIndex = -1;
                if (MainWindowCommands.OpenModule.CanExecute(null, main)) throw new InvalidOperationException("Open menu command remained enabled without selection.");
                main.Close();
                var archive = Path.Combine(folder, "backup.ipia-backup.zip");
                var tools = new UserDataRecoveryWindow(recovery, backupPicker: () => archive);
                tools.Show();
                Click((Button)tools.FindName("BackupUserDataButton")!);
                Pump(tools, () => ((TextBlock)tools.FindName("RecoveryStatus")!).Text.StartsWith("Verified full backup saved:", StringComparison.Ordinal));
                tools.Close();
                // Recovery must work with no healthy database-backed window or service.
                var target = Path.Combine(folder, "broken");
                Directory.CreateDirectory(target);
                File.WriteAllText(Path.Combine(target, "ipia.db"), "broken database");
                var closed = false;
                var shell = new UserDataRecoveryWindow(new UserDataRecoveryService(target, "1.2.3.4"), () => { closed = true; SqliteConnection.ClearAllPools(); },
                    "Database could not be opened.", restorePicker: () => archive, restoreConfirmation: () => true);
                shell.Show();
                Click((Button)shell.FindName("RestoreUserDataButton")!);
                Pump(shell, () => ((TextBlock)shell.FindName("RecoveryStatus")!).Text.StartsWith("Restore verified.", StringComparison.Ordinal));
                if (!closed || !shell.RequiresRestart || ((Button)shell.FindName("BackupUserDataButton")!).IsEnabled)
                    throw new InvalidOperationException("Restore did not quiesce services and require restart.");
                shell.Close();
                if (new SqliteEditorWorkspaceRepository("Data Source=" + Path.Combine(target, "ipia.db") + ";Pooling=False").ListAsync().GetAwaiter().GetResult().Count != 1)
                    throw new InvalidOperationException("The UI restore did not preserve the saved module.");
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
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
                if (DateTime.UtcNow >= deadline) throw new InvalidOperationException("Data recovery UI timed out.");
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            }
        }
        #endregion END private helpers
    }
}
