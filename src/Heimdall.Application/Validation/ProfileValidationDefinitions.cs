using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Heimdall.Application.Validation
{
    /// <summary>Loads packaged and explicitly supplied rule files without consulting active game folders.</summary>
    public static class ProfileValidationDefinitions
    {
        private static readonly Lazy<IReadOnlyList<ProfileValidationRule>> Portable = new(() => ReadPortable());
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        #region BEGIN Public
        /// <summary>Reads one supported packaged contract; assembly resources are copied from versioned JSON files.</summary>
        public static ProfileValidationDefinition BuiltIn(string backendId)
        {
            if (backendId is not ("dcs" or "elite-dangerous" or "star-wars-squadrons" or "war-thunder"))
                throw new InvalidDataException("No validation definition is registered for this backend.");
            using var stream = typeof(ProfileValidationDefinitions).Assembly.GetManifestResourceStream("Heimdall.Validation." + backendId + ".v1.json")
                ?? throw new InvalidDataException("Packaged validation definition is missing.");
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }
        /// <summary>Parses an explicit file's text and freezes rule collections; unknown definition members fail closed.</summary>
        public static ProfileValidationDefinition Parse(string json) => ParseCore(json, true);
        #endregion END Public
        #region BEGIN private helpers
        private static IReadOnlyList<ProfileValidationRule> ReadPortable()
        {
            using var stream = typeof(ProfileValidationDefinitions).Assembly.GetManifestResourceStream("Heimdall.Validation.portable.v1.json")
                ?? throw new InvalidDataException("Portable validation definition is missing.");
            using var reader = new StreamReader(stream);
            return ParseCore(reader.ReadToEnd(), false).Rules;
        }
        private static ProfileValidationDefinition ParseCore(string json, bool allowPortable)
        {
            ArgumentNullException.ThrowIfNull(json);
            if (json.Length is 0 or > 4 * 1024 * 1024) throw new InvalidDataException("Validation definition must contain 1–4 MiB of JSON.");
            try
            {
                var definition = JsonSerializer.Deserialize<ProfileValidationDefinition>(json, Options)
                    ?? throw new InvalidDataException("Validation definition is missing.");
                if (definition.IncludePortable)
                {
                    if (!allowPortable) throw new InvalidDataException("Portable validation rules cannot recursively include themselves.");
                    if (definition.Rules is null) throw new InvalidDataException("Validation rules are missing.");
                    definition = definition with { Rules = Portable.Value.Concat(definition.Rules).ToArray(), IncludePortable = false };
                }
                var error = Check(definition);
                if (error is not null) throw new InvalidDataException(error);
                return definition with
                {
                    Rules = Array.AsReadOnly(definition.Rules.Select(rule => rule with
                    { AllowedValues = rule.AllowedValues is null ? null : Array.AsReadOnly(rule.AllowedValues.Select(value => value.Clone()).ToArray()) }).ToArray()),
                    Ranges = Array.AsReadOnly(definition.Ranges.ToArray()),
                    ExpectedFiles = definition.ExpectedFiles is null ? null : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(definition.ExpectedFiles, StringComparer.Ordinal))
                };
            }
            catch (JsonException error) { throw new InvalidDataException("Invalid validation definition JSON.", error); }
        }
        internal static string? Check(ProfileValidationDefinition definition)
        {
            if (definition.IncludePortable || definition.SchemaVersion != 1 || string.IsNullOrWhiteSpace(definition.BackendId) || definition.BackendVersion < 1
                || definition.ProfileSchemaVersion < 1 || definition.Rules is null || definition.Ranges is null
                || definition.Rules.Count > 256 || definition.Ranges.Count > 32) return "Unsupported validation definition identity, version or size.";
            if (definition.Rules.Any(rule => rule is null || !ValidRule(rule))) return "Invalid structural validation rule.";
            if (definition.Ranges.Any(range => range is null || !ValidationPaths.Valid(range.Path) || !ValidationPaths.Valid(range.References)
                || string.IsNullOrWhiteSpace(range.Offset) || string.IsNullOrWhiteSpace(range.Count) || string.IsNullOrWhiteSpace(range.Index) || range.MaximumEnd < 1))
                return "Invalid range validation rule.";
            if (definition.ExpectedFiles is not null && (definition.ExpectedFiles.Count > 256 || definition.ExpectedFiles.Any(file =>
                string.IsNullOrWhiteSpace(file.Key) || file.Value is null || file.Value.Length != 64 || !file.Value.All(Uri.IsHexDigit))))
                return "Expected native files require relative identifiers and SHA-256 digests.";
            return null;
        }
        private static bool ValidRule(ProfileValidationRule rule) => ValidationPaths.Valid(rule.Path)
            && rule.Type is "string" or "integer" or "number" or "boolean" or "object" or "array" or "null"
            && ValidNumericBounds(rule) && ValidLengthBounds(rule) && ValidIdentityRules(rule)
            && (rule.AllowedValues is null || rule.AllowedValues.Count <= 64 && rule.AllowedValues.All(ValidLiteral));
        private static bool ValidNumericBounds(ProfileValidationRule rule) => Finite(rule.Minimum) && Finite(rule.Maximum)
            && !(rule.Minimum > rule.Maximum)
            && (rule.Minimum is null && rule.Maximum is null || rule.Type is "number" or "integer");
        private static bool Finite(double? value) => value is null || double.IsFinite(value.Value);
        private static bool ValidLengthBounds(ProfileValidationRule rule) => !(rule.MinimumLength < 0)
            && !(rule.MaximumLength < 0) && !(rule.MaximumLength < rule.MinimumLength)
            && (rule.MinimumLength is null && rule.MaximumLength is null || rule.Type is "string" or "array" or "object");
        private static bool ValidIdentityRules(ProfileValidationRule rule) =>
            (rule.UniqueBy is null || rule.Type == "array" && !string.IsNullOrWhiteSpace(rule.UniqueBy))
            && (rule.References is null || ValidationPaths.Valid(rule.References));
        private static bool ValidLiteral(JsonElement value) => value.ValueKind == JsonValueKind.Number
            ? value.TryGetDouble(out var number) && double.IsFinite(number)
            : value.ValueKind is JsonValueKind.String or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null;
        #endregion END private helpers
    }
}
