using Heimdall.Application;

namespace Heimdall.Dcs
{
    /// <summary>Stages inventoried controllers through the same module profile boundaries as bundled hardware.</summary>
    public static class DcsInventoryHardwareAuthoring
    {
        #region BEGIN Public
        /// <summary>Lists explicitly adopted units, preserving repeated names and offline identities.</summary>
        /// <param name="inventory">Saved inventory.</param>
        /// <returns>Reviewed selections in stable physical identity order.</returns>
        public static IReadOnlyList<InventoryHardwareChoice> Choices(HardwareInventorySnapshot inventory)
        {
            HardwareInventoryContract.Validate(inventory);
            return inventory.Instances.Where(x => x.Selected).OrderBy(x => x.PhysicalGuid)
                .Select(x => new InventoryHardwareChoice(x, inventory.Definitions.Single(d => d.Id == x.DefinitionId))).ToArray();
        }

        /// <summary>Lists artwork whose controls exactly match an adopted unit, without guessing its attached grip.</summary>
        /// <param name="hardware">Available pinned or bundled drawings.</param>
        /// <param name="choice">Reviewed physical unit.</param>
        /// <returns>Explicit artwork choices; unsupported native objects remain outside the mapped controls.</returns>
        public static IReadOnlyList<WorkspaceHardware> Drawings(IEnumerable<WorkspaceHardware> hardware, InventoryHardwareChoice choice) =>
            hardware.Where(x => x.Diagram.GenericCapabilities is null &&
                DcsInputKey.SamePhysicalInputs(x.Diagram.Controls.Select(c => c.InputKey), choice.Definition.Capabilities.DcsKeys))
                .DistinctBy(x => x.DeviceId).OrderBy(x => x.Title, StringComparer.Ordinal).ToArray();

        /// <summary>Applies explicitly chosen artwork to an existing inventory unit while preserving all module data.</summary>
        /// <param name="workspace">Current draft.</param>
        /// <param name="profileId">Existing physical module profile.</param>
        /// <param name="choice">Reviewed inventory unit.</param>
        /// <param name="hardware">Explicit compatible drawing.</param>
        /// <param name="drawio">Self-contained bundled editable source.</param>
        /// <returns>A draft with a pinned drawing revision; profile identities, assignments and modifiers are retained.</returns>
        public static DcsEditorWorkspaceSnapshot ApplyDrawing(DcsEditorWorkspaceSnapshot workspace, Guid profileId,
            InventoryHardwareChoice choice, WorkspaceHardware hardware, string drawio)
        {
            var membership = DcsWorkspaceStructureEditor.Describe(workspace).Devices.First(x => x.ProfileId == profileId);
            if ((membership.PhysicalGuid ?? DcsPhysicalIdentity.FromFileName(membership.FileName)) != choice.Instance.PhysicalGuid)
                throw new InvalidOperationException("Select the same inventory unit as the module controller before applying artwork.");
            var current = workspace.Diagrams.Single(x => x.ProfileId == profileId);
            if (!current.DrawingUnlinked)
                throw new InvalidOperationException("Unlink the current drawing before choosing replacement artwork.");
            if (Drawings([hardware], choice).Count == 0)
                throw new InvalidOperationException("The selected drawing must match this unit's actual supported controls.");
            var inputs = hardware.Diagram.Controls.GroupBy(x => x.Control, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First().InputKey, StringComparer.Ordinal);
            var revision = DrawioMappingImporter.Import(DrawioMappingImporter.WithCatalogInputs(drawio, inputs),
                choice.Definition, hardware.DeviceId + ".drawio", "bundled-artwork");
            return DcsUserMappingAdoption.Apply(workspace, profileId, revision);
        }

        /// <summary>Creates independent scope membership after an explicit inventory selection.</summary>
        /// <param name="workspace">Module draft.</param>
        /// <param name="choice">Reviewed physical inventory choice.</param>
        /// <param name="layer">Aircraft or module UI Layer.</param>
        /// <param name="hardware">Optional explicitly selected compatible bundled layout.</param>
        /// <returns>A portable module draft with no copied personal defaults.</returns>
        public static DcsEditorWorkspaceSnapshot Add(DcsEditorWorkspaceSnapshot workspace, InventoryHardwareChoice choice,
            AssignmentLayer layer, WorkspaceHardware? hardware = null)
        {
            HardwareInventoryContract.Validate(new(1, [choice.Definition], [choice.Instance], []));
            if (!choice.Instance.Selected)
                throw new InvalidOperationException("Adopt this physical unit in hardware inventory before using it in a module.");
            hardware ??= GenericHardwareLayout.Create(choice.Definition);
            if (!DcsInputKey.SamePhysicalInputs(hardware.Diagram.Controls.Select(x => x.InputKey),
                choice.Definition.Capabilities.DcsKeys))
                throw new InvalidOperationException("The selected layout must match the inventory's actual supported controls.");
            return DcsWorkspaceStructureEditor.AddDevice(workspace, hardware, layer, choice.FileName,
                choice.Instance.Label, choice.Instance.Role);
        }

        /// <summary>Changes a generic control's module-owned label without changing its DCS input or other units.</summary>
        /// <param name="workspace">Module draft.</param>
        /// <param name="profileId">Selected physical module profile.</param>
        /// <param name="inputKey">Actual supported DCS input.</param>
        /// <param name="label">Readable user label.</param>
        /// <returns>Updated module layout; assignments and capability evidence remain pinned.</returns>
        public static DcsEditorWorkspaceSnapshot RenameControl(DcsEditorWorkspaceSnapshot workspace, Guid profileId, string inputKey, string label)
        {
            var diagram = workspace.Diagrams.Single(x => x.ProfileId == profileId);
            GenericHardwareLayout.Validate(diagram);
            var capabilities = diagram.GenericCapabilities ?? throw new InvalidOperationException("Select a generic controller control.");
            var labels = diagram.Controls.GroupBy(x => x.InputKey, StringComparer.Ordinal)
                    .ToDictionary(x => x.Key, x => x.OrderBy(y => y.Control.EndsWith("-shifted", StringComparison.Ordinal)).First().Label, StringComparer.Ordinal);
            if (!labels.ContainsKey(inputKey)) throw new InvalidOperationException("Select an actual supported control.");
            labels[inputKey] = label.Trim();
            if (diagram.CustomMapping is not null)
            {
                var updated = diagram with { Controls = diagram.Controls.Select(x => x.InputKey == inputKey ? x with { Label = label.Trim() } : x).ToArray() };
                DcsUserMappingAdoption.Validate(updated);
                return workspace with { Diagrams = workspace.Diagrams.Select(x => x.ProfileId == profileId ? updated : x).ToArray() };
            }
            var replacement = GenericHardwareLayout.Create(new(ControllerCapabilityMapper.Fingerprint(capabilities), capabilities), labels).Diagram;
            return workspace with { Diagrams = workspace.Diagrams.Select(x => x.ProfileId == profileId
                ? x with { Controls = replacement.Controls, Svg = replacement.Svg } : x).ToArray() };
        }
        #endregion END Public
    }
}
