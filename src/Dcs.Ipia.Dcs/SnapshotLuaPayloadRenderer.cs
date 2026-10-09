using System.Text;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Collects the saved workspace's DCS Lua files for an OvGME payload.</summary>
    public static class SnapshotLuaPayloadRenderer
    {
        private const string SharedRoot = "assets/shared/ui-layer/input/UiLayer/";
        private static readonly string[] StandardKeyboardModifiers =
            ["LAlt", "LCtrl", "LShift", "LWin", "RAlt", "RCtrl", "RShift", "RWin"];
        #region BEGIN Public
        /// <summary>Renders module and UI Layer profiles with their effective modifier tables.</summary>
        /// <param name="workspace">Frozen saved workspace.</param>
        /// <returns>Package relative paths and UTF-8 Lua bytes.</returns>
        public static IReadOnlyDictionary<string, byte[]> Generate(EditorWorkspaceSnapshot workspace)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            ModulePhysicalBindingReview.Validate(workspace);
            workspace = PinnedUiLayerProfileRecovery.Restore(workspace);
            if (WorkspaceManagement.Read(workspace) is { } managed)
                return ReconcileNativeDeviceNames(GenerateManaged(workspace, managed));
            var config = workspace.Configuration;
            var moduleId = config.Module.DcsId;
            SafeSegment(moduleId);
            if (workspace.Diagrams.GroupBy(x => x.ProfileId).Any(x => x.Count() > 1))
                throw new InvalidDataException("Multiple diagrams resolve to one profile.");
            var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var modulePrefix = $"Config/Input/{moduleId}/";
            foreach (var profile in config.Profiles)
            {
                SafeFile(profile.SourceName);
                Add(result, modulePrefix + "joystick/" + profile.SourceName,
                    ModuleLuaProfileRenderer.Render(workspace, profile.Id));
            }
            if (workspace.Sources is null ||
                !workspace.Sources.TryGetValue(SharedRoot + "modifiers.lua", out var source))
                throw new InvalidDataException("The saved workspace lacks canonical keyboard modifiers; re-import it.");
            var modifiers = DcsLuaParser.ParseModifiers(source);
            var moduleNames = config.Modifiers.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var moduleModifiers = config.Modifiers.Concat(
                modifiers.Where(x => x.Device.Equals("Keyboard", StringComparison.OrdinalIgnoreCase) &&
                                     !moduleNames.Contains(x.Name))).ToArray();
            Add(result, modulePrefix + "modifiers.lua", WriteModifiers(
                CompleteModifiers(workspace, AssignmentLayer.Module, moduleModifiers, result)));
            var uiProfiles = workspace.Diagrams.Where(x => x.SupportsUiLayer &&
                                                            x.CanonicalUiLayerSourcePath is not null).ToArray();
            ValidateUiLayerProfiles(workspace);
            if (uiProfiles.Length == 0)
            {
                ValidateModifierReferences(result);
                return ReconcileNativeDeviceNames(result);
            }
            var removed = UiLayerLuaProfileRenderer.RemovedModifiers(workspace.Sources);
            if (removed.Any(x => !modifiers.Any(y => y.Name == x)))
                throw new InvalidDataException("An overlay removes an unknown canonical UI Layer modifier.");
            foreach (var diagram in uiProfiles)
            {
                var path = diagram.CanonicalUiLayerSourcePath!;
                if (!path.StartsWith(SharedRoot, StringComparison.Ordinal) ||
                    !path.EndsWith(".diff.lua", StringComparison.Ordinal) ||
                    path.Contains("..", StringComparison.Ordinal))
                    throw new InvalidDataException("Unsafe pinned UI Layer profile path.");
                var relative = path[SharedRoot.Length..];
                var parts = relative.Split('/');
                if (parts.Length != 2 || parts[0] is not ("joystick" or "keyboard"))
                    throw new InvalidDataException("Unexpected UI Layer profile path.");
                SafeFile(parts[1]);
                Add(result, "Config/Input/UiLayer/" + relative,
                    UiLayerLuaProfileRenderer.Render(workspace, diagram.ProfileId));
            }
            Add(result, "Config/Input/UiLayer/modifiers.lua",
                WriteModifiers(CompleteModifiers(workspace, AssignmentLayer.UiLayer,
                    modifiers.Where(x => !removed.Contains(x.Name)), result)));
            ValidateModifierReferences(result);
            return ReconcileNativeDeviceNames(result);
        }
        /// <summary>Reports saved UI assignments that cannot resolve to a pinned physical profile.</summary>
        /// <param name="workspace">Workspace to validate before saving or packaging.</param>
        public static void ValidateUiLayerProfiles(EditorWorkspaceSnapshot workspace)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            workspace = PinnedUiLayerProfileRecovery.Restore(workspace);
            if (WorkspaceManagement.Read(workspace) is not null)
                return;
            var uiProfiles = workspace.Diagrams.Where(x => x.SupportsUiLayer &&
                x.CanonicalUiLayerSourcePath is not null).ToArray();
            var unresolved = workspace.Assignments.Where(x => x.Slot.Layer == AssignmentLayer.UiLayer)
                .Select(x => x.Slot.ProfileId).Distinct()
                .Where(id => uiProfiles.All(x => x.ProfileId != id))
                .Select(id => workspace.Configuration.Profiles.FirstOrDefault(x => x.Id == id) is { } profile
                    ? $"{profile.DeviceId} ({profile.SourceName})" : id.ToString("D"))
                .Order(StringComparer.Ordinal).ToArray();
            if (unresolved.Length > 0)
                throw new InvalidDataException("Saved UI Layer assignments have no pinned profile: " +
                    string.Join("; ", unresolved) + ". Re-import with the matching shared UI Layer sources.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static Dictionary<string, byte[]> GenerateManaged(EditorWorkspaceSnapshot workspace, WorkspaceManagement managed)
        {
            SafeSegment(workspace.Configuration.Module.DcsId);
            if (managed.Devices.Any(x => !Enum.IsDefined(x.Layer) || workspace.Configuration.Profiles.All(p => p.Id != x.ProfileId)) ||
                managed.Devices.GroupBy(x => (x.ProfileId, x.Layer)).Any(x => x.Count() > 1) ||
                workspace.Assignments.Any(x => !managed.Devices.Any(d => d.ProfileId == x.Slot.ProfileId && d.Layer == x.Slot.Layer)))
                throw new InvalidDataException("Managed device membership differs from saved assignments or profiles.");
            var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var device in managed.Devices)
            {
                SafeFile(device.FileName);
                var folder = device.Layer == AssignmentLayer.Module ? workspace.Configuration.Module.DcsId : "UiLayer";
                var content = device.Layer == AssignmentLayer.Module
                    ? ModuleLuaProfileRenderer.Render(workspace, device.ProfileId)
                    : UiLayerLuaProfileRenderer.Render(workspace, device.ProfileId);
                Add(result, $"Config/Input/{folder}/joystick/{device.FileName}", content);
            }
            if (workspace.Sources?.ContainsKey("config/ipia-saved-games-import.json") == true)
            {
                var prefix = "src/Config/Input/" + workspace.Configuration.Module.DcsId + "/keyboard/";
                foreach (var source in workspace.Sources.Where(x => x.Key.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    var fileName = source.Key[prefix.Length..];
                    SafeFile(fileName);
                    DcsLuaParser.ParseProfile(source.Value, workspace.Configuration.Module.Id);
                    Add(result, "Config/Input/" + workspace.Configuration.Module.DcsId + "/keyboard/" + fileName, source.Value);
                }
            }
            foreach (var scope in new[] { AssignmentLayer.Module, AssignmentLayer.UiLayer })
            {
                var folder = scope == AssignmentLayer.Module ? workspace.Configuration.Module.DcsId : "UiLayer";
                var modifiers = managed.Modifiers.Where(x => x.Layer == scope)
                    .Select(x => new DcsModifier(x.Name, x.NativeDevice, x.Key, x.Mode));
                if (scope == AssignmentLayer.Module || managed.Devices.Any(x => x.Layer == scope))
                    Add(result, $"Config/Input/{folder}/modifiers.lua", WriteModifiers(
                        CompleteModifiers(workspace, scope, modifiers, result)));
            }
            ValidateModifierReferences(result);
            return result;
        }
        private static DcsModifier[] CompleteModifiers(EditorWorkspaceSnapshot workspace, AssignmentLayer scope,
            IEnumerable<DcsModifier> declared, IReadOnlyDictionary<string, byte[]> payload)
        {
            var result = declared.ToDictionary(x => x.Name, StringComparer.Ordinal);
            var shared = workspace.Sources?.TryGetValue(SharedRoot + "modifiers.lua", out var sharedSource) == true
                ? DcsLuaParser.ParseModifiers(sharedSource) : [];
            if (scope == AssignmentLayer.Module)
                foreach (var modifier in shared.Where(x => x.Device.Equals("Keyboard", StringComparison.OrdinalIgnoreCase)))
                    result.TryAdd(modifier.Name, modifier);
            // DCS also uses these modifiers in unchanged keyboard defaults absent from exported diff.lua files.
            // Custom-only and empty shared baselines must not replace that native keyboard vocabulary.
            foreach (var name in StandardKeyboardModifiers)
                if (!result.Values.Any(x => x.Device.Equals("Keyboard", StringComparison.OrdinalIgnoreCase) &&
                    DcsInputKey.SamePhysicalInput(x.Key, name)))
                    result.TryAdd(name, new DcsModifier(name, "Keyboard", name, ModifierMode.Hold));
            var folder = scope == AssignmentLayer.Module ? workspace.Configuration.Module.DcsId : "UiLayer";
            var used = payload.Where(x => x.Key.StartsWith("Config/Input/" + folder + "/", StringComparison.Ordinal) &&
                x.Key.EndsWith(".diff.lua", StringComparison.OrdinalIgnoreCase))
                .SelectMany(x => DcsLuaParser.ParseProfile(Encoding.UTF8.GetString(x.Value), workspace.Configuration.Module.Id))
                .Where(x => x.Disposition != BindingDisposition.Removed).SelectMany(x => x.Reformers).Distinct(StringComparer.Ordinal);
            foreach (var name in used.Where(x => !result.ContainsKey(x)))
            {
                var current = scope == AssignmentLayer.Module ? workspace.Configuration.Modifiers : shared;
                var definition = current.SingleOrDefault(x => x.Name == name);
                if (definition is null && scope == AssignmentLayer.Module && workspace.Sources is not null)
                {
                    var paths = new[] { "src/Config/Input/" + folder + "/modifiers.lua", "Config/Input/" + folder + "/modifiers.lua" };
                    var candidates = paths.Where(workspace.Sources.ContainsKey)
                        .SelectMany(path => DcsLuaParser.ParseModifiers(workspace.Sources[path])).Where(x => x.Name == name).Distinct().ToArray();
                    if (candidates.Length > 1)
                        throw new InvalidDataException("Conflicting saved definitions for modifier " + name + ".");
                    definition = candidates.SingleOrDefault();
                }
                if (definition is not null) result.Add(name, definition);
            }
            return result.Values.ToArray();
        }
        private static void ValidateModifierReferences(Dictionary<string, byte[]> payload)
        {
            foreach (var (path, bytes) in payload.Where(x => x.Key.EndsWith(".diff.lua", StringComparison.OrdinalIgnoreCase)))
            {
                var folder = path[..path.LastIndexOf('/', path.LastIndexOf('/') - 1)];
                var names = payload.TryGetValue(folder + "/modifiers.lua", out var table)
                    ? DcsLuaParser.ParseModifiers(Encoding.UTF8.GetString(table)).Select(x => x.Name).ToHashSet(StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal);
                var missing = DcsLuaParser.ParseProfile(Encoding.UTF8.GetString(bytes), Guid.NewGuid())
                    .Where(x => x.Disposition != BindingDisposition.Removed).SelectMany(x => x.Reformers)
                    .Where(x => !names.Contains(x)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                if (missing.Length > 0)
                    throw new InvalidDataException("Exported profile " + path + " references undeclared modifiers: " +
                        string.Join(", ", missing) + ". Correct the modifier definitions before building the package.");
            }
        }
        private static Dictionary<string, byte[]> ReconcileNativeDeviceNames(Dictionary<string, byte[]> payload)
        {
            // A pinned UI profile retains the native DCS spelling, including GUID case.
            // Reconcile only case-equivalent names; renamed controllers and other physical units stay independent.
            var names = payload.Keys.Where(path => path.StartsWith("Config/Input/UiLayer/joystick/", StringComparison.Ordinal))
                .Select(Path.GetFileName).Where(file => file is not null && DcsPhysicalIdentity.FromFileName(file) is not null)
                .Select(file => file![..^9]).ToDictionary(name => name, name => name, StringComparer.OrdinalIgnoreCase);
            if (names.Count == 0) return payload;
            var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var (path, bytes) in payload)
            {
                var target = path;
                var content = bytes;
                if (path.Contains("/joystick/", StringComparison.Ordinal) && path.EndsWith(".diff.lua", StringComparison.Ordinal))
                {
                    var native = Path.GetFileName(path)[..^9];
                    if (names.TryGetValue(native, out var captured))
                        target = path[..(path.LastIndexOf('/') + 1)] + captured + ".diff.lua";
                }
                else if (path.EndsWith("/modifiers.lua", StringComparison.Ordinal))
                {
                    var modifiers = DcsLuaParser.ParseModifiers(Encoding.UTF8.GetString(bytes)).Select(modifier =>
                        new DcsModifier(modifier.Name, names.GetValueOrDefault(modifier.Device) ?? modifier.Device,
                            modifier.Key, modifier.Mode));
                    content = Encoding.UTF8.GetBytes(WriteModifiers(modifiers));
                }
                if (!result.TryAdd(target, content))
                    throw new InvalidDataException("Two profiles resolve to the same native DCS filename: " + target);
            }
            return result;
        }
        private static void Add(Dictionary<string, byte[]> payload, string path, string content)
        {
            if (!payload.TryAdd(path, Encoding.UTF8.GetBytes(content)))
                throw new InvalidDataException("Two profiles resolve to the same package path: " + path);
        }
        private static void SafeFile(string name)
        {
            if (!name.EndsWith(".diff.lua", StringComparison.OrdinalIgnoreCase) ||
                name.Contains('/') || name.Contains('\\') || name.Contains(':') ||
                name is "." or ".." || name.Contains("..", StringComparison.Ordinal))
                throw new InvalidDataException("Unsafe DCS profile filename: " + name);
        }
        private static void SafeSegment(string name)
        {
            if (name is "." or ".." || name.Length == 0 ||
                !name.All(x => char.IsAsciiLetterOrDigit(x) || x is '-' or '_' or '.'))
                throw new InvalidDataException("Unsafe DCS module ID: " + name);
        }
        private static string WriteModifiers(IEnumerable<DcsModifier> modifiers)
        {
            var text = new StringBuilder("local modifiers = {\n");
            foreach (var modifier in modifiers.OrderBy(x => x.Name, StringComparer.Ordinal))
                text.Append("  [\"").Append(Escape(modifier.Name)).Append("\"] = { [\"device\"] = \"")
                    .Append(Escape(modifier.Device)).Append("\", [\"key\"] = \"")
                    .Append(Escape(modifier.Key)).Append("\", [\"switch\"] = ")
                    .Append(modifier.Mode == ModifierMode.Toggle ? "true" : "false").Append(" },\n");
            return text.Append("}\nreturn modifiers\n").ToString();
        }
        private static string Escape(string value)
        {
            var result = new StringBuilder();
            foreach (var c in value)
                result.Append(c switch { '\\' => "\\\\", '"' => "\\\"", '\n' => "\\n",
                                         '\r' => "\\r", '\t' => "\\t",
                                         _ when char.IsControl(c) =>
                                             throw new InvalidDataException("Unsupported Lua control character."),
                                         _ => c.ToString() });
            return result.ToString();
        }
        #endregion END private helpers
    }
}


