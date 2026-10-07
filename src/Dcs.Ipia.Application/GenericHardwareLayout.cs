using System.Xml.Linq;

namespace Dcs.Ipia.Application
{
    /// <summary>Creates portable image-free controls from a reviewed native capability definition.</summary>
    public static class GenericHardwareLayout
    {
        #region BEGIN Public
        /// <summary>Creates the reusable layout without physical identities, assignments or artwork paths.</summary>
        /// <param name="definition">Reviewed content-addressed capabilities.</param>
        /// <param name="labels">Optional module-owned input labels.</param>
        /// <returns>A deterministic hardware choice with only actual supported inputs.</returns>
        public static WorkspaceHardware Create(HardwareDefinition definition, IReadOnlyDictionary<string, string>? labels = null)
        {
            HardwareInventoryContract.Validate(new(1, [definition], [], []));
            var keys = definition.Capabilities.Inputs.OrderBy(x => x.Kind).ThenBy(x => (x.Evidence.NativeType >> 8) & 65535).SelectMany(input => input.Kind == ControllerInputKind.Hat
                ? definition.Capabilities.DcsKeys.Where(key => key.StartsWith(input.DcsKey + "_", StringComparison.Ordinal))
                : [input.DcsKey]).ToArray();
            if (keys.Length == 0)
                throw new InvalidDataException("A generic definition needs at least one supported control.");
            if (labels is not null && labels.Keys.Any(key => !keys.Contains(key, StringComparer.Ordinal)))
                throw new InvalidDataException("A control label does not belong to the definition.");
            var height = 50 + Math.Min(keys.Length, 64) * 48;
            var width = 900 * ((keys.Length + 63) / 64);
            XNamespace ns = "http://www.w3.org/2000/svg";
            var svg = new XElement(ns + "svg", new XAttribute("viewBox", $"0 0 {width} {height}"),
                new XAttribute("width", width), new XAttribute("height", height),
                new XElement(ns + "rect", new XAttribute("width", width), new XAttribute("height", height), new XAttribute("fill", "#ffffff")));
            var controls = new List<HardwareGraphicControl>();
            for (var index = 0; index < keys.Length; index++)
            {
                var key = keys[index];
                var label = labels?.GetValueOrDefault(key) ?? key;
                if (string.IsNullOrWhiteSpace(label) || label.Length > 200 || label.Any(char.IsControl))
                    throw new InvalidDataException("Control labels must be readable text of at most 200 characters.");
                var x = index / 64 * 900;
                var y = 40 + index % 64 * 48;
                var axis = definition.Capabilities.Inputs.Any(input => input.Kind == ControllerInputKind.Axis && input.DcsKey == key);
                controls.Add(new(key, label, key, x + 290, y, 580, 42, axis));
                svg.Add(new XElement(ns + "text", new XAttribute("x", x + 12), new XAttribute("y", y + 27),
                    new XAttribute("font-size", 16), new XAttribute("fill", "#111111"), label));
                svg.Add(new XElement(ns + "rect", new XAttribute("id", "box-" + key), new XAttribute("x", x + 290),
                    new XAttribute("y", y), new XAttribute("width", 580), new XAttribute("height", 42),
                    new XAttribute("fill", "#ffffff"), new XAttribute("stroke", "#64748b")));
                svg.Add(new XElement(ns + "text", new XAttribute("id", "lbl-" + key), new XAttribute("x", x + 300),
                    new XAttribute("y", y + 27), new XAttribute("font-size", 16), "Unassigned"));
            }
            var id = "generic-" + definition.Id;
            var diagram = new UiLayerDiagram("Generic controller", null, "generic/" + id + ".svg", width, height, [], [])
            {
                Svg = svg.ToString(SaveOptions.DisableFormatting), Controls = controls, GenericCapabilities = definition.Capabilities
            };
            return new(id, "Generic controller", "Generic controller.diff.lua", diagram);
        }

        /// <summary>Rejects unsupported or forged generic controls and any artwork dependency.</summary>
        /// <param name="diagram">Pinned module layout.</param>
        public static void Validate(UiLayerDiagram diagram)
        {
            if (diagram.GenericCapabilities is not { } capabilities)
            {
                if (diagram.Path.StartsWith("generic/", StringComparison.Ordinal))
                    throw new InvalidDataException("A generic layout is missing its native capability evidence.");
                return;
            }
            var definition = new HardwareDefinition(ControllerCapabilityMapper.Fingerprint(capabilities), capabilities);
            var expected = Create(definition, diagram.Controls.ToDictionary(control => control.InputKey, control => control.Label, StringComparer.Ordinal)).Diagram;
            if (diagram.Path != expected.Path || diagram.Svg != expected.Svg || diagram.Width != expected.Width ||
                diagram.Height != expected.Height || diagram.Background.Length != 0 ||
                !diagram.Controls.OrderBy(x => x.InputKey, StringComparer.Ordinal).SequenceEqual(expected.Controls.OrderBy(x => x.InputKey, StringComparer.Ordinal)))
                throw new InvalidDataException("Generic layout differs from its verified supported controls.");
        }
        /// <summary>Validates pinned generic profiles and their saved inputs before persistence or export.</summary>
        /// <param name="workspace">Module-owned snapshot.</param>
        public static void ValidateWorkspace(EditorWorkspaceSnapshot workspace)
        {
            foreach (var diagram in workspace.Diagrams)
            {
                Validate(diagram);
                var profile = workspace.Configuration.Profiles.SingleOrDefault(x => x.Id == diagram.ProfileId);
                if (diagram.GenericCapabilities is null)
                {
                    if (profile?.DeviceId.StartsWith("generic-", StringComparison.Ordinal) == true)
                        throw new InvalidDataException("Generic profile evidence is missing.");
                    continue;
                }
                if (profile is null || profile.DeviceId != "generic-" + ControllerCapabilityMapper.Fingerprint(diagram.GenericCapabilities))
                    throw new InvalidDataException("Generic profile definition identity differs from its capabilities.");
                foreach (var assignment in workspace.Assignments.Where(x => x.Slot.ProfileId == diagram.ProfileId))
                    if (!diagram.Controls.Any(x => x.IsAxis == (assignment.Slot.Section == Domain.BindingSection.Axis) &&
                        DcsInputKey.SamePhysicalInput(x.InputKey, assignment.Slot.InputKey)))
                        throw new InvalidDataException("A generic assignment references an unsupported control.");
                foreach (var modifier in (WorkspaceManagement.Read(workspace)?.Modifiers ?? []).Where(x => x.ProfileId == diagram.ProfileId))
                    if (!diagram.Controls.Any(x => !x.IsAxis && DcsInputKey.SamePhysicalInput(x.InputKey, modifier.Key)))
                        throw new InvalidDataException("A generic modifier references an unsupported digital control.");
            }
        }
        #endregion END Public
    }
}
