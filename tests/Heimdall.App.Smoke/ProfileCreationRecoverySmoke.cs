using System.IO;
using System.Windows.Threading;
using Heimdall.App.Games;
using Heimdall.App.Games.Elite;
using Heimdall.App.Games.Squadrons;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Infrastructure;

namespace Heimdall.App.Smoke
{
    /// <summary>Checks production ED/Squadrons create commands against real stores when opening a durable new profile fails.</summary>
    internal static class ProfileCreationRecoverySmoke
    {
        #region BEGIN Public
        internal static void Verify(string root)
        {
            var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
            var edStore = new CountingGameWorkspaceStore(new FileGameWorkspaceStore(Path.Combine(root, "ed-create")));
            using var ed = new EliteWorkspaceContext(new(edStore, new EmptyDeviceDiscovery()), []);
            var edSource = new GameWorkspace(1, Guid.NewGuid(), "ED source", ed.Backend.Import("controls",
                new Dictionary<string, string> { ["fixture.binds"] = File.ReadAllText(Path.Combine(fixtures, "EliteDangerous", "Synthetic.4.2.binds")) }), new());
            ed.Session = new(ed.Backend, edSource, EditionAccess.Policy, [], () => []);
            var create = new EliteCreateConfigurationCommand(ed, new Heimdall.App.Games.Profiles.DelegateInputLifetime(() => { }),
                candidate => new(ed.Backend, candidate, EditionAccess.Policy, [], () => []), _ => throw new IOException("ED open failed"), _ => { });
            var edResult = create.ExecuteAsync(new(edSource, "ED durable", true), default).GetAwaiter().GetResult();
            Check(edResult.Completion, edStore.Writes, ed.State);
            if (edStore.Load(edResult.Native.Installed.Session.Id).Workspace.Name != "ED durable") throw new InvalidOperationException("ED lost its committed configuration receipt.");
            if (create.Availability.Enabled) throw new InvalidOperationException("ED offered another create against its unrecovered session.");
            // Construction permits viewing a restricted profile; it must not authorize a durable create.
            using var restricted = new EliteWorkspaceContext(new(edStore, new EmptyDeviceDiscovery()), []);
            var denied = new EliteCreateConfigurationCommand(restricted, new Heimdall.App.Games.Profiles.DelegateInputLifetime(() => { }),
                candidate => new(ed.Backend, candidate, new EditionPolicy(), [], () => []), _ => throw new InvalidOperationException("Restricted install ran"), _ => { });
            var rejected = false;
            try { denied.ExecuteAsync(new(edSource, "Restricted", true), default).GetAwaiter().GetResult(); }
            catch (InvalidOperationException) { rejected = true; }
            if (!rejected || edStore.Writes != 1 || restricted.State.Operation is not null) throw new InvalidOperationException("Restricted ED create wrote a profile.");
            var sqStore = new CountingGameWorkspaceStore(new FileGameWorkspaceStore(Path.Combine(root, "sq-create")));
            using var sq = new SquadronsWorkspaceContext(new(sqStore, new EmptyDeviceDiscovery()));
            var sqSource = new GameWorkspace(1, Guid.NewGuid(), "SQ source", sq.Backend.Import("controls",
                new Dictionary<string, string> { [Heimdall.Squadrons.SquadronsProfileBackend.FileName] = File.ReadAllText(Path.Combine(fixtures, "Squadrons", "Synthetic_profile_synced")) }), new());
            sq.Session = new(sq.Backend, sqSource, EditionAccess.Policy, [], () => []);
            var fresh = new SquadronsNewProfileCommand(sq, new Heimdall.App.Games.Profiles.DelegateInputLifetime(() => { }), _ => throw new IOException("SQ open failed"));
            var sqResult = fresh.ExecuteAsync("SQ durable", default).GetAwaiter().GetResult();
            Check(sqResult.Completion, sqStore.Writes, sq.State);
            if (sqStore.Load(Guid.Parse(sqResult.Native.Detail)).Workspace.Name != "SQ durable") throw new InvalidOperationException("Squadrons lost its committed configuration receipt.");
            if (fresh.Availability.Enabled) throw new InvalidOperationException("Squadrons offered another create against its unrecovered session.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Check(WorkspaceOperationResult result, int writes, WorkspaceState state)
        {
            if (!result.Committed || !result.RecoveryRequired || result.Outcome != WorkspaceOperationOutcome.Applied || writes != 1
                || state.Operation is not null || state.Recovery.Length == 0 || result.Message.Length == 0)
                throw new InvalidOperationException("A durable new configuration was reported as an ordinary failure or repeated.");
        }
        #endregion END private helpers
    }
}
