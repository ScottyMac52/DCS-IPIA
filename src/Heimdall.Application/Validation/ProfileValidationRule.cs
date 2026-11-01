using System.Text.Json;

namespace Heimdall.Application.Validation
{
    /// <summary>A bounded declarative rule using JSON Pointer paths with a wildcard for collections.</summary>
    public sealed record ProfileValidationRule(string Path, string Type, bool Required = true, double? Minimum = null,
        double? Maximum = null, int? MinimumLength = null, int? MaximumLength = null,
        IReadOnlyList<JsonElement>? AllowedValues = null, string? UniqueBy = null, string? References = null);
}
