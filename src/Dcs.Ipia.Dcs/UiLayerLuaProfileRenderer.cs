using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Projects one module-owned UI Layer profile from pinned Lua and saved editor assignments.</summary>
    public static class UiLayerLuaProfileRenderer
    {
        private const string SharedRoot = "assets/shared/ui-layer/input/UiLayer/";
        #region BEGIN Public
        /// <summary>Renders an edited UI Layer diff.lua without reading a mutable checkout.</summary>
        /// <param name="workspace">Frozen saved workspace with canonical Lua and authored diagram.</param>
        /// <param name="profileId">Module device instance to render.</param>
        /// <returns>Deterministic Lua text for the resolved canonical profile.</returns>
        public static string Render(EditorWorkspaceSnapshot workspace, Guid profileId)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            var config = workspace.Configuration;
            if (config.Profiles.All(x => x.Id != profileId))
                throw new ArgumentException("Profile is outside this module.", nameof(profileId));
            var diagram = workspace.Diagrams.SingleOrDefault(x => x.ProfileId == profileId) ??
                          throw new InvalidDataException("UI Layer device has no authored hardware diagram.");
            var path = diagram.CanonicalUiLayerSourcePath;
            if (!diagram.SupportsUiLayer || path is null || !path.StartsWith(SharedRoot, StringComparison.Ordinal) ||
                !path.EndsWith(".diff.lua", StringComparison.Ordinal) || path.Contains("..", StringComparison.Ordinal) ||
                !path.Contains("/joystick/", StringComparison.Ordinal) &&
                !path.Contains("/keyboard/", StringComparison.Ordinal))
                throw new InvalidDataException("No unambiguous pinned UI Layer source for this device.");
            if (workspace.Sources is null || !workspace.Sources.TryGetValue(path, out var source) ||
                !workspace.Sources.TryGetValue(SharedRoot + "modifiers.lua", out var modifierSource))
                throw new InvalidDataException("The saved workspace lacks canonical UI Layer Lua; re-import it.");
            var known = DcsLuaParser.ParseModifiers(modifierSource).Select(x => x.Name)
                                    .ToHashSet(StringComparer.Ordinal);
            var removed = RemovedModifiers(workspace.Sources);
            if (removed.Any(x => !known.Contains(x)))
                throw new InvalidDataException("An overlay removes an unknown canonical UI Layer modifier.");
            var valid = known.Except(removed, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
            var original = DcsLuaParser.ParseProfile(source, profileId);
            var active = original.Where(x => x.Disposition != BindingDisposition.Removed).ToArray();
            if (active.GroupBy(x => Slot(x.Section, x.InputKey, string.Join("+", x.Reformers)),
                               StringComparer.Ordinal).Any(x => x.Count() > 1))
                throw new InvalidDataException("Multiple canonical UI Layer commands occupy one input slot.");
            var baseline = new HashSet<string>(StringComparer.Ordinal);
            foreach (var callout in diagram.Callouts)
            {
                var control = diagram.Controls.SingleOrDefault(x => x.Control == callout.Control) ??
                              throw new InvalidDataException("UI Layer callout has no mapped hardware control.");
                var section = control.IsAxis ? BindingSection.Axis : BindingSection.Key;
                var chord = Chord(callout.Modifier.Replace(" + ", "+", StringComparison.Ordinal));
                if (chord.Split('+', StringSplitOptions.RemoveEmptyEntries).Any(x => !valid.Contains(x)))
                    throw new InvalidDataException("Imported UI Layer callout uses an undefined modifier.");
                var key = DcsInputKey.Resolve(config, profileId, section, control.InputKey);
                if (!baseline.Add(Slot(section, key, chord)))
                    throw new InvalidDataException("Two imported UI Layer callouts occupy one input slot.");
            }
            var saved = new Dictionary<string, DraftAssignment>(StringComparer.Ordinal);
            foreach (var assignment in workspace.Assignments.Where(x => x.Slot.ProfileId == profileId &&
                                                                          x.Slot.Layer == AssignmentLayer.UiLayer))
            {
                var slot = assignment.Slot;
                if (slot.ModuleId != config.Module.Id || assignment.Command.Layer != AssignmentLayer.UiLayer ||
                    (slot.Section == BindingSection.Axis) != assignment.Command.IsAxis ||
                    !diagram.Controls.Any(x => x.IsAxis == assignment.Command.IsAxis &&
                                               DcsInputKey.SamePhysicalInput(x.InputKey, slot.InputKey)))
                    throw new InvalidDataException("Saved UI Layer assignment is outside its mapped device or type.");
                var chord = Chord(slot.ModifierChord);
                if (chord.Split('+', StringSplitOptions.RemoveEmptyEntries).Any(x => !valid.Contains(x)))
                    throw new InvalidDataException("Saved UI Layer assignment uses an undefined modifier.");
                if (!saved.TryAdd(Slot(slot.Section, slot.InputKey, chord), assignment))
                    throw new InvalidDataException("Two saved UI Layer commands occupy one input slot.");
            }
            var result = new List<DcsBinding>(original.Where(x => x.Disposition == BindingDisposition.Removed));
            var handled = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in active)
            {
                var key = Slot(binding.Section, binding.InputKey, string.Join("+", binding.Reformers));
                if (binding.Reformers.Any(x => !known.Contains(x)))
                    throw new InvalidDataException("Canonical UI Layer binding uses an undefined modifier.");
                if (binding.Reformers.Any(removed.Contains))
                    continue;
                handled.Add(key);
                if (saved.TryGetValue(key, out var edit))
                    result.Add(edit.Command.Id == binding.CommandId &&
                               SameFilter(edit.Filter, binding.Filter) ? binding : FromAssignment(profileId, edit));
                else if (!baseline.Contains(key))
                    result.Add(binding);
            }
            foreach (var (key, edit) in saved)
                if (!handled.Contains(key))
                    result.Add(FromAssignment(profileId, edit));
            return ModuleLuaProfileRenderer.Write(result);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static HashSet<string> RemovedModifiers(IReadOnlyDictionary<string, string> sources)
        {
            if (!sources.TryGetValue("config/ui-layer-overlay.json", out var source))
                return new(StringComparer.Ordinal);
            using var document = JsonDocument.Parse(source);
            if (!document.RootElement.TryGetProperty("removedModifiers", out var values))
                return new(StringComparer.Ordinal);
            if (values.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Removed UI Layer modifiers must be an array.");
            var names = values.EnumerateArray().Select(x => x.GetString() ??
                         throw new InvalidDataException("UI Layer modifier name is required.")).ToArray();
            if (names.Distinct(StringComparer.Ordinal).Count() != names.Length)
                throw new InvalidDataException("Duplicate removed UI Layer modifier.");
            return names.ToHashSet(StringComparer.Ordinal);
        }
        private static bool SameFilter(AxisFilter? first, AxisFilter? second) =>
            JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second);
        private static string Chord(string value) => string.Join("+",
            value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                 .Order(StringComparer.Ordinal));
        private static string Slot(BindingSection section, string input, string chord) =>
            section + "\u001f" + (input.StartsWith("JOY_POV", StringComparison.Ordinal)
                ? "JOY_BTN_POV" + input["JOY_POV".Length..] : input) + "\u001f" + chord;
        private static DcsBinding FromAssignment(Guid profileId, DraftAssignment assignment)
        {
            var slot = assignment.Slot;
            return new(profileId, slot.Section, assignment.Command.Id, assignment.Command.Name,
                BindingDisposition.Added, slot.InputKey,
                Chord(slot.ModifierChord).Split('+', StringSplitOptions.RemoveEmptyEntries), assignment.Filter);
        }
        #endregion END private helpers
    }
}
