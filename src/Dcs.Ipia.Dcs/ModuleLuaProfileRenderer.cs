using System.Globalization;
using System.Text;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Projects a saved module's effective assignments into deterministic DCS diff.lua.</summary>
    public static class ModuleLuaProfileRenderer
    {
        #region BEGIN Public
        /// <summary>Builds one profile without reading mutable source files or losing unmapped bindings.</summary>
        /// <param name="workspace">Frozen saved module workspace.</param>
        /// <param name="profileId">Stored device profile identity.</param>
        /// <param name="defaultsLayer">Scope owning the captured default-axis evidence.</param>
        /// <returns>Canonical Lua text ending in one LF.</returns>
        public static string Render(EditorWorkspaceSnapshot workspace, Guid profileId, AssignmentLayer defaultsLayer = AssignmentLayer.Module)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            var config = workspace.Configuration;
            if (config.Profiles.All(x => x.Id != profileId))
                throw new ArgumentException("Profile is outside this module.", nameof(profileId));
            var diagram =
                workspace.Diagrams.SingleOrDefault(x => x.ProfileId == profileId) ??
                throw new InvalidDataException("Cannot export a profile without its mapped hardware controls.");
            var original = config.Bindings.Where(x => x.ProfileId == profileId).ToArray();
            var moduleAssignments =
                workspace.Assignments
                    .Where(x => x.Slot.ProfileId == profileId && x.Slot.Layer == AssignmentLayer.Module)
                    .ToArray();
            if (moduleAssignments.Any(x => x.Slot.ModuleId != config.Module.Id ||
                                           (x.Slot.Section == BindingSection.Axis) != x.Command.IsAxis ||
                                           x.Command.Layer != AssignmentLayer.Module))
                throw new InvalidDataException("Saved assignment is outside the module or has the wrong input type.");
            var saved = new Dictionary<string, DraftAssignment>(StringComparer.Ordinal);
            foreach (var assignment in DcsInputKey.CollapseAliases(moduleAssignments))
            {
                if (!saved.TryAdd(
                        Slot(assignment.Slot.Section, assignment.Slot.InputKey, assignment.Slot.ModifierChord),
                        assignment))
                {
                    var previous = saved[Slot(assignment.Slot.Section, assignment.Slot.InputKey, assignment.Slot.ModifierChord)];
                    var profile = config.Profiles.Single(x => x.Id == profileId);
                    throw new InvalidDataException($"Module {config.Module.DcsId}; device {profile.DeviceId} " +
                        $"({profile.SourceName}, profile {profileId}); {assignment.Slot.Section} input {assignment.Slot.InputKey}; " +
                        $"modifiers [{assignment.Slot.ModifierChord}]: conflicting saved commands " +
                        $"{previous.Command.Name} [{previous.Command.Id}] and {assignment.Command.Name} [{assignment.Command.Id}]. " +
                        "Assign one command to this input and modifier combination, then save to database.");
                }
                if (!Mapped(diagram, assignment.Slot.Section, assignment.Slot.InputKey))
                    throw new InvalidDataException("Saved assignment has no authored hardware control: " +
                                                   assignment.Slot.InputKey);
            }
            var active = original.Where(x => x.Disposition != BindingDisposition.Removed).ToArray();
            // Original imports are evidence, not the final assignments. Resolve mapped slots below
            // before considering duplicates; DCS also permits multiple commands on unmapped buttons.
            var result = new List<DcsBinding>(original.Where(x => x.Disposition == BindingDisposition.Removed));
            var handled = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in active)
            {
                var key = Slot(binding.Section, binding.InputKey, string.Join("+", binding.Reformers));
                if (!Mapped(diagram, binding.Section, binding.InputKey))
                {
                    result.Add(binding.Section == BindingSection.Axis ? Removed(binding) : binding);
                    continue;
                }
                handled.Add(key);
                if (!saved.TryGetValue(key, out var current))
                {
                    if (binding.Section == BindingSection.Axis)
                        result.Add(Removed(binding));
                    continue;
                }
                if (binding.Section == BindingSection.Axis && current.Command.Id != binding.CommandId)
                    result.Add(Removed(binding));
                if (current.Command.Id == binding.CommandId && SameFilter(current.Filter, binding.Filter))
                    result.Add(binding);
                else
                    result.Add(ToBinding(profileId, current with { Slot = current.Slot with { InputKey = binding.InputKey } }));
            }
            foreach (var (key, assignment) in saved)
                if (!handled.Contains(key))
                    result.Add(ToBinding(profileId, assignment));
            var effective = result.DistinctBy(x => System.Text.Json.JsonSerializer.Serialize(x)).ToArray();
            return Write(ModuleAxisDefaults.Apply(workspace, profileId, defaultsLayer, effective));
        }
        #endregion END Public
        #region BEGIN private helpers
        internal static DcsBinding Removed(DcsBinding binding) => new(binding.ProfileId, binding.Section,
            binding.CommandId, binding.CommandName, BindingDisposition.Removed, binding.InputKey, binding.Reformers, binding.Filter);
        private static bool Mapped(UiLayerDiagram diagram, BindingSection section, string key) => diagram.Controls.Any(
            x => x.IsAxis == (section == BindingSection.Axis) && DcsInputKey.SamePhysicalInput(x.InputKey, key));
        private static string Slot(BindingSection section, string key, string chord) => section + "\u001f" +
            DcsInputKey.PhysicalSlotKey(new(Guid.Empty, Guid.Empty, AssignmentLayer.Module, section, key)) +
                                                                                        "\u001f" + chord;
        private static bool SameFilter(AxisFilter? a, AxisFilter? b) =>
            System.Text.Json.JsonSerializer.Serialize(a) == System.Text.Json.JsonSerializer.Serialize(b);
        private static DcsBinding ToBinding(Guid profileId, DraftAssignment assignment)
        {
            var slot = assignment.Slot;
            return new(
                profileId, slot.Section, assignment.Command.Id, assignment.Command.Name, BindingDisposition.Added,
                slot.InputKey,
                slot.ModifierChord.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                assignment.Filter);
        }
        internal static string Write(IReadOnlyList<DcsBinding> bindings)
        {
            var text = new StringBuilder("local diff = {\n");
            foreach (var section in new[] { (Section: BindingSection.Key, Name: "keyDiffs"),
                                            (Section: BindingSection.Axis, Name: "axisDiffs") })
            {
                text.Append("  [\"").Append(section.Name).Append("\"] = {\n");
                foreach (var group in bindings.Where(x => x.Section == section.Section)
                             .GroupBy(x => x.CommandId, StringComparer.Ordinal)
                             .OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    text.Append("    [\"").Append(Escape(group.Key)).Append("\"] = {\n");
                    foreach (var disposition in new[] { BindingDisposition.Added, BindingDisposition.Changed,
                                                        BindingDisposition.Removed })
                    {
                        var inputs = group.Where(x => x.Disposition == disposition)
                                         .OrderBy(x => x.InputKey, StringComparer.Ordinal)
                                         .ThenBy(x => string.Join("+", x.Reformers), StringComparer.Ordinal)
                                         .ToArray();
                        if (inputs.Length == 0)
                            continue;
                        text.Append("      [\"").Append(disposition.ToString().ToLowerInvariant()).Append("\"] = {\n");
                        for (var i = 0; i < inputs.Length; i++)
                        {
                            var input = inputs[i];
                            text.Append("        [")
                                .Append(i + 1)
                                .Append("] = { [\"key\"] = \"")
                                .Append(Escape(input.InputKey))
                                .Append('"');
                            if (input.Reformers.Count > 0)
                            {
                                text.Append(", [\"reformers\"] = {");
                                for (var j = 0; j < input.Reformers.Count; j++)
                                    text.Append(" [")
                                        .Append(j + 1)
                                        .Append("] = \"")
                                        .Append(Escape(input.Reformers[j]))
                                        .Append("\",");
                                text.Append(" }");
                            }
                            if (input.Filter is not null)
                                WriteFilter(text, input.Filter);
                            text.Append(" },\n");
                        }
                        text.Append("      },\n");
                    }
                    text.Append("      [\"name\"] = \"")
                        .Append(Escape(group.First().CommandName))
                        .Append("\",\n    },\n");
                }
                text.Append("  },\n");
            }
            return text.Append("}\nreturn diff\n").ToString();
        }
        private static void WriteFilter(StringBuilder text, AxisFilter filter)
        {
            text.Append(", [\"filter\"] = {");
            AddNumber("deadzone", filter.Deadzone);
            AddNumber("saturationX", filter.SaturationX);
            AddNumber("saturationY", filter.SaturationY);
            if (filter.Curvature is not null)
            {
                text.Append(" [\"curvature\"] = {");
                for (var i = 0; i < filter.Curvature.Count; i++)
                    text.Append(" [").Append(i + 1).Append("] = ").Append(Number(filter.Curvature[i])).Append(',');
                text.Append(" },");
            }
            AddBool("invert", filter.Invert);
            AddBool("slider", filter.Slider);
            AddBool("hardwareDetent", filter.HardwareDetent);
            AddNumber("hardwareDetentAB", filter.HardwareDetentAB);
            AddNumber("hardwareDetentMax", filter.HardwareDetentMax);
            text.Append(" }");
  void AddNumber(string key,double? value)
  {
      if (value is not null)
          text.Append(" [\"").Append(key).Append("\"] = ").Append(Number(value.Value)).Append(',');
  }
  void AddBool(string key,bool? value)
  {
      if (value is not null)
          text.Append(" [\"").Append(key).Append("\"] = ").Append(value.Value ? "true" : "false").Append(',');
  }
        }
        private static string Number(double value) =>
            double.IsFinite(value) ? value.ToString("R", CultureInfo.InvariantCulture)
                                   : throw new InvalidDataException("Axis filter values must be finite.");
        private static string Escape(string value)
        {
            var result = new StringBuilder();
            foreach (var c in value)
                result.Append(c switch { '\\' => "\\\\", '"' => "\\\"", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t",
                                         _ when char.IsControl(c) =>
                                             throw new InvalidDataException("Unsupported Lua control character."),
                                         _ => c.ToString() });
            return result.ToString();
        }
        #endregion END private helpers
    }
}

