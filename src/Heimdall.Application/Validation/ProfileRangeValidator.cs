using System.Globalization;
using System.Text.Json;

namespace Heimdall.Application.Validation
{
    /// <summary>Checks half-open ranges and global references independently of native encoders.</summary>
    internal static class ProfileRangeValidator
    {
        #region BEGIN Public
        internal static void Validate(JsonElement root, ProfileValidationRange rule, List<ProfileValidationIssue> issues)
        {
            var ranges = new List<(long Start, long End)>();
            foreach (var collection in ValidationPaths.Select(root, rule.Path))
            {
                if (collection.Value.ValueKind != JsonValueKind.Array)
                {
                    issues.Add(new("range.collection", collection.Path, "array", ValidationValues.Display(collection.Value), "Range declarations are missing or invalid."));
                    continue;
                }
                var index = 0;
                foreach (var item in collection.Value.EnumerateArray())
                {
                    var path = collection.Path + "/" + index++;
                    if (!Number(item, rule.Offset, out var start) || !Number(item, rule.Count, out var count)
                        || start < 0 || count < 0 || count > rule.MaximumEnd || start > rule.MaximumEnd - count)
                    { issues.Add(new("range.bounds", path, "nonnegative bounded offset/count", ValidationValues.Display(item), "Range bounds are invalid or overflow the native index space.")); continue; }
                    var end = start + count;
                    if (count > 0 && ranges.Any(range => range.Start < range.End && start < range.End && range.Start < end))
                        issues.Add(new("range.overlap", path, "disjoint half-open ranges", start + ".." + end, "Native ranges overlap."));
                    ranges.Add((start, end));
                }
            }
            foreach (var node in ValidationPaths.Select(root, rule.References))
            {
                if (!Number(node.Value, rule.Index, out var index))
                { issues.Add(new("range.reference.type", node.Path, "integer " + rule.Index, ValidationValues.Display(node.Value), "Native input reference is missing or invalid.")); continue; }
                if (rule.UnboundIndex is { } sentinel && index == sentinel) continue;
                var owners = ranges.Count(range => index >= range.Start && index < range.End);
                if (owners != 1) issues.Add(new("range.reference", node.Path + "/" + ValidationPaths.Escape(rule.Index), "exactly one range owner",
                    index.ToString(CultureInfo.InvariantCulture) + " (" + owners + " owners)", "Native input does not have a unique declared owner."));
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static bool Number(JsonElement item, string property, out long value)
        {
            value = 0;
            return item.ValueKind == JsonValueKind.Object && item.TryGetProperty(property, out var scalar)
                && scalar.ValueKind == JsonValueKind.Number && scalar.TryGetInt64(out value);
        }
        #endregion END private helpers
    }
}
