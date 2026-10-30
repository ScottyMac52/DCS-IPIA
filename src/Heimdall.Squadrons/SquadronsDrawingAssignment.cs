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
            var records = backend.ReadAssignableBindings(profile);
            if (!backend.HasCapturedJoystickConcept(profile, concept))
                records = FactoryRecords(records.Where(row => row.Concept == concept), input.Type == CommandType.Axis);
            if (input.Type == CommandType.Axis)
            {
                var positive = SquadronsAssignmentTarget.Find(records, plan.EditableRecordIds, input with { Direction = 1 }, slot - 1);
                var negative = SquadronsAssignmentTarget.Find(records, plan.EditableRecordIds, input with { Direction = -1 }, slot - 1);
                if (positive is null || negative is null)
                    throw new InvalidOperationException("This command has incomplete or ambiguous joystick axis metadata. Select a specific saved assignment for review.");
                return new(concept, input, positive.RecordId, negative.RecordId);
            }
            var record = SquadronsAssignmentTarget.Find(records, plan.EditableRecordIds, input, slot - 1);
            return new(concept, input, record?.RecordId);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static IReadOnlyList<SquadronsBindingRow> FactoryRecords(IEnumerable<SquadronsBindingRow> records, bool axis)
        {
            if (!axis)
                return records.Where(row => row.Input?.Type is CommandType.Button or CommandType.Hat)
                    .OrderBy(row => row.Fields["negate"]).ThenBy(row => row.Index).Take(1).ToArray();
            // Factory alternatives describe possible defaults, not multiple user assignments.
            // Choose one complete native physical-axis family, retaining both original identifiers.
            return records.Where(row => row.Input?.Type == CommandType.Axis)
                .GroupBy(row => row.Input!.PhysicalKey, StringComparer.Ordinal)
                .Where(group => group.Count(row => row.Input!.Direction == 1) == 1 &&
                    group.Count(row => row.Input!.Direction == -1) == 1)
                .OrderBy(group => group.Min(row => row.Index)).FirstOrDefault()?.ToArray() ?? [];
        }
        #endregion END private helpers
    }
}
