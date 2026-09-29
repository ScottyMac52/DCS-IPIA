using System.Text;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Collects the saved workspace's DCS Lua files for an OvGME payload.</summary>
    public static class SnapshotLuaPayloadRenderer
    {
        private const string SharedRoot = "assets/shared/ui-layer/input/UiLayer/";
        #region BEGIN Public
        /// <summary>Renders module and UI Layer profiles with their effective modifier tables.</summary>
        /// <param name="workspace">Frozen saved workspace.</param>
        /// <returns>Package relative paths and UTF-8 Lua bytes.</returns>
        public static IReadOnlyDictionary<string, byte[]> Generate(EditorWorkspaceSnapshot workspace)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            if (WorkspaceManagement.Read(workspace) is { } managed)
                return GenerateManaged(workspace, managed);
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
            if (config.Modifiers.Count > 0)
                Add(result, modulePrefix + "modifiers.lua", WriteModifiers(config.Modifiers));
            var uiProfiles = workspace.Diagrams.Where(x => x.SupportsUiLayer &&
                                                            x.CanonicalUiLayerSourcePath is not null).ToArray();
            if (workspace.Assignments.Where(x => x.Slot.Layer == AssignmentLayer.UiLayer)
                .Any(x => uiProfiles.All(y => y.ProfileId != x.Slot.ProfileId)))
                throw new InvalidDataException("Saved UI Layer assignments have no pinned profile.");
            if (uiProfiles.Length == 0)
                return result;
            if (workspace.Sources is null ||
                !workspace.Sources.TryGetValue(SharedRoot + "modifiers.lua", out var source))
                throw new InvalidDataException("The saved workspace lacks canonical UI Layer modifiers; re-import it.");
            var modifiers = DcsLuaParser.ParseModifiers(source);
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
                WriteModifiers(modifiers.Where(x => !removed.Contains(x.Name))));
            return result;
        }
        #endregion END Public
        #region BEGIN private helpers
        private static Dictionary<string, byte[]> GenerateManaged(EditorWorkspaceSnapshot workspace, WorkspaceManagement managed)
        {
            SafeSegment(workspace.Configuration.Module.DcsId);
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
            foreach (var scope in new[] { AssignmentLayer.Module, AssignmentLayer.UiLayer })
            {
                var folder = scope == AssignmentLayer.Module ? workspace.Configuration.Module.DcsId : "UiLayer";
                var modifiers = managed.Modifiers.Where(x => x.Layer == scope)
                    .Select(x => new DcsModifier(x.Name, x.NativeDevice, x.Key, x.Mode));
                if (scope == AssignmentLayer.Module || managed.Devices.Any(x => x.Layer == scope))
                    Add(result, $"Config/Input/{folder}/modifiers.lua", WriteModifiers(modifiers));
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
            if (!name.EndsWith(".diff.lua", StringComparison.Ordinal) ||
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

