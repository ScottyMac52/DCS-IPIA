using System.Globalization;
using System.Text.Json;

namespace Heimdall.Application.Validation
{
    /// <summary>Evaluates declarative structural rules without simulator names, file I/O or command-specific branches.</summary>
    internal static class ProfileRuleValidator
    {
        #region BEGIN Public
        internal static void Validate(JsonElement root, ProfileValidationRule rule, List<ProfileValidationIssue> issues)
        {
            foreach (var node in ValidationPaths.Select(root, rule.Path))
            {
                if (node.Value.ValueKind == JsonValueKind.Undefined)
                {
                    if (rule.Required) issues.Add(Issue("required", node, rule.Type, "Required value is missing."));
                    continue;
                }
                if (!MatchesType(node.Value, rule.Type)) { issues.Add(Issue("type", node, rule.Type, "Value has the wrong type.")); continue; }
                Bounds(node, rule, issues);
                if (rule.AllowedValues is not null && !rule.AllowedValues.Any(value => ValidationValues.Key(value) == ValidationValues.Key(node.Value)))
                    issues.Add(Issue("allowed", node, string.Join(", ", rule.AllowedValues.Select(ValidationValues.Display)), "Value is not in the allowed set."));
                if (rule.UniqueBy is not null) Unique(node, rule.UniqueBy, issues);
                if (rule.References is not null)
                {
                    var references = ValidationPaths.Select(root, rule.References);
                    if (!references.Any(reference => reference.Value.ValueKind != JsonValueKind.Undefined && ValidationValues.Key(reference.Value) == ValidationValues.Key(node.Value)))
                        issues.Add(Issue("reference", node, rule.References, "Value does not reference a declared item."));
                }
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static bool MatchesType(JsonElement value, string type) => type switch
        {
            "string" => value.ValueKind == JsonValueKind.String,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            "number" => value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number),
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "null" => value.ValueKind == JsonValueKind.Null,
            _ => false
        };
        private static void Bounds(ValidationNode node, ProfileValidationRule rule, List<ProfileValidationIssue> issues)
        {
            if (node.Value.ValueKind == JsonValueKind.Number)
            {
                var number = node.Value.GetDouble();
                if (rule.Minimum is { } minimum && number < minimum) issues.Add(Issue("minimum", node, minimum.ToString(CultureInfo.InvariantCulture), "Value is below the minimum."));
                if (rule.Maximum is { } maximum && number > maximum) issues.Add(Issue("maximum", node, maximum.ToString(CultureInfo.InvariantCulture), "Value exceeds the maximum."));
            }
            var length = node.Value.ValueKind switch
            {
                JsonValueKind.String => node.Value.GetString()!.Length,
                JsonValueKind.Array => node.Value.GetArrayLength(),
                JsonValueKind.Object => node.Value.EnumerateObject().Count(),
                _ => -1
            };
            if (length < 0) return;
            if (rule.MinimumLength is { } minimumLength && length < minimumLength) issues.Add(Issue("length.minimum", node, minimumLength.ToString(CultureInfo.InvariantCulture), "Value is too short."));
            if (rule.MaximumLength is { } maximumLength && length > maximumLength) issues.Add(Issue("length.maximum", node, maximumLength.ToString(CultureInfo.InvariantCulture), "Value is too long."));
        }
        private static void Unique(ValidationNode node, string property, List<ProfileValidationIssue> issues)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var index = 0;
            foreach (var item in node.Value.EnumerateArray())
            {
                var value = item.ValueKind == JsonValueKind.Object && item.TryGetProperty(property, out var found) ? found : default;
                if (value.ValueKind == JsonValueKind.Undefined || !keys.Add(ValidationValues.Key(value)))
                    issues.Add(new("rule.unique", node.Path + "/" + index + "/" + ValidationPaths.Escape(property), "unique " + property,
                        ValidationValues.Display(value), "Item identity is missing or duplicated."));
                index++;
            }
        }
        private static ProfileValidationIssue Issue(string code, ValidationNode node, string expected, string message) =>
            new("rule." + code, node.Path, expected, ValidationValues.Display(node.Value), message);
        #endregion END private helpers
    }
}
