using System.Text.Json;
using System.Globalization;

namespace Heimdall.Application.Validation
{
    /// <summary>Bounded JSON Pointer selection shared by structural and native-range rules.</summary>
    internal static class ValidationPaths
    {
        #region BEGIN Public
        internal static bool Valid(string? path) => path is not null && path.StartsWith('/') && path.Length <= 2048
            && path.Count(character => character == '/') <= 16 && path.Split('/').Skip(1).All(ValidSegment);
        internal static List<ValidationNode> Select(JsonElement root, string path)
        {
            List<ValidationNode> nodes = [new("", root)];
            foreach (var part in path.Split('/').Skip(1))
            {
                var next = new List<ValidationNode>();
                foreach (var node in nodes) Expand(node, part, next);
                if (next.Count > 100000) throw new InvalidDataException("Validation path selects too many values.");
                nodes = next;
            }
            return nodes;
        }
        internal static string Escape(string value) => value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
        #endregion END Public
        #region BEGIN private helpers
        private static bool ValidSegment(string segment)
        {
            for (var index = 0; index < segment.Length; index++)
                if (segment[index] == '~' && (++index >= segment.Length || segment[index] is not ('0' or '1'))) return false;
            return true;
        }
        private static void Expand(ValidationNode node, string part, List<ValidationNode> output)
        {
            if (part == "*" && node.Value.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var child in node.Value.EnumerateArray()) output.Add(new(node.Path + "/" + index++, child));
            }
            else if (part == "*" && node.Value.ValueKind == JsonValueKind.Object)
                foreach (var child in node.Value.EnumerateObject()) output.Add(new(node.Path + "/" + Escape(child.Name), child.Value));
            else
            {
                var value = default(JsonElement);
                var key = part.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
                if (node.Value.ValueKind == JsonValueKind.Object) node.Value.TryGetProperty(key, out value);
                else if (node.Value.ValueKind == JsonValueKind.Array && int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                    && index < node.Value.GetArrayLength()) value = node.Value[index];
                output.Add(new(node.Path + "/" + part, value));
            }
        }
        #endregion END private helpers
    }
}
