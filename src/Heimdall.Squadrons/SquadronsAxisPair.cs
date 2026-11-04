using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>Resolves an editable, complete saved axis without treating archived templates as active bindings.</summary>
    public sealed record SquadronsAxisPair(SquadronsWorkspaceRow Positive, SquadronsWorkspaceRow Negative)
    {
        #region BEGIN Public
        /// <summary>Accepts exactly two editable opposite directions of one native concept on one controller and axis.</summary>
        public static SquadronsAxisPair? For(IEnumerable<SquadronsWorkspaceRow> source)
        {
            var rows = source.ToArray();
            if (rows.Length != 2 || rows.Any(row => !row.Editable)) return null;
            var positive = rows.FirstOrDefault(row => row.Native.Input is { Type: CommandType.Axis, Direction: 1 });
            var negative = rows.FirstOrDefault(row => row.Native.Input is { Type: CommandType.Axis, Direction: -1 });
            if (positive is null || negative is null || positive.Native.Concept != negative.Native.Concept ||
                positive.DeviceId != negative.DeviceId || positive.Native.Input!.PhysicalKey != negative.Native.Input!.PhysicalKey)
                return null;
            return new(positive, negative);
        }

        /// <summary>Finds saved directions for a catalog command and physical target using native slot projections.</summary>
        public static SquadronsAxisPair? Find(ProfileDocument profile, string commandId, string deviceId, string physicalKey)
        {
            var backend = new SquadronsProfileBackend();
            var command = backend.GetCatalog(profile).Find(commandId);
            if (command is null) return null;
            var concept = command.Metadata["squadrons.concept"];
            return For(SquadronsWorkspaceProjection.Rows(profile).Where(row => row.DeviceId == deviceId &&
                row.Native.Concept == concept && row.Native.Input?.PhysicalKey == SquadronsInputCodec.PhysicalKey(physicalKey)));
        }
        #endregion END Public
    }
}
