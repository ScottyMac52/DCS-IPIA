using System.Text.Json;
using Heimdall.Application;
using Heimdall.Domain;

namespace Heimdall.Dcs
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
            workspace = PinnedUiLayerProfileRecovery.Restore(workspace);
            if (WorkspaceManagement.Read(workspace) is { } managed)
                return RenderManaged(workspace, profileId, managed);
            var (diagram, source, modifierSource) = ReadPinnedSources(workspace, profileId);
            var config = workspace.Configuration;
            var known = DcsLuaParser.ParseModifiers(modifierSource).Select(x => x.Name)
                                    .ToHashSet(StringComparer.Ordinal);
            var removed = RemovedModifiers(workspace.Sources!);
            ValidateRemovedModifiers(known, removed);
            var valid = known.Except(removed, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
            var original = DcsLuaParser.ParseProfile(source, profileId);
            var active = original.Where(x => x.Disposition != BindingDisposition.Removed).ToArray();
            ValidateCanonicalSlots(active);
            var baseline = ReadBaseline(config, profileId, diagram, valid);
            var saved = ReadSavedAssignments(workspace, profileId, diagram, valid);
            active = SelectActiveBindings(workspace, active, baseline, saved);
            var result = MergeBindings(original, active, profileId, known, removed, baseline, saved);
            return ModuleLuaProfileRenderer.Write(ModuleAxisDefaults.Apply(workspace, profileId, AssignmentLayer.UiLayer, result));
        }
        #endregion END Public
        #region BEGIN private helpers
        /// <summary>Checks profile ownership and resolves its pinned source texts.</summary>
        private static (UiLayerDiagram Diagram, string Source, string Modifiers) ReadPinnedSources(EditorWorkspaceSnapshot workspace,
                        Guid profileId)
        {
            var config = workspace.Configuration;
            if (config.Profiles.All(x => x.Id != profileId))
                throw new ArgumentException("Profile is outside this module.", nameof(profileId));
            var diagram = workspace.Diagrams.SingleOrDefault(x => x.ProfileId == profileId) ??
                          throw new InvalidDataException("UI Layer device has no authored hardware diagram.");
            var path = ReadCanonicalPath(diagram);
            if (workspace.Sources is null || !workspace.Sources.TryGetValue(path, out var source) ||
                !workspace.Sources.TryGetValue(SharedRoot + "modifiers.lua", out var modifierSource))
                throw new InvalidDataException("The saved workspace lacks canonical UI Layer Lua; re-import it.");
            return (diagram, source, modifierSource);
        }

        /// <summary>Requires an unambiguous safe canonical keyboard or joystick Lua path.</summary>
        private static string ReadCanonicalPath(UiLayerDiagram diagram)
        {
            var path = diagram.CanonicalUiLayerSourcePath;
            if (!diagram.SupportsUiLayer || path is null || !path.StartsWith(SharedRoot, StringComparison.Ordinal) ||
                !path.EndsWith(".diff.lua", StringComparison.Ordinal) || path.Contains("..", StringComparison.Ordinal) ||
                !path.Contains("/joystick/", StringComparison.Ordinal) &&
                !path.Contains("/keyboard/", StringComparison.Ordinal))
                throw new InvalidDataException("No unambiguous pinned UI Layer source for this device.");
            return path;
        }

        /// <summary>Rejects removal of an unknown canonical modifier.</summary>
        private static void ValidateRemovedModifiers(HashSet<string> known, HashSet<string> removed)
        {
            if (removed.Any(x => !known.Contains(x)))
                throw new InvalidDataException("An overlay removes an unknown canonical UI Layer modifier.");
        }

        /// <summary>Rejects conflicting canonical input slots before editing.</summary>
        private static void ValidateCanonicalSlots(DcsBinding[] active)
        {
            if (active.GroupBy(x => Slot(x.Section, x.InputKey, string.Join("+", x.Reformers)),
                               StringComparer.Ordinal).Any(x => x.Count() > 1))
                throw new InvalidDataException("Multiple canonical UI Layer commands occupy one input slot.");
        }

        /// <summary>Maps imported callouts to unique physical slots.</summary>
        private static HashSet<string> ReadBaseline(ModuleConfiguration config, Guid profileId, UiLayerDiagram diagram,
                                                    HashSet<string> valid)
        {
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
            return baseline;
        }

        /// <summary>Validates and indexes module-owned assignments by physical slot.</summary>
        private static Dictionary<string, DraftAssignment> ReadSavedAssignments(EditorWorkspaceSnapshot workspace, Guid profileId,
                                                                               UiLayerDiagram diagram, HashSet<string> valid)
        {
            var config = workspace.Configuration;
            var saved = new Dictionary<string, DraftAssignment>(StringComparer.Ordinal);
            foreach (var assignment in workspace.Assignments.Where(x => x.Slot.ProfileId == profileId &&
                                                                          x.Slot.Layer == AssignmentLayer.UiLayer))
            {
                var slot = assignment.Slot;
                ValidateSavedAssignment(assignment, config, diagram);
                var chord = Chord(slot.ModifierChord);
                if (chord.Split('+', StringSplitOptions.RemoveEmptyEntries).Any(x => !valid.Contains(x)))
                    throw new InvalidDataException("Saved UI Layer assignment uses an undefined modifier.");
                if (!saved.TryAdd(Slot(slot.Section, slot.InputKey, chord), assignment))
                    throw new InvalidDataException("Two saved UI Layer commands occupy one input slot.");
            }
            return saved;
        }

        /// <summary>Suppresses unselected defaults when the module declares explicit UI usage.</summary>
        private static DcsBinding[] SelectActiveBindings(EditorWorkspaceSnapshot workspace, DcsBinding[] active,
                                                          HashSet<string> baseline, Dictionary<string, DraftAssignment> saved)
        {
            if (workspace.Sources!.TryGetValue("config/kneeboard.json", out var manifest))
            {
                using var document = JsonDocument.Parse(manifest);
                if (document.RootElement.TryGetProperty("uiLayerUtilization", out var usage) &&
                    usage.TryGetProperty("mode", out var mode) && mode.GetString() == "explicit")
                    active = active.Where(x => baseline.Contains(Slot(x.Section, x.InputKey, string.Join("+", x.Reformers))) ||
                        saved.ContainsKey(Slot(x.Section, x.InputKey, string.Join("+", x.Reformers)))).ToArray();
            }
            return active;
        }

        /// <summary>Merges saved edits with canonical bindings while retaining removed history.</summary>
        private static List<DcsBinding> MergeBindings(IReadOnlyList<DcsBinding> original, DcsBinding[] active, Guid profileId,
                                                       HashSet<string> known, HashSet<string> removed, HashSet<string> baseline,
                                                       Dictionary<string, DraftAssignment> saved)
        {
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
                AddActiveBinding(binding, key, profileId, baseline, saved, result);
            }
            foreach (var (key, edit) in saved)
                if (!handled.Contains(key))
                    result.Add(FromAssignment(profileId, edit));
            return result;
        }

        /// <summary>Requires a module-owned command of the mapped hardware input type.</summary>
        private static void ValidateSavedAssignment(DraftAssignment assignment, ModuleConfiguration config, UiLayerDiagram diagram)
        {
            var slot = assignment.Slot;
            if (slot.ModuleId != config.Module.Id || assignment.Command.Layer != AssignmentLayer.UiLayer ||
                (slot.Section == BindingSection.Axis) != assignment.Command.IsAxis ||
                !diagram.Controls.Any(x => x.IsAxis == assignment.Command.IsAxis &&
                                           DcsInputKey.SamePhysicalInput(x.InputKey, slot.InputKey)))
                throw new InvalidDataException("Saved UI Layer assignment is outside its mapped device or type.");
        }

        /// <summary>Applies one edit or axis tombstone without dropping unrepresented key defaults.</summary>
        private static void AddActiveBinding(DcsBinding binding, string key, Guid profileId, HashSet<string> baseline,
                                              Dictionary<string, DraftAssignment> saved, List<DcsBinding> result)
        {
            if (saved.TryGetValue(key, out var edit))
            {
                if (binding.Section == BindingSection.Axis && edit.Command.Id != binding.CommandId)
                    result.Add(ModuleLuaProfileRenderer.Removed(binding));
                result.Add(edit.Command.Id == binding.CommandId &&
                           SameFilter(edit.Filter, binding.Filter) ? binding : FromAssignment(profileId, edit));
            }
            else if (binding.Section == BindingSection.Axis)
                result.Add(ModuleLuaProfileRenderer.Removed(binding));
            else if (!baseline.Contains(key))
                result.Add(binding);
        }
        private static string RenderManaged(EditorWorkspaceSnapshot workspace, Guid profileId, WorkspaceManagement managed)
        {
            if (!managed.Devices.Any(x => x.ProfileId == profileId && x.Layer == AssignmentLayer.UiLayer))
                throw new InvalidDataException("Device is outside the module UI Layer.");
            var path = "config/ipia-ui-" + profileId.ToString("N") + ".lua";
            var source = workspace.Sources?.GetValueOrDefault(path) ??
                         throw new InvalidDataException("The managed UI Layer profile source is missing.");
            var bindings = DcsLuaParser.ParseProfile(source, profileId);
            var modifiers = managed.Modifiers.Where(x => x.Layer == AssignmentLayer.UiLayer)
                .Select(x => new DcsModifier(x.Name, x.NativeDevice, x.Key, x.Mode));
            var config = new ModuleConfiguration(workspace.Configuration.Module, workspace.Configuration.Profiles,
                modifiers, bindings, workspace.Configuration.Provenance);
            var assignments = workspace.Assignments.Where(x => x.Slot.Layer == AssignmentLayer.UiLayer)
                .Select(x => x with
                {
                    Slot = x.Slot with { Layer = AssignmentLayer.Module },
                    Command = x.Command with { Layer = AssignmentLayer.Module }
                }).ToArray();
            return ModuleLuaProfileRenderer.Render(workspace with { Configuration = config, Assignments = assignments }, profileId, AssignmentLayer.UiLayer);
        }
        internal static HashSet<string> RemovedModifiers(IReadOnlyDictionary<string, string> sources)
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

