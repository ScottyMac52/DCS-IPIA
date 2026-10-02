using System.Text.Json;
using Dcs.Ipia.Application;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Records explicit native identity review for modules created from catalog baselines.</summary>
    public static class ModulePhysicalBindingReview
    {
        private const string SourcePath = "config/ipia-physical-bindings.json";
        #region BEGIN Public
        /// <summary>Records a unit chosen or explicitly rebound by the user, without changing shared defaults.</summary>
        /// <param name="workspace">Staged module.</param>
        /// <param name="profileId">Stable profile identity.</param>
        /// <param name="physicalGuid">Reviewed physical unit.</param>
        /// <returns>Draft with portable binding evidence.</returns>
        public static EditorWorkspaceSnapshot Confirm(EditorWorkspaceSnapshot workspace, Guid profileId, Guid physicalGuid)
        {
            if (workspace.Sources?.ContainsKey("config/ipia-created-module.json") != true) return workspace;
            var confirmed = Read(workspace);
            confirmed[profileId] = physicalGuid;
            var sources = new Dictionary<string, string>(workspace.Sources, StringComparer.Ordinal)
            {
                [SourcePath] = JsonSerializer.Serialize(confirmed)
            };
            return workspace with { Sources = sources };
        }
        /// <summary>Rejects unreviewed baseline identities before native package generation.</summary>
        /// <param name="workspace">Saved or frozen module.</param>
        public static void Validate(EditorWorkspaceSnapshot workspace)
        {
            if (workspace.Sources?.ContainsKey("config/ipia-created-module.json") != true) return;
            var confirmed = Read(workspace);
            var unresolved = WorkspaceStructureEditor.Describe(workspace).Devices.Where(x =>
                DcsPhysicalIdentity.FromFileName(x.FileName) is not { } physical ||
                !confirmed.TryGetValue(x.ProfileId, out var reviewed) || reviewed != physical).Select(x => x.Title).Distinct().ToArray();
            if (unresolved.Length > 0)
                throw new InvalidDataException("Review physical bindings in Devices before building: " + string.Join("; ", unresolved) +
                    ". Select the actual unit and use Rebind physical unit; use the current filename only if it identifies your unit. Copied baseline GUIDs are not discovery evidence.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static Dictionary<Guid, Guid> Read(EditorWorkspaceSnapshot workspace) =>
            workspace.Sources?.TryGetValue(SourcePath, out var json) == true
                ? JsonSerializer.Deserialize<Dictionary<Guid, Guid>>(json) ?? [] : [];
        #endregion END private helpers
    }
}
