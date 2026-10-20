using System.Text.Json;

namespace Heimdall.Application
{
    /// <summary>Reads the versioned DCS command catalog produced by DCS-Common scaffolding.</summary>
    public static class DcsCommandCatalogImporter
    {
        #region BEGIN Public
        /// <summary>Parses assignable commands with their authored category paths and key/axis types.</summary>
        /// <param name="json">Contents of a DCS-Common command catalog JSON file.</param>
        /// <param name="moduleId">Expected DCS module identifier, such as UH-1H.</param>
        /// <returns>Distinct module commands with named categories.</returns>
        public static IReadOnlyList<CommandPaletteEntry> Parse(string json, string moduleId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.GetProperty("schemaVersion").GetInt32() != 1)
                    throw new FormatException("Expected DCS command catalog schemaVersion 1.");
                var actual = root.GetProperty("moduleId").GetString();
                if (!string.Equals(actual, moduleId, StringComparison.OrdinalIgnoreCase))
                    throw new FormatException($"Catalog module '{actual}' does not match '{moduleId}'.");
                var commands = root.GetProperty("commands");
                if (commands.ValueKind != JsonValueKind.Array)
                    throw new FormatException("Catalog commands must be an array.");
                var entries = new List<CommandPaletteEntry>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var row in commands.EnumerateArray())
                {
                    var id = row.GetProperty("bindingKey").GetString()?.Trim();
                    var name = row.GetProperty("name").GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                        throw new FormatException("Every catalog command needs a bindingKey and name.");
                    if (!seen.Add(id))
                        throw new FormatException($"Duplicate catalog bindingKey '{id}'.");
                    var type = row.GetProperty("type").GetString()?.Trim().ToLowerInvariant();
                    if (type is not("axis" or "button" or "switch" or "button/switch"))
                        throw new FormatException($"Unsupported command type '{type}' for '{id}'.");
                    var categoryPath = row.GetProperty("categoryPath");
                    if (categoryPath.ValueKind != JsonValueKind.Array)
                        throw new FormatException($"categoryPath must be an array for '{id}'.");
                    var names = categoryPath.EnumerateArray()
                                    .Select(x => x.GetString()?.Trim())
                                    .Where(x => !string.IsNullOrWhiteSpace(x))
                                    .ToArray();
                    var category = names.Length == 0 ? "Uncategorized" : string.Join(" / ", names);
                    if (row.TryGetProperty("isAssignable", out var available) &&
                        available.ValueKind == JsonValueKind.False)
                        continue;
                    entries.Add(new(id, name, category, type == "axis"));
                }
                return entries.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.Id, StringComparer.Ordinal)
                    .ToArray();
            }
            catch (JsonException ex)
            {
                throw new FormatException("DCS command catalog is not valid JSON.", ex);
            }
            catch (KeyNotFoundException ex)
            {
                throw new FormatException("DCS command catalog is missing a required field.", ex);
            }
            catch (InvalidOperationException ex)
            {
                throw new FormatException("DCS command catalog has an invalid field type.", ex);
            }
        }
        #endregion END Public
    }
}
