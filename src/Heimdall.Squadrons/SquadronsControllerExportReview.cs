using System.Globalization;
using Heimdall.Application;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>Checks duplicate native declarations and saved physical links without rewriting identities.</summary>
    public static class SquadronsControllerExportReview
    {
        #region BEGIN Public
        /// <summary>Blocks ambiguous native declarations and physical links that conflict with their native names.
        /// Matching names do not prove runtime recognition; unlinked slots remain captured evidence.</summary>
        public static IReadOnlyList<ImportDiagnostic> Review(ProfileDocument profile,
            IReadOnlyDictionary<string, string>? linkedNames)
        {
            ArgumentNullException.ThrowIfNull(profile);
            var issues = new List<ImportDiagnostic>();
            foreach (var group in profile.Devices.Where(device => !string.IsNullOrWhiteSpace(device.Identity?.WindowsName))
                .GroupBy(device => device.Identity!.WindowsName!, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
                issues.Add(new("squadrons.export.duplicate-identity",
                    "Native controller '" + group.Key + "' is declared in multiple slots: " +
                    string.Join(", ", group.Select(device => device.LogicalId)) +
                    ". Review these declarations and their assignments before exporting. No slot ownership was inferred.", true));
            if (linkedNames is null) return issues;
            foreach (var (logicalId, reportedName) in linkedNames)
            {
                var native = profile.Devices.SingleOrDefault(device => device.LogicalId == logicalId)?.Identity?.WindowsName;
                if (native is not null && Matches(native, reportedName)) continue;
                issues.Add(new("squadrons.export.identity-mismatch",
                    "Controller " + logicalId + " declares '" + (native ?? "missing") +
                    "' but its saved physical link is '" + reportedName +
                    "'. Review the link and game device name on Devices before exporting. No name or binding was changed.", true));
            }
            return issues;
        }
        #endregion END Public
        #region BEGIN private helpers
        private static bool Matches(string native, string reported)
        {
            if (string.IsNullOrWhiteSpace(reported)) return false;
            if (string.Equals(native, reported, StringComparison.OrdinalIgnoreCase)) return true;
            var prefix = reported + " ";
            return native.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(native[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var occurrence) && occurrence > 0;
        }
        #endregion END private helpers
    }
}
