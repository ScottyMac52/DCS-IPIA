using System.Text.Json;

namespace Heimdall.Application
{
    /// <summary>Validates and canonically serializes explicit integration instructions.</summary>
    public static class ModuleIntegrationMetadata
    {
        #region BEGIN Public
        /// <summary>Reads versioned instructions and checks an optional destination module identity.</summary>
        /// <param name="source">Captured JSON metadata.</param>
        /// <param name="moduleDcsId">Exact destination DCS folder identity.</param>
        /// <returns>Validated instruction groups in ordinal name order.</returns>
        public static IReadOnlyList<ModuleIntegrationInstruction> Parse(string source, string moduleDcsId)
        {
            ArgumentNullException.ThrowIfNull(source);
            CheckSize(source);
            try
            {
                using var json = JsonDocument.Parse(source);
                var root = json.RootElement;
                CheckFields(root, ["schemaVersion", "moduleDcsId", "integrations"]);
                if (root.GetProperty("schemaVersion").GetInt32() != 1)
                    throw new InvalidDataException("Unsupported integration metadata schema; use schemaVersion 1.");
                if (root.TryGetProperty("moduleDcsId", out var module) && module.GetString() != moduleDcsId)
                    throw new InvalidDataException("Integration instructions belong to a different DCS module.");
                var entries = root.GetProperty("integrations").EnumerateArray().Select(x => {
                    CheckFields(x, ["name", "instructions"]);
                    return new ModuleIntegrationInstruction(x.GetProperty("name").GetString()!, x.GetProperty("instructions").GetString()!);
                }).ToArray();
                Validate(entries);
                return entries.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
            {
                throw new InvalidDataException("Invalid integration metadata. Expected schemaVersion 1 and integrations with name and instructions.", ex);
            }
        }

        /// <summary>Writes deterministic LF metadata bound to the destination module.</summary>
        /// <param name="entries">Complete effective instructions.</param>
        /// <param name="moduleDcsId">Destination DCS folder identity.</param>
        /// <returns>Canonical versioned JSON.</returns>
        public static string Serialize(IEnumerable<ModuleIntegrationInstruction> entries, string moduleDcsId)
        {
            var ordered = entries.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
            Validate(ordered);
            var source = JsonSerializer.Serialize(new {
                schemaVersion = 1,
                moduleDcsId,
                integrations = ordered.Select(x => new { name = x.Name, instructions = x.Instructions })
            }) + "\n";
            CheckSize(source);
            return source;
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void CheckSize(string source)
        {
            if (source.Length > 1048576) throw new InvalidDataException("Integration metadata exceeds 1 MiB of text.");
        }
        private static void CheckFields(JsonElement value, string[] allowed)
        {
            var fields = value.EnumerateObject().Select(x => x.Name).ToArray();
            if (fields.Distinct(StringComparer.Ordinal).Count() != fields.Length || fields.Any(x => !allowed.Contains(x, StringComparer.Ordinal)))
                throw new InvalidDataException("Duplicate or unsupported integration metadata field. Only instruction metadata is supported; scripts and configuration files are not imported.");
        }
        private static void Validate(ModuleIntegrationInstruction[] entries)
        {
            if (entries.Length > 64) throw new InvalidDataException("At most 64 integration instruction groups are supported.");
            if (entries.Any(x => string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 200 || x.Name.Any(char.IsControl) ||
                string.IsNullOrWhiteSpace(x.Instructions) || x.Instructions.Length > 65536 || x.Instructions.Contains('\0')))
                throw new InvalidDataException("Each integration requires a printable name (up to 200 characters) and instructions (up to 64 KiB, without NUL).");
            if (entries.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length)
                throw new InvalidDataException("Integration names must be unique, ignoring case.");
        }
        #endregion END private helpers
    }
}
