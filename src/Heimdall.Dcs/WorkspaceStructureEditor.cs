using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Heimdall.Application;
using Heimdall.Domain;

namespace Heimdall.Dcs
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
                new WorkspaceDevice(x.Id, AssignmentLayer.Module, x.SourceName, x.Instance, x.Role, DcsPhysicalIdentity.FromFileName(x.SourceName))).ToList();
            // Hardware templates describe capability, not membership in this module's UI Layer.
            // Keep authored usage visible even without a pinned source so validation cannot discard it.
            foreach (var diagram in workspace.Diagrams.Where(x => x.SupportsUiLayer &&
                (x.CanonicalUiLayerSourcePath is not null || x.Callouts.Count > 0 ||
                    workspace.Assignments.Any(a => a.Slot.ProfileId == x.ProfileId && a.Slot.Layer == AssignmentLayer.UiLayer))))
            {
                var profile = workspace.Configuration.Profiles.Single(x => x.Id == diagram.ProfileId);
                devices.Add(new(profile.Id, AssignmentLayer.UiLayer,
                    diagram.CanonicalUiLayerSourcePath is null ? profile.SourceName : Path.GetFileName(diagram.CanonicalUiLayerSourcePath), profile.Instance, profile.Role, DcsPhysicalIdentity.FromFileName(profile.SourceName)));
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
                    .Where(x => x.ProfileId != Guid.Empty || used.Contains(x.Name) || IsDefaultKeyboardModifier(x)));
            }
            return new(devices, modifiers);
        }
        /// <summary>Identifies the built-in left and right keyboard modifier keys.</summary>
        /// <param name="modifier">Imported or saved modifier definition.</param>
        /// <returns>True for a protected default keyboard modifier.</returns>
        public static bool IsDefaultKeyboardModifier(WorkspaceModifier modifier) =>
            modifier.NativeDevice.Equals("Keyboard", StringComparison.OrdinalIgnoreCase) &&
            modifier.Key is "LAlt" or "LCtrl" or "LShift" or "LWin" or "RAlt" or "RCtrl" or "RShift" or "RWin";
        /// <summary>Gets pinned shared hardware choices, including existing graphics for older workspaces.</summary>
        /// <param name="workspace">Stored shared assets.</param>
        /// <returns>Shared hardware choices.</returns>
        public static IReadOnlyList<WorkspaceHardware> Hardware(EditorWorkspaceSnapshot workspace)
        {
            if (workspace.Sources?.TryGetValue("config/ipia-hardware.json", out var source) == true)
                return JsonSerializer.Deserialize<WorkspaceHardware[]>(source) ?? [];
            return workspace.Diagrams.Select(x =>
            {
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
        /// <param name="kneeboardTitle">Optional module device page heading.</param>
        /// <param name="kneeboardSubtitle">Optional module device page subtitle.</param>
        /// <returns>Updated immutable draft.</returns>
        public static EditorWorkspaceSnapshot AddDevice(EditorWorkspaceSnapshot workspace, WorkspaceHardware hardware,
            AssignmentLayer layer, string fileName, string? instance, string? role, string? kneeboardTitle = null,
            string? kneeboardSubtitle = null)
        {
            GenericHardwareLayout.Validate(hardware.Diagram);
            if (hardware.Diagram.GenericCapabilities is not null)
            {
                var catalog = Hardware(workspace).Where(x => x.DeviceId != hardware.DeviceId).Append(hardware).ToArray();
                workspace = SetSource(workspace, "config/ipia-hardware.json", JsonSerializer.Serialize(catalog));
            }
            workspace = Prepare(workspace);
            var state = Describe(workspace);
            ValidateFile(fileName);
            instance = Optional(instance);
            role = Optional(role);
            var physicalGuid = DcsPhysicalIdentity.FromFileName(fileName.Trim());
            var identity = instance ?? role ?? physicalGuid?.ToString("D");
            var profile = FindExistingProfile(workspace, state, hardware, physicalGuid, identity);
            if (profile is not null && state.Devices.Any(x => x.ProfileId == profile.Id && x.Layer == layer))
                throw new InvalidOperationException("This physical device already belongs to that scope. Select a different physical unit or scope.");
            var isNew = profile is null;
            profile ??= new(Guid.NewGuid(), hardware.DeviceId, identity, role ?? hardware.DeviceId, fileName.Trim());
            var membership = new WorkspaceDevice(profile.Id, layer, fileName.Trim(), instance, role, physicalGuid);
            ValidateMembership(state, membership, null);
            if (isNew)
            {
                var diagram = hardware.Diagram with { ProfileId = profile.Id, Instance = instance ?? role, Callouts = [], SupportsUiLayer = layer == AssignmentLayer.UiLayer };
                workspace = workspace with
                {
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
            workspace = SetUiSupport(Store(workspace, state with { Devices = [.. state.Devices, membership] }), profile.Id);
            workspace = SetDevicePageText(workspace, profile.Id, layer, kneeboardTitle, kneeboardSubtitle);
            if (physicalGuid is { } reviewed)
                workspace = ModulePhysicalBindingReview.Confirm(workspace, profile.Id, reviewed);
            return workspace with { Diagrams = workspace.Diagrams.Select(x => x.ProfileId == profile.Id ? x with { Instance = instance ?? role ?? (isNew ? physicalGuid?.ToString("D") : x.Instance) } : x).ToArray() };
        }
        /// <summary>Updates identity, filename, and module MFD category labels.</summary>
        /// <param name="workspace">Current staged workspace.</param>
        /// <param name="device">Selected membership.</param>
        /// <param name="fileName">Diff.lua filename.</param>
        /// <param name="instance">Optional instance.</param>
        /// <param name="role">Optional role.</param>
        /// <param name="mfdLabels">Module MFD left, right, top, and bottom labels.</param>
        /// <param name="hardware">Optional replacement shared hardware; changing type requires an unused device.</param>
        /// <param name="kneeboardTitle">Optional module device page heading; null preserves its current title.</param>
        /// <param name="kneeboardSubtitle">Optional module device page subtitle; null preserves it and empty clears it.</param>
        /// <returns>Updated immutable draft.</returns>
        public static EditorWorkspaceSnapshot UpdateDevice(EditorWorkspaceSnapshot workspace, WorkspaceDevice device,
            string fileName, string? instance, string? role, IReadOnlyDictionary<string, string>? mfdLabels = null, WorkspaceHardware? hardware = null,
            string? kneeboardTitle = null, string? kneeboardSubtitle = null)
        {
            workspace = Prepare(workspace);
            var state = Describe(workspace);
            ValidateFile(fileName);
            instance = Optional(instance);
            role = Optional(role);
            var physicalGuid = device.PhysicalGuid ?? DcsPhysicalIdentity.FromFileName(device.FileName);
            var fileGuid = DcsPhysicalIdentity.FromFileName(fileName.Trim());
            if (physicalGuid is not null && fileGuid != physicalGuid)
                throw new InvalidOperationException("Keep this physical device GUID in the filename. Add a different physical device separately.");
            physicalGuid ??= fileGuid;
            var updated = device with { FileName = fileName.Trim(), Instance = instance, Role = role, PhysicalGuid = physicalGuid };
            ValidateMembership(state, updated, device);
            var profile = workspace.Configuration.Profiles.Single(x => x.Id == device.ProfileId);
            if (hardware is not null && hardware.DeviceId != profile.DeviceId)
                (workspace, profile) = ReplaceDeviceHardware(workspace, state, profile, hardware, instance, role);
            var identityChanged = instance != device.Instance || role != device.Role;
            var identity = identityChanged ? instance ?? role ?? physicalGuid?.ToString("D") : profile.Instance;
            var diagramInstance = identityChanged ? instance ?? role : workspace.Diagrams.Single(x => x.ProfileId == profile.Id).Instance;
            if (workspace.Configuration.Profiles.Any(x => x.Id != profile.Id && x.DeviceId == profile.DeviceId && x.Instance == identity))
                throw new InvalidOperationException("Another device already uses that instance or role.");
            workspace = RenameProfile(workspace, profile,
                device.Layer == AssignmentLayer.Module || !state.Devices.Any(x => x.ProfileId == profile.Id && x.Layer == AssignmentLayer.Module) ? updated.FileName : profile.SourceName, identity, role);
            workspace = UpdateDeviceMembership(workspace, state, profile, device, updated, instance, physicalGuid, diagramInstance);
            if (device.Layer == AssignmentLayer.Module && profile.DeviceId == "tm-mfd" && mfdLabels is not null)
                workspace = MfdLabels(workspace, profile.Id, mfdLabels);
            workspace = SetDevicePageText(workspace, profile.Id, device.Layer, kneeboardTitle, kneeboardSubtitle);
            return workspace;
        }
        /// <summary>Detaches physical identity in both scopes, retaining authored controls, assignments and drawing.</summary>
        public static EditorWorkspaceSnapshot UnlinkPhysicalDevice(EditorWorkspaceSnapshot workspace, WorkspaceDevice device)
        {
            workspace = Prepare(workspace);
            var state = Describe(workspace);
            if (!state.Devices.Contains(device)) throw new InvalidOperationException("Select the current device before unlinking.");
            var file = "Unlinked-" + device.ProfileId.ToString("N") + ".diff.lua";
            var profile = workspace.Configuration.Profiles.Single(x => x.Id == device.ProfileId);
            workspace = RenameProfile(workspace, profile, file, profile.Instance, profile.Role);
            return Store(workspace, state with
            {
                Devices = state.Devices.Select(x => x.ProfileId == device.ProfileId ? x with { FileName = file, PhysicalGuid = null } : x).ToArray(),
                Modifiers = state.Modifiers.Select(x => x.ProfileId == device.ProfileId ? x with { NativeDevice = Native(file) } : x).ToArray()
            });
        }
        /// <summary>Explicitly unlinks/restores illustration independently of physical ownership and assignments.</summary>
        public static EditorWorkspaceSnapshot SetDrawingLinked(EditorWorkspaceSnapshot workspace, Guid profileId, bool linked) =>
            workspace with { Diagrams = workspace.Diagrams.Select(x => x.ProfileId == profileId ? x with { DrawingUnlinked = !linked } : x).ToArray(), Images = [] };
        /// <summary>Explicitly rebinds both scopes to another physical unit while retaining all authored profile data.</summary>
        /// <param name="workspace">Current staged workspace.</param>
        /// <param name="device">Reviewed membership to rebind.</param>
        /// <param name="fileName">Actual GUID-qualified native filename.</param>
        /// <param name="instance">Human instance label.</param>
        /// <param name="role">Optional role.</param>
        /// <returns>Updated module-owned identities and modifier references.</returns>
        public static EditorWorkspaceSnapshot RebindDevice(EditorWorkspaceSnapshot workspace, WorkspaceDevice device,
            string fileName, string? instance, string? role)
        {
            workspace = Prepare(workspace);
            var state = Describe(workspace);
            if (!state.Devices.Contains(device))
                throw new InvalidOperationException("The device changed. Select it again before rebinding.");
            ValidateFile(fileName);
            var guid = DcsPhysicalIdentity.FromFileName(fileName.Trim())
                ?? throw new InvalidOperationException("Enter the physical unit's GUID-qualified native DCS filename.");
            instance = Optional(instance);
            role = Optional(role);
            var profile = workspace.Configuration.Profiles.Single(x => x.Id == device.ProfileId);
            var identity = instance ?? role ?? guid.ToString("D");
            if (workspace.Configuration.Profiles.Any(x => x.Id != profile.Id && x.DeviceId == profile.DeviceId && x.Instance == identity))
                throw new InvalidOperationException("Another device already uses that instance or role.");
            var memberships = state.Devices.Select(x => x.ProfileId == profile.Id
                ? x with { FileName = fileName.Trim(), PhysicalGuid = guid, Instance = instance, Role = role } : x).ToArray();
            foreach (var updated in memberships.Where(x => x.ProfileId == profile.Id))
                ValidateMembership(state, updated, state.Devices.Single(x => x.ProfileId == profile.Id && x.Layer == updated.Layer));
            var modifiers = state.Modifiers.Select(x => x.ProfileId == profile.Id
                ? x with { NativeDevice = Native(fileName.Trim()) } : x).ToArray();
            workspace = RenameProfile(workspace, profile, fileName.Trim(), identity, role);
            workspace = Store(workspace, state with { Devices = memberships, Modifiers = modifiers });
            workspace = ModulePhysicalBindingReview.Confirm(workspace, profile.Id, guid);
            return workspace with
            {
                Diagrams = workspace.Diagrams.Select(x => x.ProfileId == profile.Id ? x with { Device = DcsPhysicalIdentity.NameFromFileName(fileName.Trim()), Instance = instance ?? role ?? guid.ToString("D") } : x).ToArray(),
                Images = []
            };
        }
        /// <summary>Prunes explicitly approved snapshot devices and dependent chords in both scopes, without changing the source snapshot.</summary>
        public static EditorWorkspaceSnapshot RemoveDevicesForSnapshotImport(EditorWorkspaceSnapshot workspace, IReadOnlySet<Guid> removedProfiles)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            ArgumentNullException.ThrowIfNull(removedProfiles);
            if (removedProfiles.Count == 0) return workspace;
            if (removedProfiles.Any(id => workspace.Configuration.Profiles.All(p => p.Id != id)))
                throw new ArgumentException("Select only devices belonging to the snapshot.", nameof(removedProfiles));
            workspace = Prepare(workspace);
            var state = Describe(workspace);
            var retainedModifiers = state.Modifiers.Where(m => !removedProfiles.Contains(m.ProfileId)).ToArray();
            var removedNames = Enum.GetValues<AssignmentLayer>().ToDictionary(layer => layer, layer =>
                state.Modifiers.Where(m => m.Layer == layer && removedProfiles.Contains(m.ProfileId))
                    .Select(m => m.Name).Concat(state.Modifiers.Where(m => m.Layer == layer && removedProfiles.Contains(m.ProfileId))
                        .Select(m => m.SemanticLayer).Except(retainedModifiers.Where(m => m.Layer == layer).Select(m => m.SemanticLayer), StringComparer.Ordinal))
                    .ToHashSet(StringComparer.Ordinal));
            bool KeepChord(AssignmentLayer layer, string chord) => !chord.Split('+', StringSplitOptions.RemoveEmptyEntries).Any(removedNames[layer].Contains);
            bool KeepBinding(AssignmentLayer layer, DcsBinding binding) => !removedProfiles.Contains(binding.ProfileId) && !binding.Reformers.Any(removedNames[layer].Contains);
            var profiles = workspace.Configuration.Profiles.Where(p => !removedProfiles.Contains(p.Id)).ToArray();
            foreach (var profile in workspace.Configuration.Profiles.Where(p => removedProfiles.Contains(p.Id)).ToArray())
                workspace = Manifest(workspace, profile, profile.SourceName, true);
            var sources = new Dictionary<string, string>(workspace.Sources!, StringComparer.Ordinal);
            foreach (var profile in workspace.Configuration.Profiles)
            {
                var ui = UiSource(profile.Id);
                if (removedProfiles.Contains(profile.Id)) sources.Remove(ui);
                else if (sources.TryGetValue(ui, out var lua)) sources[ui] = ModuleLuaProfileRenderer.Write(
                    DcsLuaParser.ParseProfile(lua, profile.Id).Where(b => KeepBinding(AssignmentLayer.UiLayer, b)).ToArray());
                foreach (var layer in Enum.GetValues<AssignmentLayer>())
                {
                    var path = "config/ipia-axis-defaults/" + layer + "/" + profile.Id.ToString("N") + ".diff.lua";
                    if (removedProfiles.Contains(profile.Id)) sources.Remove(path);
                    else if (sources.TryGetValue(path, out var defaults))
                    {
                        var bindings = DcsLuaParser.ParseProfile(defaults, profile.Id).Where(b => KeepBinding(layer, b)).ToArray();
                        if (bindings.Length == 0) sources.Remove(path);
                        else sources[path] = ModuleLuaProfileRenderer.Write(bindings);
                    }
                }
            }
            var labels = workspace.Labels.Where(pair =>
            {
                var parts = pair.Key.Split('\u001f');
                return parts.Length != 6 || !Guid.TryParse(parts[1], out var profile) ||
                    !Enum.TryParse<AssignmentLayer>(parts[2], out var layer) ||
                    !removedProfiles.Contains(profile) && KeepChord(layer, parts[5]);
            }).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            workspace = workspace with
            {
                Configuration = Configuration(workspace, profiles,
                    retainedModifiers.Where(m => m.Layer == AssignmentLayer.Module).Select(m => new DcsModifier(m.Name, m.NativeDevice, m.Key, m.Mode)),
                    workspace.Configuration.Bindings.Where(b => KeepBinding(AssignmentLayer.Module, b))),
                Diagrams = workspace.Diagrams.Where(d => !removedProfiles.Contains(d.ProfileId)).ToArray(),
                Assignments = workspace.Assignments.Where(a => !removedProfiles.Contains(a.Slot.ProfileId) && KeepChord(a.Slot.Layer, a.Slot.ModifierChord)).ToArray(),
                Labels = labels, Sources = sources
            };
            return Store(workspace, new(state.Devices.Where(d => !removedProfiles.Contains(d.ProfileId)).ToArray(), retainedModifiers));
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
            state = state with { Devices = state.Devices.Where(x => x.ProfileId != device.ProfileId || x.Layer != device.Layer).ToArray() };
            workspace = Store(workspace, state);
            if (state.Devices.Any(x => x.ProfileId == device.ProfileId))
            {
                var remaining = state.Devices.First(x => x.ProfileId == device.ProfileId);
                var retained = workspace.Configuration.Profiles.Single(x => x.Id == device.ProfileId);
                workspace = RenameProfile(workspace, retained, remaining.FileName, retained.Instance, remaining.Role);
                return SetUiSupport(workspace, device.ProfileId);
            }
            var profile = workspace.Configuration.Profiles.Single(x => x.Id == device.ProfileId);
            workspace = Manifest(workspace, profile, profile.SourceName, true);
            return workspace with
            {
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
            if (current is not null && IsDefaultKeyboardModifier(current))
                throw new InvalidOperationException("Default keyboard modifier definitions cannot be changed.");
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
            if (IsDefaultKeyboardModifier(modifier))
                throw new InvalidOperationException("Default keyboard modifiers cannot be removed.");
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
            var bindings = workspace.Configuration.Bindings.Where(x => x.ProfileId != device.ProfileId ||
                x.Disposition == BindingDisposition.Removed || x.Section == BindingSection.Axis)
                .Select(x => x.ProfileId == device.ProfileId && x.Section == BindingSection.Axis ? ModuleLuaProfileRenderer.Removed(x) : x);
            workspace = workspace with
            {
                Configuration = Configuration(workspace, workspace.Configuration.Profiles, workspace.Configuration.Modifiers, bindings),
                Assignments = workspace.Assignments.Where(x => x.Slot.ProfileId != device.ProfileId).ToArray(),
                Labels = workspace.Labels.Where(x => !cleared.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
            };
            if (workspace.Sources!.TryGetValue(UiSource(device.ProfileId), out var source))
                workspace = SetSource(workspace, UiSource(device.ProfileId), ModuleLuaProfileRenderer.Write(
                    DcsLuaParser.ParseProfile(source, device.ProfileId).Where(x => x.Disposition == BindingDisposition.Removed || x.Section == BindingSection.Axis)
                        .Select(x => x.Section == BindingSection.Axis ? ModuleLuaProfileRenderer.Removed(x) : x).ToArray()));
            return workspace;
        }
        /// <summary>Clears one device's selected scope and modifier chord, including unmapped keys and axes.</summary>
        /// <param name="workspace">Current staged workspace.</param>
        /// <param name="device">Device and scope selected for clearing.</param>
        /// <param name="modifierChord">Modifier names, or empty for Base.</param>
        /// <returns>Updated reversible draft preserving other scopes and chords.</returns>
        public static EditorWorkspaceSnapshot ClearLayerAssignments(EditorWorkspaceSnapshot workspace, WorkspaceDevice device, string modifierChord)
        {
            workspace = Prepare(workspace);
            var chord = modifierChord.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal).ToArray();
            var normalized = string.Join("+", chord);
            var cleared = workspace.Assignments.Where(x => x.Slot.ProfileId == device.ProfileId && x.Slot.Layer == device.Layer &&
                x.Slot.ModifierChord == normalized).Select(x => x.Slot.Key).ToHashSet(StringComparer.Ordinal);
            bool Keep(DcsBinding binding) => binding.Disposition == BindingDisposition.Removed ||
                !binding.Reformers.Order(StringComparer.Ordinal).SequenceEqual(chord, StringComparer.Ordinal);
            workspace = workspace with
            {
                Assignments = workspace.Assignments.Where(x => !cleared.Contains(x.Slot.Key)).ToArray(),
                Labels = workspace.Labels.Where(x => !cleared.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
            };
            if (device.Layer == AssignmentLayer.Module)
                workspace = workspace with
                {
                    Configuration = Configuration(workspace, workspace.Configuration.Profiles, workspace.Configuration.Modifiers,
                    workspace.Configuration.Bindings.Where(x => x.ProfileId != device.ProfileId || Keep(x) || x.Section == BindingSection.Axis)
                        .Select(x => x.ProfileId == device.ProfileId && !Keep(x) && x.Section == BindingSection.Axis ? ModuleLuaProfileRenderer.Removed(x) : x))
                };
            else if (workspace.Sources!.TryGetValue(UiSource(device.ProfileId), out var source))
                workspace = SetSource(workspace, UiSource(device.ProfileId), ModuleLuaProfileRenderer.Write(
                    DcsLuaParser.ParseProfile(source, device.ProfileId).Where(x => Keep(x) || x.Section == BindingSection.Axis)
                        .Select(x => !Keep(x) && x.Section == BindingSection.Axis ? ModuleLuaProfileRenderer.Removed(x) : x).ToArray()));
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
        private static EditorWorkspaceSnapshot UpdateDeviceMembership(EditorWorkspaceSnapshot workspace, WorkspaceManagement state, DeviceProfile profile,
            WorkspaceDevice device, WorkspaceDevice updated, string? instance, Guid? physicalGuid, string? diagramInstance)
        {
            var modifiers = state.Modifiers.Select(x => x.ProfileId == profile.Id && x.Layer == device.Layer
                ? x with { NativeDevice = Native(updated.FileName) } : x).ToArray();
            workspace = Store(workspace, state with { Devices = state.Devices.Select(x => x.ProfileId == device.ProfileId && x.Layer == device.Layer ? updated : x.ProfileId == profile.Id ? x with { Instance = instance, PhysicalGuid = physicalGuid } : x).ToArray(), Modifiers = modifiers });
            workspace = workspace with { Diagrams = workspace.Diagrams.Select(x => x.ProfileId == profile.Id ? x with { Instance = diagramInstance } : x).ToArray() };
            return workspace;
        }
        private static EditorWorkspaceSnapshot SetDevicePageText(EditorWorkspaceSnapshot workspace, Guid profileId, AssignmentLayer layer, string? title, string? subtitle)
        {
            if (layer == AssignmentLayer.Module && title is not null) workspace = SetKneeboardTitle(workspace, profileId, title);
            if (layer == AssignmentLayer.Module && subtitle is not null) workspace = SetKneeboardSubtitle(workspace, profileId, subtitle);
            return workspace;
        }
        private static (EditorWorkspaceSnapshot Workspace, DeviceProfile Profile) ReplaceDeviceHardware(EditorWorkspaceSnapshot workspace, WorkspaceManagement state, DeviceProfile profile, WorkspaceHardware hardware, string? instance, string? role)
        {
            if (EffectiveBindings(workspace).Any(x => x.ProfileId == profile.Id) || state.Modifiers.Any(x => x.ProfileId == profile.Id))
                throw new InvalidOperationException("Clear assignments and move or remove modifiers before changing shared hardware.");
            var replacement = new DeviceProfile(profile.Id, hardware.DeviceId, instance ?? role, role ?? hardware.DeviceId, profile.SourceName);
            workspace = workspace with
            {
                Configuration = Configuration(workspace, workspace.Configuration.Profiles.Select(x => x.Id == profile.Id ? replacement : x),
                    workspace.Configuration.Modifiers, workspace.Configuration.Bindings.Where(x => x.ProfileId != profile.Id)),
                Diagrams = workspace.Diagrams.Select(x => x.ProfileId == profile.Id
                    ? hardware.Diagram with
                    {
                        ProfileId = profile.Id,
                        Instance = instance ?? role,
                        Callouts = [],
                        SupportsUiLayer = state.Devices.Any(d => d.ProfileId == profile.Id && d.Layer == AssignmentLayer.UiLayer)
                    } : x).ToArray()
            };
            workspace = SetSource(workspace, UiSource(profile.Id), ModuleLuaProfileRenderer.Write([]));
            workspace = Manifest(workspace, replacement, profile.SourceName, true);
            workspace = Manifest(workspace, replacement, null, false);
            return (workspace, replacement);
        }
        private static DeviceProfile? FindExistingProfile(EditorWorkspaceSnapshot workspace, WorkspaceManagement state, WorkspaceHardware hardware, Guid? physicalGuid, string? identity)
        {
            return workspace.Configuration.Profiles.FirstOrDefault(x => x.DeviceId == hardware.DeviceId &&
                (physicalGuid is not null ? state.Devices.Any(d => d.ProfileId == x.Id && (d.PhysicalGuid ?? DcsPhysicalIdentity.FromFileName(d.FileName)) == physicalGuid) : x.Instance == identity));
        }
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
            if (device.PhysicalGuid is { } guid && state.Devices.Any(x => x.ProfileId != device.ProfileId &&
                (x.PhysicalGuid ?? DcsPhysicalIdentity.FromFileName(x.FileName)) == guid))
                throw new InvalidOperationException("This physical GUID already belongs to another shared hardware profile.");
            if (!Enum.IsDefined(device.Layer) || state.Devices.Any(x => (current is null || x.ProfileId != current.ProfileId || x.Layer != current.Layer) && (x.Layer == device.Layer || x.ProfileId != device.ProfileId) && x.FileName.Equals(device.FileName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("That filename already belongs to another device or instance.");
        }
        private static EditorWorkspaceSnapshot RenameProfile(EditorWorkspaceSnapshot workspace, DeviceProfile profile, string file, string? instance, string? role)
        {
            var updated = new DeviceProfile(profile.Id, profile.DeviceId, instance, role ?? profile.DeviceId, file);
            workspace = workspace with
            {
                Configuration = Configuration(workspace, workspace.Configuration.Profiles.Select(x => x.Id == profile.Id ? updated : x), workspace.Configuration.Modifiers, workspace.Configuration.Bindings),
                Diagrams = workspace.Diagrams.Select(x => x.ProfileId == profile.Id ? x with { Instance = instance } : x).ToArray()
            };
            return Manifest(workspace, updated, profile.SourceName, false);
        }
        private static EditorWorkspaceSnapshot SetUiSupport(EditorWorkspaceSnapshot workspace, Guid id) => workspace with
        {
            Diagrams = workspace.Diagrams.Select(x => x.ProfileId == id ? x with { SupportsUiLayer = Describe(workspace).Devices.Any(d => d.ProfileId == id && d.Layer == AssignmentLayer.UiLayer) } : x).ToArray()
        };
        private static EditorWorkspaceSnapshot RenameModifier(EditorWorkspaceSnapshot workspace, AssignmentLayer layer, string oldName, string newName)
        {
            DcsBinding Rename(DcsBinding binding) => new(binding.ProfileId, binding.Section, binding.CommandId, binding.CommandName,
                binding.Disposition, binding.InputKey, binding.Reformers.Select(x => x == oldName ? newName : x), binding.Filter);
            var assignments = workspace.Assignments.Select(x => x.Slot.Layer == layer ? x with
            {
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
            if (remove) return ModuleKneeboardPageEditor.RemoveDevicePages(workspace, profile);
            var root = JsonNode.Parse(workspace.Sources!["config/kneeboard.json"])!.AsObject();
            var profiles = root["profiles"] as JsonObject;
            if (profiles is null) root["profiles"] = profiles = new JsonObject();
            var keys = profiles.Where(x => oldFile is not null &&
                (x.Value?.GetValue<string>() ?? "").Replace('\\', '/').Split('/')[^1].Equals(oldFile, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
            if (keys.Count == 0) keys.Add("device-" + profile.Id.ToString("N"));
            var key = keys.First();
            var pages = root["pages"] as JsonArray;
            if (pages is null) root["pages"] = pages = new JsonArray();
            var affected = pages.Where(x => keys.Contains(x?["profile"]?.GetValue<string>() ?? "")).ToArray();
            foreach (var alias in keys)
                profiles[alias] = $"src/Config/Input/{workspace.Configuration.Module.DcsId}/joystick/{profile.SourceName}";
            if (affected.Length == 0)
            {
                var pageKey = "device-" + profile.Id.ToString("N");
                pages.Add(new JsonObject { ["file"] = pageKey, ["profile"] = key, ["title"] = workspace.Diagrams.Single(x => x.ProfileId == profile.Id).Title, ["kicker"] = "MODULE DEVICE", ["categoryLabels"] = new JsonObject() });
                workspace = workspace with { Pages = [.. workspace.Pages, new KneeboardPage(Guid.NewGuid(), workspace.Configuration.Module.Id, null, KneeboardPageKind.Device, pageKey, profile.DeviceId, [], workspace.Pages.Count)] };
            }
            return SetSource(workspace, "config/kneeboard.json", root.ToJsonString());
        }
        private static EditorWorkspaceSnapshot SetKneeboardTitle(EditorWorkspaceSnapshot workspace, Guid id, string title)
        {
            if (string.IsNullOrWhiteSpace(title) || title.Any(char.IsControl))
                throw new InvalidOperationException("Enter a kneeboard title without control characters.");
            var profile = workspace.Configuration.Profiles.Single(x => x.Id == id);
            var root = JsonNode.Parse(workspace.Sources!["config/kneeboard.json"])!;
            var key = root["profiles"]!.AsObject().First(x => Path.GetFileName(x.Value!.GetValue<string>()) == profile.SourceName).Key;
            var affected = root["pages"]!.AsArray().Where(x => x?["profile"]?.GetValue<string>() == key).ToArray();
            var pageKeys = affected.Select(x => x!["file"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
            foreach (var page in affected)
                page!["title"] = title.Trim();
            workspace = workspace with
            {
                Pages = workspace.Pages.Select(x => pageKeys.Contains(x.StableKey)
                ? x with { Title = title.Trim() } : x).ToArray()
            };
            return SetSource(workspace, "config/kneeboard.json", root.ToJsonString());
        }
        private static EditorWorkspaceSnapshot SetKneeboardSubtitle(EditorWorkspaceSnapshot workspace, Guid id, string subtitle)
        {
            if (subtitle.Any(char.IsControl))
                throw new InvalidOperationException("Enter a kneeboard subtitle without control characters.");
            var profile = workspace.Configuration.Profiles.Single(x => x.Id == id);
            var root = JsonNode.Parse(workspace.Sources!["config/kneeboard.json"])!;
            var key = root["profiles"]!.AsObject().First(x => Path.GetFileName(x.Value!.GetValue<string>()) == profile.SourceName).Key;
            foreach (var page in root["pages"]!.AsArray().Where(x => x?["profile"]?.GetValue<string>() == key))
                page!["kicker"] = subtitle.Trim();
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
                categories.Remove("middle");
                categories.Remove("mfd-category-middle");
                foreach (var name in new[] { "left", "right", "top", "bottom" })
                {
                    categories.Remove("mfd-category-" + name);
                    categories[name] = labels.GetValueOrDefault(name) ?? "";
                }
                if (page["layers"] is JsonArray layers)
                    foreach (var layer in layers)
                        if (layer?["categoryLabels"] is JsonObject layerCategories)
                        {
                            foreach (var name in new[] { "left", "right", "top", "bottom" })
                            {
                                layerCategories.Remove(name);
                                layerCategories.Remove("mfd-category-" + name);
                            }
                            layerCategories.Remove("middle");
                            layerCategories.Remove("mfd-category-middle");
                        }
            }
            var diagrams = workspace.Diagrams.Select(d =>
            {
                if (d.ProfileId != id) return d;
                var svg = XDocument.Parse(d.Svg);
                foreach (var obsolete in svg.Descendants().Where(x => x.Attribute("id")?.Value == "meta-mfd-category-middle").ToArray())
                    obsolete.Remove();
                return d with { Svg = svg.ToString(SaveOptions.DisableFormatting) };
            }).ToArray();
            return SetSource(workspace with { Diagrams = diagrams }, "config/kneeboard.json", root.ToJsonString());
        }
        #endregion END private helpers
    }
}
