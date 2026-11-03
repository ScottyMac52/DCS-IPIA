using System.IO;
using System.Text.Json;
using Heimdall.Application;
using Heimdall.App;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks the headless evidence command with injected discovery, without acquiring real devices or loading profiles.</summary>
    internal static class ControllerEvidenceSmoke
    {
        #region BEGIN Public
        internal static void Verify()
        {
            var root = Path.Combine(Path.GetTempPath(), "heimdall-native-evidence-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var device = new DetectedDcsDevice(Guid.NewGuid(), "Evidence fixture", true);
                var discovery = new GameSmokeDiscovery(device);
                discovery.Inputs[device.PhysicalGuid] = ControllerCapabilityMapper.Map([
                    new(Guid.NewGuid(), 2, "Unknown axis") { Details = new(24, 0x100, UsagePage: 1, Usage: 54) }]);
                var path = Path.Combine(root, "evidence.json");
                if (ControllerEvidenceCommand.RunIfRequested([], discovery) is not null
                    || ControllerEvidenceCommand.RunIfRequested(["--ordinary-startup"], discovery) is not null)
                    throw new InvalidOperationException("Evidence mode intercepted ordinary startup arguments.");
                if (ControllerEvidenceCommand.RunIfRequested(["--controller-evidence"], discovery) != 2
                    || ControllerEvidenceCommand.RunIfRequested(["--controller-evidence", " "], discovery) != 2)
                    throw new InvalidOperationException("Invalid evidence arguments were accepted.");
                if (ControllerEvidenceCommand.RunIfRequested(["--controller-evidence", path], discovery) != 0)
                    throw new InvalidOperationException("Headless controller evidence was not exported.");
                var original = File.ReadAllText(path);
                using var report = JsonDocument.Parse(original);
                var catalog = report.RootElement.GetProperty("devices")[0].GetProperty("reportedInputs");
                if (catalog.GetProperty("objects").GetArrayLength() != 1 || !catalog.GetProperty("hasNativeDetails").GetBoolean())
                    throw new InvalidOperationException("Evidence export lost an axis rejected by the compatibility mapper.");
                if (ControllerEvidenceCommand.RunIfRequested(["--controller-evidence", path], discovery) != 1 || File.ReadAllText(path) != original)
                    throw new InvalidOperationException("Evidence export overwrote a prior capture.");
                discovery.InputScan = () => throw new InvalidOperationException("Synthetic discovery failure");
                var failed = Path.Combine(root, "failed.json");
                if (ControllerEvidenceCommand.RunIfRequested(["--controller-evidence", failed], discovery) != 1 || File.Exists(failed))
                    throw new InvalidOperationException("Failed discovery left a misleading evidence file.");
                if (Directory.GetFiles(root, "*.tmp").Length != 0) throw new InvalidOperationException("Evidence export leaked a temporary file.");
            }
            finally { Directory.Delete(root, true); }
        }
        #endregion END Public
    }
}
