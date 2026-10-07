using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Creates isolated aircraft workspaces from pinned internal hardware and saved shared defaults.</summary>
    public static class NewModuleWorkspaceFactory
    {
        #region BEGIN Public
        /// <summary>Creates an unsaved module without a consumer repository or Saved Games source.</summary>
        /// <param name="dcsId">Exact DCS input folder identifier.</param>
        /// <param name="displayName">Module display name.</param>
        /// <param name="defaults">Verified saved default revision.</param>
        /// <param name="hardwareSources">Pinned internal hardware catalog and source evidence.</param>
        /// <param name="emptyDefaults">Whether to start with no inherited devices, modifiers or assignments.</param>
        /// <returns>Independent module draft ready for device selection and SQLite persistence.</returns>
        public static EditorWorkspaceSnapshot Create(string dcsId, string displayName, SharedUiLayerWorkspace defaults,
            IReadOnlyDictionary<string, string> hardwareSources, bool emptyDefaults = false)
        {
            ValidateIdentity(dcsId, displayName);
            if (!emptyDefaults) defaults = SharedUiLayerRevisionReference.Freeze(defaults);
            var hardware = JsonSerializer.Deserialize<WorkspaceHardware[]>(hardwareSources["config/ipia-hardware.json"])
                ?? throw new InvalidDataException("The internal hardware catalog is missing.");
            var resolved = SharedUiLayerHardwareMapping.Resolve(defaults, hardware);
            var module = new DcsModule(Guid.NewGuid(), dcsId, displayName);
            var profiles = new List<DeviceProfile>();
            var diagrams = new List<UiLayerDiagram>();
            var assignments = new List<DraftAssignment>();
            var devices = new List<WorkspaceDevice>();
            var sources = new Dictionary<string, string>(hardwareSources, StringComparer.Ordinal);
            foreach (var source in defaults.Sources.Where(x => !emptyDefaults || !x.Key.StartsWith("assets/shared/ui-layer/input/", StringComparison.Ordinal)))
                sources[source.Key] = source.Value;
            if (emptyDefaults)
            {
                foreach (var path in sources.Keys.Where(x => x.StartsWith("assets/shared/ui-layer/input/", StringComparison.Ordinal)).ToArray()) sources.Remove(path);
                sources["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = "local modifiers = {}\nreturn modifiers\n";
            }
            var catalog = StableIdentity.Create("catalog", "dcs-common-ui-layer");
            var commands = SharedUiLayerCommandEditor.Commands(defaults).Select(x =>
                new DcsCommandDefinition(StableIdentity.Create("ui-function", x.Id), catalog, x.Command, x.Label, x.Category, false)).ToList();
            foreach (var path in emptyDefaults ? [] : SharedUiLayerBindingEditor.Profiles(defaults))
            {
                var bindings = SharedUiLayerBindingEditor.Bindings(defaults, path);
                foreach (var binding in bindings.Where(x => x.Disposition != BindingDisposition.Removed))
                    if (!commands.Any(x => x.DcsCommandId == binding.CommandId))
                        commands.Add(new(StableIdentity.Create("ui-command", binding.CommandId), catalog, binding.CommandId,
                            binding.CommandName, "Imported defaults", binding.Section == BindingSection.Axis));
                if (!resolved.TryGetValue(path, out var graphic))
                    continue;
                var choice = hardware.First(x => x.Diagram.Path == graphic.Path);
                var profile = new DeviceProfile(Guid.NewGuid(), choice.DeviceId, DcsPhysicalIdentity.FromFileName(Path.GetFileName(path))?.ToString("D") ?? Path.GetFileName(path), "UI Layer", Path.GetFileName(path));
                profiles.Add(profile);
                diagrams.Add(graphic with { ProfileId = profile.Id, CanonicalUiLayerSourcePath = path, SupportsUiLayer = true });
                devices.Add(new(profile.Id, AssignmentLayer.UiLayer, profile.SourceName, null, "UI Layer",
                    DcsPhysicalIdentity.FromFileName(profile.SourceName)));
                sources["config/ipia-ui-" + profile.Id.ToString("N") + ".lua"] = sources[path];
                assignments.AddRange(bindings.Where(x => x.Disposition != BindingDisposition.Removed &&
                    graphic.Controls.Any(c => c.IsAxis == (x.Section == BindingSection.Axis) && DcsInputKey.SamePhysicalInput(c.InputKey, x.InputKey)))
                    .Select(x => new DraftAssignment(new(module.Id, profile.Id, AssignmentLayer.UiLayer, x.Section, x.InputKey,
                        string.Join("+", x.Reformers)), new(x.CommandId, x.CommandName, x.Section == BindingSection.Axis, AssignmentLayer.UiLayer))
                        { Filter = x.Filter }));
            }
            var modifiers = (emptyDefaults ? [] : SharedUiLayerModifierEditor.Modifiers(defaults)).Select(x => new WorkspaceModifier(AssignmentLayer.UiLayer,
                profiles.FirstOrDefault(p => p.SourceName[..^9].Equals(x.Device, StringComparison.OrdinalIgnoreCase))?.Id ?? Guid.Empty,
                x.Name, x.Key, x.Name, x.Mode, x.Device)).ToArray();
            sources[WorkspaceManagement.SourcePath] = JsonSerializer.Serialize(new WorkspaceManagement(devices, modifiers));
            sources["config/kneeboard.json"] = """{"includeUiLayer":true,"profiles":{},"pages":[],"summaryPages":[],"labels":{}}""";
            sources["config/ipia-default-choice.json"] = JsonSerializer.Serialize(new { schemaVersion = 1, choice = emptyDefaults ? "empty" : "shared-revision" });
            sources["config/ipia-created-module.json"] = JsonSerializer.Serialize(new { schemaVersion = 1, moduleId = module.Id, dcsId = module.DcsId });
            var result = new EditorWorkspaceSnapshot(new(module, profiles, [], [], "Created in IPIA"), [], [], diagrams, [],
                commands, assignments, new Dictionary<string, string>(), sources) { SharedDefault = emptyDefaults ? null : defaults };
            SnapshotLuaPayloadRenderer.ValidateUiLayerProfiles(result);
            return result;
        }

        /// <summary>Rejects unsafe or reserved DCS input folder identities before creating a draft.</summary>
        /// <param name="dcsId">DCS input folder identifier.</param>
        /// <param name="displayName">Module display name.</param>
        public static void ValidateIdentity(string dcsId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(dcsId) || dcsId != dcsId.Trim() || dcsId is "." or ".." ||
                dcsId.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 || dcsId.Any(char.IsControl) ||
                dcsId.EndsWith('.') || dcsId.Equals("UiLayer", StringComparison.OrdinalIgnoreCase) ||
                new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                    "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(dcsId.Split('.')[0], StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException("Enter a valid aircraft DCS input folder name, without a directory or reserved name.", nameof(dcsId));
            if (string.IsNullOrWhiteSpace(displayName) || displayName.Any(char.IsControl))
                throw new ArgumentException("Enter a module display name without control characters.", nameof(displayName));
        }
        #endregion END Public
    }
}
