using Heimdall.Application;
using Heimdall.Domain;

namespace Heimdall.Dcs
{
    /// <summary>The one native composition of a saved DCS workspace's validated draft catalog, shared by open, save-accept and discard.</summary>
    public static class DcsSavedWorkspaceDraft
    {
        #region BEGIN Public
        /// <summary>Builds the module command catalog: module-device bindings, the stored module catalog and commands already assigned on the module layer.</summary>
        /// <param name="saved">The persisted workspace.</param>
        /// <returns>The module palette used to validate module-layer assignments.</returns>
        public static IReadOnlyList<DcsCommandPaletteEntry> ModuleCatalog(DcsEditorWorkspaceSnapshot saved)
        {
            ArgumentNullException.ThrowIfNull(saved);
            var moduleProfiles = DcsWorkspaceStructureEditor.Describe(saved).Devices
                .Where(x => x.Layer == AssignmentLayer.Module).Select(x => x.ProfileId).ToHashSet();
            return DcsCommandPalette.FromBindings(saved.Configuration.Bindings.Where(x => moduleProfiles.Contains(x.ProfileId)),
                saved.ModuleCommands.Concat(saved.Assignments.Where(x => x.Slot.Layer == AssignmentLayer.Module)
                    .Select(x => new DcsCommandPaletteEntry(x.Command.Id, x.Command.Name, "Uncategorized", x.Command.IsAxis)))
                    .DistinctBy(x => x.Id).ToArray());
        }
        /// <summary>Creates the draft for a saved baseline, validating every saved assignment against the full native catalog.</summary>
        /// <param name="saved">The persisted workspace.</param>
        /// <returns>A draft whose baseline is exactly the saved assignments.</returns>
        public static DcsDraftAssignmentService Create(DcsEditorWorkspaceSnapshot saved)
        {
            ArgumentNullException.ThrowIfNull(saved);
            var commands = ModuleCatalog(saved).Select(x => new DcsDraftCommand(x.Id, x.Name, x.IsAxis, AssignmentLayer.Module))
                .Concat(saved.UiCommands.Select(x => new DcsDraftCommand(x.DcsCommandId, x.Name, x.IsAxis, AssignmentLayer.UiLayer)));
            return new(saved.Configuration, commands, saved.Assignments, DcsWorkspaceStructureEditor.UiModifierNames(saved));
        }
        #endregion END Public
    }
}
