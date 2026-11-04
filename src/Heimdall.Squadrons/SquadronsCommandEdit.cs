using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>Owns native command planning and dispatch; UI consumers supply only the selected command and physical target.</summary>
    public sealed class SquadronsCommandEdit(SquadronsCommandEditKind kind, string commandId,
        string logicalDeviceId, string key, CommandType type, string recordId = "") : IVerifiedSimNativeEdit
    {
        #region BEGIN Public
        /// <summary>Gets the requested whole-control, record or reversal operation.</summary>
        public SquadronsCommandEditKind Kind { get; } = kind;
        /// <summary>Gets the catalog command identity captured when the action was requested.</summary>
        public string CommandId { get; } = commandId;
        /// <summary>Gets the native controller receiving the physical input.</summary>
        public string LogicalDeviceId { get; } = logicalDeviceId;
        /// <summary>Gets the adapter input key, independent of the displayed caption.</summary>
        public string Key { get; } = key;
        /// <summary>Gets the selected input kind.</summary>
        public CommandType Type { get; } = type;
        /// <summary>Gets the explicit record address for advanced single-record editing.</summary>
        public string RecordId { get; } = recordId;
        /// <summary>Gets the adapter responsible for native encoding and preservation.</summary>
        public string BackendId => "star-wars-squadrons";
        /// <summary>Gets the physical key used by the shared session's current hardware-evidence gate.</summary>
        public string PhysicalInputKey => SquadronsInputCodec.PhysicalKey(Key);
        /// <summary>Returns an isolated native result; no session or UI state is mutated here.</summary>
        public ProfileDocument Apply(ProfileDocument profile)
        {
            var backend = new SquadronsProfileBackend();
            return Kind switch
            {
                SquadronsCommandEditKind.AssignControl => AssignControl(backend, profile),
                SquadronsCommandEditKind.AssignRecord => AssignRecord(backend, profile),
                SquadronsCommandEditKind.ReverseAxis => ReverseAxis(backend, profile),
                _ => throw new InvalidOperationException("Unsupported Squadrons command edit.")
            };
        }
        #endregion END Public
        #region BEGIN private helpers
        private ProfileDocument AssignControl(SquadronsProfileBackend backend, ProfileDocument profile)
        {
            var plan = SquadronsDrawingAssignment.For(backend, profile, CommandId, LogicalDeviceId, PhysicalInputKey, Type == CommandType.Axis);
            if (plan.NegativeRecordId is not null)
                return backend.RebindAxisPair(profile, plan.RecordId!, plan.NegativeRecordId, LogicalDeviceId, plan.Input.Key);
            return plan.RecordId is not null
                ? backend.RebindNative(profile, plan.RecordId, LogicalDeviceId, plan.Input.Key, plan.Input.Type, plan.Input.Direction)
                : backend.AddNativeBinding(profile, plan.Concept, LogicalDeviceId, plan.Input.Key, plan.Input.Type, plan.Input.Direction);
        }
        private ProfileDocument AssignRecord(SquadronsProfileBackend backend, ProfileDocument profile)
        {
            var command = backend.GetCatalog(profile).Find(CommandId)
                ?? throw new InvalidOperationException("Select a captured command before editing a native record.");
            var row = backend.ReadBindings(profile).SingleOrDefault(row => row.RecordId == RecordId);
            if (row is null || row.Concept != command.Metadata["squadrons.concept"])
                throw new InvalidOperationException("The native record belongs to another command or no longer exists.");
            var direction = Type == CommandType.Axis
                ? row.Input is { Type: CommandType.Axis } previous ? previous.Direction : 1 : 0;
            return backend.RebindNative(profile, RecordId, LogicalDeviceId, Key, Type, direction);
        }
        private ProfileDocument ReverseAxis(SquadronsProfileBackend backend, ProfileDocument profile)
        {
            if (Type != CommandType.Axis) throw new InvalidOperationException("Select an axis to reverse.");
            var pair = SquadronsAxisPair.Find(profile, CommandId, LogicalDeviceId, Key)
                ?? throw new InvalidOperationException("Select a complete saved axis assignment to reverse.");
            return backend.ReverseAxisPair(profile, pair.Positive.RecordId, pair.Negative.RecordId, LogicalDeviceId, Key);
        }
        #endregion END private helpers
    }
}
