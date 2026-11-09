using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;

namespace Heimdall.WarThunder
{
    /// <summary>War Thunder native occurrence and ownership hooks for the shared browser.</summary>
    public class WarThunderAssignmentBrowser(IGameWorkspaceContext<GameWorkspace> context, WarThunderProfileBackend backend)
        : GameAssignmentBrowserBase<SimCommand, WarThunderWorkspaceRow, WarThunderBrowserFilter>(context, new())
    {
        private IReadOnlyList<WarThunderWorkspaceRow> _rows = [];
        private GameCommandQuery _query = new();
        private Dictionary<string, GameCommandEntry> _entries = new Dictionary<string, GameCommandEntry>();
        protected override bool SelectInitialCommand => true;
        protected override bool SelectInitialRecord => true;
        protected override WarThunderBrowserFilter FilterForSelection(WarThunderBrowserFilter filter, WorkspaceSelection selection) => filter with { ControllerId = selection.ControllerId };
        protected override WorkspaceSelection ScopeSelection => Context.State.Selection with { ControllerId = Filter.ControllerId, NativeScope = Context.State.Identity.Key + ":" + Filter.ControllerId + ":" + Filter.Category + ":" + Filter.InputType };
        protected override IReadOnlyList<SimCommand> ReadCommands()
        {
            var snapshot = context.Working;
            if (snapshot is null) { _rows = []; _entries = new Dictionary<string, GameCommandEntry>(); return []; }
            var commands = backend.GetCatalog(snapshot.Profile).Commands; _rows = WarThunderWorkspaceProjection.Rows(snapshot.Profile, commands);
            _entries = WarThunderWorkspaceProjection.CommandEntries(commands, _rows).ToDictionary(x => x.Command.Id, StringComparer.Ordinal);
            return commands;
        }
        protected override IReadOnlyList<WarThunderWorkspaceRow> ReadRecords() => _rows;
        protected override string CommandId(SimCommand command) => command.Id;
        protected override string RecordId(WarThunderWorkspaceRow record) => record.RecordId;
        protected override void PrepareQuery(WarThunderBrowserFilter filter, string search) => _query = new(Category: filter.Category, InputType: filter.InputType, AssignmentStatus: filter.Status);
        protected override bool IncludeCommand(SimCommand command, WarThunderBrowserFilter filter, string search) =>
            WarThunderWorkspaceProjection.MatchesCommand(_entries[command.Id], _query, search, filter.ControllerId);
        protected override bool IncludeRecord(WarThunderWorkspaceRow record, WarThunderBrowserFilter filter, string search) =>
            VisibleCommands.Count > 0 && (Context.State.Selection.CommandId is { } id ? record.Command.Id == id
                : filter.ControllerId is null || record.DeviceIds.Contains(filter.ControllerId) || record.Binding.Empty);
        protected override WorkspaceSelection SelectionForCommand(SimCommand command) => ScopeSelection with { CommandId = command.Id };
        protected override WorkspaceSelection SelectionForRecord(WarThunderWorkspaceRow record) => ScopeSelection with
        { CommandId = record.Command.Id, AlternativeId = record.RecordId, InputId = null };
    }
}
