using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using Heimdall.App.Games;
using Heimdall.App.Games.Dcs;
using Heimdall.App.Games.Elite;
using Heimdall.App.Games.Squadrons;
using Heimdall.App.Games.WarThunder;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Heimdall.TestSupport;
using Heimdall.WarThunder;

namespace Heimdall.App.Smoke
{
    /// <summary>Constructs all four production context adapters with real native sessions and isolated persistence.</summary>
    internal static class WorkspaceContextCases
    {
        #region BEGIN Public
        internal static IReadOnlyList<WorkspaceContextCase> Create(string root, EditionPolicy owner)
        {
            // Each game owns its counting store (over the same durable file store) so one fixture's setup or saves
            // can never appear as another fixture's writes.
            var files = new FileGameWorkspaceStore(Path.Combine(root, "profiles"));
            var (eliteStore, squadronsStore, thunderStore) = (new CountingGameWorkspaceStore(files), new CountingGameWorkspaceStore(files), new CountingGameWorkspaceStore(files));
            var elite = new EliteWorkspaceContext(new GameWorkspaceServices(eliteStore, new EmptyDeviceDiscovery()), []);
            var squadrons = new SquadronsWorkspaceContext(new GameWorkspaceServices(squadronsStore, new EmptyDeviceDiscovery()));
            var thunder = new WarThunderWorkspaceContext(new GameWorkspaceServices(thunderStore, new EmptyDeviceDiscovery()));
            var edProfile = elite.Backend.Import("controls", new Dictionary<string, string>
            { ["controls.binds"] = "<Root PresetName=\"Context\" MajorVersion=\"4\" MinorVersion=\"0\" />" });
            var sqProfile = squadrons.Backend.Import("controls", new Dictionary<string, string>
            { [SquadronsProfileBackend.FileName] = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced")) });
            var wtProfile = thunder.Backend.Import("controls", new Dictionary<string, string>
            { ["controls.blk"] = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "WarThunder", "Synthetic.blk")) });
            // Production native adapters supply the hooks; post-commit window refreshes are not part of the context contract.
            return [Dcs(root),
                Profile("Elite", elite, elite.Services, eliteStore, edProfile, elite.Backend, owner, open => new EliteProfileSaving(elite, () => { },
                    () => elite.State.EditedName.Trim(), (saved, revision) => { elite.Session = open(saved); elite.Revision = revision; }, () => "Elite saved.", () => { }, () => { })),
                Profile("Squadrons", squadrons, squadrons.Services, squadronsStore, sqProfile, squadrons.Backend, owner, _ => new SquadronsProfileSaving(squadrons, () => { }, () => { }, () => { })),
                Profile("War Thunder", thunder, thunder.Services, thunderStore, wtProfile, thunder.Backend, owner, _ => new WarThunderProfileSaving(thunder, () => { }, () => { }, () => { }))];
        }
        #endregion END Public
        #region BEGIN private helpers
        private static WorkspaceContextCase Profile(string name, GameProfileWorkspaceContextBase context, GameWorkspaceServices services, CountingGameWorkspaceStore store,
            Heimdall.Domain.MultiSim.ProfileDocument profile, ISimProfileBackend backend, EditionPolicy owner,
            Func<Func<GameWorkspace, GameProfileSession>, IGameProfileOperations> profiles)
        {
            var workspace = new GameWorkspace(1, Guid.NewGuid(), name + " context", profile, new());
            GameProfileSession Open(EditionPolicy policy) => new(backend, workspace, policy, [], () => []);
            context.Session = Open(owner); context.Revision = services.Store.Save(workspace, null);
            var writes = store.Writes;
            return new(name, context, text => context.Session!.Rename(text),
                profiles(saved => new(backend, saved, owner, [], () => [])),
                () => context.Session = Open(owner), () => context.Session = Open(new()),
                () => JsonSerializer.Serialize(context.Session?.Snapshot), () => store.Writes - writes, error => store.Fail = error,
                () => store.Load(workspace.Id).Workspace.Name);
        }
        private static WorkspaceContextCase Dcs(string root)
        {
            var connection = "Data Source=" + Path.Combine(root, "dcs.db") + ";Pooling=False";
            new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
            var repository = new DcsSqliteEditorWorkspaceRepository(connection);
            var workspace = MixedSnapshotFixture.Create(); repository.SaveAsync(workspace).GetAwaiter().GetResult();
            var counter = new CountingDcsRepository(repository);
            var session = new DcsWorkspaceSession(new(workspace), counter, new EmptyDeviceDiscovery(), null, null, null, null, null, null, null, null, null);
            var context = new DcsWorkspaceContext(session, new DispatcherWorkspaceScheduler(Dispatcher.CurrentDispatcher),
                () => throw new InvalidOperationException("Use the context-issued lease in this conformance fixture."), () => throw new InvalidOperationException("Use the context-issued lease."));
            return new("DCS", context, text => session.SetLabel("context-conformance", text), new DcsProfileOperations(context, session),
                () => context.RunOperation(WorkspaceOperationKind.Discard, lease => { session.Discard(lease); return new(WorkspaceOperationOutcome.Applied, ""); }),
                () => { EditionAccess.SetForTests(new()); context.RefreshNativeState(); },
                () => JsonSerializer.Serialize(new { session.Working, Assignments = session.Draft?.Snapshot, session.Labels }), () => counter.Writes, error => counter.Fail = error,
                () => repository.GetAsync(workspace.Configuration.Module.Id).GetAwaiter().GetResult()?.Configuration.Module.DisplayName);
        }
        #endregion END private helpers
    }
}
