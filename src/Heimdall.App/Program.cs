using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Heimdall.App
{
    /// <summary>Owns configuration, logging and the WPF application lifetime.</summary>
    public static class Program
    {
        #region BEGIN Public
        /// <summary>Starts HEIMDALL with JSON-configured logging before opening local data.</summary>
        /// <param name="args">Production, isolated profile or release-smoke arguments.</param>
        /// <returns>The WPF exit code, or one when startup fails.</returns>
        [STAThread]
        public static int Main(string[] args)
        {
            IConfigurationRoot? configuration = null;
            ServiceProvider? services = null;
            ILogger<App>? logger = null;
            try
            {
                configuration = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true).Build();
                services = ConfigureServices(configuration).AddSingleton<App>().BuildServiceProvider();
                logger = services.GetRequiredService<ILogger<App>>();
                var app = services.GetRequiredService<App>();
                app.InitializeComponent();
                if (logger.IsEnabled(LogLevel.Information))
                    ApplicationLog.Starting(logger, ApplicationVersion.Label, null);
                return app.Run();
            }
            catch (Exception ex)
            {
                if (logger is not null) ApplicationLog.Terminated(logger, ex);
                ThemeMessageBox.Show("Unable to start HEIMDALL: " + ex.Message +
                    "\nCheck appsettings.json and the configured log folder.", "HEIMDALL", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
            finally
            {
                // Dispose logging after App and its database services, flushing every file sink.
                services?.Dispose();
                (configuration as IDisposable)?.Dispose();
            }
        }

        /// <summary>Registers Serilog and namespace-qualified typed loggers through DI.</summary>
        /// <param name="configuration">The appsettings.json configuration and reload source.</param>
        /// <returns>Logging services to compose with the application services.</returns>
        public static IServiceCollection ConfigureServices(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            return new ServiceCollection().AddSingleton(configuration)
                .AddSerilog((provider, loggerConfiguration) => loggerConfiguration
                    .ReadFrom.Configuration(provider.GetRequiredService<IConfiguration>())
                    .ReadFrom.Services(provider))
                .AddSingleton(typeof(ILogger<>), typeof(AssemblyLogger<>));
        }
        #endregion END Public
    }
}
