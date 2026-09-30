using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Matches a native controller to pinned shared hardware without guessing attached grips.</summary>
    public static class DetectedDeviceHardwareMatcher
    {
        #region BEGIN Public
        /// <summary>Uses source-ordered shared recognition rules or an exact unique default filename.</summary>
        /// <param name="workspace">Workspace containing pinned hardware and optional recognition rules.</param>
        /// <param name="device">Selected physical instance.</param>
        /// <returns>Recognized shared hardware, or null when the user must choose.</returns>
        public static WorkspaceHardware? Match(EditorWorkspaceSnapshot workspace, DetectedDcsDevice device)
        {
            var choices = WorkspaceStructureEditor.Hardware(workspace);
            if (workspace.Sources?.TryGetValue("config/ipia-device-map.json", out var source) == true)
            {
                var map = JsonSerializer.Deserialize<SharedDeviceMap>(source);
                var rule = map?.Rules.FirstOrDefault(r => r.Patterns.Any(p => device.Name.Contains(p, StringComparison.OrdinalIgnoreCase)));
                if (rule is not null) return choices.SingleOrDefault(x => x.DeviceId == rule.DeviceKey);
            }
            var matches = choices.Where(x => DcsPhysicalIdentity.FromFileName(x.DefaultFileName) is not null &&
                DcsPhysicalIdentity.NameFromFileName(x.DefaultFileName).Equals(device.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }
        #endregion END Public
    }
}
