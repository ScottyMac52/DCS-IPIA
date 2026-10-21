using Heimdall.Application.Interfaces;

namespace Heimdall.Application.Games
{
    /// <summary>Checks output device references against fresh native observations without guessing identical-unit ordering.</summary>
    public static class GameExportPreflight
    {
        #region BEGIN Public
        /// <summary>Returns unresolved references and all affected fields; never mutates the workspace.</summary>
        public static IReadOnlyList<GameExportIssue> Review(GameWorkspace workspace, IReadOnlyList<SimDeviceUsage> usages,
            IReadOnlyList<DetectedDcsDevice> observed, ISimDeviceReferenceMatcher? matcher)
        {
            var issues = new List<GameExportIssue>();
            foreach (var usage in usages)
            {
                var token = usage.Identity.SimSpecificIds["ed.device"];
                if (GameWorkspaceAccess.IsSystemDevice(token) || token == "{NoDevice}") continue;
                var matches = observed.Where(x => x.Connected && x.PhysicalGuid != Guid.Empty && matcher?.Matches(usage.Identity, x) == true).ToArray();
                var reason = Resolve(workspace, usages, usage, matches, matcher);
                if (reason is not null)
                    issues.Add(new(usage.DeviceId, token + (usage.Identity.SimSpecificIds.TryGetValue("ed.deviceIndex", out var index) ? " · ED index " + index : ""),
                        reason, usage.Assignments, workspace.Profile.Devices.Any(x => x.LogicalId == usage.DeviceId)));
            }
            return issues;
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string? Resolve(GameWorkspace workspace, IReadOnlyList<SimDeviceUsage> usages, SimDeviceUsage usage,
            DetectedDcsDevice[] matches, ISimDeviceReferenceMatcher? matcher)
        {
            if (matches.Length == 0) return "No connected controller matches this native reference.";
            if (matches.Select(x => x.PhysicalGuid).Distinct().Count() != matches.Length) return "Discovery returned ambiguous instance evidence. Rescan before export.";
            workspace.Hardware.TryGetValue(usage.DeviceId, out var pin);
            var selected = pin is null ? null : matches.SingleOrDefault(x => x.PhysicalGuid == pin.Device.PhysicalGuid && x.ProductGuid == pin.Device.ProductGuid);
            if (pin is not null && selected is null) return "The linked physical controller is disconnected or its product identity changed.";
            var repeated = matches.Length > 1 || usages.Any(x => x.DeviceId != usage.DeviceId && matcher?.Matches(x.Identity, matches[0]) == true);
            if (repeated && (!usage.Identity.SimSpecificIds.ContainsKey("ed.deviceIndex") || selected is null))
                return "Identical products require a captured ED index and an explicit link to a connected physical unit.";
            if (selected is not null && workspace.Hardware.Any(x => x.Key != usage.DeviceId && x.Value.Device.PhysicalGuid == selected.PhysicalGuid))
                return "More than one native reference is linked to this physical unit. Review the links.";
            return null;
        }
        #endregion END private helpers
    }
}
