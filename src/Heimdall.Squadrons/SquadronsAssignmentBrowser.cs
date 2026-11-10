using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Squadrons
{
    /// <summary>Native captured-record, command/type and hardware-evidence hooks for the common browser.</summary>
    public class SquadronsAssignmentBrowser(IGameWorkspaceContext<GameWorkspace> context, SquadronsProfileBackend backend,
        Func<IReadOnlyList<ControllerObservation>> observations)
        : GameAssignmentBrowserBase<SimCommand, SquadronsWorkspaceRow, SquadronsBrowserFilter>(context, new())
    {
        private IReadOnlyList<SquadronsWorkspaceRow> _rows = [];
        private IReadOnlyDictionary<string, SquadronsBindingRow[]> _records = new Dictionary<string, SquadronsBindingRow[]>();
        private Dictionary<string, SquadronsBindingRow[]> _assignable = [];
        private IReadOnlyList<ControllerObservation>? _evidence;
        private long _evidenceVersion;
        #region BEGIN Public
        /// <summary>Gets a command hint from cached native evidence rather than reparsing the capture on selection.</summary>
        public SquadronsAssignmentPlan Plan(string concept)
        {
            return SquadronsAssignmentPlanner.FromRecords(concept, _records.GetValueOrDefault(concept)?.Length ?? 0, _assignable.GetValueOrDefault(concept) ?? []);
        }
        #endregion END Public
        protected override SquadronsBrowserFilter FilterForSelection(SquadronsBrowserFilter filter, WorkspaceSelection selection) => filter with { Source = selection.ControllerId };
        protected override WorkspaceSelection ScopeSelection => Context.State.Selection with { ControllerId = Filter.Source, NativeScope = Context.State.Identity.Key + ":" + Filter.DrawingSection + ":" + Filter.Source };
        protected override long NativeVersion
        {
            get { var current = observations(); if (!ReferenceEquals(current, _evidence)) { _evidence = current; _evidenceVersion++; } return _evidenceVersion; }
        }
        protected override IReadOnlyList<SimCommand> ReadCommands()
        {
            var snapshot = context.Working;
            if (snapshot is null) { _rows = []; _records = new Dictionary<string, SquadronsBindingRow[]>(); _assignable.Clear(); return []; }
            var commands = backend.GetCatalog(snapshot.Profile).Commands; var bindings = backend.ReadBindings(snapshot.Profile);
            _assignable = backend.ReadAssignableBindings(snapshot.Profile).GroupBy(x => x.Concept, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
            _records = bindings.GroupBy(x => x.Concept, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
            var verified = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
            foreach (var (id, pin) in snapshot.Hardware)
                if (GameControllerInputs.VerifiedKeys(pin, _evidence ?? []) is { } keys) verified[id] = keys;
            _rows = SquadronsWorkspaceProjection.ReviewInputs(SquadronsWorkspaceProjection.Rows(snapshot.Profile,
                commands.ToDictionary(x => x.Metadata["squadrons.concept"], StringComparer.Ordinal), bindings), verified);
            return commands.OrderBy(x => x.Category, StringComparer.Ordinal).ThenBy(x => x.DisplayName, StringComparer.Ordinal).ToArray();
        }
        protected override IReadOnlyList<SquadronsWorkspaceRow> ReadRecords() => _rows;
        protected override string CommandId(SimCommand command) => command.Id;
        protected override string RecordId(SquadronsWorkspaceRow record) => record.RecordId;
        protected override bool IncludeCommand(SimCommand command, SquadronsBrowserFilter filter, string search) =>
            SquadronsCommandFilter.Matches(command, _records.GetValueOrDefault(command.Metadata["squadrons.concept"]) ?? [],
                filter.Category, filter.InputType, filter.Status, search, filter.Source);
        protected override bool IncludeRecord(SquadronsWorkspaceRow record, SquadronsBrowserFilter filter, string search)
        {
            var source = filter.Source ?? "all";
            if (filter.InspectedControl is { } control && source.StartsWith("squadrons.joystick.", StringComparison.Ordinal))
                return SquadronsDrawingProjection.ForControl(control, [record], source, filter.DrawingSection).Count > 0;
            return (filter.FocusedConcept is null || record.Native.Concept == filter.FocusedConcept)
                && SquadronsWorkspaceProjection.Filter([record], filter.RecordSection, filter.RecordType, filter.RecordSearch, source, filter.Review).Count > 0;
        }
        protected override WorkspaceSelection SelectionForCommand(SimCommand command) => ScopeSelection with { CommandId = command.Id };
        protected override WorkspaceSelection SelectionForRecord(SquadronsWorkspaceRow record) => ScopeSelection with
        { CommandId = AllCommands.First(x => x.Metadata["squadrons.concept"] == record.Native.Concept).Id, AlternativeId = record.RecordId, InputId = record.Native.Input?.Key };
    }
}
