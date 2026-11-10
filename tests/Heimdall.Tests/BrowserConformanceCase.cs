using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;
using Heimdall.EliteDangerous;
using Heimdall.Squadrons;
using Heimdall.WarThunder;
using Heimdall.TestSupport;

namespace Heimdall.Tests
{
    /// <summary>Typed construction ports for the four production browser adapters; the shared tests need no native casts.</summary>
    public sealed record BrowserConformanceCase(IGameWorkspaceContext Context, IGameAssignmentBrowser Browser,
        Func<IReadOnlyList<string>> Ids, Func<IReadOnlyList<string>> RecordIds, Func<BrowserWorkCounts> Metrics,
        Action<string?> Controller, Action Assigned, Action Reset, Action<string> Edit, Func<int> Copies, Func<string> Native,
        Func<Action> Baseline, Action<EventHandler> Subscribe) : IDisposable
    {
        #region BEGIN Public
        /// <summary>Builds each adapter over the same real synthetic capture used by native regression tests.</summary>
        public static BrowserConformanceCase Open(string game, EditionPolicy policy)
        {
            if (game == "DCS")
            {
                var snapshot = MixedSnapshotFixture.Create(); var context = new DcsBrowserContext(snapshot); var browser = new DcsAssignmentBrowser(context);
                return new(context, browser, () => browser.Commands.Select(x => x.Id).ToArray(), () => browser.Records.Select(x => DcsInputKey.PhysicalSlotKey(x.Slot)).ToArray(),
                    () => browser.Metrics, id => browser.SetFilter(browser.Filter with { ControllerId = id is null ? null : Guid.Parse(id) }),
                    () => browser.SetFilter(browser.Filter with { Status = DcsCommandAssignmentStatus.AssignedHere }), () => browser.SetFilter(new()),
                    context.Edit, () => context.Reads, () => System.Text.Json.JsonSerializer.Serialize(context.Working),
                    () => () => DcsCommandBrowser.Filter(snapshot.ModuleCommands, snapshot.Assignments, AssignmentLayer.Module, null, snapshot.Configuration.Profiles.Select(x => x.Id).ToArray(), search: "a"),
                    handler => browser.ProjectionChanged += handler);
            }
            var profileContext = new ProfileWorkspaceContextProbe();
            var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
            var (backend, file, name) = game switch
            {
                "Elite" => ((ISimProfileBackend)new EliteDangerousProfileBackend(), Path.Combine(fixtures, "EliteDangerous", "Synthetic.4.2.binds"), "fixture.binds"),
                "Squadrons" => (new SquadronsProfileBackend(), Path.Combine(fixtures, "Squadrons", "Synthetic_profile_synced"), SquadronsProfileBackend.FileName),
                _ => (new WarThunderProfileBackend(), Path.Combine(fixtures, "WarThunder", "Synthetic.blk"), "fixture.blk")
            };
            var profile = backend.Import("controls", new Dictionary<string, string> { [name] = File.ReadAllText(file) });
            var workspace = new GameWorkspace(1, Guid.NewGuid(), game + " browser", profile, new());
            var session = new GameProfileSession(backend, workspace, policy, [], () => []);
            profileContext.Session = session; profileContext.Revision = "saved";
            if (game == "Elite")
            {
                var ed = new EliteAssignmentBrowser(profileContext, (EliteDangerousProfileBackend)backend);
                var commands = session.Catalog.Commands; var rows = EliteWorkspaceProjection.Rows(profile, session.Catalog);
                return new(profileContext, ed, () => ed.Commands.Select(x => x.Id).ToArray(), () => ed.Records.Select(EliteAssignmentBrowser.Address).ToArray(), () => ed.Metrics,
                    id => ed.SetFilter(ed.Filter with { ControllerId = id }), () => ed.SetFilter(ed.Filter with { Status = GameCommandAssignmentStatus.AssignedToController }), () => ed.SetFilter(new()),
                    session.Rename, () => session.SnapshotCopies, () => session.ContentFingerprint,
                    () => () => GameCommandBrowser.Filter(EliteCommandBrowser.Entries(commands, rows), new(Search: "a")), handler => ed.ProjectionChanged += handler);
            }
            if (game == "Squadrons")
            {
                var sqBackend = (SquadronsProfileBackend)backend;
                var sq = new SquadronsAssignmentBrowser(profileContext, sqBackend, () => []);
                return new(profileContext, sq, () => sq.Commands.Select(x => x.Id).ToArray(), () => sq.Records.Select(x => x.RecordId).ToArray(), () => sq.Metrics,
                    id => sq.SetFilter(sq.Filter with { Source = id }), () => sq.SetFilter(sq.Filter with { Status = "Assigned to selected controller" }), () => sq.SetFilter(new()),
                    session.Rename, () => session.SnapshotCopies, () => session.ContentFingerprint,
                    () => () => { var source = session.Snapshot.Profile; SquadronsCommandFilter.Apply(sqBackend.GetCatalog(source).Commands, sqBackend.ReadBindings(source), "All", "All types", "All", "a", null); },
                    handler => sq.ProjectionChanged += handler);
            }
            var wt = new WarThunderAssignmentBrowser(profileContext, (WarThunderProfileBackend)backend);
            return new(profileContext, wt, () => wt.Commands.Select(x => x.Id).ToArray(), () => wt.Records.Select(x => x.RecordId).ToArray(), () => wt.Metrics,
                id => wt.SetFilter(wt.Filter with { ControllerId = id }), () => wt.SetFilter(wt.Filter with { Status = GameCommandAssignmentStatus.AssignedToController }), () => wt.SetFilter(new()),
                session.Rename, () => session.SnapshotCopies, () => session.ContentFingerprint,
                () => () => WarThunderWorkspaceProjection.Filter(session.Catalog.Commands, wt.AllRecords, null, "a", null, null, GameCommandAssignmentStatus.All), handler => wt.ProjectionChanged += handler);
        }
        /// <inheritdoc/>
        public void Dispose() { Browser.Dispose(); Context.Dispose(); GC.SuppressFinalize(this); }
        #endregion END Public
    }
}
