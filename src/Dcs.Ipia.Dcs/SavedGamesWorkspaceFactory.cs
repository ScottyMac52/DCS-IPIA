using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Turns reviewed Saved Games profiles into an isolated editable module without aircraft-specific rules.</summary>
    public static class SavedGamesWorkspaceFactory
    {
        #region BEGIN Public
        /// <summary>Creates a module with every reviewed device, explicit assignment and imported removal.</summary>
        /// <param name="folder">Captured profiles, bindings and modifier definitions.</param>
        /// <param name="dcsId">Reviewed destination DCS input folder identifier.</param>
        /// <param name="displayName">Reviewed module display name.</param>
        /// <param name="defaults">Saved shared UI Layer revision to pin independently.</param>
        /// <param name="hardwareSources">Pinned hardware catalog and matching rules.</param>
        /// <param name="selections">One confirmed hardware template for every source profile.</param>
        /// <returns>A complete unsaved workspace; the source folder is never modified.</returns>
        public static EditorWorkspaceSnapshot Create(DcsFolderImportResult folder, string dcsId, string displayName,
            SharedUiLayerWorkspace defaults, IReadOnlyDictionary<string, string> hardwareSources,
            IReadOnlyList<SavedGamesDeviceSelection> selections)
        {
            if (!folder.Succeeded)
                throw new InvalidDataException("Resolve Saved Games import errors before creating the module: " +
                    string.Join("; ", folder.Diagnostics.Where(x => x.IsError).Select(x => x.Message)));
            if (selections.Count != folder.Profiles.Count || selections.Select(x => x.ProfileId).Distinct().Count() != selections.Count ||
                selections.Any(x => folder.Profiles.All(p => p.Id != x.ProfileId)))
                throw new InvalidDataException("Choose hardware for every imported profile exactly once.");
            var workspace = NewModuleWorkspaceFactory.Create(dcsId, displayName, defaults, hardwareSources);
            var choices = WorkspaceStructureEditor.Hardware(workspace);
            var bindings = new List<DcsBinding>();
            var assignments = new List<DraftAssignment>(workspace.Assignments);
            foreach (var imported in folder.Profiles)
            {
                var selected = selections.Single(x => x.ProfileId == imported.Id).Hardware;
                var hardware = choices.SingleOrDefault(x => x.DeviceId == selected.DeviceId && x.Diagram.Path == selected.Diagram.Path)
                    ?? throw new InvalidDataException("Choose hardware from the pinned catalog for " + imported.SourceName + ".");
                workspace = WorkspaceStructureEditor.AddDevice(workspace, hardware, AssignmentLayer.Module,
                    imported.SourceName, DcsPhysicalIdentity.FromFileName(imported.SourceName)?.ToString("D") ?? imported.SourceName, null);
                var device = WorkspaceStructureEditor.Describe(workspace).Devices.Single(x => x.Layer == AssignmentLayer.Module &&
                    x.FileName.Equals(imported.SourceName, StringComparison.OrdinalIgnoreCase));
                var original = folder.Bindings.Where(x => x.ProfileId == imported.Id).Select(x =>
                    new DcsBinding(device.ProfileId, x.Section, x.CommandId, x.CommandName, x.Disposition, x.InputKey, x.Reformers, x.Filter)).ToArray();
                bindings.AddRange(original);
                foreach (var binding in original.Where(x => x.Disposition != BindingDisposition.Removed))
                {
                    var mapped = hardware.Diagram.Controls.Any(x => x.IsAxis == (binding.Section == BindingSection.Axis) &&
                        DcsInputKey.SamePhysicalInput(x.InputKey, binding.InputKey));
                    if (!mapped && binding.Section == BindingSection.Axis)
                        throw new InvalidDataException(imported.SourceName + ": active axis " + binding.InputKey +
                            " is absent from the selected hardware. Choose a matching template before importing.");
                    if (mapped)
                        assignments.Add(new(new(workspace.Configuration.Module.Id, device.ProfileId, AssignmentLayer.Module,
                            binding.Section, binding.InputKey, string.Join("+", binding.Reformers)),
                            new(binding.CommandId, binding.CommandName, binding.Section == BindingSection.Axis, AssignmentLayer.Module))
                        { Filter = binding.Filter });
                }
            }
            var state = WorkspaceStructureEditor.Describe(workspace);
            var modifiers = folder.Modifiers.Select(x => new WorkspaceModifier(AssignmentLayer.Module,
                state.Devices.FirstOrDefault(d => d.Layer == AssignmentLayer.Module &&
                    d.FileName[..^9].Equals(x.Device, StringComparison.OrdinalIgnoreCase))?.ProfileId ?? Guid.Empty,
                x.Name, x.Key, x.Name, x.Mode, x.Device)).ToArray();
            var sources = new Dictionary<string, string>(workspace.Sources!, StringComparer.Ordinal);
            foreach (var source in folder.Sources)
                sources["src/Config/Input/" + dcsId + "/" + source.Key] = source.Value;
            sources[WorkspaceManagement.SourcePath] = JsonSerializer.Serialize(state with
            {
                Modifiers = [.. state.Modifiers.Where(x => x.Layer != AssignmentLayer.Module), .. modifiers]
            });
            sources["config/ipia-saved-games-import.json"] = JsonSerializer.Serialize(new { schemaVersion = 1, dcsId });
            var configuration = new ModuleConfiguration(workspace.Configuration.Module, workspace.Configuration.Profiles,
                folder.Modifiers, bindings, "Imported DCS Saved Games profiles");
            var commands = CommandPalette.FromBindings(bindings);
            var draft = new DraftAssignmentService(configuration,
                commands.Select(x => new DraftCommand(x.Id, x.Name, x.IsAxis, AssignmentLayer.Module))
                    .Concat(workspace.UiCommands.Select(x => new DraftCommand(x.DcsCommandId, x.Name, x.IsAxis, AssignmentLayer.UiLayer))),
                assignments, WorkspaceStructureEditor.UiModifierNames(workspace));
            var result = workspace with { Configuration = configuration, ModuleCommands = commands, Assignments = draft.Snapshot, Sources = sources };
            // Reject unsupported duplicate command/slot combinations before the caller can persist a partial import.
            foreach (var device in WorkspaceStructureEditor.Describe(result).Devices.Where(x => x.Layer == AssignmentLayer.Module))
                ModuleLuaProfileRenderer.Render(result, device.ProfileId);
            return result;
        }
        #endregion END Public
    }
}
