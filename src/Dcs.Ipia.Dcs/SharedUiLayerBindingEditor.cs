using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Stages shared default key bindings without changing modules, profile identities, or untouched sources.</summary>
    public static class SharedUiLayerBindingEditor
    {
        private const string Root = "assets/shared/ui-layer/input/UiLayer/";
        private static readonly Guid ProfileId = Guid.Parse("8fc7d99f-eccb-426e-b0d9-9ce616088d55");
        #region BEGIN Public
        /// <summary>Lists existing canonical device profiles.</summary>
        /// <param name="workspace">Current defaults.</param>
        /// <returns>Ordinal profile source paths.</returns>
        public static IReadOnlyList<string> Profiles(SharedUiLayerWorkspace workspace) => workspace.Sources.Keys
            .Where(x => x.StartsWith(Root, StringComparison.Ordinal) && x.EndsWith(".diff.lua", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).ToArray();
        /// <summary>Reads all bindings, including axis filters and removal history.</summary>
        /// <param name="workspace">Current defaults.</param>
        /// <param name="path">An existing profile path.</param>
        /// <returns>Parsed binding rows.</returns>
        public static IReadOnlyList<DcsBinding> Bindings(SharedUiLayerWorkspace workspace, string path)
        {
            if (!Profiles(workspace).Contains(path, StringComparer.Ordinal))
                throw new InvalidOperationException("Select an existing shared default profile.");
            return DcsLuaParser.ParseProfile(workspace.Sources[path], ProfileId);
        }
        /// <summary>Combines catalog commands with imported key commands so unlisted bindings remain editable.</summary>
        /// <param name="workspace">Current defaults.</param>
        /// <returns>Distinct UI Layer key commands.</returns>
        public static IReadOnlyList<DraftCommand> Commands(SharedUiLayerWorkspace workspace)
        {
            var commands = SharedUiLayerCommandEditor.Commands(workspace)
                .Select(x => new DraftCommand(x.Command, x.Label, false, AssignmentLayer.UiLayer)).ToList();
            commands.AddRange(Profiles(workspace).SelectMany(path => Bindings(workspace, path))
                .Where(x => x.Section == BindingSection.Key).Select(x => new DraftCommand(x.CommandId, x.CommandName, false, AssignmentLayer.UiLayer)));
            return commands.DistinctBy(x => x.Id).OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
        }
        /// <summary>Adds or updates one active key binding, rejecting stale rows, unknown modifiers, and occupied chords.</summary>
        /// <param name="workspace">Current draft sources.</param>
        /// <param name="path">Existing device profile.</param>
        /// <param name="current">Selected active key binding, or null to add.</param>
        /// <param name="commandId">An existing UI command identifier.</param>
        /// <param name="key">DCS key or button name.</param>
        /// <param name="modifiers">Declared modifier names.</param>
        /// <returns>Immutable draft retaining its baseline revision.</returns>
        public static SharedUiLayerWorkspace Set(SharedUiLayerWorkspace workspace, string path, DcsBinding? current,
            string commandId, string key, IEnumerable<string> modifiers)
        {
            var bindings = Bindings(workspace, path).ToList();
            var index = CurrentIndex(bindings, current);
            var command = Commands(workspace).SingleOrDefault(x => x.Id == commandId)
                ?? throw new InvalidOperationException("Select a declared or imported UI Layer command.");
            if (string.IsNullOrWhiteSpace(key) || key.Any(char.IsControl))
                throw new InvalidOperationException("Enter a DCS key or button name without control characters.");
            var candidate = new DcsBinding(ProfileId, BindingSection.Key, command.Id, command.Name,
                current?.Disposition ?? BindingDisposition.Added, key, modifiers);
            ValidateBinding(workspace, candidate);
            if (bindings.Where((_, i) => i != index).Any(x => Occupies(x, candidate)))
                throw new InvalidOperationException("That input and modifier chord already has a binding. Remove it explicitly before replacing it.");
            if (index >= 0) bindings[index] = candidate;
            else bindings.Add(candidate);
            return Store(workspace, path, bindings);
        }
        /// <summary>Removes one active key binding without dropping unrelated bindings or removal history.</summary>
        /// <param name="workspace">Current draft sources.</param>
        /// <param name="path">Existing device profile.</param>
        /// <param name="current">Selected active binding.</param>
        /// <returns>Immutable draft retaining its baseline revision.</returns>
        public static SharedUiLayerWorkspace Remove(SharedUiLayerWorkspace workspace, string path, DcsBinding current)
        {
            var bindings = Bindings(workspace, path).ToList();
            bindings.RemoveAt(CurrentIndex(bindings, current));
            return Store(workspace, path, bindings);
        }
        /// <summary>Clears active keys and axes for one profile and modifier chord, retaining removal history.</summary>
        /// <param name="workspace">Current draft defaults.</param>
        /// <param name="path">Existing device profile.</param>
        /// <param name="modifierChord">Modifier names, or empty for Base.</param>
        /// <returns>Immutable draft retaining other profiles and chords.</returns>
        public static SharedUiLayerWorkspace ClearLayerAssignments(SharedUiLayerWorkspace workspace, string path, string modifierChord)
        {
            var chord = modifierChord.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal).ToArray();
            return Store(workspace, path, Bindings(workspace, path).Where(x => x.Disposition == BindingDisposition.Removed ||
                !x.Reformers.Order(StringComparer.Ordinal).SequenceEqual(chord, StringComparer.Ordinal)).ToArray());
        }
        /// <summary>Checks modifier references and physical input conflicts before persisting a revision.</summary>
        /// <param name="workspace">Complete staged defaults.</param>
        public static void Validate(SharedUiLayerWorkspace workspace)
        {
            SharedUiLayerCommandEditor.Commands(workspace);
            foreach (var path in Profiles(workspace))
            {
                var active = Bindings(workspace, path).Where(x => x.Disposition != BindingDisposition.Removed).ToArray();
                for (var i = 0; i < active.Length; i++)
                {
                    ValidateBinding(workspace, active[i]);
                    if (active.Take(i).Any(x => Occupies(x, active[i])))
                        throw new InvalidOperationException("Conflicting input chord in " + path);
                }
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static int CurrentIndex(IReadOnlyList<DcsBinding> bindings, DcsBinding? current)
        {
            if (current is null) return -1;
            if (current.Section != BindingSection.Key || current.Disposition == BindingDisposition.Removed)
                throw new InvalidOperationException("Axis bindings and removal history are preserved read-only in this editor.");
            var matches = bindings.Select((x, i) => (Binding: x, Index: i)).Where(x =>
                x.Binding.Section == current.Section && x.Binding.Disposition == current.Disposition &&
                x.Binding.CommandId == current.CommandId && x.Binding.InputKey == current.InputKey &&
                x.Binding.Reformers.SequenceEqual(current.Reformers, StringComparer.Ordinal)).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("The selected binding changed. Select it again.");
            return matches[0].Index;
        }
        private static bool Occupies(DcsBinding a, DcsBinding b) => a.Disposition != BindingDisposition.Removed &&
            a.Section == b.Section && DcsInputKey.SamePhysicalInput(a.InputKey, b.InputKey) &&
            a.Reformers.SequenceEqual(b.Reformers, StringComparer.Ordinal);
        private static void ValidateBinding(SharedUiLayerWorkspace workspace, DcsBinding binding)
        {
            var declared = DcsLuaParser.ParseModifiers(workspace.Sources[Root + "modifiers.lua"]).Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
            if (binding.Reformers.Any(x => !declared.Contains(x)))
                throw new InvalidOperationException("Every modifier in the chord must be declared in shared defaults.");
        }
        private static SharedUiLayerWorkspace Store(SharedUiLayerWorkspace workspace, string path, IReadOnlyList<DcsBinding> bindings)
        {
            var sources = new Dictionary<string, string>(workspace.Sources, StringComparer.Ordinal) { [path] = ModuleLuaProfileRenderer.Write(bindings) };
            return workspace with { Sources = sources, ContentSha256 = null };
        }
        #endregion END private helpers
    }
}
