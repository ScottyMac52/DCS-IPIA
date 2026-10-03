using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Captures command-specific DCS default-axis removals in a module's frozen sources.</summary>
    public static class ModuleAxisDefaults
    {
        #region BEGIN Public
        /// <summary>Reviews a DCS profile saved after clearing all axes, without changing the draft.</summary>
        /// <param name="workspace">Selected module draft.</param>
        /// <param name="profileId">Device receiving the captured defaults.</param>
        /// <param name="layer">Aircraft or module-owned UI Layer scope.</param>
        /// <param name="source">DCS-generated cleared-axis diff.lua text.</param>
        /// <returns>Exact default command, axis and chord removals.</returns>
        public static IReadOnlyList<DcsBinding> Review(EditorWorkspaceSnapshot workspace, Guid profileId,
            AssignmentLayer layer, string source)
        {
            if (!WorkspaceStructureEditor.Describe(workspace).Devices.Any(x => x.ProfileId == profileId && x.Layer == layer))
                throw new InvalidDataException("Select a device in this module and scope.");
            return Parse(source, profileId);
        }

        /// <summary>Stages reviewed axis defaults while preserving current assignments and other devices.</summary>
        /// <param name="workspace">Selected module draft.</param>
        /// <param name="profileId">Selected device identity.</param>
        /// <param name="layer">Selected assignment scope.</param>
        /// <param name="source">Reviewed cleared-axis source captured in memory.</param>
        /// <returns>Reversible draft containing the source evidence.</returns>
        public static EditorWorkspaceSnapshot Import(EditorWorkspaceSnapshot workspace, Guid profileId,
            AssignmentLayer layer, string source)
        {
            Review(workspace, profileId, layer, source);
            var sources = new Dictionary<string, string>(workspace.Sources ?? new Dictionary<string, string>(), StringComparer.Ordinal)
            {
                [PathFor(profileId, layer)] = source
            };
            return workspace with { Sources = sources };
        }

        /// <summary>Applies captured removals except exact command, axis and chord combinations still assigned.</summary>
        /// <param name="workspace">Frozen module state.</param>
        /// <param name="profileId">Rendered device.</param>
        /// <param name="layer">Rendered assignment scope.</param>
        /// <param name="bindings">Projected assignments and imported removal history.</param>
        /// <returns>Deduplicated bindings with unwanted defaults suppressed.</returns>
        public static IReadOnlyList<DcsBinding> Apply(EditorWorkspaceSnapshot workspace, Guid profileId,
            AssignmentLayer layer, IReadOnlyList<DcsBinding> bindings)
        {
            if (workspace.Sources is null || !workspace.Sources.TryGetValue(PathFor(profileId, layer), out var source))
                return bindings;
            var active = bindings.Where(x => x.Section == BindingSection.Axis && x.Disposition != BindingDisposition.Removed)
                .Select(Identity).ToHashSet(StringComparer.Ordinal);
            return bindings.Concat(Parse(source, profileId).Where(x => !active.Contains(Identity(x))))
                .DistinctBy(x => x.Disposition + "\u001f" + Identity(x)).ToArray();
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string PathFor(Guid profileId, AssignmentLayer layer) =>
            "config/ipia-axis-defaults/" + layer + "/" + profileId.ToString("N") + ".diff.lua";
        private static string Identity(DcsBinding binding) => binding.Section + "\u001f" + binding.CommandId + "\u001f" +
            binding.InputKey + "\u001f" + string.Join("+", binding.Reformers);
        private static DcsBinding[] Parse(string source, Guid profileId)
        {
            var axes = DcsLuaParser.ParseProfile(source, profileId).Where(x => x.Section == BindingSection.Axis).ToArray();
            if (axes.Length == 0 || axes.Any(x => x.Disposition != BindingDisposition.Removed))
                throw new InvalidDataException("Reset this device's axes to defaults in DCS, clear all axes, then save its profile. The file must contain axis removals and no active axes.");
            if (axes.Select(Identity).Distinct(StringComparer.Ordinal).Count() != axes.Length)
                throw new InvalidDataException("The cleared-axis profile contains duplicate removals.");
            // Verify that all captured identifiers can be represented by the deterministic Lua writer.
            ModuleLuaProfileRenderer.Write(axes);
            return axes;
        }
        #endregion END private helpers
    }
}
