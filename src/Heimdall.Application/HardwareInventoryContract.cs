using System.Text.Json;

namespace Heimdall.Application
{
    /// <summary>Validates portable inventory before database writes or snapshot restoration.</summary>
    public static class HardwareInventoryContract
    {
        #region BEGIN Public
        /// <summary>Validates versioned relationships and native control interpretation.</summary>
        /// <param name="snapshot">Inventory to validate.</param>
        public static void Validate(HardwareInventorySnapshot snapshot)
        {
            if (snapshot.SchemaVersion != 1 || snapshot.Definitions is null || snapshot.Instances is null || snapshot.Observations is null)
                throw new InvalidDataException("Unsupported or incomplete hardware inventory snapshot.");
            var definitions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in snapshot.Definitions)
            {
                ValidateCapabilities(definition.Capabilities);
                if (!definitions.Add(definition.Id) || definition.Id != ControllerCapabilityMapper.Fingerprint(definition.Capabilities))
                    throw new InvalidDataException("Invalid or duplicate hardware definition.");
            }
            var instances = new HashSet<Guid>();
            foreach (var instance in snapshot.Instances)
                if (instance.PhysicalGuid == Guid.Empty || !instances.Add(instance.PhysicalGuid) || !definitions.Contains(instance.DefinitionId) ||
                    string.IsNullOrWhiteSpace(instance.Label) || instance.Label.Length > 200 || string.IsNullOrWhiteSpace(instance.NativeName) ||
                    instance.Role is null || instance.Role.Length > 200 || instance.TemplateCandidates is null ||
                    instance.TemplateCandidates.Any(string.IsNullOrWhiteSpace))
                    throw new InvalidDataException("Invalid physical instance or missing definition.");
            var observations = new HashSet<Guid>();
            foreach (var observed in snapshot.Observations)
            {
                if (observed.Device is null || observed.Device.PhysicalGuid == Guid.Empty || !observations.Add(observed.Device.PhysicalGuid) ||
                    string.IsNullOrWhiteSpace(observed.Device.Name))
                    throw new InvalidDataException("Invalid or duplicate discovery observation.");
                if (observed.Capabilities is not null) ValidateCapabilities(observed.Capabilities);
            }
        }
        /// <summary>Exports inventory without including any module assignment or deployment record.</summary>
        /// <param name="snapshot">Reviewed inventory.</param>
        /// <returns>Versioned portable JSON.</returns>
        public static string Export(HardwareInventorySnapshot snapshot)
        {
            Validate(snapshot);
            return JsonSerializer.Serialize(snapshot);
        }
        /// <summary>Reads and validates portable inventory without trusting historical connection state.</summary>
        /// <param name="json">Versioned portable JSON.</param>
        /// <returns>Validated inventory with disconnected historical observations.</returns>
        public static HardwareInventorySnapshot Import(string json)
        {
            var snapshot = JsonSerializer.Deserialize<HardwareInventorySnapshot>(json)
                ?? throw new InvalidDataException("Missing inventory snapshot.");
            Validate(snapshot);
            return snapshot with { Observations = snapshot.Observations.Select(x => x with { Device = x.Device with { Connected = false } }).ToArray() };
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void ValidateCapabilities(ControllerCapabilities capabilities)
        {
            if (capabilities is null || capabilities.SchemaVersion != 1 || capabilities.Inputs is null || capabilities.Unsupported is null)
                throw new InvalidDataException("Incomplete capability evidence.");
            var mapped = ControllerCapabilityMapper.Map(capabilities.Inputs.Select(x => x.Evidence)
                .Concat(capabilities.Unsupported.Select(x => x.Evidence)));
            if (!mapped.Inputs.SequenceEqual(capabilities.Inputs) ||
                !mapped.Unsupported.Select(x => x.Evidence).OrderBy(x => x.NativeType).ThenBy(x => x.ObjectGuid)
                    .SequenceEqual(capabilities.Unsupported.Select(x => x.Evidence).OrderBy(x => x.NativeType).ThenBy(x => x.ObjectGuid)))
                throw new InvalidDataException("Capability controls disagree with the live-test mapping.");
        }
        #endregion END private helpers
    }
}
