using System.Text.Json;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Application
{
    /// <summary>Projects the current database draft into complete per-device kneeboard layers.</summary>
    public static class KneeboardDraftProjection
    {
        #region BEGIN Public
        /// <summary>Gets every current scope and used modifier layer for one selected device.</summary>
        /// <param name="snapshot">Saved module and authored controls.</param>
        /// <param name="diagram">Selected hardware profile.</param>
        /// <param name="assignments">Current staged assignments, including unsaved changes.</param>
        /// <param name="labels">Current staged displayed labels.</param>
        /// <returns>Complete pages for the device's module and supported UI Layer scopes.</returns>
        public static IReadOnlyList<KneeboardDraftPage> Create(EditorWorkspaceSnapshot snapshot, UiLayerDiagram diagram,
                                                               IEnumerable<DraftAssignment> assignments,
                                                               IReadOnlyDictionary<string, string> labels)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(diagram);
            ArgumentNullException.ThrowIfNull(assignments);
            ArgumentNullException.ThrowIfNull(labels);
            var current = DcsInputKey.CollapseAliases(assignments
                              .Where(x => x.Slot.ModuleId == snapshot.Configuration.Module.Id &&
                                          x.Slot.ProfileId == diagram.ProfileId)
                              .ToArray());
            var bySlot = current.ToDictionary(x => DcsInputKey.PhysicalSlotKey(x.Slot), x => x, StringComparer.Ordinal);
            var pages = new List<KneeboardDraftPage>();
            foreach (var scope in diagram.SupportsUiLayer ? new[] { AssignmentLayer.Module, AssignmentLayer.UiLayer }
                                                          : [AssignmentLayer.Module])
            {
                var chords = current.Where(x => x.Slot.Layer == scope)
                                 .Select(x => x.Slot.ModifierChord)
                                 .Where(x => x.Length > 0)
                                 .Distinct(StringComparer.Ordinal)
                                 .Order(StringComparer.Ordinal);
                foreach (var chord in Enumerable.Repeat("", 1).Concat(chords))
                {
                    var captions = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var control in diagram.Controls)
                    {
                        var slot = SlotForControl(snapshot, diagram, control, scope, chord);
                        if (slot is null)
                            continue;
                        captions[control.Control] = bySlot.TryGetValue(DcsInputKey.PhysicalSlotKey(slot), out var assignment)
                                                        ? labels.GetValueOrDefault(assignment.Slot.Key) ?? assignment.Command.Name
                                                        : "Unassigned";
                    }
                    pages.Add(new(scope, chord, captions));
                }
            }
            return pages;
        }
        /// <summary>Composes all current Module and UI Layer labels onto one device page with a color legend.</summary>
        /// <param name="snapshot">Saved device graphic and authored page source.</param>
        /// <param name="diagram">Selected device.</param>
        /// <param name="assignments">Current draft, including pending changes.</param>
        /// <param name="labels">Current displayed label edits.</param>
        /// <returns>A single combined page description.</returns>
        public static KneeboardComposite CreateCombined(EditorWorkspaceSnapshot snapshot, UiLayerDiagram diagram,
                                                        IEnumerable<DraftAssignment> assignments,
                                                        IReadOnlyDictionary<string, string> labels)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(diagram);
            ArgumentNullException.ThrowIfNull(assignments);
            ArgumentNullException.ThrowIfNull(labels);
            var current = DcsInputKey.CollapseAliases(assignments
                              .Where(x => x.Slot.ModuleId == snapshot.Configuration.Module.Id &&
                                          x.Slot.ProfileId == diagram.ProfileId)
                              .ToArray());
            var bySlot = current.ToDictionary(x => DcsInputKey.PhysicalSlotKey(x.Slot), x => x, StringComparer.Ordinal);
            var moduleChords =
                current.Where(x => x.Slot.Layer == AssignmentLayer.Module && x.Slot.ModifierChord.Length > 0)
                    .Select(x => x.Slot.ModifierChord)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
            var uiChords =
                current.Where(x => x.Slot.Layer == AssignmentLayer.UiLayer && x.Slot.ModifierChord.Length > 0)
                    .Select(x => x.Slot.ModifierChord)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
            var moduleColors = new[] { "#dc2626", "#ea580c", "#2563eb", "#16a34a", "#0891b2" };
            var uiColors = new[] { "#0891b2", "#7c3aed", "#be185d", "#0f766e" };
            var modifiers = ModifierDefinitions(snapshot);
            var used = new HashSet<string>(StringComparer.Ordinal);
            var variants = new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>(StringComparer.Ordinal);
            foreach (var control in diagram.Controls)
            {
                var entries = new List<KneeboardLabelVariant>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                void Add(AssignmentLayer scope, string chord, string color)
                {
                    var slot = SlotForControl(snapshot, diagram, control, scope, chord);
                    if (slot is null || !seen.Add(DcsInputKey.PhysicalSlotKey(slot)) || !bySlot.TryGetValue(DcsInputKey.PhysicalSlotKey(slot), out var assignment))
                        return;
                    var text = labels.GetValueOrDefault(assignment.Slot.Key) ?? assignment.Command.Name;
                    entries.Add(new(text, color, scope, chord));
                    used.Add(scope + "\u001f" + chord);
                }
                Add(AssignmentLayer.Module, "", "#ffffff");
                for (var i = 0; i < moduleChords.Length; i++)
                    Add(AssignmentLayer.Module, moduleChords[i], moduleColors[Math.Min(i, moduleColors.Length - 1)]);
                if (diagram.SupportsUiLayer)
                {
                    Add(AssignmentLayer.UiLayer, "", uiColors[0]);
                    for (var i = 0; i < uiChords.Length; i++)
                        Add(AssignmentLayer.UiLayer, uiChords[i], uiColors[Math.Min(i + 1, uiColors.Length - 1)]);
                }
                if (entries.Count > 0)
                    variants[control.Control] = entries;
            }
            var (title, kicker, categories, staticLabels) = ReadPageMetadata(snapshot, diagram);
            foreach (var (control, text) in staticLabels)
                if (!variants.ContainsKey(control))
                    variants[control] = [new(text, "#ffffff", AssignmentLayer.Module, "")];
            var legend = CreateLegend(snapshot, diagram, modifiers, moduleChords, uiChords, moduleColors, uiColors, used);
            AddModifierLabels(snapshot, diagram, modifiers, moduleChords, uiChords, moduleColors, uiColors, variants, legend);
            return new(title, kicker, variants, categories, legend);
        }
        /// <summary>Maps one graphic control to the exact assignment slot for a scope and chord.</summary>
        /// <param name="snapshot">Selected module configuration.</param>
        /// <param name="diagram">Authored device graphic.</param>
        /// <param name="control">Physical graphic control.</param>
        /// <param name="scope">Module or module UI Layer.</param>
        /// <param name="chord">Modifier chord; empty means Base.</param>
        /// <returns>The mapped slot, or null when the control does not appear on this layer.</returns>
        public static DraftAssignmentSlot? SlotForControl(EditorWorkspaceSnapshot snapshot, UiLayerDiagram diagram,
                                                          HardwareGraphicControl control, AssignmentLayer scope,
                                                          string chord)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(diagram);
            ArgumentNullException.ThrowIfNull(control);
            ArgumentNullException.ThrowIfNull(chord);
            if (scope == AssignmentLayer.UiLayer && !diagram.SupportsUiLayer)
                return null;
            var shifted = control.Control.EndsWith("-shifted", StringComparison.Ordinal);
            if (shifted && chord.Length == 0)
                return null;
            var hasShiftedPair = !shifted && diagram.Controls.Any(x => x.Control == control.Control + "-shifted");
            if (scope == AssignmentLayer.UiLayer && hasShiftedPair && chord.Length > 0)
                return null;
            var effectiveChord = hasShiftedPair ? "" : chord;
            var section = control.IsAxis ? BindingSection.Axis : BindingSection.Key;
            var input = DcsInputKey.Resolve(snapshot.Configuration, diagram.ProfileId, section, control.InputKey,
                                            scope == AssignmentLayer.Module ? effectiveChord : null);
            return new(snapshot.Configuration.Module.Id, diagram.ProfileId, scope, section, input, effectiveChord);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static List<KneeboardModifierLegend> CreateLegend(EditorWorkspaceSnapshot snapshot, UiLayerDiagram diagram, IReadOnlyList<WorkspaceModifier> modifiers,
            string[] moduleChords, string[] uiChords, string[] moduleColors, string[] uiColors, HashSet<string> used)
        {
            var legend = new List<KneeboardModifierLegend>();
            if (used.Any(x => x != AssignmentLayer.Module + "\u001f"))
                legend.Add(new("Base (no modifier)", "#ffffff"));
            for (var i = 0; i < moduleChords.Length; i++)
                if (used.Contains(AssignmentLayer.Module + "\u001f" + moduleChords[i]))
                    legend.Add(new(DescribeChord(snapshot, diagram, modifiers, AssignmentLayer.Module, moduleChords[i]),
                                   moduleColors[Math.Min(i, moduleColors.Length - 1)]));
            if (diagram.SupportsUiLayer && used.Contains(AssignmentLayer.UiLayer + "\u001f"))
                legend.Add(new("UI Layer — Base", uiColors[0]));
            for (var i = 0; i < uiChords.Length; i++)
                if (used.Contains(AssignmentLayer.UiLayer + "\u001f" + uiChords[i]))
                    legend.Add(new("UI Layer — " + DescribeChord(snapshot, diagram, modifiers, AssignmentLayer.UiLayer, uiChords[i]),
                                   uiColors[Math.Min(i + 1, uiColors.Length - 1)]));
            return legend;
        }
        private static void AddModifierLabels(EditorWorkspaceSnapshot snapshot, UiLayerDiagram diagram, IReadOnlyList<WorkspaceModifier> modifiers,
            string[] moduleChords, string[] uiChords, string[] moduleColors, string[] uiColors,
            Dictionary<string, IReadOnlyList<KneeboardLabelVariant>> variants, List<KneeboardModifierLegend> legend)
        {
            foreach (var modifier in modifiers.Where(x => x.ProfileId == diagram.ProfileId &&
                (x.Layer == AssignmentLayer.Module || diagram.SupportsUiLayer)))
            {
                var matching = diagram.Controls.Where(x => !x.IsAxis && !x.Control.EndsWith("-shifted", StringComparison.Ordinal) &&
                    DcsInputKey.SamePhysicalInput(x.InputKey, modifier.Key)).ToArray();
                var chords = modifier.Layer == AssignmentLayer.Module ? moduleChords : uiChords;
                var index = Array.FindIndex(chords, chord => chord.Split('+').Contains(modifier.Name, StringComparer.Ordinal));
                var color = index < 0 ? "#94a3b8" : modifier.Layer == AssignmentLayer.Module
                    ? moduleColors[Math.Min(index, moduleColors.Length - 1)]
                    : uiColors[Math.Min(index + 1, uiColors.Length - 1)];
                foreach (var control in matching)
                {
                    var existing = variants.GetValueOrDefault(control.Control) ?? [];
                    variants[control.Control] = existing.Append(new KneeboardLabelVariant(
                        (modifier.Layer == AssignmentLayer.UiLayer ? "UI: " : "") + modifier.Name + " (" + modifier.Mode + ")",
                        color, modifier.Layer, "")).ToArray();
                }
                if (index < 0 && matching.Length > 0)
                    legend.Add(new((modifier.Layer == AssignmentLayer.UiLayer ? "UI Layer — " : "") +
                        DescribeChord(snapshot, diagram, modifiers, modifier.Layer, modifier.Name), color));
            }
        }
        private static (string Title, string Kicker, Dictionary<string, string> Categories, Dictionary<string, string> StaticLabels) ReadPageMetadata(EditorWorkspaceSnapshot snapshot, UiLayerDiagram diagram)
        {
            var title = diagram.Title;
            var kicker = "CURRENT DRAFT";
            var categories = new Dictionary<string, string>(StringComparer.Ordinal);
            var staticLabels = new Dictionary<string, string>(StringComparer.Ordinal);
            if (snapshot.Sources?.TryGetValue("config/kneeboard.json", out var source) == true)
            {
                using var document = JsonDocument.Parse(source);
                var root = document.RootElement;
                var profile = snapshot.Configuration.Profiles.FirstOrDefault(x => x.Id == diagram.ProfileId);
                if (profile is not null && root.TryGetProperty("profiles", out var profiles) &&
                    profiles.ValueKind == JsonValueKind.Object && root.TryGetProperty("pages", out var pages) &&
                    pages.ValueKind == JsonValueKind.Array)
                {
                    var profileKey =
                        profiles.EnumerateObject()
                            .Where(x => Path.GetFileName(x.Value.GetString()) == profile.SourceName)
                            .Select(x => x.Name)
                            .FirstOrDefault();
                    foreach (var page in pages.EnumerateArray())
                    {
                        if (!page.TryGetProperty("profile", out var key) || key.GetString() != profileKey)
                            continue;
                        if (page.TryGetProperty("title", out var value))
                            title = value.GetString() ?? title;
                        if (page.TryGetProperty("kicker", out value))
                            kicker = value.GetString() ?? kicker;
                        ReadLabels(page, staticLabels, categories);
                        if (page.TryGetProperty("layers", out var layers) && layers.ValueKind == JsonValueKind.Array)
                            foreach (var layer in layers.EnumerateArray())
                                ReadLabels(layer, staticLabels, categories);
                        break;
                    }
                }
            }
            return (title, kicker, categories, staticLabels);
        }
        private static IReadOnlyList<WorkspaceModifier> ModifierDefinitions(EditorWorkspaceSnapshot snapshot)
        {
            if (WorkspaceManagement.Read(snapshot) is { } managed) return managed.Modifiers;
            return snapshot.Configuration.Modifiers.Select(modifier =>
            {
                var matches = snapshot.Configuration.Profiles.Where(profile =>
                    string.Equals(NativeName(profile.SourceName), modifier.Device, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(profile.DeviceId, modifier.Device, StringComparison.OrdinalIgnoreCase)).ToArray();
                return new WorkspaceModifier(AssignmentLayer.Module, matches.Length == 1 ? matches[0].Id : Guid.Empty,
                    modifier.Name, modifier.Key, modifier.Name, modifier.Mode, modifier.Device);
            }).ToArray();
        }
        private static string NativeName(string source) => source.EndsWith(".diff.lua", StringComparison.OrdinalIgnoreCase)
            ? source[..^9] : source;
        private static string DescribeChord(EditorWorkspaceSnapshot snapshot, UiLayerDiagram current,
            IReadOnlyList<WorkspaceModifier> modifiers, AssignmentLayer scope, string chord) =>
            string.Join(" + ", chord.Split('+').Select(name =>
            {
                var modifier = modifiers.SingleOrDefault(x => x.Layer == scope && x.Name == name);
                if (modifier is null) return name;
                var device = snapshot.Diagrams.SingleOrDefault(x => x.ProfileId == modifier.ProfileId);
                var control = device?.Controls.FirstOrDefault(x => !x.IsAxis && !x.Control.EndsWith("-shifted", StringComparison.Ordinal) &&
                    DcsInputKey.SamePhysicalInput(x.InputKey, modifier.Key));
                var position = control?.Label ?? modifier.Key;
                var deviceName = modifier.ProfileId == current.ProfileId ? "" : (device?.Title ?? modifier.NativeDevice) + " / ";
                return name + " — " + deviceName + position + " [" + modifier.Key + ", " + modifier.Mode + "]";
            }));
        private static void ReadLabels(JsonElement page, Dictionary<string, string> labels,
                                       Dictionary<string, string> categories)
        {
            if (page.TryGetProperty("labels", out var values) && values.ValueKind == JsonValueKind.Object)
                foreach (var item in values.EnumerateObject())
                    if (item.Value.ValueKind == JsonValueKind.String)
                        labels[item.Name] = item.Value.GetString() ?? "";
            if (page.TryGetProperty("categoryLabels", out values) && values.ValueKind == JsonValueKind.Object)
                foreach (var item in values.EnumerateObject())
                    if (item.Value.ValueKind == JsonValueKind.String)
                        categories[item.Name.StartsWith("mfd-category-", StringComparison.Ordinal)
                                       ? item.Name
                                       : "mfd-category-" + item.Name] = item.Value.GetString() ?? "";
        }
        #endregion END private helpers
    }
}

