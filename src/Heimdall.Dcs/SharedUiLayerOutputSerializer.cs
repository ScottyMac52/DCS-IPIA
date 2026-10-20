using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Heimdall.Application;
using Heimdall.Domain;

namespace Heimdall.Dcs
{
    /// <summary>Freezes and reproduces shared defaults without an aircraft, database or source checkout.</summary>
    public static class SharedUiLayerOutputSerializer
    {
        private const string Root = "assets/shared/ui-layer/input/UiLayer/";
        #region BEGIN Public
        /// <summary>Freezes verified saved defaults and copies the exact resolved graphics.</summary>
        /// <param name="defaults">Saved revision with a verified digest.</param>
        /// <param name="hardwareSourceCommit">Pinned hardware catalog commit.</param>
        /// <param name="diagrams">Explicitly resolved joystick graphics.</param>
        /// <returns>Portable output content with no mutable input collection references.</returns>
        public static SharedUiLayerOutputSnapshot Freeze(SharedUiLayerWorkspace defaults, string hardwareSourceCommit,
            IReadOnlyDictionary<string, UiLayerDiagram> diagrams)
        {
            defaults = SharedUiLayerRevisionReference.Freeze(defaults);
            SharedUiLayerBindingEditor.Validate(defaults);
            if (hardwareSourceCommit.Length != 40 || !hardwareSourceCommit.All(Uri.IsHexDigit))
                throw new InvalidDataException("Invalid hardware source commit.");
            var paths = SharedUiLayerBindingEditor.Profiles(defaults).Where(x => x.Contains("/joystick/", StringComparison.Ordinal)).ToArray();
            if (!paths.Order(StringComparer.Ordinal).SequenceEqual(diagrams.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal))
                throw new InvalidDataException("Every joystick profile requires one pinned output graphic.");
            var copy = new SortedDictionary<string, UiLayerDiagram>(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                var diagram = diagrams[path];
                if (diagram.Svg.Length == 0 || diagram.Controls.Count == 0 || diagram.Width <= 0 || diagram.Height <= 0)
                    throw new InvalidDataException("Incomplete output graphic for " + path);
                copy.Add(path, diagram with { ProfileId = StableIdentity.Create("shared-output-profile", path),
                    CanonicalUiLayerSourcePath = path, SupportsUiLayer = true,
                    Background = diagram.Background.ToArray(), Callouts = [],
                    Controls = diagram.Controls.OrderBy(x => x.Control, StringComparer.Ordinal).ToArray() });
            }
            return new(defaults, hardwareSourceCommit, copy);
        }
        /// <summary>Serializes canonical JSON and hashes its exact UTF-8 bytes.</summary>
        /// <param name="snapshot">Portable output inputs.</param>
        /// <returns>Immutable content and digest.</returns>
        public static ImmutableSnapshot Serialize(SharedUiLayerOutputSnapshot snapshot)
        {
            snapshot = Freeze(snapshot.Defaults, snapshot.HardwareSourceCommit, snapshot.Diagrams);
            var content = JsonSerializer.Serialize(new SharedUiLayerOutputDocument(1, snapshot)) + "\n";
            return new(content, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content))));
        }
        /// <summary>Verifies the digest, schema and canonical form before restoring output inputs.</summary>
        /// <param name="snapshot">Previously exported immutable content.</param>
        /// <returns>Verified portable output inputs.</returns>
        public static SharedUiLayerOutputSnapshot Deserialize(ImmutableSnapshot snapshot)
        {
            if (Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot.Content))) != snapshot.Sha256)
                throw new InvalidDataException("Shared output snapshot SHA-256 mismatch.");
            var document = JsonSerializer.Deserialize<SharedUiLayerOutputDocument>(snapshot.Content) ??
                throw new InvalidDataException("Missing shared output document.");
            if (document.SchemaVersion != 1) throw new InvalidDataException("Unsupported shared output schema.");
            var frozen = Freeze(document.Snapshot.Defaults, document.Snapshot.HardwareSourceCommit, document.Snapshot.Diagrams);
            if (Serialize(frozen).Content != snapshot.Content) throw new InvalidDataException("Noncanonical shared output snapshot.");
            return frozen;
        }
        /// <summary>Emits exact saved Lua, preserving native filenames, declarations, filters and removal history.</summary>
        /// <param name="snapshot">Verified portable output inputs.</param>
        /// <returns>Saved Games relative Lua paths and text.</returns>
        public static IReadOnlyDictionary<string, string> GenerateLua(SharedUiLayerOutputSnapshot snapshot)
        {
            snapshot = Freeze(snapshot.Defaults, snapshot.HardwareSourceCommit, snapshot.Diagrams);
            return new SortedDictionary<string, string>(snapshot.Defaults.Sources.Where(x => x.Key.StartsWith(Root, StringComparison.Ordinal) &&
                (x.Key.EndsWith(".diff.lua", StringComparison.Ordinal) || x.Key == Root + "modifiers.lua"))
                .ToDictionary(x => "Config/Input/UiLayer/" + x.Key[Root.Length..], x => x.Value, StringComparer.Ordinal), StringComparer.Ordinal);
        }
        /// <summary>Creates a module-neutral rendering projection exclusively from the portable snapshot.</summary>
        /// <param name="snapshot">Verified portable output inputs.</param>
        /// <returns>In-memory projection; never saved as an aircraft module.</returns>
        public static EditorWorkspaceSnapshot CreatePreview(SharedUiLayerOutputSnapshot snapshot)
        {
            snapshot = Freeze(snapshot.Defaults, snapshot.HardwareSourceCommit, snapshot.Diagrams);
            var module = new DcsModule(StableIdentity.Create("shared-output", "UiLayer"), "UiLayer", "Shared default UI Layer");
            var profiles = snapshot.Diagrams.Select(x => new DeviceProfile(x.Value.ProfileId, x.Value.Path, null, "UI Layer", Path.GetFileName(x.Key))).ToArray();
            var assignments = snapshot.Diagrams.SelectMany(x => SharedUiLayerBindingEditor.Bindings(snapshot.Defaults, x.Key)
                .Where(b => b.Disposition != BindingDisposition.Removed && x.Value.Controls.Any(c => c.IsAxis == (b.Section == BindingSection.Axis) &&
                    DcsInputKey.SamePhysicalInput(c.InputKey, b.InputKey)))
                .Select(b => new DraftAssignment(new(module.Id, x.Value.ProfileId, AssignmentLayer.UiLayer, b.Section, b.InputKey, string.Join("+", b.Reformers)),
                    new(b.CommandId, b.CommandName, b.Section == BindingSection.Axis, AssignmentLayer.UiLayer)) { Filter = b.Filter })).ToArray();
            var devices = profiles.Select(x => new WorkspaceDevice(x.Id, AssignmentLayer.UiLayer, x.SourceName, null, "UI Layer")).ToArray();
            var modifiers = SharedUiLayerModifierEditor.Modifiers(snapshot.Defaults).Select(x => new WorkspaceModifier(AssignmentLayer.UiLayer,
                profiles.FirstOrDefault(p => p.SourceName[..^9].Equals(x.Device, StringComparison.OrdinalIgnoreCase))?.Id ?? Guid.Empty,
                x.Name, x.Key, x.Name, x.Mode, x.Device)).ToArray();
            var sources = new Dictionary<string, string>(snapshot.Defaults.Sources, StringComparer.Ordinal) {
                [WorkspaceManagement.SourcePath] = JsonSerializer.Serialize(new WorkspaceManagement(devices, modifiers))
            };
            return new(new(module, profiles, [], [], "shared default output"), [], [], snapshot.Diagrams.Values.ToArray(), [], [], assignments,
                new Dictionary<string, string>(), sources);
        }
        #endregion END Public
    }
}
