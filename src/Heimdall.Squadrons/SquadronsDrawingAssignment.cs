using System.Globalization;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>A simulator-owned native operation resolved from one whole physical drawing input.</summary>
    public sealed record SquadronsDrawingAssignment(string Concept, SquadronsInput Input,
        string? RecordId, string? NegativeRecordId = null)
    {
        #region BEGIN Public
        /// <summary>Resolves existing records or a creation request without changing the profile.
        /// Whole axes require two unambiguous directional records; the session validates and stages them atomically.</summary>
        public static SquadronsDrawingAssignment For(SquadronsProfileBackend backend, ProfileDocument profile,
            string commandId, string controllerId, string physicalKey, bool isAxis)
        {
            ArgumentNullException.ThrowIfNull(backend);
            ArgumentNullException.ThrowIfNull(profile);
            const string prefix = "squadrons.joystick.";
            if (!controllerId.StartsWith(prefix, StringComparison.Ordinal) ||
                !int.TryParse(controllerId[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var slot) ||
                slot is < 1 or > 10 || !profile.Devices.Any(x => x.LogicalId == controllerId))
                throw new InvalidOperationException("Select an existing Squadrons joystick slot.");
            var command = backend.GetCatalog(profile).Commands.Single(x => x.Id == commandId);
            var concept = command.Metadata["squadrons.concept"];
            var plan = SquadronsAssignmentPlanner.For(backend, profile, concept);
            var input = SquadronsInputCodec.FromDrawing(physicalKey, isAxis);
            var records = backend.ReadBindings(profile);
            if (input.Type == CommandType.Axis)
            {
                var positive = SquadronsAssignmentTarget.Find(records, plan.EditableRecordIds, input with { Direction = 1 }, slot - 1);
                var negative = SquadronsAssignmentTarget.Find(records, plan.EditableRecordIds, input with { Direction = -1 }, slot - 1);
                if (positive is null || negative is null)
                    throw new InvalidOperationException("This command requires both editable native direction records to assign a whole physical axis.");
                return new(concept, input, positive.RecordId, negative.RecordId);
            }
            var record = SquadronsAssignmentTarget.Find(records, plan.EditableRecordIds, input, slot - 1);
            return new(concept, input, record?.RecordId);
        }
        #endregion END Public
    }
}
