using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application
{
    /// <summary>Builds separate searchable palettes from imported bindings and canonical UI Layer commands.</summary>
    public static class CommandPalette
    {
        #region BEGIN Public
        /// <summary>Lists imported bindings and optional DCS catalog commands with their named categories.</summary>
        /// <param name="bindings">The selected module's imported bindings.</param>
        /// <param name="catalog">Assignable commands from a validated module catalog, when available.</param>
        /// <returns>Distinct commands ordered by name, category, and source ID.</returns>
        public static IReadOnlyList<CommandPaletteEntry> FromBindings(IEnumerable<DcsBinding> bindings,
                                                                      IEnumerable<CommandPaletteEntry>? catalog = null)
        {
            ArgumentNullException.ThrowIfNull(bindings);
            var known = (catalog ?? []).ToDictionary(x => x.Id, StringComparer.Ordinal);
            foreach (var binding in bindings)
            {
                if (known.TryGetValue(binding.CommandId, out var command) &&
                    command.IsAxis != (binding.Section == BindingSection.Axis))
                    throw new ArgumentException($"Catalog type conflicts with imported binding {binding.CommandId}.",
                                                nameof(catalog));
                known.TryAdd(binding.CommandId, new(binding.CommandId, binding.CommandName, "Uncategorized",
                                                    binding.Section == BindingSection.Axis));
            }
            return Sort(known.Values);
        }
        /// <summary>Lists all commands in the validated canonical UI Layer catalog.</summary>
        /// <param name="commands">DCS-Common command definitions from the selected source.</param>
        /// <returns>Distinct UI Layer commands ordered by name, category, and source ID.</returns>
        public static IReadOnlyList<CommandPaletteEntry> FromUiLayer(IEnumerable<DcsCommandDefinition> commands)
        {
            ArgumentNullException.ThrowIfNull(commands);
            return Sort(commands.Select(x => new CommandPaletteEntry(x.DcsCommandId, x.Name, x.Category, x.IsAxis))
                            .Distinct()
                            .ToArray());
        }
        /// <summary>Searches only the supplied palette by name, ID, or category.</summary>
        /// <param name="entries">Entries from one palette.</param>
        /// <param name="query">Optional case-insensitive search text.</param>
        /// <returns>Matching entries in their original order.</returns>
        public static IReadOnlyList<CommandPaletteEntry> Search(IEnumerable<CommandPaletteEntry> entries, string? query)
        {
            ArgumentNullException.ThrowIfNull(entries);
            return Filter(entries, query, null);
        }
        /// <summary>Filters one scope by exact category and optional name, ID, or category text.</summary>
        /// <param name="entries">Entries from one command scope.</param>
        /// <param name="query">Case-insensitive search text.</param>
        /// <param name="category">Exact category, or null for all categories.</param>
        /// <returns>Matching commands in original palette order.</returns>
        public static IReadOnlyList<CommandPaletteEntry> Filter(IEnumerable<CommandPaletteEntry> entries, string? query,
                                                                string? category)
        {
            ArgumentNullException.ThrowIfNull(entries);
            var term = query?.Trim();
            return entries
                .Where(x => (category is null || string.Equals(x.Category, category, StringComparison.Ordinal)) &&
                            (string.IsNullOrEmpty(term) || x.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                             x.Id.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                             x.Category.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }
        #endregion END Public
        #region BEGIN private helpers
        private static CommandPaletteEntry[] Sort(IEnumerable<CommandPaletteEntry> entries) =>
            entries.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Id, StringComparer.Ordinal)
                .ToArray();
        #endregion END private helpers
    }
}
