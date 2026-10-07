using System.IO;
using System.Windows;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace DcsIpia.Presentation
{
    /// <summary>Starts the WPF application with dependency-injected services.</summary>
    public partial class App : System.Windows.Application
    {
        private ServiceProvider? _services;
        #region BEGIN Public
        /// <summary>Creates import and settings services before showing the main window.</summary>
        /// <param name="e">The WPF startup event.</param>
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var releaseSmoke = e.Args.Length == 2 && e.Args[0] == "--release-smoke";
            var userFolder =
                releaseSmoke ? Path.GetFullPath(e.Args[1]) :
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DCS-IPIA");
            Directory.CreateDirectory(userFolder);
            var connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(userFolder, "ipia.db"),
                                                                       ForeignKeys = true }
                                       .ToString();
            try
            {
                await new SqliteMigrationRunner(connectionString).MigrateAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to open local database: " + ex.Message, "DCS-IPIA", MessageBoxButton.OK,
                                MessageBoxImage.Error);
                Shutdown(1);
                return;
            }
            _services =
                new ServiceCollection()
                    .AddSingleton<IAppSettingsService>(
                        _ => new SqliteAppSettingsService(
                            connectionString, Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
                            Path.Combine(userFolder, "user-settings.json"), Path.Combine(userFolder, "theme.txt"),
                            Path.Combine(userFolder, "appsettings.json")))
                    .AddSingleton<IModuleConfigurationRepository>(
                        _ => new SqliteConfigurationRepository(connectionString))
                    .AddSingleton<IEditorWorkspaceRepository>(
                        _ => new SqliteEditorWorkspaceRepository(connectionString))
                    .AddSingleton<ISharedUiLayerWorkspaceRepository>(
                        _ => new SqliteSharedUiLayerWorkspaceRepository(connectionString))
                    .AddSingleton<IFlyNowPayloadBuilder, FlyNowPayloadBuilder>()
                    .AddSingleton<IFlyNowService>(provider => new SqliteFlyNowService(connectionString,
                        Path.Combine(userFolder, "FlyNowBackups"), provider.GetRequiredService<IFlyNowPayloadBuilder>()))
                    .AddSingleton<IModuleWorkspaceLoader, ModuleWorkspaceLoader>()
                    .AddSingleton<IDcsFolderImporter, DcsFolderImporter>()
                    .AddTransient<MainWindow>()
                    .BuildServiceProvider();
            MainWindow = _services.GetRequiredService<MainWindow>();
            MainWindow.Show();
            if (releaseSmoke)
            {
                try { await PublishedApplicationSmoke.VerifyAsync(MainWindow, userFolder); Shutdown(0); }
                catch (Exception ex) { File.WriteAllText(Path.Combine(userFolder, "release-smoke.error"), ex.ToString()); Shutdown(1); }
            }
        }
        /// <summary>Releases the service provider on application exit.</summary>
        /// <param name="e">The WPF exit event.</param>
        protected override void OnExit(ExitEventArgs e)
        {
            _services?.Dispose();
            base.OnExit(e);
        }
        #endregion END Public
    }
}
