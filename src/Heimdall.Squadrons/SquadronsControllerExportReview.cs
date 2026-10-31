using System.Globalization;
using Heimdall.Application;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>Checks explicitly saved physical links against native names without rewriting either identity.</summary>
    public static class SquadronsControllerExportReview
    {
        #region BEGIN Public
        /// <summary>Blocks review copies whose linked physical controller conflicts with the native declaration.
        /// Matching names do not prove runtime recognition; unlinked slots remain captured evidence.</summary>
        public static IReadOnlyList<ImportDiagnostic> Review(ProfileDocument profile,
            IReadOnlyDictionary<string, string>? linkedNames)
        {
            ArgumentNullException.ThrowIfNull(profile);
            var issues = new List<ImportDiagnostic>();
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
