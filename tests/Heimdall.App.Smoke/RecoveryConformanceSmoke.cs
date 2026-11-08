using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Heimdall.App.Games;
using Heimdall.App.Games.Dcs;
using Heimdall.App.Games.Squadrons;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Dcs;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Heimdall.TestSupport;

namespace Heimdall.App.Smoke
{
    /// <summary>
    /// Stale-session recovery through production entry points. The fault is a native install observer failing on an
    /// existing native change event after the single durable write (a partial install); no adapter catch or fault API is added.
    /// DCS: the sealed session refuses every mounted mutation route; disposal releases the seal and a detached draft stays isolated.
    /// Squadrons (representative real window): profile switch, import, game switch and close leave a stale session without a
    /// prompt, discard or write; ordinary Save/Discard/Cancel prompts are unchanged.
    /// </summary>
    internal static class RecoveryConformanceSmoke
    {
        #region BEGIN Public
        internal static void Verify()
        {
            var previous = EditionAccess.Policy; using var issuer = new OwnerLicenseTestIssuer();
            var root = Path.Combine(Path.GetTempPath(), "heimdall-recovery-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                EditionAccess.SetForTests(issuer.PremiumPolicy);
                DcsPersistenceCompletionSmoke.Verify(root);
                ProfileCreationRecoverySmoke.Verify(root);
                VerifyDcsSeal(root); Console.WriteLine("Recovery conformance: DCS seal passed.");
                VerifySquadronsWindow(root); Console.WriteLine("Recovery conformance: Squadrons window entry points passed.");
            }
            finally { EditionAccess.SetForTests(previous); Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void VerifyDcsSeal(string root)
        {
            var connection = "Data Source=" + Path.Combine(root, "dcs.db") + ";Pooling=False";
            new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
            var repository = new DcsSqliteEditorWorkspaceRepository(connection);
            var workspace = MixedSnapshotFixture.Create(); repository.SaveAsync(workspace).GetAwaiter().GetResult();
            var counter = new CountingDcsRepository(repository);
            var session = new DcsWorkspaceSession(new(workspace), counter, new EmptyDeviceDiscovery(), null, null, null, null, null, null, null, null, null);
            var context = new DcsWorkspaceContext(session, new DispatcherWorkspaceScheduler(Dispatcher.CurrentDispatcher),
                () => throw new InvalidOperationException("Use the context-issued lease."), () => throw new InvalidOperationException("Use the context-issued lease."));
            var profiles = new DcsProfileOperations(context, session);
            string Native() => JsonSerializer.Serialize(new { session.Working, session.Saved, Assignments = session.Draft?.Snapshot, session.Labels });
            string Durable() => JsonSerializer.Serialize(repository.GetAsync(workspace.Configuration.Module.Id).GetAwaiter().GetResult());
            try
            {
                session.SetLabel("recovery", "durable label");
                var armed = true;
                session.StateChanged += (_, _) => { if (armed && session.Saved?.Labels.ContainsKey("recovery") == true) { armed = false; throw new IOException("Synthetic native install observer failure"); } };
                var saved = Await(profiles.SaveAsync(default));
                Require(saved.Committed && saved.RecoveryRequired && counter.Writes == 1 && context.State.Recovery.Length > 0 && context.State.Operation is null,
                    "DCS partial install did not commit once and enter recovery.");
                var durable = Durable();
                Require(durable.Contains("durable label", StringComparison.Ordinal), "DCS durable write lost the staged label.");
                var (native, draft) = (Native(), session.Draft!);
                var slot = draft.Snapshot[0].Slot;
                Require(Throws<InvalidOperationException>(() => session.SetLabel("stale", "x")), "Sealed DCS label edit was accepted.");
                Require(session.RunStructureEdit(x => x).Outcome == WorkspaceOperationOutcome.Blocked, "Sealed DCS structure edit was accepted.");
                Require(draft.Remove(slot).Outcome != DraftOutcome.Applied, "Retained mounted DCS draft accepted a mutation.");
                Require(Await(profiles.DiscardAsync(default)).Outcome == WorkspaceOperationOutcome.Blocked, "Stale DCS discard was admitted.");
                Require(Await(profiles.SaveAsync(default)).Outcome == WorkspaceOperationOutcome.Blocked, "Stale DCS save was admitted.");
                Require(Native() == native && Durable() == durable && counter.Writes == 1, "A refused DCS mutation changed native or durable state.");
                context.Dispose();
                // Disposal releases the seal; the detached draft may edit, but it can no longer reach mounted or durable state.
                Require(draft.Remove(slot).Outcome == DraftOutcome.Applied && session.Draft is null && Durable() == durable && counter.Writes == 1,
                    "Disposal stranded the seal or a detached DCS draft reached durable state.");
            }
            finally { profiles.Dispose(); context.Dispose(); }
        }

        private static void VerifySquadronsWindow(string root)
        {
            var files = new FileGameWorkspaceStore(Path.Combine(root, "squadrons")); var store = new CountingGameWorkspaceStore(files);
            var backend = new SquadronsProfileBackend();
            var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced");
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = File.ReadAllText(fixture) });
            var (a, b) = (new GameWorkspace(1, Guid.NewGuid(), "Recovery A", profile, new()), new GameWorkspace(1, Guid.NewGuid(), "Recovery B", profile, new()));
            files.Save(a, null); files.Save(b, null);
            var capture = Path.Combine(root, "capture"); Directory.CreateDirectory(capture);
            var source = Path.Combine(capture, SquadronsProfileBackend.FileName); File.Copy(fixture, source);
            var services = new GameWorkspaceServices(store, new EmptyDeviceDiscovery());
            var prompts = 0; var choice = MessageBoxResult.Cancel;
            MessageBoxResult Choose() { prompts++; return choice; }

            // Profile switch, then import, after partial-install recovery; ordinary prompts in between.
            var window = new SquadronsWorkspaceWindow(services, a.Id, Choose);
            try
            {
                window.Show(); ThemeControlSmoke.Pump(window);
                Recover(window, files, store, "Recovery A saved");
                var writes = store.Writes;
                Pick(window, b.Id);
                Require(prompts == 0 && window.Session!.Id == b.Id && window.WorkspaceContext.State.Recovery.Length == 0 && store.Writes == writes,
                    "Profile switch did not abandon the stale session without a prompt or write.");
                Require(files.Load(a.Id).Workspace.Name == "Recovery A saved", "The durable receipt changed while leaving.");
                // Ordinary guard: Cancel stays, Discard leaves without writing, Save writes exactly once.
                Rename(window, "Recovery B pending"); choice = MessageBoxResult.Cancel; Pick(window, a.Id);
                Require(prompts == 1 && window.Session!.Id == b.Id && window.WorkspaceContext.State.Dirty && store.Writes == writes, "Ordinary Cancel prompt changed.");
                choice = MessageBoxResult.No; Pick(window, a.Id);
                Require(prompts == 2 && window.Session!.Id == a.Id && store.Writes == writes && files.Load(b.Id).Workspace.Name == "Recovery B", "Ordinary Discard prompt changed.");
                Rename(window, "Recovery A again"); choice = MessageBoxResult.Yes; Pick(window, b.Id);
                Require(prompts == 3 && window.Session!.Id == b.Id && store.Writes == writes + 1 && files.Load(a.Id).Workspace.Name == "Recovery A again", "Ordinary Save prompt changed.");
                Recover(window, files, store, "Recovery B saved"); writes = store.Writes; prompts = 0;
                window.ImportFile(source);
                Require(prompts == 0 && window.Session!.Id != b.Id && window.WorkspaceContext.State.Recovery.Length == 0 && store.Writes == writes,
                    "Import did not replace the stale session without a prompt or write.");
            }
            finally { choice = MessageBoxResult.No; window.Close(); window.Dispose(); }

            // Game switch after recovery.
            prompts = 0; window = new SquadronsWorkspaceWindow(services, a.Id, Choose);
            try
            {
                window.Show(); ThemeControlSmoke.Pump(window);
                Recover(window, files, store, "Recovery A game");
                var writes = store.Writes;
                ((ComboBox)window.FindName("GamePicker")).SelectedIndex = 1; ThemeControlSmoke.Pump(window);
                Require(prompts == 0 && window.RequestedGame == "elite-dangerous" && !window.IsVisible && store.Writes == writes, "Game switch did not leave the stale session.");
            }
            finally { window.Dispose(); }

            // Close: the ordinary prompt still guards a dirty workspace, and a stale one closes without a prompt.
            prompts = 0; window = new SquadronsWorkspaceWindow(services, a.Id, Choose);
            try
            {
                window.Show(); ThemeControlSmoke.Pump(window);
                Rename(window, "Recovery A unsaved"); choice = MessageBoxResult.Cancel; window.Close(); ThemeControlSmoke.Pump(window);
                Require(prompts == 1 && window.IsVisible, "Ordinary close prompt changed.");
                choice = MessageBoxResult.No; Rename(window, "Recovery A game");
                Recover(window, files, store, "Recovery A closed");
                var writes = store.Writes; prompts = 0;
                window.Close(); ThemeControlSmoke.Pump(window);
                Require(prompts == 0 && !window.IsVisible && store.Writes == writes, "Close did not leave the stale session.");
            }
            finally { window.Dispose(); }
        }

        private static void Recover(SquadronsWorkspaceWindow window, FileGameWorkspaceStore files, CountingGameWorkspaceStore store, string name)
        {
            Rename(window, name);
            var session = window.Session!; var armed = true; var writes = store.Writes;
            session.Changed += (_, _) => { if (armed && session.Name == name) { armed = false; throw new IOException("Synthetic native install observer failure"); } };
            window.SaveProfile(); ThemeControlSmoke.Pump(window);
            var state = window.WorkspaceContext.State;
            Require(state.Recovery.Length > 0 && state.Operation is null && store.Writes == writes + 1, "Partial install did not commit once and enter recovery.");
            var durable = files.Load(session.Id).Workspace;
            Require(durable.Name == name && JsonSerializer.Serialize(durable.Profile) == JsonSerializer.Serialize(session.Snapshot.Profile),
                "The mounted stale document diverged from the durable receipt.");
            var snapshot = window.WorkspaceContext.CaptureSnapshot();
            var nameBox = (TextBox)window.FindName("ProfileName");
            Require(!nameBox.IsEnabled && !((Button)window.FindName("SaveButton")).IsEnabled, "Stale window still offers rename or save.");
            Require(Throws<InvalidOperationException>(() => session.Rename("stale")) && Throws<InvalidOperationException>(() => window.WorkspaceContext.SetEditedName("stale")),
                "A mounted stale mutation was accepted.");
            Require(!window.SaveProfile() && window.WorkspaceContext.CaptureSnapshot() == snapshot && store.Writes == writes + 1, "A stale save wrote or changed state.");
        }

        private static void Rename(SquadronsWorkspaceWindow window, string name)
        {
            ((TextBox)window.FindName("ProfileName")).Text = name; ThemeControlSmoke.Pump(window);
        }

        private static void Pick(SquadronsWorkspaceWindow window, Guid id)
        {
            var picker = (ComboBox)window.FindName("ProfilePicker");
            picker.SelectedItem = picker.Items.Cast<GameWorkspaceEntry>().Single(x => x.Id == id); ThemeControlSmoke.Pump(window);
        }

        private static WorkspaceOperationResult Await(Task<WorkspaceOperationResult> task)
        {
            var frame = new DispatcherFrame(); task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
            if (!task.IsCompleted) Dispatcher.PushFrame(frame);
            return task.GetAwaiter().GetResult();
        }

        private static bool Throws<T>(Action action) where T : Exception
        {
            try { action(); return false; }
            catch (T) { return true; }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Recovery conformance: " + message);
        }
        #endregion END private helpers
    }
}
