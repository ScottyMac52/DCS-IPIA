using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Reconciles selected defaults against inherited bindings and catalogs while retaining module overrides.</summary>
    public static class SharedUiLayerSemanticAdoption
    {
        private const string Root = "assets/shared/ui-layer/input/UiLayer/";
        private const string Functions = "assets/shared/ui-layer/functions.json";
        #region BEGIN Public
        /// <summary>Reviews and stages inherited value propagation without writing module storage.</summary>
        /// <param name="module">Saved module with verified old default evidence.</param>
        /// <param name="target">Verified saved target revision.</param>
        /// <param name="selectedPaths">Reviewed changed source files to adopt.</param>
        /// <returns>Proposed module and preservation evidence.</returns>
        public static SharedUiLayerSemanticPlan Review(EditorWorkspaceSnapshot module, SharedUiLayerWorkspace target,
            IEnumerable<string> selectedPaths)
        {
            ArgumentNullException.ThrowIfNull(selectedPaths);
            var selected = selectedPaths.ToHashSet(StringComparer.Ordinal);
            var updated = SharedUiLayerAdoption.Apply(module, target, selected);
            var baseline = module.SharedDefault!;
            var changes = new List<SharedUiLayerSemanticChange>();
            var assignments = module.Assignments.ToList();
            var labels = new Dictionary<string, string>(module.Labels, StringComparer.Ordinal);
            var sources = new Dictionary<string, string>(updated.Sources!, StringComparer.Ordinal);
            var state = WorkspaceStructureEditor.Describe(module);
            var processed = false;
            var paths = new Dictionary<Guid, string>();
            foreach (var device in state.Devices.Where(x => x.Layer == AssignmentLayer.UiLayer))
            {
                var diagram = module.Diagrams.Single(x => x.ProfileId == device.ProfileId);
                var oldPath = Resolve(module, baseline, diagram, device.FileName);
                var newPath = Resolve(module, target, diagram, device.FileName);
                if (newPath is not null) paths[device.ProfileId] = newPath;
                else if (oldPath is not null) paths[device.ProfileId] = oldPath;
                var oldBindings = Read(baseline, oldPath, device.ProfileId);
                var newBindings = Read(target, newPath, device.ProfileId);
                var current = DcsLuaParser.ParseProfile(UiLayerLuaProfileRenderer.Render(module, device.ProfileId), device.ProfileId);
                // A managed envelope must materialize every UI device, including untouched ones.
                sources[UiSource(device.ProfileId)] = ModuleLuaProfileRenderer.Write(current);
                if (!(oldPath is not null && selected.Contains(oldPath) || newPath is not null && selected.Contains(newPath)))
                    continue;
                processed = true;
                var before = Active(oldBindings);
                var after = Active(newBindings);
                var local = Active(current);
                var retained = current.Where(x => x.Disposition == BindingDisposition.Removed).ToList();
                var merged = new List<DcsBinding>();
                var removedModifiers = UiLayerLuaProfileRenderer.RemovedModifiers(module.Sources!);
                MergeBindings(device, before, after, local, removedModifiers, merged, changes);
                // Preserve untouched history; new default history is appended without removing module tombstones.
                retained.AddRange(newBindings.Where(x => x.Disposition == BindingDisposition.Removed));
                sources[UiSource(device.ProfileId)] = ModuleLuaProfileRenderer.Write(retained.DistinctBy(x => JsonSerializer.Serialize(x)).Concat(merged).ToArray());
                assignments.RemoveAll(x => x.Slot.Layer == AssignmentLayer.UiLayer && x.Slot.ProfileId == device.ProfileId);
                foreach (var binding in merged)
                {
                    if (!diagram.Controls.Any(x => x.IsAxis == (binding.Section == BindingSection.Axis) &&
                        DcsInputKey.SamePhysicalInput(x.InputKey, binding.InputKey))) continue;
                    var slot = new DraftAssignmentSlot(module.Configuration.Module.Id, device.ProfileId, AssignmentLayer.UiLayer,
                        binding.Section, binding.InputKey, string.Join("+", binding.Reformers));
                    var original = DcsInputKey.CollapseAliases(module.Assignments.Where(x =>
                        x.Slot.Layer == AssignmentLayer.UiLayer && x.Slot.ProfileId == device.ProfileId))
                        .SingleOrDefault(x => DcsInputKey.PhysicalSlotKey(x.Slot) == DcsInputKey.PhysicalSlotKey(slot));
                    if (original is not null) slot = original.Slot;
                    assignments.Add(new(slot, new(binding.CommandId, binding.CommandName,
                        binding.Section == BindingSection.Axis, AssignmentLayer.UiLayer))
                    { Filter = binding.Filter });
                    if (original is not null && labels.TryGetValue(original.Slot.Key, out var label) && label == original.Command.Name)
                    {
                        labels.Remove(original.Slot.Key);
                        labels[slot.Key] = binding.CommandName;
                    }
                }
            }
            if (processed || selected.Contains(Root + "modifiers.lua"))
            {
                if (selected.Contains(Root + "modifiers.lua"))
                    state = MergeModifiers(module, baseline, target, state, paths, assignments, changes);
                sources[WorkspaceManagement.SourcePath] = JsonSerializer.Serialize(state);
            }
            updated = updated with { Sources = sources, Assignments = assignments, Labels = labels };
            if (selected.Contains(Functions)) updated = MergeCatalog(module, updated, baseline, target, changes);
            return new(updated, changes.OrderBy(x => x.Kind, StringComparer.Ordinal).ThenBy(x => x.Device, StringComparer.Ordinal)
                .ThenBy(x => x.Identity, StringComparer.Ordinal).ToArray());
        }
        #endregion END Public
        #region BEGIN private helpers
        private static WorkspaceDevice? ModifierDevice(DcsModifier? proposed, Dictionary<Guid, string> paths, WorkspaceManagement state)
        {
            return proposed is null ? null : state.Devices.FirstOrDefault(x => x.Layer == AssignmentLayer.UiLayer &&
                    paths.TryGetValue(x.ProfileId, out var path) &&
                    (Path.GetFileName(path)[..^9].Equals(proposed.Device, StringComparison.OrdinalIgnoreCase) ||
                        DcsPhysicalIdentity.FromFileName(Path.GetFileName(path)) is Guid physical &&
                        DcsPhysicalIdentity.FromFileName(proposed.Device + ".diff.lua") == physical));
        }
        private static bool IsInheritedModifier(WorkspaceModifier? current, DcsModifier? previous, Dictionary<Guid, string> paths, WorkspaceManagement state)
        {
            return current is null ? previous is null : previous is not null &&
                    current.Key == previous.Key && current.Mode == previous.Mode &&
                    NativeMatches(previous.Device, current, paths, state);
        }
        private static void MergeBindings(WorkspaceDevice device, Dictionary<string, DcsBinding> before, Dictionary<string, DcsBinding> after,
            Dictionary<string, DcsBinding> local, IReadOnlySet<string> removedModifiers, List<DcsBinding> merged, List<SharedUiLayerSemanticChange> changes)
        {
            foreach (var key in before.Keys.Union(after.Keys, StringComparer.Ordinal).Union(local.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                before.TryGetValue(key, out var old);
                after.TryGetValue(key, out var proposed);
                local.TryGetValue(key, out var actual);
                var chosen = actual;
                var inherited = Same(actual, old);
                var protectedRemoval = proposed?.Reformers.Any(removedModifiers.Contains) == true;
                if (inherited && !protectedRemoval) chosen = proposed;
                if (!Same(old, proposed))
                    changes.Add(new("Binding", device.FileName, key,
                        protectedRemoval ? "Keep removed modifier" : inherited ? "Apply inherited change" :
                            actual is null ? "Keep module clear" : "Keep module override",
                        Display(actual), Display(proposed)));
                if (chosen is not null) merged.Add(chosen);
            }
        }
        private static string UiSource(Guid id) => "config/ipia-ui-" + id.ToString("N") + ".lua";
        private static IReadOnlyList<DcsBinding> Read(SharedUiLayerWorkspace defaults, string? path, Guid id) =>
            path is null ? [] : DcsLuaParser.ParseProfile(defaults.Sources[path], id);
        private static Dictionary<string, DcsBinding> Active(IEnumerable<DcsBinding> bindings) =>
            bindings.Where(x => x.Disposition != BindingDisposition.Removed).GroupBy(Slot, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.All(y => Same(y, x.First())) ? x.First() :
                    throw new InvalidDataException("Conflicting defaults occupy " + x.Key), StringComparer.Ordinal);
        private static string Slot(DcsBinding binding) => DcsInputKey.PhysicalSlotKey(
            new(Guid.Empty, Guid.Empty, AssignmentLayer.UiLayer, binding.Section, binding.InputKey, string.Join("+", binding.Reformers)));
        private static bool Same(DcsBinding? first, DcsBinding? second) => first is null ? second is null :
            second is not null && first.CommandId == second.CommandId && first.CommandName == second.CommandName &&
            JsonSerializer.Serialize(first.Filter) == JsonSerializer.Serialize(second.Filter);
        private static string Display(DcsBinding? binding) => binding is null ? "Unassigned" : binding.CommandName + " [" + binding.CommandId + "]";
        /// <summary>Prefers explicit provenance, then physical identity, then shared hardware names.</summary>
        private static string? Resolve(EditorWorkspaceSnapshot module, SharedUiLayerWorkspace defaults,
                                        UiLayerDiagram diagram, string file)
        {
            if (diagram.CanonicalUiLayerSourcePath is string path && defaults.Sources.ContainsKey(path))
                return path;
            var candidates = ReadProfileCandidates(defaults);
            var physical = FindPhysicalProfile(candidates, file);
            if (physical is not null)
                return physical;
            var matches = FindNamedProfiles(module, diagram, candidates, file);
            return matches.Length switch
            {
                0 => null,
                1 => matches[0],
                _ => throw new InvalidDataException("Ambiguous shared default profile for " + file)
            };
        }

        private static string[] ReadProfileCandidates(SharedUiLayerWorkspace defaults) =>
            defaults.Sources.Keys.Where(x => x.StartsWith(Root, StringComparison.Ordinal) &&
                x.EndsWith(".diff.lua", StringComparison.Ordinal)).ToArray();

        /// <summary>Accepts exactly one native filename or physical GUID match before name fallback.</summary>
        private static string? FindPhysicalProfile(string[] candidates, string file)
        {
            var guid = DcsPhysicalIdentity.FromFileName(file);
            var physical = candidates.Where(x => Path.GetFileName(x).Equals(file, StringComparison.OrdinalIgnoreCase) ||
                guid is not null && DcsPhysicalIdentity.FromFileName(Path.GetFileName(x)) == guid).ToArray();
            return physical.Length == 1 ? physical[0] : null;
        }

        /// <summary>Matches native base names and pinned shared hardware aliases without guessing ambiguous identities.</summary>
        private static string[] FindNamedProfiles(EditorWorkspaceSnapshot module, UiLayerDiagram diagram,
                                                   string[] candidates, string file)
        {
            var profile = module.Configuration.Profiles.Single(x => x.Id == diagram.ProfileId);
            var hardware = WorkspaceStructureEditor.Hardware(module).Where(x => x.DeviceId == profile.DeviceId).ToArray();
            var names = hardware.Select(x => BaseName(x.DefaultFileName)).Append(BaseName(file))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return candidates.Where(x => names.Contains(BaseName(Path.GetFileName(x)))).ToArray();
        }
        private static string BaseName(string file)
        {
            var index = file.LastIndexOf('{');
            return DcsPhysicalIdentity.FromFileName(file) is not null && index >= 0 ? file[..index].Trim() : file[..^9];
        }
        private static WorkspaceManagement MergeModifiers(EditorWorkspaceSnapshot module, SharedUiLayerWorkspace baseline,
            SharedUiLayerWorkspace target, WorkspaceManagement state, Dictionary<Guid, string> paths,
            IReadOnlyList<DraftAssignment> assignments, List<SharedUiLayerSemanticChange> changes)
        {
            var old = DcsLuaParser.ParseModifiers(baseline.Sources[Root + "modifiers.lua"]).ToDictionary(x => x.Name, StringComparer.Ordinal);
            var next = DcsLuaParser.ParseModifiers(target.Sources[Root + "modifiers.lua"]).ToDictionary(x => x.Name, StringComparer.Ordinal);
            var result = state.Modifiers.ToList();
            var removed = UiLayerLuaProfileRenderer.RemovedModifiers(module.Sources!);
            foreach (var name in old.Keys.Union(next.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                old.TryGetValue(name, out var previous);
                next.TryGetValue(name, out var proposed);
                if (previous == proposed) continue;
                var current = result.SingleOrDefault(x => x.Layer == AssignmentLayer.UiLayer && x.Name == name);
                var inherited = IsInheritedModifier(current, previous, paths, state);
                var used = assignments.Any(x => x.Slot.Layer == AssignmentLayer.UiLayer &&
                    x.Slot.ModifierChord.Split('+').Contains(name, StringComparer.Ordinal));
                var apply = inherited && !removed.Contains(name) && (proposed is not null || !used);
                var device = ModifierDevice(proposed, paths, state);
                var absentDevice = proposed is not null && proposed.Device != "Keyboard" && device is null;
                if (apply && absentDevice && used)
                    throw new InvalidDataException("The new modifier requires a UI device not present in this module: " + proposed!.Device);
                if (absentDevice) apply = false;
                changes.Add(new("Modifier", "UI Layer", name, apply ? "Apply inherited change" :
                    absentDevice ? "Keep absent device" : "Keep module modifier", current?.Key ?? "Absent", proposed?.Key ?? "Absent"));
                if (!apply) continue;
                if (current is not null) result.Remove(current);
                if (proposed is null) continue;
                result.Add(new(AssignmentLayer.UiLayer, device?.ProfileId ?? Guid.Empty, proposed.Name, proposed.Key,
                    current?.SemanticLayer ?? proposed.Name, proposed.Mode, device is null ? "Keyboard" : device.FileName[..^9]));
            }
            return state with { Modifiers = result };
        }
        private static bool NativeMatches(string native, WorkspaceModifier current, Dictionary<Guid, string> paths,
                                          WorkspaceManagement state) =>
            current.NativeDevice.Equals(native, StringComparison.OrdinalIgnoreCase) ||
            paths.TryGetValue(current.ProfileId, out var path) && (Path.GetFileName(path)[..^9].Equals(native, StringComparison.OrdinalIgnoreCase) ||
                DcsPhysicalIdentity.FromFileName(Path.GetFileName(path)) is Guid physical &&
                DcsPhysicalIdentity.FromFileName(native + ".diff.lua") == physical) &&
            state.Devices.Any(x => x.ProfileId == current.ProfileId && x.Layer == AssignmentLayer.UiLayer);
        private static EditorWorkspaceSnapshot MergeCatalog(EditorWorkspaceSnapshot original, EditorWorkspaceSnapshot updated,
            SharedUiLayerWorkspace baseline, SharedUiLayerWorkspace target, List<SharedUiLayerSemanticChange> changes)
        {
            var old = Commands(baseline).ToDictionary(x => x.DcsCommandId, StringComparer.Ordinal);
            var next = Commands(target).ToDictionary(x => x.DcsCommandId, StringComparer.Ordinal);
            var result = original.UiCommands.ToDictionary(x => x.DcsCommandId, StringComparer.Ordinal);
            var assignments = updated.Assignments.ToArray();
            var labels = new Dictionary<string, string>(updated.Labels, StringComparer.Ordinal);
            foreach (var code in old.Keys.Union(next.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                old.TryGetValue(code, out var previous);
                next.TryGetValue(code, out var proposed);
                result.TryGetValue(code, out var current);
                if (previous == proposed) continue;
                var inherited = current == previous;
                var used = assignments.Any(x => x.Slot.Layer == AssignmentLayer.UiLayer && x.Command.Id == code);
                var apply = inherited && (proposed is not null || !used);
                changes.Add(new("Catalog", "UI Layer catalog", code, apply ? "Apply inherited change" : "Keep module catalog",
                    current?.Name ?? "Absent", proposed?.Name ?? "Absent"));
                if (!apply) continue;
                result.Remove(code);
                if (proposed is null) continue;
                result[code] = proposed;
                for (var i = 0; i < assignments.Length; i++)
                {
                    var assignment = assignments[i];
                    if (assignment.Slot.Layer != AssignmentLayer.UiLayer || assignment.Command.Id != code ||
                        previous is not null && assignment.Command.Name != previous.Name) continue;
                    if (labels.TryGetValue(assignment.Slot.Key, out var label) && label == assignment.Command.Name)
                        labels[assignment.Slot.Key] = proposed.Name;
                    assignments[i] = assignment with { Command = assignment.Command with { Name = proposed.Name } };
                }
            }
            return updated with
            {
                UiCommands = result.Values.OrderBy(x => x.DcsCommandId, StringComparer.Ordinal).ToArray(),
                Assignments = assignments,
                Labels = labels
            };
        }
        private static DcsCommandDefinition[] Commands(SharedUiLayerWorkspace defaults)
        {
            var catalog = StableIdentity.Create("catalog", "dcs-common-ui-layer");
            return SharedUiLayerCommandEditor.Commands(defaults).Select(x =>
                new DcsCommandDefinition(StableIdentity.Create("ui-function", x.Id), catalog, x.Command, x.Label, x.Category, false)).ToArray();
        }
        #endregion END private helpers
    }
}


