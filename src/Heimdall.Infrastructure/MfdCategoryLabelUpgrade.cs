using System.Text.Json.Nodes;
using System.Xml.Linq;
using Heimdall.Application;

namespace Heimdall.Infrastructure
{
    /// <summary>Repairs the obsolete center category saved by older MFD editors while retaining authored Bottom labels.</summary>
    internal static class MfdCategoryLabelUpgrade
    {
        #region BEGIN Public
        /// <summary>Maps legacy category values to Bottom and removes synthetic center callouts.</summary>
        /// <param name="snapshot">Saved module sources and graphics.</param>
        /// <returns>The original snapshot when no repair is necessary, otherwise an immutable upgraded snapshot.</returns>
        public static EditorWorkspaceSnapshot Apply(EditorWorkspaceSnapshot snapshot)
        {
            var ids = snapshot.Configuration.Profiles.Where(x => x.DeviceId == "tm-mfd").Select(x => x.Id).ToHashSet();
            if (ids.Count == 0) return snapshot;
            var changed = false;
            var sources = new Dictionary<string, string>(snapshot.Sources ?? new Dictionary<string, string>(), StringComparer.Ordinal);
            if (sources.TryGetValue("config/kneeboard.json", out var source))
            {
                var root = JsonNode.Parse(source)!;
                if (root["profiles"] is JsonObject profiles && root["pages"] is JsonArray pages)
                {
                    var names = snapshot.Configuration.Profiles.Where(x => ids.Contains(x.Id)).Select(x => x.SourceName).ToHashSet(StringComparer.Ordinal);
                    var keys = profiles.Where(x => names.Contains(Path.GetFileName(x.Value!.GetValue<string>()))).Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
                    foreach (var page in pages.Where(x => x?["profile"] is JsonValue value && keys.Contains(value.GetValue<string>())))
                    {
                        var authoredBottom = HasBottom(page!["categoryLabels"] as JsonObject);
                        changed |= Repair(page["categoryLabels"] as JsonObject, false);
                        if (page["layers"] is JsonArray layers)
                            foreach (var layer in layers)
                                changed |= Repair(layer?["categoryLabels"] as JsonObject, authoredBottom);
                    }
                }
                if (changed) sources["config/kneeboard.json"] = root.ToJsonString();
            }
            var diagrams = snapshot.Diagrams.Select(diagram => {
                if (!ids.Contains(diagram.ProfileId) || !diagram.Svg.Contains("meta-mfd-category-middle", StringComparison.Ordinal)) return diagram;
                var svg = XDocument.Parse(diagram.Svg);
                foreach (var obsolete in svg.Descendants().Where(x => x.Attribute("id")?.Value == "meta-mfd-category-middle").ToArray())
                    obsolete.Remove();
                changed = true;
                return diagram with { Svg = svg.ToString(SaveOptions.DisableFormatting) };
            }).ToArray();
            return changed ? snapshot with { Sources = sources, Diagrams = diagrams } : snapshot;
        }
        #endregion END Public
        #region BEGIN private helpers
        private static bool HasBottom(JsonObject? categories) => categories is not null &&
            (categories.ContainsKey("bottom") || categories.ContainsKey("mfd-category-bottom"));
        private static bool Repair(JsonObject? categories, bool preserveAuthoredBottom)
        {
            if (categories is null || !categories.ContainsKey("middle") && !categories.ContainsKey("mfd-category-middle")) return false;
            if (!preserveAuthoredBottom && !HasBottom(categories))
                categories["bottom"] = (categories["middle"] ?? categories["mfd-category-middle"])?.DeepClone();
            categories.Remove("middle");
            categories.Remove("mfd-category-middle");
            return true;
        }
        #endregion END private helpers
    }
}
