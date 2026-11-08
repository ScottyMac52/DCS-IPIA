using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>
    /// DCS same-data lifecycle at the native boundary on Linux: the real SQLite repository and the single draft composition used by
    /// open, save-accept and discard, over the fixtures the Windows conformance smokes use. The DCS session/context adapter lives in
    /// the WPF app and is exercised only by Heimdall.App.Smoke on Windows.
    /// </summary>
    public sealed class DcsLifecycleNativeConformanceTests : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "heimdall-dcs-lifecycle-" + Guid.NewGuid().ToString("N") + ".db");

        #region BEGIN Public
        /// <summary>Gets the shared DCS fixtures: the context-conformance mixed snapshot and the binding-derived command-smoke shape.</summary>
        public static TheoryData<string> Fixtures => new() { "mixed", "binding-derived" };

        /// <summary>Save, reopen, accept/discard baseline and an edited save round-trip the exact native snapshot.</summary>
        /// <param name="name">The fixture.</param>
        /// <returns>Completion.</returns>
        [Theory, MemberData(nameof(Fixtures))]
        public async Task Save_reopen_accept_discard_and_edit_round_trip_exact_native_bytes(string name)
        {
            var original = name == "mixed" ? MixedSnapshotFixture.Create() : DcsSavedWorkspaceDraftTests.BindingDerived();
            var connection = "Data Source=" + _path + ";Pooling=False";
            await new SqliteMigrationRunner(connection).MigrateAsync();
            var repository = new DcsSqliteEditorWorkspaceRepository(connection);
            var id = original.Configuration.Module.Id;
            await repository.SaveAsync(original);

            var reopened = (await repository.GetAsync(id))!;
            Assert.Equal(DcsEditorSnapshotSerializer.Serialize(original).Sha256, DcsEditorSnapshotSerializer.Serialize(reopened).Sha256);
            // Open, save-accept and discard all rebuild through this one composition; the baseline is exactly the saved assignments.
            var baseline = DcsSavedWorkspaceDraft.Create(reopened);
            Assert.Equal(Keys(reopened.Assignments), Keys(baseline.Snapshot)); Assert.False(baseline.HasChanges);
            Assert.Equal(DcsSavedWorkspaceDraft.ModuleCatalog(original), DcsSavedWorkspaceDraft.ModuleCatalog(reopened));

            var module = reopened.Configuration.Module;
            var edited = reopened with
            {
                Configuration = new DcsModuleConfiguration(new DcsModule(module.Id, module.DcsId, module.DisplayName + " edited"),
                    reopened.Configuration.Profiles, reopened.Configuration.Modifiers, reopened.Configuration.Bindings, reopened.Configuration.Provenance)
            };
            _ = DcsSavedWorkspaceDraft.Create(edited); // The accepted baseline is built before the write, as DcsWorkspaceSession.PrepareSave does.
            await repository.SaveAsync(edited);
            var after = (await repository.GetAsync(id))!;
            Assert.Equal(module.DisplayName + " edited", after.Configuration.Module.DisplayName);
            Assert.Equal(Keys(reopened.Assignments), Keys(after.Assignments));
            Assert.Equal(DcsEditorSnapshotSerializer.Serialize(edited).Sha256, DcsEditorSnapshotSerializer.Serialize(after).Sha256);
            Assert.Single(await repository.ListAsync(), x => x == id);
        }

        /// <summary>An invalid saved assignment is rejected while building the baseline, before any write.</summary>
        /// <returns>Completion.</returns>
        [Fact]
        public async Task Invalid_candidate_is_rejected_before_persistence_and_the_saved_row_is_unchanged()
        {
            var original = DcsSavedWorkspaceDraftTests.BindingDerived();
            var connection = "Data Source=" + _path + ";Pooling=False";
            await new SqliteMigrationRunner(connection).MigrateAsync();
            var repository = new DcsSqliteEditorWorkspaceRepository(connection);
            await repository.SaveAsync(original);
            var stray = original with { Assignments = [new(original.Assignments[0].Slot, new("a9999cdnil", "Stray", true, AssignmentLayer.UiLayer))] };
            Assert.Throws<ArgumentException>(() => DcsSavedWorkspaceDraft.Create(stray));
            var stored = (await repository.GetAsync(original.Configuration.Module.Id))!;
            Assert.Equal(DcsEditorSnapshotSerializer.Serialize(original).Sha256, DcsEditorSnapshotSerializer.Serialize(stored).Sha256);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_path)) File.Delete(_path);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string[] Keys(IEnumerable<DcsDraftAssignment> assignments) =>
            [.. assignments.Select(x => x.Slot.Key + "=" + x.Command.Layer + ":" + x.Command.Id + ":" + x.Filter).Order(StringComparer.Ordinal)];
        #endregion END private helpers
    }
}
