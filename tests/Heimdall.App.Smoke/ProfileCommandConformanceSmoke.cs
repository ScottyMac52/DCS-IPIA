using System.IO;
using System.Windows.Threading;
using Heimdall.App.Games;
using Heimdall.App.Games.Dcs;
using Heimdall.App.Games.Elite;
using Heimdall.App.Games.Profiles;
using Heimdall.App.Games.Squadrons;
using Heimdall.App.Games.WarThunder;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>Runs the typed export/import command contract against the production native commands of all four games, measuring real files on disk.</summary>
    internal static class ProfileCommandConformanceSmoke
    {

        #region BEGIN Public
        internal static void Verify()
        {
            var previous = EditionAccess.Policy; using var issuer = new OwnerLicenseTestIssuer();
            // Production commands start from WPF handlers, where the dispatcher SynchronizationContext returns every awaited
            // native continuation to the owner thread. This plain STA host has no context until a frame is pushed, so install it.
            var previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var root = Path.Combine(Path.GetTempPath(), "heimdall-commands-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            IReadOnlyList<WorkspaceContextCase> profiles = [];
            DcsWorkspaceContext? dcs = null;
            try
            {
                EditionAccess.SetForTests(issuer.PremiumPolicy);
                profiles = WorkspaceContextCases.Create(Path.Combine(root, "profiles"), issuer.PremiumPolicy);
                var input = new DelegateInputLifetime(() => { });
                var cases = new List<ProfileCommandExportCase>();
                (dcs, var dcsCase) = Dcs(root, input); cases.Add(dcsCase);
                foreach (var fixture in profiles)
                {
                    switch (fixture.Context)
                    {
                        case EliteWorkspaceContext elite:
                            // Fixture precondition, not an expectation: the clean-export case needs a preset with no unresolved controller references.
                            Require(elite.Session!.ReviewGameExport().Count == 0, "Elite", "Fixture precondition: the export fixture has unresolved controller references.");
                            string? parent = null;
                            var ed = new EliteExportCommand(elite, input, () => Path.Combine(root, "ed-bindings"), () => parent, _ => false, _ => { }, _ => { });
                            cases.Add(new("Elite", elite, async folder => { parent = folder; return (await ed.ExecuteAsync(new(true), default)).Succeeded; },
                                () => ed.Availability.Enabled, () => fixture.Edit("Dirty"), () => Await(fixture.Profiles.DiscardAsync(default)),
                                async folder => { parent = null; return (await ed.ExecuteAsync(new(true), default)).Succeeded; }));
                            break;
                        case SquadronsWorkspaceContext squadrons:
                            var sq = new SquadronsExportCommand(squadrons, input, _ => { });
                            VerifySquadronsDuplicateReview(fixture, squadrons, sq, Path.Combine(root, "out-Squadrons-rejected"));
                            cases.Add(new("Squadrons", squadrons, async folder => (await sq.ExecuteAsync(folder, default)).Succeeded,
                                () => sq.Availability.Enabled, () => fixture.Edit("Dirty"), () => Await(fixture.Profiles.DiscardAsync(default)), null));
                            break;
                        case WarThunderWorkspaceContext thunder:
                            Require(thunder.Session!.Export().Succeeded, "War Thunder", "Fixture precondition: the export fixture has native export diagnostics.");
                            var wt = new WarThunderExportCommand(thunder, input, _ => { });
                            cases.Add(new("War Thunder", thunder, async folder => (await wt.ExecuteAsync(folder, default)).Succeeded,
                                () => wt.Availability.Enabled, () => fixture.Edit("Dirty"), () => Await(fixture.Profiles.DiscardAsync(default)), null));
                            break;
                    }
                }
                Require(cases.Count == 4, "conformance", "Every game must supply an export command case.");
                foreach (var item in cases) { EditionAccess.SetForTests(issuer.PremiumPolicy); VerifyExport(item, Path.Combine(root, "out-" + item.Name.Replace(' ', '-'))); }
                VerifyImports(profiles, root, input);
            }
            finally
            {
                foreach (var fixture in profiles) { fixture.Profiles.Dispose(); fixture.Context.Dispose(); }
                dcs?.Dispose();
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                SynchronizationContext.SetSynchronizationContext(previousContext);
                EditionAccess.SetForTests(previous); Directory.Delete(root, true);
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void VerifyExport(ProfileCommandExportCase item, string output)
        {
            Directory.CreateDirectory(output);
            int Entries() => Directory.GetFileSystemEntries(output).Length;
            Require(item.Available() && !item.Context.State.Dirty, item.Name, "A saved clean workspace must offer export.");
            Require(Await(item.Export(output)) && Entries() == 1 && item.Context.State.Operation is null, item.Name, "Export did not write exactly one native output.");
            item.Dirty();
            Require(!item.Available(), item.Name, "Export was offered with unsaved edits.");
            Require(Throws<InvalidOperationException>(() => Await(item.Export(output))) && Entries() == 1, item.Name, "Unsaved export ran or wrote output.");
            item.Clean();
            var pending = new TaskCompletionSource<WorkspaceOperationResult>();
            var running = item.Context.RunOperationAsync(WorkspaceOperationKind.Save, _ => pending.Task, default);
            Require(!item.Available() && Throws<InvalidOperationException>(() => Await(item.Export(output))) && Entries() == 1, item.Name, "Export ran inside another operation.");
            pending.SetResult(new(WorkspaceOperationOutcome.NoChange, "")); Await(running);
            if (item.Cancelled is { } cancelled)
                Require(!Await(cancelled(output)) && Entries() == 1 && item.Context.State.Operation is null, item.Name, "A cancelled review wrote output or kept the operation.");
            Console.WriteLine("Profile command conformance: " + item.Name + " export passed.");
        }
        /// <summary>
        /// The shared Squadrons capture deliberately declares one controller in two slots. Export must refuse it (native review),
        /// leave the session, output folder and store untouched and release the operation; the user's reviewed remove-unused-slot
        /// workflow then makes the same data exportable after a save.
        /// </summary>
        private static void VerifySquadronsDuplicateReview(WorkspaceContextCase fixture, SquadronsWorkspaceContext context, SquadronsExportCommand export, string output)
        {
            Directory.CreateDirectory(output);
            Require(context.Session!.Snapshot.Profile.Devices.Count(x => x.Identity?.WindowsName == "Smoke Throttle 1") == 2, "Squadrons",
                "Fixture precondition: the shared capture no longer declares the duplicate controller.");
            var before = context.CaptureSnapshot(); var writes = fixture.Writes();
            for (var attempt = 0; attempt < 2; attempt++)
            {
                Require(export.Availability.Enabled, "Squadrons", "Export availability changed for a saved duplicate capture (current policy offers it and reviews on run).");
                string? reason = null;
                try { Await(export.ExecuteAsync(output, default)); }
                catch (InvalidOperationException error) { reason = error.Message; }
                Require(reason?.Contains("declared in multiple slots", StringComparison.Ordinal) == true && reason.Contains("squadrons.joystick.10", StringComparison.Ordinal),
                    "Squadrons", "A duplicate native declaration was not refused by the export review: " + reason);
                Require(Directory.GetFileSystemEntries(output).Length == 0 && context.CaptureSnapshot() == before && fixture.Writes() == writes
                    && context.State.Operation is null && !context.State.Dirty, "Squadrons", "A refused export wrote output, changed the session or kept the operation.");
            }
            // Reviewed workflow (SquadronsControllerPresenter.RemoveUnusedSlotClicked): preview, accept the review, apply, then save.
            var change = context.Session.PreviewRemoveUnusedCapturedDevice("squadrons.joystick.10");
            context.Session.ApplyControllerChange(change);
            Require(context.State.Dirty && !export.Availability.Enabled, "Squadrons", "A staged slot removal must be saved before export.");
            var saving = fixture.Profiles.SaveAsync(default); AwaitTask(saving); var saved = saving.GetAwaiter().GetResult();
            Require(saved.Committed && !saved.RecoveryRequired && fixture.Writes() == writes + 1 && !context.State.Dirty, "Squadrons", "The reviewed slot removal did not save once.");
            Console.WriteLine("Profile command conformance: Squadrons duplicate-declaration refusal and reviewed slot removal passed.");
        }
        private static void VerifyImports(IReadOnlyList<WorkspaceContextCase> profiles, string root, IWorkspaceInputLifetime input)
        {
            var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
            var oversized = Path.Combine(root, "oversized.dat");
            using (var stream = File.Create(oversized)) stream.SetLength(9L * 1024 * 1024);
            foreach (var fixture in profiles)
            {
                if (fixture.Context is not GameProfileWorkspaceContextBase context) continue;
                static Func<GameWorkspace, GameProfileSession> Creator(ISimProfileBackend backend) => workspace => new(backend, workspace, EditionAccess.Policy, [], () => []);
                void Install(GameSessionInstall installed) { context.Session = installed.Session; context.Revision = installed.Revision; }
                Func<Task> import; Func<Task> rejected;
                switch (context)
                {
                    case EliteWorkspaceContext elite:
                        var binds = Path.Combine(root, "import.binds");
                        File.WriteAllText(binds, "<Root PresetName=\"Imported\" MajorVersion=\"4\" MinorVersion=\"0\" />");
                        var ed = new EliteImportCommand(elite, input, Creator(elite.Backend), Install, _ => { });
                        import = () => ed.ExecuteAsync(binds, default); rejected = () => ed.ExecuteAsync(Path.Combine(root, "missing.binds"), default);
                        break;
                    case SquadronsWorkspaceContext squadrons:
                        var sq = new SquadronsImportCommand(squadrons, input, Creator(squadrons.Backend), imported => { squadrons.Session = imported.Session; squadrons.Revision = null; squadrons.SourceDirectory = imported.SourceDirectory; });
                        import = () => sq.ExecuteAsync(Path.Combine(fixtures, "Squadrons", "Synthetic_profile_synced"), default);
                        rejected = () => sq.ExecuteAsync(oversized, default);
                        break;
                    case WarThunderWorkspaceContext thunder:
                        var wt = new WarThunderImportCommand(thunder, input, Creator(thunder.Backend), Install, _ => { });
                        import = () => wt.ExecuteAsync(WarThunderImportRequest.Read(thunder.Backend, Path.Combine(fixtures, "WarThunder", "Synthetic.blk")), default);
                        rejected = () => { WarThunderImportRequest.Read(thunder.Backend, oversized); return Task.CompletedTask; };
                        break;
                    default: continue;
                }
                var before = context.CaptureSnapshot(); var writes = fixture.Writes();
                Require(ThrowsAny(() => AwaitTask(rejected())) && context.CaptureSnapshot() == before && context.State.Operation is null, fixture.Name, "A rejected import replaced the open session.");
                var previousId = context.Session?.Snapshot.Id;
                AwaitTask(import());
                Require(context.Session is not null && context.Session.Snapshot.Id != previousId && context.Revision is null && context.State.Dirty && fixture.Writes() == writes,
                    fixture.Name, "Import did not install exactly one new unsaved session without writing.");
                Console.WriteLine("Profile command conformance: " + fixture.Name + " import passed.");
            }
        }
        private static (DcsWorkspaceContext, ProfileCommandExportCase) Dcs(string root, IWorkspaceInputLifetime input)
        {
            var connection = "Data Source=" + Path.Combine(root, "dcs.db") + ";Pooling=False";
            new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
            var repository = new DcsSqliteEditorWorkspaceRepository(connection);
            var workspace = AxisCleanupFlyNowSmoke.Workspace(Guid.NewGuid(), "COMMAND-CONFORMANCE"); repository.SaveAsync(workspace).GetAwaiter().GetResult();
            var session = new DcsWorkspaceSession(new(workspace), repository, new EmptyDeviceDiscovery(), null, null, null, null, null, null, null, null, null);
            var context = new DcsWorkspaceContext(session, new DispatcherWorkspaceScheduler(Dispatcher.CurrentDispatcher), () => Task.CompletedTask, () => { });
            var profiles = new DcsProfileOperations(context, session);
            string? destination = null;
            var command = new DcsOutputCommand(context, session, () => false, _ => destination, () => destination, () => destination,
                (operation, _, _, error) => throw new InvalidOperationException(operation + " failed: " + error.Message, error));
            async Task<bool> Run(string? path) { destination = path; return (await command.ExecuteAsync(new("1.0.0.0", DcsOutputKind.ModuleSnapshot), default)).Succeeded; }
            return (context, new("DCS", context, folder => Run(Path.Combine(folder, "module.heimdall-module.json")), () => command.Availability.Enabled,
                () => session.SetLabel("command-conformance", "Dirty"), () => Await(profiles.DiscardAsync(default)), _ => Run(null)));
        }
        private static bool Throws<TException>(Action action) where TException : Exception
        { try { action(); return false; } catch (TException) { return true; } }
        private static bool ThrowsAny(Action action) { try { action(); return false; } catch (Exception) { return true; } }
        private static T Await<T>(Task<T> task) { AwaitTask(task); return task.GetAwaiter().GetResult(); }
        private static void Await(Task<WorkspaceOperationResult> task) => AwaitTask(task);
        private static void AwaitTask(Task task)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Profile command did not complete.");
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
            }
            task.GetAwaiter().GetResult();
        }
        private static void Require(bool condition, string name, string message)
        { if (!condition) throw new InvalidOperationException(name + " profile command: " + message); }
        #endregion END private helpers
    }
}
