using System.Text;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Suppresses unassigned aircraft axes on captured physical controllers, including unused units.</summary>
    public static class ModuleAxisCleanup
    {
        private const string SourcePath = "config/ipia-axis-cleanup-devices.json";
        private static readonly string[] AxisKeys =
            ["JOY_X", "JOY_Y", "JOY_Z", "JOY_RX", "JOY_RY", "JOY_RZ", "JOY_SLIDER1", "JOY_SLIDER2"];
        #region BEGIN Public
        /// <summary>Pins observed controller filenames without adding devices, artwork or assignments.</summary>
        /// <param name="workspace">Created or imported module draft.</param>
        /// <param name="fileNames">Connected or captured native DCS filenames.</param>
        /// <returns>Draft retaining previous offline identities and all authored state.</returns>
        public static EditorWorkspaceSnapshot CaptureDevices(EditorWorkspaceSnapshot workspace, IEnumerable<string> fileNames)
        {
            var files = ReadDevices(workspace).ToDictionary(PhysicalId);
            foreach (var file in fileNames) files[PhysicalId(file)] = file;
            if (files.Count == 0) return workspace;
            var json = JsonSerializer.Serialize(files.Values.Order(StringComparer.Ordinal).ToArray());
            if (workspace.Sources?.GetValueOrDefault(SourcePath) == json) return workspace;
            var sources = new Dictionary<string, string>(workspace.Sources ?? new Dictionary<string, string>(), StringComparer.Ordinal) { [SourcePath] = json };
            return workspace with { Sources = sources };
        }
        /// <summary>Adds axis removals using known module commands while preserving rendered active combinations.</summary>
        /// <param name="workspace">Frozen module state with captured controller identities.</param>
        /// <param name="payload">Generated Lua files to complete before archive creation.</param>
        public static void CompletePayload(EditorWorkspaceSnapshot workspace, Dictionary<string, byte[]> payload)
        {
            if (workspace.Sources?.ContainsKey(SourcePath) != true) return;
            var aircraftProfiles = WorkspaceStructureEditor.Describe(workspace).Devices
                .Where(x => x.Layer == AssignmentLayer.Module).Select(x => x.ProfileId).ToHashSet();
            // Command IDs come from the module catalog; no aircraft-specific IDs or names are inferred.
            var commands = workspace.ModuleCommands.Where(x => x.IsAxis)
                .Concat(workspace.Assignments.Where(x => x.Slot.Layer == AssignmentLayer.Module && x.Command.IsAxis)
                    .Select(x => new CommandPaletteEntry(x.Command.Id, x.Command.Name, "Axes", true)))
                .Concat(workspace.Configuration.Bindings.Where(x => x.Section == BindingSection.Axis && aircraftProfiles.Contains(x.ProfileId))
                    .Select(x => new CommandPaletteEntry(x.CommandId, x.CommandName, "Axes", true)))
                .DistinctBy(x => x.Id).ToArray();
            if (commands.Length == 0) return;
            var prefix = "Config/Input/" + workspace.Configuration.Module.DcsId + "/joystick/";
            var files = ReadDevices(workspace).ToDictionary(PhysicalId);
            foreach (var path in payload.Keys.Where(x => x.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
            {
                var file = path[prefix.Length..];
                if (DcsPhysicalIdentity.FromFileName(file) is null) continue;
                files[PhysicalId(file)] = file;
            }
            foreach (var file in files.Values.Order(StringComparer.Ordinal))
            {
                var path = prefix + file;
                var profileId = workspace.Configuration.Profiles.FirstOrDefault(x =>
                    DcsPhysicalIdentity.FromFileName(x.SourceName) == PhysicalId(file))?.Id ?? PhysicalId(file);
                var bindings = payload.TryGetValue(path, out var bytes)
                    ? DcsLuaParser.ParseProfile(Encoding.UTF8.GetString(bytes), profileId) : [];
                var active = bindings.Where(x => x.Section == BindingSection.Axis && x.Disposition != BindingDisposition.Removed)
                    .Select(Identity).ToHashSet(StringComparer.Ordinal);
                var existing = bindings.Select(Identity).ToHashSet(StringComparer.Ordinal);
                var removals = new List<DcsBinding>();
                foreach (var command in commands)
                    foreach (var key in AxisKeys)
                    {
                        var removal = new DcsBinding(profileId, BindingSection.Axis, command.Id, command.Name,
                            BindingDisposition.Removed, key, []);
                        if (!active.Contains(Identity(removal)) && existing.Add(Identity(removal))) removals.Add(removal);
                    }
                // Only base defaults are suppressed; explicit shifted axes and sparse filter values remain intact.
                payload[path] = Encoding.UTF8.GetBytes(ModuleLuaProfileRenderer.Write(bindings.Concat(removals).ToArray()));
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string[] ReadDevices(EditorWorkspaceSnapshot workspace)
        {
            if (workspace.Sources is null || !workspace.Sources.TryGetValue(SourcePath, out var source)) return [];
            var files = JsonSerializer.Deserialize<string[]>(source)
                ?? throw new InvalidDataException("Missing axis cleanup device identities.");
            if (files.Select(PhysicalId).Distinct().Count() != files.Length)
                throw new InvalidDataException("Duplicate physical axis cleanup identities.");
            return files;
        }
        private static Guid PhysicalId(string file)
        {
            if (string.IsNullOrWhiteSpace(file) || file.IndexOfAny(['/', '\\', ':']) >= 0 || file.Contains("..", StringComparison.Ordinal) ||
                file.Any(char.IsControl) || DcsPhysicalIdentity.FromFileName(file) is not { } id)
                throw new InvalidDataException("Axis cleanup requires a safe GUID-qualified native DCS filename.");
            return id;
        }
        private static string Identity(DcsBinding binding) => binding.CommandId + "\u001f" + binding.InputKey + "\u001f" +
            string.Join("+", binding.Reformers);
        #endregion END private helpers
    }
}
