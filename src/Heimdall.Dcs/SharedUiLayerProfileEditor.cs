using Heimdall.Application;
using Heimdall.Domain;

namespace Heimdall.Dcs
{
    /// <summary>Stages shared joystick profile creation, cloning, renaming and guarded removal independently of modules.</summary>
    public static class SharedUiLayerProfileEditor
    {
        private const string Root = "assets/shared/ui-layer/input/UiLayer/";
        #region BEGIN Public
        /// <summary>Lists device profiles with live binding and modifier reference counts.</summary>
        /// <param name="workspace">Saved or staged defaults.</param>
        /// <returns>Profiles in ordinal path order.</returns>
        public static IReadOnlyList<SharedUiLayerDeviceProfile> Profiles(SharedUiLayerWorkspace workspace)
        {
            var modifiers = SharedUiLayerModifierEditor.Modifiers(workspace);
            return SharedUiLayerBindingEditor.Profiles(workspace).Select(path => {
                var file = Path.GetFileName(path);
                return new SharedUiLayerDeviceProfile(path, file, workspace.Sources[path],
                    SharedUiLayerBindingEditor.Bindings(workspace, path).Count(x => x.Disposition != BindingDisposition.Removed),
                    modifiers.Count(x => x.Device.Equals(file[..^9], StringComparison.OrdinalIgnoreCase)),
                    !path.StartsWith(Root + "joystick/", StringComparison.Ordinal));
            }).ToArray();
        }
        /// <summary>Creates an empty joystick profile or copies an existing joystick profile's exact Lua.</summary>
        /// <param name="workspace">Current shared draft.</param>
        /// <param name="fileName">Exact GUID-qualified native DCS filename.</param>
        /// <param name="template">An existing joystick profile, or null for an empty profile.</param>
        /// <returns>Immutable draft retaining its baseline revision.</returns>
        public static SharedUiLayerWorkspace Add(SharedUiLayerWorkspace workspace, string fileName,
            SharedUiLayerDeviceProfile? template = null)
        {
            ValidateTarget(workspace, fileName, null);
            if (template is not null && Current(workspace, template).Protected)
                throw new InvalidOperationException("Select a joystick template; the keyboard profile is protected.");
            var source = template?.Source ?? "local diff = { [\"keyDiffs\"] = {}, [\"axisDiffs\"] = {} }\nreturn diff\n";
            var sources = new Dictionary<string, string>(workspace.Sources, StringComparer.Ordinal) { [Root + "joystick/" + fileName] = source };
            return workspace with { Sources = sources, ContentSha256 = null };
        }
        /// <summary>Renames a profile and its modifier device references without changing assignments or physical identity.</summary>
        /// <param name="workspace">Current draft.</param>
        /// <param name="current">Exact selected profile.</param>
        /// <param name="fileName">New native filename retaining the physical GUID.</param>
        /// <returns>Immutable draft retaining profile Lua and modifier semantics.</returns>
        public static SharedUiLayerWorkspace Rename(SharedUiLayerWorkspace workspace, SharedUiLayerDeviceProfile current, string fileName)
        {
            current = Current(workspace, current);
            if (current.Protected) throw new InvalidOperationException("The keyboard profile is protected.");
            ValidateTarget(workspace, fileName, current.Path);
            if (DcsPhysicalIdentity.FromFileName(current.FileName) is { } physical && DcsPhysicalIdentity.FromFileName(fileName) != physical)
                throw new InvalidOperationException("Keep this physical device GUID. Add a different device separately.");
            var sources = new Dictionary<string, string>(workspace.Sources, StringComparer.Ordinal);
            sources.Remove(current.Path);
            sources[Root + "joystick/" + fileName] = current.Source;
            var draft = workspace with { Sources = sources, ContentSha256 = null };
            foreach (var modifier in SharedUiLayerModifierEditor.Modifiers(workspace).Where(x =>
                x.Device.Equals(current.FileName[..^9], StringComparison.OrdinalIgnoreCase)))
                draft = SharedUiLayerModifierEditor.Set(draft, modifier,
                    new DcsModifier(modifier.Name, fileName[..^9], modifier.Key, modifier.Mode));
            return draft;
        }
        /// <summary>Removes a joystick profile after all active assignments and modifier references are cleared.</summary>
        /// <param name="workspace">Current draft.</param>
        /// <param name="current">Exact selected profile.</param>
        /// <returns>Immutable draft; old saved revisions retain the removed source and history.</returns>
        public static SharedUiLayerWorkspace Remove(SharedUiLayerWorkspace workspace, SharedUiLayerDeviceProfile current)
        {
            current = Current(workspace, current);
            if (current.Protected) throw new InvalidOperationException("The keyboard profile is protected.");
            if (current.ActiveBindings > 0 || current.ModifierCount > 0)
                throw new InvalidOperationException("Clear all active key/axis assignments and move or remove device modifiers before deleting this profile.");
            var sources = new Dictionary<string, string>(workspace.Sources, StringComparer.Ordinal);
            sources.Remove(current.Path);
            return workspace with { Sources = sources, ContentSha256 = null };
        }
        /// <summary>Checks profile paths and duplicate physical identities before saving a shared revision.</summary>
        /// <param name="workspace">Complete staged defaults.</param>
        public static void Validate(SharedUiLayerWorkspace workspace)
        {
            var paths = SharedUiLayerBindingEditor.Profiles(workspace);
            if (paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Count)
                throw new InvalidOperationException("Duplicate shared profile filename.");
            var physical = new HashSet<Guid>();
            foreach (var path in paths)
            {
                var file = Path.GetFileName(path);
                ValidateFile(file);
                if (path != Root + "joystick/" + file && path != Root + "keyboard/" + file)
                    throw new InvalidOperationException("Shared profiles must be directly inside the joystick or keyboard folder.");
                if (path.StartsWith(Root + "joystick/", StringComparison.Ordinal) && DcsPhysicalIdentity.FromFileName(file) is { } guid && !physical.Add(guid))
                    throw new InvalidOperationException("A physical GUID already belongs to another shared profile.");
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static SharedUiLayerDeviceProfile Current(SharedUiLayerWorkspace workspace, SharedUiLayerDeviceProfile selected)
        {
            var current = Profiles(workspace).SingleOrDefault(x => x.Path == selected.Path);
            if (current is null || current.Source != selected.Source || current.FileName != selected.FileName)
                throw new InvalidOperationException("The selected profile changed. Select it again.");
            return current;
        }
        private static void ValidateTarget(SharedUiLayerWorkspace workspace, string file, string? currentPath)
        {
            ValidateFile(file);
            var guid = DcsPhysicalIdentity.FromFileName(file) ??
                throw new InvalidOperationException("Use the exact native DCS joystick filename with its physical GUID.");
            if (Profiles(workspace).Any(x => x.Path != currentPath &&
                (x.FileName.Equals(file, StringComparison.OrdinalIgnoreCase) || DcsPhysicalIdentity.FromFileName(x.FileName) == guid)))
                throw new InvalidOperationException("That filename or physical GUID already belongs to another shared profile.");
        }
        private static void ValidateFile(string file)
        {
            if (string.IsNullOrWhiteSpace(file) || file != file.Trim() || !file.EndsWith(".diff.lua", StringComparison.Ordinal) ||
                file.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 || file.Contains("..", StringComparison.Ordinal) || file.Any(char.IsControl))
                throw new InvalidOperationException("Enter a native filename ending in .diff.lua without directory paths or unsafe characters.");
        }
        #endregion END private helpers
    }
}
