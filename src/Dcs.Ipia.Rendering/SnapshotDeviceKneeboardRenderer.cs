using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Rendering
{
    /// <summary>Generates every authored device kneeboard from one saved workspace.</summary>
    public static class SnapshotDeviceKneeboardRenderer
    {
        #region BEGIN Public
        /// <summary>Projects one combined PNG per profile without using imported PNGs or a repository checkout.</summary>
        /// <param name="workspace">Frozen, validated editor workspace.</param>
        /// <returns>Package-relative device PNG paths and bytes in ordinal path order.</returns>
        public static IReadOnlyDictionary<string, byte[]> Generate(EditorWorkspaceSnapshot workspace)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            var configuration = workspace.Configuration;
            if (workspace.Sources is null || !workspace.Sources.TryGetValue("config/kneeboard.json", out var source))
                throw new InvalidDataException("A pinned authored kneeboard manifest is required.");
            using var document = JsonDocument.Parse(source);
            var root = document.RootElement;
            if (!root.TryGetProperty("profiles", out var profiles) || profiles.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("The authored kneeboard profile and page mapping is incomplete.");
            var results = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            var usedPages = new HashSet<string>(StringComparer.Ordinal);
            foreach (var profile in configuration.Profiles)
            {
                var names = profiles.EnumerateObject()
                                .Where(x => string.Equals(Path.GetFileName(x.Value.GetString()), profile.SourceName,
                                                          StringComparison.Ordinal))
                                .ToArray();
                if (names.Length != 1)
                    throw new InvalidDataException("Expected one authored page profile for " + profile.SourceName);
                var pageRows =
                    pages.EnumerateArray()
                        .Where(x => x.TryGetProperty("profile", out var value) && value.GetString() == names[0].Name)
                        .ToArray();
                if (pageRows.Length != 1 || !pageRows[0].TryGetProperty("file", out var file))
                    throw new InvalidDataException("Expected one combined device page for " + profile.SourceName);
                var key = file.GetString() ?? "";
                if (key.Length == 0 || key is "." or ".." ||
                    !key.All(x => char.IsAsciiLetterOrDigit(x) || x is '-' or '_'))
                    throw new InvalidDataException("Unsafe device page name: " + key);
                var authored = workspace.Pages
                                   .Where(x => x.Kind == KneeboardPageKind.Device && x.StableKey == key &&
                                               x.ModuleId == configuration.Module.Id)
                                   .ToArray();
                if (authored.Length != 1 || !usedPages.Add(key))
                    throw new InvalidDataException("Device page is missing or ambiguously mapped: " + key);
                var diagram = workspace.Diagrams.SingleOrDefault(x => x.ProfileId == profile.Id &&
                                                                      x.Controls.Count > 0 && x.Svg.Length > 0) ??
                              throw new InvalidDataException("No mapped authored SVG for " + profile.SourceName);
                var assigned = DcsInputKey.CollapseAliases(workspace.Assignments.Where(x => x.Slot.ProfileId == profile.Id)).ToArray();
                if (assigned.Select(x => DcsInputKey.PhysicalSlotKey(x.Slot)).Distinct(StringComparer.Ordinal).Count() != assigned.Length)
                    throw new InvalidDataException("Duplicate assignment slot for " + profile.SourceName);
                foreach (var assignment in assigned)
                {
                    if (assignment.Slot.ModuleId != configuration.Module.Id ||
                        assignment.Slot.Layer == AssignmentLayer.UiLayer && !diagram.SupportsUiLayer ||
                        !diagram.Controls.Any(control => KneeboardDraftProjection
                                                             .SlotForControl(workspace, diagram, control,
                                                                             assignment.Slot.Layer,
                                                                             assignment.Slot.ModifierChord)
                                                             is { } slot && DcsInputKey.PhysicalSlotKey(slot) == DcsInputKey.PhysicalSlotKey(assignment.Slot)))
                        throw new InvalidDataException("Assignment lacks a usable authored control: " +
                                                       assignment.Slot.Key);
                }
                var combined = KneeboardDraftProjection.CreateCombined(workspace, diagram, assigned, workspace.Labels);
                results.Add($"KNEEBOARD/{configuration.Module.DcsId}/{key}.png",
                            CompositeKneeboardRenderer.Render(diagram, combined, ""));
            }
            if (usedPages.Count != workspace.Pages.Count(x => x.Kind == KneeboardPageKind.Device))
                throw new InvalidDataException("An authored device page has no matching profile or diagram.");
            return results;
        }
        #endregion END Public
    }
}

