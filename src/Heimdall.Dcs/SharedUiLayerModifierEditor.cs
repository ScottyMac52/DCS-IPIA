using System.Text;
using Heimdall.Application;
using Heimdall.Domain;

namespace Heimdall.Dcs
{
    /// <summary>Stages independent shared default modifiers with keyboard, activator, and binding safeguards.</summary>
    public static class SharedUiLayerModifierEditor
    {
        private const string Source = "assets/shared/ui-layer/input/UiLayer/modifiers.lua";
        #region BEGIN Public
        /// <summary>Reads the current shared modifier definitions.</summary>
        /// <param name="workspace">Saved or staged defaults.</param>
        /// <returns>Definitions in ordinal name order.</returns>
        public static IReadOnlyList<DcsModifier> Modifiers(SharedUiLayerWorkspace workspace) =>
            DcsLuaParser.ParseModifiers(workspace.Sources[Source]);
        /// <summary>Lists native names from existing profiles without reformatting physical GUIDs.</summary>
        /// <param name="workspace">Current default profiles.</param>
        /// <returns>Keyboard and native device choices.</returns>
        public static IReadOnlyList<string> Devices(SharedUiLayerWorkspace workspace) =>
            Enumerable.Repeat("Keyboard", 1).Concat(SharedUiLayerBindingEditor.Profiles(workspace)
                .Select(path => Path.GetFileName(path)[..^9])).Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray();
        /// <summary>Identifies protected built-in keyboard defaults.</summary>
        /// <param name="modifier">Selected definition.</param>
        /// <returns>Whether this is a standard keyboard modifier.</returns>
        public static bool IsProtected(DcsModifier modifier) =>
            modifier.Device.Equals("Keyboard", StringComparison.OrdinalIgnoreCase) && IsKeyboardName(modifier.Name);
        /// <summary>Adds or updates a modifier while preserving bound names and unique physical activators.</summary>
        /// <param name="workspace">Current draft.</param>
        /// <param name="current">Exact selected definition, or null to add.</param>
        /// <param name="candidate">Name, native device, DCS input, and Hold/Toggle mode.</param>
        /// <returns>Immutable draft retaining its baseline revision and provenance.</returns>
        public static SharedUiLayerWorkspace Set(SharedUiLayerWorkspace workspace, DcsModifier? current, DcsModifier candidate)
        {
            var modifiers = Modifiers(workspace).ToList();
            var index = CurrentIndex(modifiers, current);
            CheckFields(candidate);
            if (!Devices(workspace).Contains(candidate.Device, StringComparer.Ordinal))
                throw new InvalidOperationException("Select an existing shared default device using its exact native name.");
            if (IsKeyboardName(candidate.Name) || current is not null && IsProtected(current))
                throw new InvalidOperationException("Built-in keyboard modifiers are protected.");
            if (current is not null && current.Name != candidate.Name && Used(workspace, current.Name))
                throw new InvalidOperationException("Clear assignments using this modifier before renaming it.");
            if (modifiers.Where((_, i) => i != index).Any(x =>
                x.Name.Equals(candidate.Name, StringComparison.OrdinalIgnoreCase) || SameActivator(x, candidate)))
                throw new InvalidOperationException("That modifier name or physical input already exists.");
            if (index < 0) modifiers.Add(candidate);
            else modifiers[index] = candidate;
            return Store(workspace, modifiers);
        }
        /// <summary>Removes an unused modifier without changing bindings, filters, or removal history.</summary>
        /// <param name="workspace">Current draft.</param>
        /// <param name="current">Exact selected definition.</param>
        /// <returns>Immutable draft retaining unrelated sources.</returns>
        public static SharedUiLayerWorkspace Remove(SharedUiLayerWorkspace workspace, DcsModifier current)
        {
            var modifiers = Modifiers(workspace).ToList();
            var index = CurrentIndex(modifiers, current);
            if (IsProtected(current)) throw new InvalidOperationException("Built-in keyboard modifiers are protected.");
            if (Used(workspace, current.Name))
                throw new InvalidOperationException("Clear assignments using this modifier before removing it.");
            modifiers.RemoveAt(index);
            return Store(workspace, modifiers);
        }
        /// <summary>Checks modifier fields and physical aliases before a revision is saved.</summary>
        /// <param name="workspace">Complete staged defaults.</param>
        public static void Validate(SharedUiLayerWorkspace workspace)
        {
            var modifiers = Modifiers(workspace);
            for (var i = 0; i < modifiers.Count; i++)
            {
                CheckFields(modifiers[i]);
                if (modifiers.Take(i).Any(x => SameActivator(x, modifiers[i])))
                    throw new InvalidOperationException("Shared modifiers use the same physical activator.");
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static bool IsKeyboardName(string name) => name is "LAlt" or "LCtrl" or "LShift" or "LWin" or
            "RAlt" or "RCtrl" or "RShift" or "RWin";
        private static int CurrentIndex(IReadOnlyList<DcsModifier> modifiers, DcsModifier? current)
        {
            if (current is null) return -1;
            var matches = modifiers.Select((value, index) => (value, index)).Where(x => x.value == current).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("The selected modifier changed. Select it again.");
            return matches[0].index;
        }
        private static bool SameActivator(DcsModifier first, DcsModifier second) =>
            first.Device.Equals(second.Device, StringComparison.OrdinalIgnoreCase) &&
            DcsInputKey.SamePhysicalInput(first.Key, second.Key);
        private static bool Used(SharedUiLayerWorkspace workspace, string name) =>
            SharedUiLayerBindingEditor.Profiles(workspace).SelectMany(path => SharedUiLayerBindingEditor.Bindings(workspace, path))
                .Any(binding => binding.Disposition != BindingDisposition.Removed && binding.Reformers.Contains(name, StringComparer.Ordinal));
        private static void CheckFields(DcsModifier modifier)
        {
            if (!Enum.IsDefined(modifier.Mode) || modifier.Name.Contains('+') ||
                modifier.Name.Any(char.IsControl) || modifier.Device.Any(char.IsControl) || modifier.Key.Any(char.IsWhiteSpace) || modifier.Key.Any(char.IsControl))
                throw new InvalidOperationException("Enter a modifier name without + or control characters, a DCS input token, and Hold or Toggle mode.");
        }
        private static SharedUiLayerWorkspace Store(SharedUiLayerWorkspace workspace, IEnumerable<DcsModifier> modifiers)
        {
            var text = new StringBuilder("local modifiers = {\n");
            foreach (var modifier in modifiers.OrderBy(x => x.Name, StringComparer.Ordinal))
                text.Append("  [").Append(Quote(modifier.Name)).Append("] = { [\"device\"] = ").Append(Quote(modifier.Device))
                    .Append(", [\"key\"] = ").Append(Quote(modifier.Key)).Append(", [\"switch\"] = ")
                    .Append(modifier.Mode == ModifierMode.Toggle ? "true" : "false").Append(" },\n");
            var sources = new Dictionary<string, string>(workspace.Sources, StringComparer.Ordinal) {
                [Source] = text.Append("}\nreturn modifiers\n").ToString()
            };
            return workspace with { Sources = sources, ContentSha256 = null };
        }
        private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        #endregion END private helpers
    }
}
