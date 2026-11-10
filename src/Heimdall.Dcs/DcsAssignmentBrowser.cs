using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;

namespace Heimdall.Dcs
{
    /// <summary>Native DCS module/UI-layer palettes, draft addresses and every-device ownership behind the common browser.</summary>
    public class DcsAssignmentBrowser(IGameWorkspaceContext<DcsEditorWorkspaceSnapshot> context, Func<IReadOnlyList<DcsDraftAssignment>>? draft = null)
        : GameAssignmentBrowserBase<DcsCommandPaletteEntry, DcsDraftAssignment, DcsBrowserFilter>(context, new())
    {
        private IReadOnlyList<DcsDraftAssignment> _records = [];
        private GameCommandQuery _query = new();
        private readonly Dictionary<DcsCommandPaletteEntry, AssignmentLayer> _layers = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<(AssignmentLayer Layer, string Id), GameCommandEntry> _entries = [];
        private readonly Dictionary<AssignmentLayer, Guid[]> _devices = [];
        private readonly Dictionary<(AssignmentLayer Layer, string Id), int> _bound = [];
        #region BEGIN Public
        /// <summary>Gets native categories without rebuilding either palette.</summary>
        public IReadOnlyList<string> Categories => AllCommands.Where(x => _layers[x] == Filter.Layer).Select(x => x.Category)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        /// <summary>Gets the native command count in the selected module/UI-layer palette.</summary>
        public int PaletteCount => AllCommands.Count(x => _layers[x] == Filter.Layer);
        /// <summary>Gets the complete bound count for a native command in the selected layer.</summary>
        public int BoundCount(string id) => _bound.GetValueOrDefault((Filter.Layer, id));
        #endregion END Public
        protected override DcsBrowserFilter FilterForSelection(DcsBrowserFilter filter, WorkspaceSelection selection) => filter with { ControllerId = Guid.TryParse(selection.ControllerId, out var id) ? id : null };
        protected override WorkspaceSelection ScopeSelection => Context.State.Selection with { ControllerId = Filter.ControllerId?.ToString("N") };
        protected override IReadOnlyList<DcsCommandPaletteEntry> ReadCommands()
        {
            _layers.Clear(); _entries.Clear(); _devices.Clear(); _bound.Clear();
            var snapshot = context.Working;
            if (snapshot is null) { _records = []; return []; }
            _records = draft?.Invoke() ?? snapshot.Assignments;
            var structure = DcsWorkspaceStructureEditor.Describe(snapshot);
            foreach (var layer in Enum.GetValues<AssignmentLayer>()) _devices[layer] = structure.Devices.Where(x => x.Layer == layer).Select(x => x.ProfileId).Distinct().ToArray();
            var module = snapshot.ModuleCommands; var ui = DcsCommandPalette.FromUiLayer(snapshot.UiCommands);
            foreach (var command in module) _layers[command] = AssignmentLayer.Module;
            foreach (var command in ui) _layers[command] = AssignmentLayer.UiLayer;
            var owners = _records.ToLookup(x => (x.Slot.Layer, x.Command.Id));
            foreach (var group in owners) _bound[group.Key] = group.Count();
            foreach (var (command, layer) in _layers)
                _entries[(layer, command.Id)] = new(DcsCommandBrowser.Describe(command), "", command.Category,
                    [command.IsAxis ? Heimdall.Domain.MultiSim.CommandType.Axis : Heimdall.Domain.MultiSim.CommandType.Button],
                    owners[(layer, command.Id)].Select(x => x.Slot.ProfileId.ToString("D")).Distinct(StringComparer.Ordinal).ToArray());
            return module.Concat(ui).OrderBy(x => x.Category, StringComparer.Ordinal).ThenBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
        }
        protected override IReadOnlyList<DcsDraftAssignment> ReadRecords() => _records;
        protected override string CommandId(DcsCommandPaletteEntry command) => command.Id;
        protected override string RecordId(DcsDraftAssignment record) => DcsInputKey.PhysicalSlotKey(record.Slot);
        protected override void PrepareQuery(DcsBrowserFilter filter, string search)
        {
            var status = DcsCommandBrowser.SharedStatus(filter.Status, filter.ControllerId);
            _query = new(Category: filter.Category, InputType: filter.InputType, Search: search, AssignmentStatus: status);
        }
        protected override bool IncludeCommand(DcsCommandPaletteEntry command, DcsBrowserFilter filter, string search)
        {
            if (_layers[command] != filter.Layer) return false;
            var entry = _entries[(filter.Layer, command.Id)];
            return GameCommandBrowser.Matches(entry, _query, filter.ControllerId?.ToString("D"))
                && (filter.Status != DcsCommandAssignmentStatus.AssignedEverywhere || _devices[filter.Layer].Length > 0
                    && _devices[filter.Layer].All(id => entry.AssignedControllers.Contains(id.ToString("D"), StringComparer.Ordinal)));
        }
        protected override bool IncludeRecord(DcsDraftAssignment record, DcsBrowserFilter filter, string search) => record.Slot.Layer == filter.Layer
            && (filter.ControllerId is null || record.Slot.ProfileId == filter.ControllerId) && record.Command.Id == Context.State.Selection.CommandId;
        protected override WorkspaceSelection SelectionForCommand(DcsCommandPaletteEntry command) => ScopeSelection with { CommandId = command.Id };
        protected override WorkspaceSelection SelectionForRecord(DcsDraftAssignment record) => ScopeSelection with
        { ControllerId = record.Slot.ProfileId.ToString("N"), CommandId = record.Command.Id, AlternativeId = RecordId(record), InputId = record.Slot.InputKey };
    }
}
