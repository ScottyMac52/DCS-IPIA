using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;

namespace Heimdall.EliteDangerous
{
    /// <summary>ED native projection hooks for the shared browser; catalogs, slot rows and ownership are cached per content revision.</summary>
    public class EliteAssignmentBrowser(IGameWorkspaceContext<GameWorkspace> context, EliteDangerousProfileBackend backend)
        : GameAssignmentBrowserBase<SimCommand, EliteAssignmentRow, EliteBrowserFilter>(context, new())
    {
        private IReadOnlyList<EliteAssignmentRow> _rows = [];
        private GameCommandQuery _query = new();
        private Dictionary<string, GameCommandEntry> _entries = new Dictionary<string, GameCommandEntry>();
        #region BEGIN Public
        /// <summary>Gets the stable address of an ED slot row without relying on its display label.</summary>
        public static string Address(EliteAssignmentRow row) => row.DeviceId.Length + ":" + row.DeviceId + row.CommandId.Length + ":" + row.CommandId + row.Slot;
        /// <summary>Gets native display categories for the selected section.</summary>
        public IReadOnlyList<string> Categories => AllCommands.Where(x => Filter.Section == "All" || x.Category == Filter.Section)
            .Select(EliteCommandBrowser.Category).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        #endregion END Public
        protected override bool SelectInitialCommand => true;
        protected override EliteBrowserFilter FilterForSelection(EliteBrowserFilter filter, WorkspaceSelection selection) => filter with { ControllerId = selection.ControllerId };
        protected override WorkspaceSelection ScopeSelection => Context.State.Selection with { ControllerId = Filter.ControllerId, NativeScope = Context.State.Identity.Key + ":" + Filter.Section };
        protected override IReadOnlyList<SimCommand> ReadCommands()
        {
            var snapshot = context.Working;
            if (snapshot is null) { _rows = []; _entries = new Dictionary<string, GameCommandEntry>(); return []; }
            var catalog = backend.GetCatalog(snapshot.Profile); _rows = EliteWorkspaceProjection.Rows(snapshot.Profile, catalog);
            var entries = EliteCommandBrowser.Entries(catalog.Commands, _rows);
            _entries = entries.ToDictionary(x => x.Command.Id, StringComparer.Ordinal);
            return entries.OrderBy(x => x.Category, StringComparer.Ordinal).ThenBy(x => x.Command.DisplayName, StringComparer.Ordinal)
                .ThenBy(x => x.Command.Id, StringComparer.Ordinal).Select(x => x.Command).ToArray();
        }
        protected override IReadOnlyList<EliteAssignmentRow> ReadRecords() => _rows;
        protected override string CommandId(SimCommand command) => command.Id;
        protected override string RecordId(EliteAssignmentRow record) => Address(record);
        protected override void PrepareQuery(EliteBrowserFilter filter, string search) => _query =
            new(filter.Section == "All" ? null : filter.Section, filter.Category, filter.InputType, search, filter.Status);
        protected override bool IncludeCommand(SimCommand command, EliteBrowserFilter filter, string search) =>
            GameCommandBrowser.Matches(_entries[command.Id], _query, filter.ControllerId);
        protected override bool IncludeRecord(EliteAssignmentRow record, EliteBrowserFilter filter, string search) =>
            record.DeviceId == filter.ControllerId && (filter.Section == "All" || record.Section == filter.Section);
        protected override WorkspaceSelection SelectionForCommand(SimCommand command) => ScopeSelection with
        {
            CommandId = command.Id,
            SlotId = command.Type == CommandType.Axis ? "Binding" : Context.State.Selection.SlotId is "Primary" or "Secondary"
                ? Context.State.Selection.SlotId : "Primary"
        };
        protected override WorkspaceSelection SelectionForRecord(EliteAssignmentRow record) => ScopeSelection with
        { ControllerId = record.DeviceId, CommandId = record.CommandId, SlotId = record.Slot, AlternativeId = null, InputId = record.Input };
    }
}

