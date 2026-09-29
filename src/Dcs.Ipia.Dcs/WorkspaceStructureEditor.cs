using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Stages validated module-owned device and modifier edits without writing shared repositories.</summary>
    public static class WorkspaceStructureEditor
    {
        #region BEGIN Public
        /// <summary>Reads settings or projects imported device and modifier definitions.</summary>
        /// <param name="workspace">Saved or staged workspace.</param>
        /// <returns>Editable memberships and modifiers.</returns>
        public static WorkspaceManagement Describe(EditorWorkspaceSnapshot workspace)
        {
            var settings = WorkspaceManagement.Read(workspace);
            if (settings is not null)
                return settings;
            var devices = workspace.Configuration.Profiles.Select(x =>
                new WorkspaceDevice(x.Id, AssignmentLayer.Module, x.SourceName, x.Instance, x.Role)).ToList();
            foreach (var diagram in workspace.Diagrams.Where(x => x.SupportsUiLayer))
            {
                var profile = workspace.Configuration.Profiles.Single(x => x.Id == diagram.ProfileId);
                devices.Add(new(profile.Id, AssignmentLayer.UiLayer,
                    diagram.CanonicalUiLayerSourcePath is null ? profile.SourceName : Path.GetFileName(diagram.CanonicalUiLayerSourcePath), profile.Instance, profile.Role));
            }
            var modifiers = workspace.Configuration.Modifiers.Select(x => ImportModifier(workspace, devices, x, AssignmentLayer.Module)).ToList();
            if (workspace.Sources?.TryGetValue("assets/shared/ui-layer/input/UiLayer/modifiers.lua", out var lua) == true)
            {
                var removed = UiLayerLuaProfileRenderer.RemovedModifiers(workspace.Sources);
                var used = workspace.Diagrams.Where(x => x.SupportsUiLayer && x.CanonicalUiLayerSourcePath is not null)
                    .SelectMany(x => workspace.Sources.TryGetValue(x.CanonicalUiLayerSourcePath!, out var source)
                        ? DcsLuaParser.ParseProfile(source, x.ProfileId).SelectMany(b => b.Reformers) : [])
                    .ToHashSet(StringComparer.Ordinal);
                modifiers.AddRange(DcsLuaParser.ParseModifiers(lua).Where(x => !removed.Contains(x.Name))
                    .Select(x => ImportModifier(workspace, devices, x, AssignmentLayer.UiLayer))
                    .Where(x => x.ProfileId != Guid.Empty || used.Contains(x.Name)));
            }
            return new(devices, modifiers);
        }
        /// <summary>Gets pinned shared hardware choices, including existing graphics for older workspaces.</summary>
        /// <param name="workspace">Stored shared assets.</param>
        /// <returns>Shared hardware choices.</returns>
        public static IReadOnlyList<WorkspaceHardware> Hardware(EditorWorkspaceSnapshot workspace)
        {
            if (workspace.Sources?.TryGetValue("config/ipia-hardware.json", out var source) == true)
                return JsonSerializer.Deserialize<WorkspaceHardware[]>(source) ?? [];
            return workspace.Diagrams.Select(x => {
                var profile = workspace.Configuration.Profiles.Single(p => p.Id == x.ProfileId);
                return new WorkspaceHardware(profile.DeviceId, x.Device, profile.SourceName, x);
            }).DistinctBy(x => x.DeviceId).ToArray();
        }
        /// <summary>Adds shared hardware to a scope with an editable native filename.</summary>
        /// <param name="workspace">Current staged workspace.</param>
        /// <param name="hardware">Shared hardware choice.</param>
        /// <param name="layer">Module or UI Layer.</param>
        /// <param name="fileName">DCS diff.lua filename.</param>
        /// <param name="instance">Optional instance.</param>
        /// <param name="role">Optional role.</param>
        /// <returns>Updated immutable draft.</returns>
        public static EditorWorkspaceSnapshot AddDevice(EditorWorkspaceSnapshot workspace, WorkspaceHardware hardware,
            AssignmentLayer layer, string fileName, string? instance, string? role)
        {
            workspace = Prepare(workspace);
            var state = Describe(workspace);
            ValidateFile(fileName);
            instance = Optional(instance);
            role = Optional(role);
            var identity = instance ?? role;
            var profile = workspace.Configuration.Profiles.FirstOrDefault(x => x.DeviceId == hardware.DeviceId && x.Instance == identity);
            if (profile is not null && state.Devices.Any(x => x.ProfileId == profile.Id && x.Layer == layer))
                throw new InvalidOperationException("This device already belongs to that scope. Supply a unique instance or role.");
            var isNew = profile is null;
            profile ??= new(Guid.NewGuid(), hardware.DeviceId, identity, role ?? hardware.DeviceId, fileName.Trim());
            var membership = new WorkspaceDevice(profile.Id, layer, fileName.Trim(), instance, role);
            ValidateMembership(state, membership, null);
            if (isNew)
            {
                var diagram = hardware.Diagram with { ProfileId = profile.Id, Instance = identity, Callouts = [], SupportsUiLayer = layer == AssignmentLayer.UiLayer };
                workspace = workspace with {
                    Configuration = Configuration(workspace, workspace.Configuration.Profiles.Append(profile), workspace.Configuration.Modifiers, workspace.Configuration.Bindings),
                    Diagrams = [.. workspace.Diagrams, diagram]
                };
                workspace = SetSource(workspace, UiSource(profile.Id), ModuleLuaProfileRenderer.Write([]));
                workspace = Manifest(workspace, profile, null, false);
            }
            if (!workspace.Sources!.ContainsKey(UiSource(profile.Id)))
                workspace = SetSource(workspace, UiSource(profile.Id), ModuleLuaProfileRenderer.Write([]));
            if (layer == AssignmentLayer.Module && profile.SourceName != membership.FileName)
                workspace = RenameProfile(workspace, profile, membership.FileName, identity, role);
            return SetUiSupport(Store(workspace, state with { Devices = [.. state.Devices, membership] }), profile.Id);
        }
        /// <summary>Updates identity, filename, and module MFD category labels.</summary>
        /// <param name="workspace">Current staged workspace.</param>
        /// <param name="device">Selected membership.</param>
        /// <param name="fileName">Diff.lua filename.</param>
        /// <param name="instance">Optional instance.</param>
        /// <param name="role">Optional role.</param>
        /// <param name="mfdLabels">Module MFD left, right, top, and middle labels.</param>
        /// <param name="hardware">Optional replacement shared hardware; changing type requires an unused device.</param>
        /// <returns>Updated immutable draft.</returns>
        public static EditorWorkspaceSnapshot UpdateDevice(EditorWorkspaceSnapshot workspace, WorkspaceDevice device,
            string fileName, string? instance, string? role, IReadOnlyDictionary<string, string>? mfdLabels = null, WorkspaceHardware? hardware = null)
        {
            workspace = Prepare(workspace);
            var state = Describe(workspace);
            ValidateFile(fileName);
            instance = Optional(instance);
            role = Optional(role);
            var updated = device with { FileName = fileName.Trim(), Instance = instance, Role = role };
            ValidateMembership(state, updated, device);
            var profile = workspace.Configuration.Profiles.Single(x => x.Id == device.ProfileId);
            if (hardware is not null && hardware.DeviceId != profile.DeviceId)
            {
                if (EffectiveBindings(workspace).Any(x => x.ProfileId == profile.Id) || state.Modifiers.Any(x => x.ProfileId == profile.Id))
                    throw new InvalidOperationException("Clear assignments and move or remove modifiers before changing shared hardware.");
                var replacement = new DeviceProfile(profile.Id, hardware.DeviceId, instance ?? role, role ?? hardware.DeviceId, profile.SourceName);
                workspace = workspace with {
                    Configuration = Configuration(workspace, workspace.Configuration.Profiles.Select(x => x.Id == profile.Id ? replacement : x),
                        workspace.Configuration.Modifiers, workspace.Configuration.Bindings.Where(x => x.ProfileId != profile.Id)),
                    Diagrams = workspace.Diagrams.Select(x => x.ProfileId == profile.Id
                        ? hardware.Diagram with { ProfileId = profile.Id, Instance = instance ?? role, Callouts = [],
                            SupportsUiLayer = state.Devices.Any(d => d.ProfileId == profile.Id && d.Layer == AssignmentLayer.UiLayer) } : x).ToArray()
                };
                workspace = SetSource(workspace, UiSource(profile.Id), ModuleLuaProfileRenderer.Write([]));
                profile = replacement;
            }
            var identity = instance ?? role;
            if (workspace.Configuration.Profiles.Any(x => x.Id != profile.Id && x.DeviceId == profile.DeviceId && x.Instance == identity))
                throw new InvalidOperationException("Another device already uses that instance or role.");
            workspace = RenameProfile(workspace, profile,
                device.Layer == AssignmentLayer.Module ? updated.FileName : profile.SourceName, identity, role);
            var modifiers = state.Modifiers.Select(x => x.ProfileId == profile.Id && x.Layer == device.Layer
                ? x with { NativeDevice = Native(updated.FileName) } : x).ToArray();
            workspace = Store(workspace, state with { Devices = state.Devices.Select(x => x == device ? updated : x.ProfileId == profile.Id ? x with { Instance = instance } : x).ToArray(), Modifiers = modifiers });
            if (device.Layer == AssignmentLayer.Module && profile.DeviceId == "tm-mfd" && mfdLabels is not null)
                workspace = MfdLabels(workspace, profile.Id, mfdLabels);
            return workspace;
        }
        /// <summary>Removes an unused membership after checking both scopes.</summary>
        /// <param name="workspace">Current staged workspace.</param>
        /// <param name="device">Membership requested for removal.</param>
        /// <returns>Updated immutable draft.</returns>
        public static EditorWorkspaceSnapshot RemoveDevice(EditorWorkspaceSnapshot workspace, WorkspaceDevice device)
        {
            workspace = Prepare(workspace);
            var state = Describe(workspace);
            var assignments = EffectiveBindings(workspace).Count(x => x.ProfileId == device.ProfileId);
            var modifiers = state.Modifiers.Count(x => x.ProfileId == device.ProfileId);
            if (assignments + modifiers > 0)
                throw new InvalidOperationException($"This device is used by {assignments} assignment(s) and {modifiers} modifier(s) across Module and UI Layer. Clear assignments and move or remove modifiers first.");
            state = state with { Devices = state.Devices.Where(x => x != device).ToArray() };
            workspace = Store(workspace, state);
            if (state.Devices.Any(x => x.ProfileId == device.ProfileId))
                return SetUiSupport(workspace, device.ProfileId);
            var profile = workspace.Configuration.Profiles.Single(x => x.Id == device.ProfileId);
            workspace = Manifest(workspace, profile, profile.SourceName, true);
            return workspace with {
                Configuration = Configuration(workspace, workspace.Configuration.Profiles.Where(x => x.Id != profile.Id), workspace.Configuration.Modifiers,
                    workspace.Configuration.Bindings.Where(x => x.ProfileId != profile.Id)),
                Diagrams = workspace.Diagrams.Where(x => x.ProfileId != profile.Id).ToArray(),
                Assignments = workspace.Assignments.Where(x => x.Slot.ProfileId != profile.Id).ToArray()
            };
        }
        /// <summary>Adds or updates a modifier and migrates renamed assignment references.</summary>
        /// <param name="workspace">Current staged workspace.</param>
        /// <param name="current">Existing modifier or null to add.</param>
        /// <param name="candidate">Device, control, name, semantic layer, and mode.</param>
        /// <returns>Updated immutable draft.</returns>
        public static EditorWorkspaceSnapshot SetModifier(EditorWorkspaceSnapshot workspace, WorkspaceModifier? current, WorkspaceModifier candidate)
        {
            workspace = Prepare(workspace);
            var state = Describe(workspace);
            var device = state.Devices.SingleOrDefault(x => x.ProfileId == candidate.ProfileId && x.Layer == candidate.Layer)
                ?? throw new InvalidOperationException("Select a device belonging to this scope.");
            var diagram = workspace.Diagrams.Single(x => x.ProfileId == candidate.ProfileId);
            if (!diagram.Controls.Any(x => !x.IsAxis && !x.Control.EndsWith("-shifted", StringComparison.Ordinal) && DcsInputKey.SamePhysicalInput(x.InputKey, candidate.Key)))
                throw new InvalidOperationException("Select a shared digital control on that device.");
            if (string.IsNullOrWhiteSpace(candidate.Name) || candidate.Name.Contains('+') || candidate.Name.Any(char.IsControl) ||
                string.IsNullOrWhiteSpace(candidate.SemanticLayer) || !Enum.IsDefined(candidate.Mode))
                throw new InvalidOperationException("Name, Semantic Layer, and Hold or Toggle mode are required. Names cannot contain + or control characters.");
            if (current is not null && current.Layer != candidate.Layer)
                throw new InvalidOperationException("A modifier's scope cannot change during an update.");
            candidate = candidate with { Name = candidate.Name.Trim(), SemanticLayer = candidate.SemanticLayer.Trim(), NativeDevice = Native(device.FileName) };
            var others = state.Modifiers.Where(x => x != current).ToArray();
            if (others.Any(x => x.Layer == candidate.Layer && (x.Name.Equals(candidate.Name, StringComparison.OrdinalIgnoreCase) ||
                x.NativeDevice.Equals(candidate.NativeDevice, StringComparison.OrdinalIgnoreCase) && DcsInputKey.SamePhysicalInput(x.Key, candidate.Key))))
                throw new InvalidOperationException("That modifier name or physical input already exists in this scope.");
            if (current is not null && current.Name != candidate.Name)
                workspace = RenameModifier(workspace, current.Layer, current.Name, candidate.Name);
            return Store(workspace, state with { Modifiers = [.. others, candidate] });
        }
        /// <summary>Removes a modifier only when its native and semantic names have no use in either scope.</summary>
        /// <param name="workspace">Current staged workspace.</param>
        /// <param name="modifier">Definition requested for removal.</param>
        /// <returns>Updated immutable draft.</returns>
        public static EditorWorkspaceSnapshot RemoveModifier(EditorWorkspaceSnapshot workspace, WorkspaceModifier modifier)
        {
            workspace = Prepare(workspace);
            var state = Describe(workspace);
            var names = state.Modifiers.Where(x => x.Name == modifier.Name || x.SemanticLayer == modifier.SemanticLayer)
                .Select(x => x.Name).Append(modifier.SemanticLayer).ToHashSet(StringComparer.Ordinal);
            var uses = EffectiveBindings(workspace).Count(x => x.Reformers.Any(names.Contains));
            if (uses > 0)
                throw new InvalidOperationException($"{modifier.Name} is used by {uses} assignment(s) across Module and UI Layer. Clear those assignments first.");
            return Store(workspace, state with { Modifiers = state.Modifiers.Where(x => x != modifier).ToArray() });
        }
        /// <summary>Explicitly clears every assignment on a physical device in both scopes, including unmapped imports.</summary>
        /// <param name="workspace">Current staged workspace.</param>
        /// <param name="device">Device selected for clearing.</param>
        /// <returns>Updated reversible draft.</returns>
        public static EditorWorkspaceSnapshot ClearDeviceAssignments(EditorWorkspaceSnapshot workspace, WorkspaceDevice device)
        {
            workspace = Prepare(workspace);
            var cleared = workspace.Assignments.Where(x => x.Slot.ProfileId == device.ProfileId).Select(x => x.Slot.Key).ToHashSet(StringComparer.Ordinal);
            var bindings = workspace.Configuration.Bindings.Where(x => x.ProfileId != device.ProfileId || x.Disposition == BindingDisposition.Removed);
            workspace = workspace with {
                Configuration = Configuration(workspace, workspace.Configuration.Profiles, workspace.Configuration.Modifiers, bindings),
                Assignments = workspace.Assignments.Where(x => x.Slot.ProfileId != device.ProfileId).ToArray(),
                Labels = workspace.Labels.Where(x => !cleared.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
            };
            if (workspace.Sources!.TryGetValue(UiSource(device.ProfileId), out var source))
                workspace = SetSource(workspace, UiSource(device.ProfileId), ModuleLuaProfileRenderer.Write(
                    DcsLuaParser.ParseProfile(source, device.ProfileId).Where(x => x.Disposition == BindingDisposition.Removed).ToArray()));
            return workspace;
        }
        /// <summary>Gets UI Layer modifier names for assignment validation.</summary>
        /// <param name="workspace">Current workspace.</param>
        /// <returns>Declared modifier names.</returns>
        public static IEnumerable<string> UiModifierNames(EditorWorkspaceSnapshot workspace) =>
            Describe(workspace).Modifiers.Where(x => x.Layer == AssignmentLayer.UiLayer).Select(x => x.Name)
                .Concat(WorkspaceManagement.Read(workspace) is null
                    ? workspace.Diagrams.SelectMany(x => x.Callouts).SelectMany(x =>
                        x.Modifier.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    : []).Distinct(StringComparer.Ordinal);
        #endregion END Public
        #region BEGIN private helpers
        private static EditorWorkspaceSnapshot Prepare(EditorWorkspaceSnapshot workspace)
        {
            var state = Describe(workspace);
            var bindings = state.Devices.Where(x => x.Layer == AssignmentLayer.Module)
                .SelectMany(p => DcsLuaParser.ParseProfile(ModuleLuaProfileRenderer.Render(workspace, p.ProfileId), p.ProfileId)).ToArray();
            foreach (var device in state.Devices.Where(x => x.Layer == AssignmentLayer.UiLayer))
                workspace = SetSource(workspace, UiSource(device.ProfileId), UiLayerLuaProfileRenderer.Render(workspace, device.ProfileId));
            workspace = workspace with { Configuration = Configuration(workspace, workspace.Configuration.Profiles, workspace.Configuration.Modifiers, bindings) };
            return Store(workspace, state);
        }
        private static IEnumerable<DcsBinding> EffectiveBindings(EditorWorkspaceSnapshot workspace)
        {
            foreach (var device in Describe(workspace).Devices)
            {
                var lua = device.Layer == AssignmentLayer.Module ? ModuleLuaProfileRenderer.Render(workspace, device.ProfileId)
                    : UiLayerLuaProfileRenderer.Render(workspace, device.ProfileId);
                foreach (var binding in DcsLuaParser.ParseProfile(lua, device.ProfileId).Where(x => x.Disposition != BindingDisposition.Removed))
                    yield return binding;
            }
        }
        private static WorkspaceModifier ImportModifier(EditorWorkspaceSnapshot workspace, IEnumerable<WorkspaceDevice> devices, DcsModifier modifier, AssignmentLayer layer)
        {
            var device = devices.FirstOrDefault(x => x.Layer == layer && Native(x.FileName).Trim() == modifier.Device.Trim());
            if (device is null && layer == AssignmentLayer.Module)
                device = devices.FirstOrDefault(x => x.Layer == layer && workspace.Diagrams.Any(d => d.ProfileId == x.ProfileId &&
                    d.CanonicalUiLayerSourcePath is not null && Native(Path.GetFileName(d.CanonicalUiLayerSourcePath)).Trim() == modifier.Device.Trim()));
            return new(layer, device?.ProfileId ?? Guid.Empty, modifier.Name, modifier.Key, modifier.Name, modifier.Mode,
                device is null ? modifier.Device : Native(device.FileName));
        }
        private static EditorWorkspaceSnapshot Store(EditorWorkspaceSnapshot workspace, WorkspaceManagement state)
        {
            var modifiers = state.Modifiers.Where(x => x.Layer == AssignmentLayer.Module).Select(x => new DcsModifier(x.Name, x.NativeDevice, x.Key, x.Mode));
            workspace = workspace with { Configuration = Configuration(workspace, workspace.Configuration.Profiles, modifiers, workspace.Configuration.Bindings) };
            return SetSource(workspace, WorkspaceManagement.SourcePath, JsonSerializer.Serialize(state));
        }
        private static ModuleConfiguration Configuration(EditorWorkspaceSnapshot workspace, IEnumerable<DeviceProfile> profiles,
            IEnumerable<DcsModifier> modifiers, IEnumerable<DcsBinding> bindings) =>
            new(workspace.Configuration.Module, profiles, modifiers, bindings, workspace.Configuration.Provenance);
        private static EditorWorkspaceSnapshot SetSource(EditorWorkspaceSnapshot workspace, string path, string value)
        {
            var sources = new Dictionary<string, string>(workspace.Sources ?? new Dictionary<string, string>(), StringComparer.Ordinal) { [path] = value };
            return workspace with { Sources = sources };
        }
        private static string UiSource(Guid id) => "config/ipia-ui-" + id.ToString("N") + ".lua";
        private static string Native(string file) => file.EndsWith(".diff.lua", StringComparison.OrdinalIgnoreCase) ? file[..^9] : file;
        private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private static void ValidateFile(string file)
        {
            if (string.IsNullOrWhiteSpace(file) || !file.EndsWith(".diff.lua", StringComparison.OrdinalIgnoreCase) ||
                file.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 || file.Contains("..", StringComparison.Ordinal) || file.Any(char.IsControl))
                throw new InvalidOperationException("Enter a valid filename ending in .diff.lua, without a directory.");
        }
        private static void ValidateMembership(WorkspaceManagement state, WorkspaceDevice device, WorkspaceDevice? current)
        {
            if (!Enum.IsDefined(device.Layer) || state.Devices.Any(x => x != current && x.Layer == device.Layer && x.FileName.Equals(device.FileName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("That filename already belongs to a device in this scope.");
        }
        private static EditorWorkspaceSnapshot RenameProfile(EditorWorkspaceSnapshot workspace, DeviceProfile profile, string file, string? instance, string? role)
        {
            var updated = new DeviceProfile(profile.Id, profile.DeviceId, instance, role ?? profile.DeviceId, file);
            workspace = workspace with {
                Configuration = Configuration(workspace, workspace.Configuration.Profiles.Select(x => x.Id == profile.Id ? updated : x), workspace.Configuration.Modifiers, workspace.Configuration.Bindings),
                Diagrams = workspace.Diagrams.Select(x => x.ProfileId == profile.Id ? x with { Instance = instance } : x).ToArray()
            };
            return Manifest(workspace, updated, profile.SourceName, false);
        }
        private static EditorWorkspaceSnapshot SetUiSupport(EditorWorkspaceSnapshot workspace, Guid id) => workspace with {
            Diagrams = workspace.Diagrams.Select(x => x.ProfileId == id ? x with { SupportsUiLayer = Describe(workspace).Devices.Any(d => d.ProfileId == id && d.Layer == AssignmentLayer.UiLayer) } : x).ToArray()
        };
        private static EditorWorkspaceSnapshot RenameModifier(EditorWorkspaceSnapshot workspace, AssignmentLayer layer, string oldName, string newName)
        {
            DcsBinding Rename(DcsBinding binding) => new(binding.ProfileId, binding.Section, binding.CommandId, binding.CommandName,
                binding.Disposition, binding.InputKey, binding.Reformers.Select(x => x == oldName ? newName : x), binding.Filter);
            var assignments = workspace.Assignments.Select(x => x.Slot.Layer == layer ? x with {
                Slot = x.Slot with { ModifierChord = string.Join("+", x.Slot.ModifierChord.Split('+').Select(n => n == oldName ? newName : n)) }
            } : x).ToArray();
            var labels = new Dictionary<string, string>(workspace.Labels, StringComparer.Ordinal);
            for (var i = 0; i < assignments.Length; i++)
                if (assignments[i].Slot.Key != workspace.Assignments[i].Slot.Key && labels.Remove(workspace.Assignments[i].Slot.Key, out var text))
                    labels[assignments[i].Slot.Key] = text;
            if (layer == AssignmentLayer.Module)
            {
                var modifiers = workspace.Configuration.Modifiers.Select(x => x.Name == oldName ? new DcsModifier(newName, x.Device, x.Key, x.Mode) : x);
                workspace = workspace with { Configuration = Configuration(workspace, workspace.Configuration.Profiles, modifiers, workspace.Configuration.Bindings.Select(Rename)) };
            }
            else
                foreach (var device in Describe(workspace).Devices.Where(x => x.Layer == layer))
                    workspace = SetSource(workspace, UiSource(device.ProfileId), ModuleLuaProfileRenderer.Write(DcsLuaParser.ParseProfile(workspace.Sources![UiSource(device.ProfileId)], device.ProfileId).Select(Rename).ToArray()));
            return workspace with { Assignments = assignments, Labels = labels };
        }
        private static EditorWorkspaceSnapshot Manifest(EditorWorkspaceSnapshot workspace, DeviceProfile profile, string? oldFile, bool remove)
        {
            var root = JsonNode.Parse(workspace.Sources!["config/kneeboard.json"])!.AsObject();
            var profiles = root["profiles"] as JsonObject;
            if (profiles is null) root["profiles"] = profiles = new JsonObject();
            var key = profiles.FirstOrDefault(x => oldFile is not null && Path.GetFileName(x.Value?.GetValue<string>()) == oldFile).Key;
            key ??= "device-" + profile.Id.ToString("N");
            var pages = root["pages"] as JsonArray;
            if (pages is null) root["pages"] = pages = new JsonArray();
            var affected = pages.Where(x => x?["profile"]?.GetValue<string>() == key).ToArray();
            var pageKeys = affected.Select(x => x!["file"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
            if (remove)
            {
                profiles.Remove(key);
                foreach (var page in affected) pages.Remove(page);
                workspace = workspace with { Pages = workspace.Pages.Where(x => !pageKeys.Contains(x.StableKey)).ToArray(), Images = workspace.Images.Where(x => !pageKeys.Contains(x.PageKey)).ToArray() };
            }
            else
            {
                profiles[key] = $"src/Config/Input/{workspace.Configuration.Module.DcsId}/joystick/{profile.SourceName}";
                if (affected.Length == 0)
                {
                    var pageKey = "device-" + profile.Id.ToString("N");
                    pages.Add(new JsonObject { ["file"] = pageKey, ["profile"] = key, ["title"] = workspace.Diagrams.Single(x => x.ProfileId == profile.Id).Title, ["kicker"] = "MODULE DEVICE", ["categoryLabels"] = new JsonObject() });
                    workspace = workspace with { Pages = [.. workspace.Pages, new KneeboardPage(Guid.NewGuid(), workspace.Configuration.Module.Id, null, KneeboardPageKind.Device, pageKey, profile.DeviceId, [], workspace.Pages.Count)] };
                }
            }
            return SetSource(workspace, "config/kneeboard.json", root.ToJsonString());
        }
        private static EditorWorkspaceSnapshot MfdLabels(EditorWorkspaceSnapshot workspace, Guid id, IReadOnlyDictionary<string, string> labels)
        {
            var profile = workspace.Configuration.Profiles.Single(x => x.Id == id);
            var root = JsonNode.Parse(workspace.Sources!["config/kneeboard.json"])!;
            var key = root["profiles"]!.AsObject().First(x => Path.GetFileName(x.Value!.GetValue<string>()) == profile.SourceName).Key;
            foreach (var page in root["pages"]!.AsArray().Where(x => x?["profile"]?.GetValue<string>() == key))
            {
                var categories = page!["categoryLabels"] as JsonObject;
                if (categories is null) page["categoryLabels"] = categories = new JsonObject();
                foreach (var name in new[] { "left", "right", "top", "middle" })
                    categories[name] = labels.GetValueOrDefault(name) ?? "";
            }
            var diagrams = workspace.Diagrams.Select(d => {
                if (d.ProfileId != id || string.IsNullOrEmpty(labels.GetValueOrDefault("middle"))) return d;
                var svg = XDocument.Parse(d.Svg);
                if (!svg.Descendants().Any(x => x.Attribute("id")?.Value == "meta-mfd-category-middle"))
                {
                    var top = svg.Descendants().FirstOrDefault(x => x.Attribute("id")?.Value == "meta-mfd-category-top");
                    var bottom = svg.Descendants().FirstOrDefault(x => x.Attribute("id")?.Value == "meta-mfd-category-bottom");
                    if (top is not null && bottom is not null)
                        svg.Root!.Add(new XElement(top.Name, new XAttribute("id", "meta-mfd-category-middle"),
                            new XAttribute("x", top.Attribute("x")!.Value),
                            new XAttribute("y", (double.Parse(top.Attribute("y")!.Value, System.Globalization.CultureInfo.InvariantCulture) + double.Parse(bottom.Attribute("y")!.Value, System.Globalization.CultureInfo.InvariantCulture)) / 2),
                            new XAttribute("text-anchor", "middle"), new XAttribute("fill", "#000000"), new XAttribute("font-size", "16")));
                }
                return d with { Svg = svg.ToString(SaveOptions.DisableFormatting) };
            }).ToArray();
            return SetSource(workspace with { Diagrams = diagrams }, "config/kneeboard.json", root.ToJsonString());
        }
        #endregion END private helpers
    }
}
