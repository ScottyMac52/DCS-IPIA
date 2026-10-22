using System.Globalization;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;

namespace Heimdall.EliteDangerous
{
    /// <summary>Projects one controller into complete printable rows without modifying native assignments or artwork.</summary>
    public static class EliteReferenceSheetBuilder
    {
        #region BEGIN Public
        /// <summary>Includes selected activity assignments and optionally commands unassigned on this controller.</summary>
        public static ReferenceSheetContent Build(ProfileDocument profile, ICommandCatalog catalog, string deviceId,
            string controllerName, string section, bool assignedOnly)
        {
            if (section is not ("All" or "Ship" or "SRV" or "On Foot" or "General"))
                throw new ArgumentException("Choose a supported Elite section.", nameof(section));
            if (!profile.Devices.Any(x => x.LogicalId == deviceId)) throw new InvalidOperationException("Select a controller in this profile.");
            var assignments = EliteWorkspaceProjection.Rows(profile, catalog).Where(x => x.DeviceId == deviceId).ToLookup(x => x.CommandId);
            var rows = new List<ReferenceSheetRow>();
            foreach (var command in EliteCommandBrowser.Filter(catalog.Commands, section))
            {
                var bound = assignments[command.Id].ToArray();
                if (bound.Length == 0 && !assignedOnly)
                    rows.Add(new(command.Category + " · " + command.DisplayName, "Not assigned", "No assignment on this controller"));
                foreach (var row in bound)
                    rows.Add(new(command.Category + " · " + row.CommandName, Input(row, profile), Details(row, profile)));
            }
            return new(profile.Name, controllerName, section, rows);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Input(EliteAssignmentRow row, ProfileDocument profile)
        {
            var modifiers = row.Modifiers.Select(modifier =>
            {
                var identity = profile.Devices.Single(x => x.LogicalId == modifier.Source).Identity!;
                var token = identity.SimSpecificIds["ed.device"];
                var index = identity.SimSpecificIds.TryGetValue("ed.deviceIndex", out var value) ? " [" + value + "]" : "";
                return modifier.Value + " (" + token + index + ")";
            });
            return string.Join(" + ", modifiers.Append(row.Input));
        }
        private static string Details(EliteAssignmentRow row, ProfileDocument profile)
        {
            var text = row.IsAxis
                ? "Axis · " + (row.Invert ? "Inverted" : "Normal") + " · Deadzone " + row.Deadzone.ToString("0.###", CultureInfo.InvariantCulture) + "%"
                : row.Slot + " · " + row.Mode;
            if (profile.Metadata.GetValueOrDefault("ed.toggle:" + row.CommandId) == "1") text += " · Toggle";
            return text;
        }
        #endregion END private helpers
    }
}
