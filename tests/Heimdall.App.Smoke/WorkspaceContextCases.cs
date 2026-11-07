using System.IO;
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
            var services = new GameWorkspaceServices(new FileGameWorkspaceStore(Path.Combine(root, "profiles")), new EmptyDeviceDiscovery());
            var elite = new EliteWorkspaceContext(services, []);
            var squadrons = new SquadronsWorkspaceContext(services);
            var thunder = new WarThunderWorkspaceContext(services);
            var edProfile = services.Backend.Import("controls", new Dictionary<string, string>
            { ["controls.binds"] = "<Root PresetName=\"Context\" MajorVersion=\"4\" MinorVersion=\"0\" />" });
            var sqProfile = squadrons.Backend.Import("controls", new Dictionary<string, string>
            { [SquadronsProfileBackend.FileName] = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced")) });
            var wtProfile = thunder.Backend.Import("controls", new Dictionary<string, string>
            { ["controls.blk"] = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "WarThunder", "Synthetic.blk")) });
            return [Dcs(root),
                Profile("Elite", elite, services, edProfile, services.Backend, owner),
                Profile("Squadrons", squadrons, services, sqProfile, squadrons.Backend, owner),
                Profile("War Thunder", thunder, services, wtProfile, thunder.Backend, owner)];
        }
        #endregion END Public
        #region BEGIN private helpers
        private static WorkspaceContextCase Profile(string name, GameProfileWorkspaceContextBase context, GameWorkspaceServices services,
            Heimdall.Domain.MultiSim.ProfileDocument profile, ISimProfileBackend backend, EditionPolicy owner)
        {
            var workspace = new GameWorkspace(1, Guid.NewGuid(), name + " context", profile, new());
            GameProfileSession Open(EditionPolicy policy) => new(backend, workspace, policy, [], () => []);
            context.Session = Open(owner); context.Revision = services.Store.Save(workspace, null);
            return new(name, context, text => context.Session!.Rename(text), lease =>
            {
                var candidate = context.Session!.Snapshot with { Name = context.State.EditedName };
                var revision = services.Store.Save(candidate, context.Revision);
                context.Session.AcceptSaved(candidate, lease); context.Revision = revision; workspace = candidate;
                if (services.Store.Load(candidate.Id).Workspace.Name != candidate.Name) throw new InvalidOperationException("Native persisted baseline was not accepted.");
                return Task.CompletedTask;
            }, lease => context.Session!.Discard(lease), () => context.Session = Open(owner), () => context.Session = Open(new()));
        }
        private static WorkspaceContextCase Dcs(string root)
        {
            var connection = "Data Source=" + Path.Combine(root, "dcs.db") + ";Pooling=False";
            new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
            var repository = new DcsSqliteEditorWorkspaceRepository(connection);
            var workspace = MixedSnapshotFixture.Create(); repository.SaveAsync(workspace).GetAwaiter().GetResult();
            var session = new DcsWorkspaceSession(new(workspace), repository, new EmptyDeviceDiscovery(), null, null, null, null, null, null, null, null, null);
            var context = new DcsWorkspaceContext(session, new DispatcherWorkspaceScheduler(Dispatcher.CurrentDispatcher),
                () => throw new InvalidOperationException("Use the context-issued lease in this conformance fixture."), () => throw new InvalidOperationException("Use the context-issued lease."));
            return new("DCS", context, text => session.SetLabel("context-conformance", text), session.SaveAsync, session.Discard,
                () => context.RunOperation(WorkspaceOperationKind.Discard, lease => { session.Discard(lease); return new(WorkspaceOperationOutcome.Applied, ""); }),
                () => { EditionAccess.SetForTests(new()); context.RefreshNativeState(); });
        }
        #endregion END private helpers
    }
}
