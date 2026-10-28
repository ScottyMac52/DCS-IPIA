using Heimdall.Domain.MultiSim;
using Heimdall.Application.Games;

namespace Heimdall.EliteDangerous
{
    /// <summary>Composes display-only command filters independently of WPF and native profile editing.</summary>
    public static class EliteCommandBrowser
    {
        #region BEGIN Public
        /// <summary>Gets an assigned display category, retaining unknown commands under Other.</summary>
        public static string Category(SimCommand command) => command.Metadata.GetValueOrDefault("ed.category") ?? "Other";
        /// <summary>Lists all categories in an activity, independent of the current text and type filters.</summary>
        public static IReadOnlyList<string> Categories(IEnumerable<SimCommand> commands, string section = "All") =>
            GameCommandBrowser.Categories(Entries(commands), section == "All" ? null : section);
        /// <summary>Projects Elite categories and binding owners into shared browser facts.</summary>
        public static IReadOnlyList<GameCommandEntry> Entries(IEnumerable<SimCommand> commands,
            IReadOnlyList<EliteAssignmentRow>? assignments = null)
        {
            var owners = (assignments ?? []).ToLookup(x => x.CommandId, StringComparer.Ordinal);
            return commands.Select(command => new GameCommandEntry(command, command.Category, Category(command),
                [command.Type], owners[command.Id].Select(x => x.DeviceId).Distinct(StringComparer.Ordinal).ToArray())).ToArray();
        }
        /// <summary>Declares native input filters instead of hard-coding them in shared WPF.</summary>
        public static IReadOnlyList<GameCommandTypeOption> InputTypes { get; } =
            [new("All types", null), new("Buttons", CommandType.Button), new("Axes", CommandType.Axis)];
        /// <summary>Declares ownership filters supported by Elite's assignment projection.</summary>
        public static IReadOnlyList<GameCommandStatusOption> AssignmentStatuses { get; } =
        [
            new("All", GameCommandAssignmentStatus.All),
            new("Assigned Only", GameCommandAssignmentStatus.AssignedToController),
            new("Not Assigned", GameCommandAssignmentStatus.NotAssignedToController),
            new("Not Assigned by Any", GameCommandAssignmentStatus.UnassignedEverywhere),
            new("Assigned by Any", GameCommandAssignmentStatus.AssignedAnywhere)
        ];
        /// <summary>Intersects activity, category, native input type and case-insensitive text; never mutates the catalog.</summary>
        public static IReadOnlyList<SimCommand> Filter(IEnumerable<SimCommand> commands, string section = "All",
            string? category = null, CommandType? type = null, string search = "",
            IReadOnlyList<EliteAssignmentRow>? assignments = null, string? deviceId = null, EliteAssignmentStatus status = EliteAssignmentStatus.All)
        {
            if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
            var query = new GameCommandQuery(section == "All" ? null : section, category, type, search,
                (GameCommandAssignmentStatus)(int)status);
            return GameCommandBrowser.Filter(Entries(commands, assignments), query, deviceId).Select(x => x.Command).ToArray();
        }
        #endregion END Public
    }
}
