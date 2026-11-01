using System.Globalization;
using Heimdall.Application;
using Heimdall.Domain.MultiSim;

namespace Heimdall.WarThunder
{
    /// <summary>Projects captured bindings and builds the same native edit for drawing Apply and drop.</summary>
    public static class WarThunderDrawingProjection
    {
        #region BEGIN Public
        /// <summary>Labels physical callouts using verified local indices and saved global offsets.</summary>
        public static KneeboardComposite Diagram(UiLayerDiagram drawing, ProfileDocument profile, string deviceId, WarThunderPhysicalMap map)
        {
            var native = WarThunderProfileBackend.ReadDevices(profile).Single(device => device.Id == deviceId);
            var rows = WarThunderWorkspaceProjection.Rows(profile, new WarThunderProfileBackend().GetCatalog(profile).Commands);
            var labels = drawing.Controls.ToDictionary(control => control.Control, control =>
            {
                var inputs = map.Inputs.Where(candidate => candidate.Key == control.InputKey).ToArray();
                return (IReadOnlyList<KneeboardLabelVariant>)(inputs.Length == 0 ? [] : MatchingRows(rows, native, inputs)
                    .Select(row => row.Name).Distinct(StringComparer.Ordinal).Select(name => new KneeboardLabelVariant(name,
                        KneeboardStateColors.Base, AssignmentLayer.Module, "")).ToArray());
            }, StringComparer.Ordinal);
            return new(drawing.Title, "War Thunder", labels, new Dictionary<string, string>(), []);
        }
        /// <summary>Returns captured alternatives assigned to one physical callout, using the same ownership rules as its labels.</summary>
        public static IReadOnlyList<WarThunderWorkspaceRow> Assignments(ProfileDocument profile, string deviceId, string key, WarThunderPhysicalMap map)
        {
            var native = WarThunderProfileBackend.ReadDevices(profile).Single(device => device.Id == deviceId);
            var rows = WarThunderWorkspaceProjection.Rows(profile, new WarThunderProfileBackend().GetCatalog(profile).Commands);
            return MatchingRows(rows, native, map.Inputs.Where(input => input.Key == key).ToArray()).ToArray();
        }
        /// <summary>Rebinds a simple existing alternative or adds a new one without destroying chords or keyboard alternatives.</summary>
        public static WarThunderNativeEdit Assignment(ProfileDocument profile, string commandId, string deviceId, WarThunderPhysicalInput input)
        {
            var command = new WarThunderProfileBackend().GetCatalog(profile).Find(commandId) ?? throw new InvalidOperationException("Select a captured command.");
            if (command.Type != input.Type) throw new InvalidOperationException("The command and physical input kinds do not match.");
            var bindings = WarThunderProfileBackend.ReadBindings(profile).Where(binding => binding.Section == command.Metadata["war-thunder.section"]
                && binding.Command == command.Metadata["war-thunder.command"]).ToArray();
            var existing = bindings.FirstOrDefault(binding => WarThunderWorkspaceProjection.InputFields(binding).Count == 1
                && (input.Type == CommandType.Axis || binding.Fields.All(scalar => WarThunderWorkspaceProjection.FieldName(scalar) is "joyButton" or "activationType")));
            if (existing is not null)
            {
                var capabilities = WarThunderProfileBackend.GetBindingCapabilities(profile, existing.Id);
                if (!capabilities.RebindNativeInputs) throw new InvalidOperationException("This captured record does not support rebinding.");
                return new(WarThunderEditKind.Rebind, existing.Id, WarThunderWorkspaceProjection.InputFields(existing).Single().Address,
                    deviceId, commandId, input.Type, input.Index);
            }
            if (!bindings.Any(binding => WarThunderProfileBackend.GetBindingCapabilities(profile, binding.Id).AddNativeInput))
                throw new InvalidOperationException("This command does not support adding a native input.");
            return new(WarThunderEditKind.Add, DeviceId: deviceId, CommandId: commandId, Type: input.Type, NativeIndex: input.Index);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static IEnumerable<WarThunderWorkspaceRow> MatchingRows(IReadOnlyList<WarThunderWorkspaceRow> rows,
            WarThunderDevice native, WarThunderPhysicalInput[] inputs) => rows.Where(row => inputs.Any(input =>
                WarThunderWorkspaceProjection.InputFields(row.Binding).Any(scalar =>
                    WarThunderWorkspaceProjection.FieldName(scalar) == (input.Type == CommandType.Axis ? "axisId" : "joyButton")
                    && int.TryParse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var global)
                    && global == native.Encode(input.Type, input.Index))));
        #endregion END private helpers
    }
}
