using System.IO;
using System.Text.Json.Nodes;
using Dcs.Ipia.Application;
using DcsIpia.Presentation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Verifies real JSON-configured DI logging against an isolated physical file.</summary>
    internal static class LoggingSmoke
    {
        #region BEGIN Public
        /// <summary>Checks filtering, assembly isolation, configuration reload and sink disposal.</summary>
        public static void Verify()
        {
            var folder = Path.Combine(Path.GetTempPath(), "ipia-logging-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var settingsPath = Path.Combine(folder, "appsettings.json");
            var logPath = Path.Combine(folder, "ipia.log");
            var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json")))!;
            var overrides = settings["Serilog"]!["MinimumLevel"]!["Override"]!;
            foreach (var assembly in new[] { "App", "Application", "Dcs", "Domain", "Infrastructure", "Packaging", "Rendering" })
                if (overrides["Dcs.Ipia." + assembly]!.GetValue<string>() != "Error")
                    throw new InvalidOperationException("Every production assembly must initially log at Error.");
            var sink = settings["Serilog"]!["WriteTo"]![0]!["Args"]!;
            sink["path"] = logPath;
            sink["rollingInterval"] = "Infinite";
            File.WriteAllText(settingsPath, settings.ToJsonString());
            IConfigurationRoot? configuration = null;
            try
            {
                configuration = new ConfigurationBuilder().SetBasePath(folder)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false).Build();
                using (var services = global::DcsIpia.Presentation.Program.ConfigureServices(configuration).BuildServiceProvider())
                {
                    var appLogger = services.GetRequiredService<ILogger<global::DcsIpia.Presentation.App>>();
                    var applicationLogger = services.GetRequiredService<ILogger<AppSettings>>();
                    if (appLogger.IsEnabled(LogLevel.Warning) || applicationLogger.IsEnabled(LogLevel.Warning))
                        throw new InvalidOperationException("Initial Error filtering was not applied.");
                    appLogger.LogInformation("suppressed-info-marker");
                    using (appLogger.BeginScope(new Dictionary<string, object> { ["PhysicalGuid"] = "physical-guid-marker" }))
                        appLogger.LogError(new InvalidOperationException("exception-detail-marker"), "error-file-marker");
                    applicationLogger.LogError("application-error-marker");
                    overrides["Dcs.Ipia.App"] = "Debug";
                    File.WriteAllText(settingsPath, settings.ToJsonString());
                    configuration.Reload();
                    if (!appLogger.IsEnabled(LogLevel.Debug) || applicationLogger.IsEnabled(LogLevel.Debug))
                        throw new InvalidOperationException("Assembly-specific level changes did not remain independent.");
                    appLogger.LogDebug("app-debug-marker");
                    applicationLogger.LogDebug("suppressed-application-debug-marker");
                    overrides["Dcs.Ipia.App"] = "Error";
                    File.WriteAllText(settingsPath, settings.ToJsonString());
                    configuration.Reload();
                    appLogger.LogDebug("suppressed-after-reload-marker");
                }
                var text = File.ReadAllText(logPath);
                foreach (var marker in new[] { "error-file-marker", "exception-detail-marker", "physical-guid-marker",
                    "application-error-marker", "app-debug-marker", "Dcs.Ipia.App.DcsIpia.Presentation.App",
                    "Dcs.Ipia.Application.Dcs.Ipia.Application.AppSettings" })
                    if (!text.Contains(marker, StringComparison.Ordinal))
                        throw new InvalidOperationException("Flushed physical log is missing " + marker);
                if (text.Contains("suppressed-", StringComparison.Ordinal))
                    throw new InvalidOperationException("A filtered message reached the physical log.");
                using var exclusive = File.Open(logPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            finally
            {
                (configuration as IDisposable)?.Dispose();
                Directory.Delete(folder, true);
            }
        }
        #endregion END Public
    }
}
