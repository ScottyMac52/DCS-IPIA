using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using Heimdall.Application;
using Heimdall.Application.Interfaces;

namespace Heimdall.App
{
    /// <summary>Runs acquisition-free input discovery before WPF, edition policy, profiles or databases are opened.</summary>
    internal static class ControllerEvidenceCommand
    {
        #region BEGIN Public
        internal static int? RunIfRequested(string[] args, IControllerCapabilityDiscovery? discovery = null)
        {
            if (args.Length == 0 || args[0] != "--controller-evidence") return null;
            if (args.Length != 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                Console.Error.WriteLine("Usage: Heimdall --controller-evidence <new-output.json>");
                return 2;
            }
            try
            {
                var observations = (discovery ?? new WindowsConnectedDeviceDiscovery()).DiscoverAsync().GetAwaiter().GetResult();
                var json = ControllerEvidenceReport.Create(observations, DateTimeOffset.UtcNow);
                WriteNew(args[1], json);
                return 0;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException
                or ExternalException or DllNotFoundException or BadImageFormatException or NotSupportedException)
            {
                Console.Error.WriteLine("Controller evidence export failed: " + error.Message);
                return 1;
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void WriteNew(string destination, string json)
        {
            var path = Path.GetFullPath(destination);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                File.Move(temporary, path, overwrite: false);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        #endregion END private helpers
    }
}
