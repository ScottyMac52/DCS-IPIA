using System.Text.Json;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.EliteDangerous;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Heimdall.TestSupport;
using Heimdall.WarThunder;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>
    /// Same-data lifecycle conformance for the store-backed games: the real native backend, the real file store, a real
    /// <see cref="GameProfileSession"/> and the shared save/discard sequence, over the same fixture files the Windows command smoke uses.
    /// Windows-only adapter wiring (windows, dialogs, dispatcher) remains covered only by Heimdall.App.Smoke.
    /// </summary>
    public sealed class ProfileLifecycleConformanceTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "heimdall-lifecycle-" + Guid.NewGuid().ToString("N"));
        private readonly OwnerLicenseTestIssuer _issuer = new();

        #region BEGIN Public
        /// <summary>Gets the store-backed games.</summary>
        public static TheoryData<string> Games => new() { "Elite", "Squadrons", "War Thunder" };
        /// <summary>Open, clean save, edit, save, reopen, export and discard keep native bytes, identity and revision consistent.</summary>
        [Theory, MemberData(nameof(Games))]
        public async Task Open_edit_save_reopen_export_discard_round_trip_native_bytes(string game)
        {
            using var fixture = Open(game);
            var (context, store, profiles, backend) = (fixture.Context, fixture.Store, fixture.Profiles, fixture.Backend);
            var start = store.Writes;
            Assert.Equal(WorkspaceOperationOutcome.NoChange, (await profiles.SaveAsync(default)).Outcome);
            Assert.Equal(0, store.Writes - start);
            var exportedBefore = Export(backend, context.Session!.Snapshot);

            context.SetEditedName(game + " renamed");
            var saved = await profiles.SaveAsync(default);
            Assert.Equal((WorkspaceOperationOutcome.Applied, true, false), (saved.Outcome, saved.Committed, saved.RecoveryRequired));
            Assert.Equal(1, store.Writes - start);
            Assert.Null(context.State.Operation); Assert.False(context.State.Dirty);
            var reopened = store.Load(context.Session!.Snapshot.Id);
            Assert.Equal(reopened.Revision, context.Revision);
            Assert.Equal(Json(context.Session.Snapshot), Json(reopened.Workspace));
            // Renaming is HEIMDALL metadata: the native output bytes are unchanged by the save/reopen round trip.
            Assert.Equal(exportedBefore, Export(backend, reopened.Workspace));

            var baseline = Json(context.Session.Snapshot);
            context.SetEditedName(game + " draft");
            Assert.True(context.State.Dirty);
            var discarded = await profiles.DiscardAsync(default);
            Assert.Equal(WorkspaceOperationOutcome.Applied, discarded.Outcome);
            Assert.Equal(baseline, Json(context.Session!.Snapshot)); Assert.False(context.State.Dirty);
            Assert.Equal(1, store.Writes - start); Assert.Null(context.State.Operation);
        }

        /// <summary>Pre-commit cancellation and a stale revision write nothing, keep the draft and release the operation.</summary>
        [Theory, MemberData(nameof(Games))]
        public async Task Conflict_and_precommit_cancellation_write_nothing_and_keep_the_draft(string game)
        {
            using var fixture = Open(game);
            var (context, store, profiles) = (fixture.Context, fixture.Store, fixture.Profiles);
            context.SetEditedName(game + " pending");
            var start = store.Writes;
            using (var cancelled = new CancellationTokenSource())
            {
                await cancelled.CancelAsync();
                Assert.Equal(WorkspaceOperationOutcome.Cancelled, (await profiles.SaveAsync(cancelled.Token)).Outcome);
            }
            Assert.Equal(0, store.Writes - start); Assert.True(context.State.Dirty); Assert.Null(context.State.Operation);

            // Another writer advances the durable revision: the stale save is rejected before any write by this workspace.
            var revision = context.Revision;
            _ = fixture.Files.Save(fixture.Files.Load(context.Session!.Snapshot.Id).Workspace with { Name = "Elsewhere" }, revision);
            await Assert.ThrowsAnyAsync<Exception>(() => profiles.SaveAsync(default));
            Assert.Equal(0, store.Writes - start); Assert.True(context.State.Dirty); Assert.Null(context.State.Operation);
            Assert.Equal(revision, context.Revision);
            Assert.Equal("Elsewhere", fixture.Files.Load(context.Session.Snapshot.Id).Workspace.Name);
        }

        /// <summary>A native accept failure after the durable write is Committed with RecoveryRequired, never an uncommitted failure.</summary>
        [Theory, MemberData(nameof(Games))]
        public async Task Postcommit_accept_failure_reports_committed_recovery_and_releases_the_operation(string game)
        {
            using var fixture = Open(game);
            var (context, store, profiles) = (fixture.Context, fixture.Store, fixture.Profiles);
            context.SetEditedName(game + " durable");
            profiles.AcceptFailure = new InvalidOperationException("native reopen failed");
            var start = store.Writes;
            var result = await profiles.SaveAsync(default);
            Assert.Equal((WorkspaceOperationOutcome.Applied, true, true), (result.Outcome, result.Committed, result.RecoveryRequired));
            Assert.Contains("native reopen failed", result.Message, StringComparison.Ordinal);
            Assert.Equal(1, store.Writes - start); Assert.Null(context.State.Operation);
            Assert.Equal(game + " durable", fixture.Files.Load(context.Session!.Snapshot.Id).Workspace.Name);
            Assert.Equal(0, profiles.Refreshes);
        }

        /// <summary>Any refresh failure after the durable write is reported as a warning on the committed save.</summary>
        [Theory, MemberData(nameof(Games))]
        public async Task Postcommit_refresh_failure_of_any_kind_is_a_warning_on_a_committed_save(string game)
        {
            using var fixture = Open(game);
            var (context, store, profiles) = (fixture.Context, fixture.Store, fixture.Profiles);
            context.SetEditedName(game + " refreshed");
            profiles.RefreshFailure = new KeyNotFoundException("panel gone");
            var start = store.Writes;
            var result = await profiles.SaveAsync(default);
            Assert.Equal((WorkspaceOperationOutcome.Applied, true, false), (result.Outcome, result.Committed, result.RecoveryRequired));
            Assert.Contains("panel gone", result.Message, StringComparison.Ordinal);
            Assert.Equal(1, store.Writes - start); Assert.Null(context.State.Operation); Assert.False(context.State.Dirty);
        }

        /// <summary>The duplicate Squadrons declaration is refused until the reviewed slot removal is saved; bindings are unchanged.</summary>
        [Fact]
        public async Task Squadrons_duplicate_declaration_is_rejected_until_the_reviewed_slot_removal_is_saved()
        {
            using var fixture = Open("Squadrons");
            var backend = (SquadronsProfileBackend)fixture.Backend;
            var session = fixture.Context.Session!;
            var source = Json(session.Snapshot);
            var rejected = backend.ExportReview(session.Snapshot.Profile, new Dictionary<string, string>());
            Assert.False(rejected.Succeeded);
            Assert.Contains(rejected.Diagnostics, x => x.Code == "squadrons.export.duplicate-identity" && x.Message.Contains("squadrons.joystick.10", StringComparison.Ordinal));
            Assert.Equal(source, Json(session.Snapshot));

            var bindings = backend.ReadBindings(session.Snapshot.Profile).Count;
            session.ApplyControllerChange(session.PreviewRemoveUnusedCapturedDevice("squadrons.joystick.10"));
            Assert.True(fixture.Context.State.Dirty);
            Assert.True((await fixture.Profiles.SaveAsync(default)).Committed);
            var reopened = fixture.Files.Load(session.Snapshot.Id).Workspace;
            var accepted = backend.ExportReview(reopened.Profile, new Dictionary<string, string>());
            Assert.True(accepted.Succeeded);
            var output = accepted.Files[SquadronsProfileBackend.FileName];
            Assert.DoesNotContain("GstInput.JoystickDevice10 ", output, StringComparison.Ordinal);
            Assert.Contains("GstInput.JoystickDevice5 Smoke Throttle 1", output, StringComparison.Ordinal);
            Assert.Equal(bindings, backend.ReadBindings(reopened.Profile).Count);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _issuer.Dispose();
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        #endregion END Public

        #region BEGIN private helpers
        private static string Json(GameWorkspace workspace) => JsonSerializer.Serialize(workspace);
        private static string Export(ISimProfileBackend backend, GameWorkspace workspace) =>
            string.Join("\n", backend.Export(workspace.Profile).Files.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key + "=" + x.Value));
        private LifecycleFixture Open(string game)
        {
            var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
            (ISimProfileBackend backend, Dictionary<string, string> files) = game switch
            {
                "Elite" => ((ISimProfileBackend)new EliteDangerousProfileBackend(), new Dictionary<string, string> { ["Synthetic.4.2.binds"] = File.ReadAllText(Path.Combine(fixtures, "EliteDangerous", "Synthetic.4.2.binds")) }),
                "Squadrons" => (new SquadronsProfileBackend(), new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = File.ReadAllText(Path.Combine(fixtures, "Squadrons", "Synthetic_profile_synced")) }),
                _ => (new WarThunderProfileBackend(), new Dictionary<string, string> { ["controls.blk"] = File.ReadAllText(Path.Combine(fixtures, "WarThunder", "Synthetic.blk")) })
            };
            var durable = new FileGameWorkspaceStore(Path.Combine(_root, game.Replace(' ', '-')));
            var store = new CountingWorkspaceStore(durable);
            var workspace = new GameWorkspace(1, Guid.NewGuid(), game + " lifecycle", backend.Import("controls", files), new());
            var context = new ProfileWorkspaceContextProbe();
            context.Session = new GameProfileSession(backend, workspace, _issuer.PremiumPolicy, [], () => []);
            context.Revision = durable.Save(workspace, null);
            return new(context, store, durable, new LifecycleProfileOperations(context, store), backend);
        }
        #endregion END private helpers
    }
}
