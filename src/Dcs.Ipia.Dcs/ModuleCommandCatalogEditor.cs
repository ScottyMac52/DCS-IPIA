using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Stages aircraft command catalog edits while preserving assignments and UI Layer ownership.</summary>
    public static class ModuleCommandCatalogEditor
    {
        #region BEGIN Public
        /// <summary>Adds or updates one aircraft command with stable identity and guarded input type.</summary>
        /// <param name="workspace">Current module draft.</param>
        /// <param name="current">Selected original entry, or null to add.</param>
        /// <param name="candidate">DCS code, name, category and input type.</param>
        /// <returns>Updated module draft with refreshed assignment names.</returns>
        public static EditorWorkspaceSnapshot Set(EditorWorkspaceSnapshot workspace, CommandPaletteEntry? current, CommandPaletteEntry candidate)
        {
            Validate(candidate);
            var entries = workspace.ModuleCommands.ToList();
            var index = CurrentIndex(entries, current);
            if (current is not null && current.Id != candidate.Id)
                throw new InvalidOperationException("Command codes are stable. Add a new command to use a different code.");
            if (entries.Where((_, i) => i != index).Any(x => x.Id == candidate.Id))
                throw new InvalidOperationException("That aircraft command code already exists.");
            if (current is not null && current.IsAxis != candidate.IsAxis && IsAssigned(workspace, current.Id))
                throw new InvalidOperationException("Clear this command's assignments before changing its input type.");
            if (index < 0)
                entries.Add(candidate);
            else
                entries[index] = candidate;
            var configuration = workspace.Configuration;
            var bindings = configuration.Bindings.Select(x => x.CommandId == candidate.Id
                ? new DcsBinding(x.ProfileId, x.Section, x.CommandId, candidate.Name, x.Disposition, x.InputKey, x.Reformers, x.Filter) : x);
            return Store(workspace with
            {
                Configuration = new(configuration.Module, configuration.Profiles, configuration.Modifiers, bindings, configuration.Provenance),
                Assignments = workspace.Assignments.Select(x => x.Slot.Layer == AssignmentLayer.Module && x.Command.Id == candidate.Id
                    ? x with { Command = x.Command with { Name = candidate.Name } } : x).ToArray()
            }, entries);
        }

        /// <summary>Removes an unused aircraft command while preserving imported history and other scopes.</summary>
        /// <param name="workspace">Current module draft.</param>
        /// <param name="current">Selected original entry.</param>
        /// <returns>Draft with the unused command removed.</returns>
        public static EditorWorkspaceSnapshot Remove(EditorWorkspaceSnapshot workspace, CommandPaletteEntry current)
        {
            var entries = workspace.ModuleCommands.ToList();
            var index = CurrentIndex(entries, current);
            if (IsAssigned(workspace, current.Id))
                throw new InvalidOperationException("Clear this command's assignments before removing it, including unmapped imported bindings.");
            entries.RemoveAt(index);
            return Store(workspace, entries);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Validate(CommandPaletteEntry entry)
        {
            if (new[] { entry.Id, entry.Name, entry.Category }.Any(x => string.IsNullOrWhiteSpace(x) || x != x.Trim() || x.Any(char.IsControl)) ||
                entry.Id.Any(char.IsWhiteSpace))
                throw new InvalidOperationException("Enter a DCS command code, name and category without surrounding whitespace or control characters. Codes cannot contain spaces.");
        }
        private static int CurrentIndex(List<CommandPaletteEntry> entries, CommandPaletteEntry? current)
        {
            if (current is null)
                return -1;
            var index = entries.FindIndex(x => x.Id == current.Id);
            if (index < 0 || entries[index] != current)
                throw new InvalidOperationException("The selected command changed. Select it again.");
            return index;
        }
        private static bool IsAssigned(EditorWorkspaceSnapshot workspace, string code) =>
            workspace.Assignments.Any(x => x.Slot.Layer == AssignmentLayer.Module && x.Command.Id == code) ||
            workspace.Configuration.Bindings.Any(x => x.CommandId == code && x.Disposition != BindingDisposition.Removed &&
                !workspace.Diagrams.Any(d => d.ProfileId == x.ProfileId &&
                    d.Controls.Any(c => c.IsAxis == (x.Section == BindingSection.Axis) && DcsInputKey.SamePhysicalInput(c.InputKey, x.InputKey))));
        private static EditorWorkspaceSnapshot Store(EditorWorkspaceSnapshot workspace, IEnumerable<CommandPaletteEntry> entries)
        {
            var commands = entries.OrderBy(x => x.Category, StringComparer.Ordinal).ThenBy(x => x.Name, StringComparer.Ordinal)
                .ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
            var sources = new Dictionary<string, string>(workspace.Sources ?? new Dictionary<string, string>(), StringComparer.Ordinal)
            {
                ["config/ipia-module-commands.json"] = JsonSerializer.Serialize(commands)
            };
            return workspace with { ModuleCommands = commands, Sources = sources };
        }
        #endregion END private helpers
    }
}
