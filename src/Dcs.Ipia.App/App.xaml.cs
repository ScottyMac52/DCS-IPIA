using System.IO;
using DcsIpia.Presentation.Games;
using System.Windows;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DcsIpia.Presentation
{
    /// <summary>Starts the WPF application with dependency-injected services.</summary>
    public partial class App : System.Windows.Application, IDisposable
    {
        private ServiceProvider? _services;
        private FileStream? _dataLease;
        private readonly ILogger<App> _logger;
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>Uses the Program-owned logger factory throughout the WPF lifetime.</summary>
        /// <param name="logger">Application failure logger.</param>
        /// <param name="loggerFactory">Shared factory, disposed after database shutdown.</param>
        public App(ILogger<App> logger, ILoggerFactory loggerFactory)
        {
            ApplicationTheme.Initialize();
            _logger = logger;
            _loggerFactory = loggerFactory;
            DispatcherUnhandledException += (_, e) => ApplicationLog.DispatcherFailure(_logger, e.Exception);
        }
        #region BEGIN Public
        /// <summary>Creates import and settings services before showing the main window.</summary>
        /// <param name="e">The WPF startup event.</param>
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var releaseSmoke = e.Args.Length == 2 && e.Args[0] == "--release-smoke";
            UserDataProfile profile;
            try
            {
                var name = e.Args.Length == 2 && e.Args[0] == "--test-profile" ? e.Args[1] : null;
                if (!releaseSmoke && e.Args.Length != 0 && name is null)
                    throw new ArgumentException("Use --test-profile NAME, or start without arguments for Production.");
                profile = UserDataProfile.Select(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), name);
            }
            catch (Exception ex)
            {
                ApplicationLog.InvalidProfile(_logger, ex);
                ThemeMessageBox.Show(ex.Message, "Invalid IPIA profile", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(1);
                return;
            }
            var userFolder = releaseSmoke ? Path.GetFullPath(e.Args[1]) : profile.DataDirectory;
            EditionAccess.Load();
            AppearanceManager.Configure(new FileAppearanceStore(userFolder));
            var recovery = new UserDataRecoveryService(userFolder, ApplicationVersion.Label);
            var connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(userFolder, "ipia.db"),
                                                                       ForeignKeys = true }
                                       .ToString();
            var readOnlyConnection = new SqliteConnectionStringBuilder(connectionString)
            { Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
            var supportReports = new SupportReportService(new SqliteSupportDatabaseInspector(readOnlyConnection),
                new SqliteHardwareInventoryRepository(readOnlyConnection),
                new SqliteEditorWorkspaceRepository(readOnlyConnection, persistReadUpgrades: false),
                new FileUserMappingLibrary(userFolder), typeof(App).Assembly.GetName().Version!, SqliteMigrationRunner.CurrentVersion);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(userFolder)!);
                _dataLease = new FileStream(userFolder + ".session.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ApplicationLog.DataLockFailure(_logger, userFolder, ex);
                ThemeMessageBox.Show("Unable to acquire the user-data lock. Close other IPIA instances before retrying: " + ex.Message,
                    "DCS-IPIA", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(1);
                return;
            }
            try
            {
                recovery.RecoverInterruptedRestore();
                Directory.CreateDirectory(userFolder);
                var temporary = Path.Combine(userFolder, "Temp");
                Directory.CreateDirectory(temporary);
                Environment.SetEnvironmentVariable("TMP", temporary);
                Environment.SetEnvironmentVariable("TEMP", temporary);
                Environment.SetEnvironmentVariable("TMPDIR", temporary);
                await new SqliteMigrationRunner(connectionString).MigrateAsync();
            }
            catch (Exception ex)
            {
                ApplicationLog.DataRecoveryFailure(_logger, userFolder, ex);
                var window = new UserDataRecoveryWindow(recovery, CloseDatabase, "Unable to open local data: " + ex.Message + " Restore a verified backup, or close IPIA and inspect the retained data.", supportReports: supportReports);
                MainWindow = window;
                window.ShowDialog();
                Shutdown(window.RequiresRestart ? 0 : 1);
                return;
            }
            var registrations =
                new ServiceCollection()
                    .AddSingleton(supportReports)
                    .AddSingleton(_loggerFactory)
                    .AddSingleton(typeof(ILogger<>), typeof(AssemblyLogger<>))
                    .AddSingleton<IUserDataRecoveryService>(recovery)
                    .AddSingleton<IUserMappingLibrary>(new FileUserMappingLibrary(userFolder))
                    .AddSingleton<Action>(_ => CloseDatabase)
                    .AddSingleton<IAppSettingsService>(
                        _ => new SqliteAppSettingsService(
                            connectionString, Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
                            Path.Combine(userFolder, "user-settings.json"), Path.Combine(userFolder, "theme.txt"),
                            Path.Combine(userFolder, "appsettings.json")))
                    .AddSingleton<IModuleConfigurationRepository>(
                        _ => new SqliteConfigurationRepository(connectionString))
                    .AddSingleton<IEditorWorkspaceRepository>(
                        _ => new SqliteEditorWorkspaceRepository(connectionString))
                    .AddSingleton<IHardwareInventoryRepository>(_ => new SqliteHardwareInventoryRepository(connectionString))
                    .AddSingleton<IControllerCapabilityDiscovery, WindowsConnectedDeviceDiscovery>()
                    .AddSingleton<IConnectedDeviceDiscovery, WindowsConnectedDeviceDiscovery>()
                    .AddSingleton<HardwareInventoryManager>()
                    .AddSingleton<ISetupRepository>(_ => new SqliteSetupRepository(connectionString))
                    .AddSingleton<SetupManager>()
                    .AddSingleton<ISharedUiLayerWorkspaceRepository>(
                        _ => new SqliteSharedUiLayerWorkspaceRepository(connectionString))
                    .AddSingleton<IFlyNowPayloadBuilder, FlyNowPayloadBuilder>()
                    .AddSingleton<IFlyNowService>(provider => profile.IsTest ? new IsolatedFlyNowService() :
                        new SqliteFlyNowService(connectionString, Path.Combine(userFolder, "FlyNowBackups"),
                            provider.GetRequiredService<IFlyNowPayloadBuilder>()))
                    .AddSingleton<IModuleWorkspaceLoader, ModuleWorkspaceLoader>()
                    .AddSingleton<IDcsFolderImporter, DcsFolderImporter>()
                    .AddSingleton<IGameWorkspaceStore>(_ => new FileGameWorkspaceStore(userFolder))
                    .AddSingleton<GameWorkspaceServices>()
                    .AddTransient<MainWindow>();
            if (!releaseSmoke)
            {
                registrations.AddSingleton(profile);
                registrations.AddSingleton<IUserDataProfileService>(new UserDataProfileService(profile, ApplicationVersion.Label));
            }
            _services = registrations.BuildServiceProvider();
            MainWindow = _services.GetRequiredService<MainWindow>();
            MainWindow.Show();
            if (releaseSmoke)
            {
                try { await PublishedApplicationSmoke.VerifyAsync(MainWindow, userFolder); Shutdown(0); }
                catch (Exception ex)
                {
                    ApplicationLog.ReleaseSmokeFailure(_logger, userFolder, ex);
                    File.WriteAllText(Path.Combine(userFolder, "release-smoke.error"), ex.ToString());
                    Shutdown(1);
                }
            }
        }
        /// <summary>Releases the service provider on application exit.</summary>
        /// <param name="e">The WPF exit event.</param>
        protected override void OnExit(ExitEventArgs e)
        {
            Dispose();
            base.OnExit(e);
        }
        /// <summary>Releases database services and the user-profile lease.</summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        /// <summary>Releases owned managed resources for the application lifetime.</summary>
        /// <param name="disposing">Whether managed resources should be released.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;
            CloseDatabase();
            _dataLease?.Dispose();
            _dataLease = null;
        }
        #endregion END Public
        #region BEGIN private helpers
        private void CloseDatabase()
        {
            _services?.Dispose();
            _services = null;
            SqliteConnection.ClearAllPools();
        }
        #endregion END private helpers
    }
}

