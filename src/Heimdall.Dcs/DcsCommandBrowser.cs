using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Dcs
{
    /// <summary>Projects native module/UI-layer palettes into the shared browser without changing draft assignments.</summary>
    public static class DcsCommandBrowser
    {
        #region BEGIN Public
        /// <summary>Maps a native command for shared presentation; native codes remain opaque identities.</summary>
        public static SimCommand Describe(CommandPaletteEntry command) => new()
        {
            Id = command.Id, DisplayName = command.Name, Category = command.Category,
            Type = command.IsAxis ? CommandType.Axis : CommandType.Button
        };
        /// <summary>Intersects display filters with native layer/controller ownership, retaining the every-device filter.</summary>
        public static IReadOnlyList<CommandPaletteEntry> Filter(IReadOnlyList<CommandPaletteEntry> commands,
            IReadOnlyList<DraftAssignment> assignments, AssignmentLayer? layer, Guid? controllerId,
            IReadOnlyCollection<Guid> devices, string? category = null, CommandType? type = null,
            string search = "", DcsCommandAssignmentStatus status = DcsCommandAssignmentStatus.All)
        {
            if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
            var owners = assignments.Where(x => layer is null || x.Slot.Layer == layer)
                .ToLookup(x => x.Command.Id, StringComparer.Ordinal);
            var entries = commands.Select(command => new GameCommandEntry(Describe(command), "", command.Category,
                [command.IsAxis ? CommandType.Axis : CommandType.Button],
                owners[command.Id].Select(x => x.Slot.ProfileId.ToString("D")).Distinct(StringComparer.Ordinal).ToArray()));
            var query = new GameCommandQuery(Category: category, InputType: type, Search: search,
                AssignmentStatus: SharedStatus(status, controllerId));
            var visible = GameCommandBrowser.Filter(entries, query, controllerId?.ToString("D"));
            if (status == DcsCommandAssignmentStatus.AssignedEverywhere)
                visible = visible.Where(x => devices.Count > 0 && devices.All(id => x.AssignedControllers.Contains(id.ToString("D"), StringComparer.Ordinal))).ToArray();
            var native = commands.ToDictionary(x => x.Id, StringComparer.Ordinal);
            return visible.Select(x => native[x.Command.Id]).ToArray();
        }
        #endregion END Public
        #region BEGIN private helpers
        private static GameCommandAssignmentStatus SharedStatus(DcsCommandAssignmentStatus status, Guid? controllerId) => status switch
        {
            DcsCommandAssignmentStatus.AssignedHere => controllerId.HasValue
                ? GameCommandAssignmentStatus.AssignedToController : GameCommandAssignmentStatus.AssignedAnywhere,
            DcsCommandAssignmentStatus.UnassignedHere => controllerId.HasValue
                ? GameCommandAssignmentStatus.NotAssignedToController : GameCommandAssignmentStatus.UnassignedEverywhere,
            DcsCommandAssignmentStatus.UnassignedEverywhere => GameCommandAssignmentStatus.UnassignedEverywhere,
            _ => GameCommandAssignmentStatus.All
        };
        #endregion END private helpers
    }
}
