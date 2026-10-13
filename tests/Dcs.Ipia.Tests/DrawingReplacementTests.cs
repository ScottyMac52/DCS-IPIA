using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Exercises selective built-in replacement, portable rollback and explicit multi-module saves.</summary>
    public sealed class DrawingReplacementTests
    {
        #region BEGIN Public
        /// <summary>Import, review, stage and discard never replace a saved drawing; only selected units are saved.</summary>
        [Fact]
        public async Task Upload_only_and_selective_replacement_preserve_unselected_modules_and_units()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-global-drawings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var connection = "Data Source=" + Path.Combine(root, "ipia.db") + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                var workspace = BuiltIn("First", units: 2);
                var other = BuiltIn("Second");
                await repository.SaveAsync(workspace);
                await repository.SaveAsync(other);
                var originalJson = JsonSerializer.Serialize(await repository.GetAsync(workspace.Configuration.Module.Id));
                var otherJson = JsonSerializer.Serialize(await repository.GetAsync(other.Configuration.Module.Id));
                var revision = Revision();
                await new FileUserMappingLibrary(root).SaveAsync(revision);
                var session = new DrawingReplacementSession(revision, repository);
                await session.LoadAsync();
                Assert.Equal(3, session.CompatibleTargets.Count);
                Assert.Equal(0, session.StagedModuleCount);
                Assert.Equal(originalJson, JsonSerializer.Serialize(await repository.GetAsync(workspace.Configuration.Module.Id)));
                var selected = session.Targets.First(x => x.Workspace.Configuration.Module.Id == workspace.Configuration.Module.Id);
                Assert.Contains(selected.Diagram.Instance!, selected.Title, StringComparison.Ordinal);
                Assert.Contains(selected.Workspace.Configuration.Profiles.Single(x => x.Id == selected.Diagram.ProfileId).SourceName, selected.Title, StringComparison.Ordinal);
                session.Stage([selected]);
                Assert.Equal(originalJson, JsonSerializer.Serialize(await repository.GetAsync(workspace.Configuration.Module.Id)));
                Assert.Throws<InvalidOperationException>(() => session.Stage([selected]));
                await Assert.ThrowsAsync<InvalidOperationException>(session.LoadAsync);
                session.Discard();
                Assert.Equal(0, session.StagedModuleCount);
                session.Stage([selected, selected]); // Repeated UI selection cannot duplicate a physical profile.
                await session.SaveAsync();
                Assert.Equal(1, session.SavedModuleCount);
                Assert.True(session.RequiresReload);
                Assert.Throws<InvalidOperationException>(() => session.Stage([selected]));
                var saved = (await repository.GetAsync(workspace.Configuration.Module.Id))!;
                Assert.Equal(revision.Id, saved.Diagrams.Single(x => x.ProfileId == selected.Diagram.ProfileId).CustomMapping!.Id);
                Assert.Equal(selected.Diagram.Svg, saved.Diagrams.Single(x => x.ProfileId == selected.Diagram.ProfileId).OriginalDrawing!.Svg);
                Assert.Equal(JsonSerializer.Serialize(workspace.Assignments), JsonSerializer.Serialize(saved.Assignments));
                Assert.Equal(JsonSerializer.Serialize(workspace.Configuration), JsonSerializer.Serialize(saved.Configuration));
                Assert.Equal(otherJson, JsonSerializer.Serialize(await repository.GetAsync(other.Configuration.Module.Id)));
                var untouched = workspace.Diagrams.Single(x => x.ProfileId != selected.Diagram.ProfileId);
                Assert.Equal(JsonSerializer.Serialize(untouched), JsonSerializer.Serialize(saved.Diagrams.Single(x => x.ProfileId == untouched.ProfileId)));
                await session.LoadAsync();
                Assert.Equal(2, session.CompatibleTargets.Count);
                Assert.Contains(session.Targets, x => x.AlreadyApplied && !x.CanReplace && x.CanRestoreOriginal);
                session.Stage(session.CompatibleTargets);
                await session.SaveAsync();
                Assert.Equal(2, session.SavedModuleCount);
                foreach (var id in await repository.ListAsync())
                    Assert.All((await repository.GetAsync(id))!.Diagrams, x => Assert.Equal(revision.Id, x.CustomMapping!.Id));
            }
            finally { SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
        }

        /// <summary>Initial built-in artwork survives SQLite and portable snapshots and restores with current user labels.</summary>
        [Fact]
        public async Task Original_artwork_rollback_is_portable_and_keeps_custom_history()
        {
            var original = BuiltIn("Rollback");
            var id = original.Diagrams.Single().ProfileId;
            var first = Revision();
            var second = Revision("#eeeeee");
            var adopted = UserMappingAdoption.Apply(original, id, first);
            var changed = UserMappingAdoption.Apply(adopted, id, second);
            Assert.Same(adopted.Diagrams.Single().OriginalDrawing, changed.Diagrams.Single().OriginalDrawing);
            var restoredSnapshot = EditorSnapshotSerializer.Deserialize(EditorSnapshotSerializer.Serialize(changed));
            var renamed = restoredSnapshot with { Diagrams = restoredSnapshot.Diagrams.Select(x => x with
                { Controls = x.Controls.Select(c => c with { Label = "Personal " + c.Control }).ToArray() }).ToArray() };
            var rollback = UserMappingAdoption.RestoreOriginal(renamed, id);
            var diagram = rollback.Diagrams.Single();
            Assert.Null(diagram.CustomMapping);
            Assert.Null(diagram.OriginalDrawing);
            Assert.Equal(original.Diagrams.Single().Svg, diagram.Svg);
            Assert.Equal(original.Diagrams.Single().Path, diagram.Path);
            Assert.Equal(original.Diagrams.Single().Background, diagram.Background);
            Assert.All(diagram.Controls, c => Assert.Equal("Personal " + c.Control, c.Label));
            Assert.Equal(2, diagram.MappingHistory!.Count);
            Assert.Same(renamed.Assignments, rollback.Assignments);
            Assert.Same(renamed.Configuration, rollback.Configuration);
            Assert.Same(renamed.Sources, rollback.Sources);
            Assert.Same(renamed.Pages, rollback.Pages);
            Assert.Same(renamed.Labels, rollback.Labels);
            var roundTrip = EditorSnapshotSerializer.Deserialize(EditorSnapshotSerializer.Serialize(rollback));
            Assert.Equal(2, roundTrip.Diagrams.Single().MappingHistory!.Count);
            var readopted = UserMappingAdoption.Apply(roundTrip, id, first);
            Assert.Equal(second.Id, Assert.Single(readopted.Diagrams.Single().MappingHistory!).Id);
            Assert.NotNull(readopted.Diagrams.Single().OriginalDrawing);
            var repository = new Fakes.DrawingReviewRepository([renamed]);
            var session = new DrawingReplacementSession(second, repository);
            await session.LoadAsync();
            session.Stage(session.Targets, restoreOriginal: true);
            Assert.Equal(second.Id, (await repository.GetAsync(original.Configuration.Module.Id))!.Diagrams.Single().CustomMapping!.Id);
            await session.SaveAsync();
            Assert.Null((await repository.GetAsync(original.Configuration.Module.Id))!.Diagrams.Single().CustomMapping);
        }

        /// <summary>Built-in stable markers cannot be renamed, removed or remapped even when native key sets match.</summary>
        [Theory]
        [InlineData("rename")]
        [InlineData("remove")]
        [InlineData("remap")]
        [InlineData("axis")]
        public async Task Incompatible_built_in_replacements_are_blocked(string kind)
        {
            var workspace = BuiltIn("Blocked");
            var diagram = workspace.Diagrams.Single();
            var controls = diagram.Controls.ToArray();
            if (kind == "rename") controls[0] = controls[0] with { Control = "renamed" };
            if (kind == "remove") controls[0] = controls[0] with { Control = "retained-built-in-marker" };
            if (kind == "remap")
            {
                var key = controls[0].InputKey;
                controls[0] = controls[0] with { InputKey = controls[1].InputKey };
                controls[1] = controls[1] with { InputKey = key };
            }
            if (kind == "axis") controls[0] = controls[0] with { IsAxis = !controls[0].IsAxis };
            workspace = workspace with { Diagrams = [diagram with { Controls = controls }] };
            var repository = new Fakes.DrawingReviewRepository([workspace]);
            var session = new DrawingReplacementSession(Revision(), repository);
            await session.LoadAsync();
            Assert.Empty(session.CompatibleTargets);
            Assert.NotEmpty(session.Targets.Single().Review.Conflicts);
            Assert.Throws<InvalidOperationException>(() => session.Stage(session.Targets));
            Assert.Throws<InvalidOperationException>(() => UserMappingAdoption.Apply(workspace, diagram.ProfileId, Revision()));
            Assert.Equal(0, session.StagedModuleCount);
            Assert.Equal(0, repository.WriteCount);
        }

        /// <summary>Empty, foreign and non-restorable selections never stage; empty/discarded drafts cannot save.</summary>
        [Fact]
        public async Task Selection_boundaries_and_read_only_storage_are_enforced()
        {
            var workspace = BuiltIn("Selection");
            var repository = new Fakes.DrawingReviewRepository([workspace], includeMissingModule: true);
            var session = new DrawingReplacementSession(Revision(), repository);
            await session.LoadAsync();
            Assert.Single(session.Targets);
            Assert.True(session.CanSave);
            Assert.False(session.Targets.Single().CanRestoreOriginal);
            Assert.Throws<InvalidOperationException>(() => session.Stage([]));
            Assert.Throws<InvalidOperationException>(() => session.Stage(session.Targets, restoreOriginal: true));
            var another = new DrawingReplacementSession(Revision(), new Fakes.DrawingReviewRepository([BuiltIn("Foreign")]));
            await another.LoadAsync();
            Assert.Throws<InvalidOperationException>(() => session.Stage(another.Targets));
            await Assert.ThrowsAsync<InvalidOperationException>(session.SaveAsync);
            var readonlySession = new DrawingReplacementSession(Revision(), new Fakes.ReadOnlyDrawingRepository(workspace));
            await readonlySession.LoadAsync();
            Assert.False(readonlySession.CanSave);
            readonlySession.Stage(readonlySession.CompatibleTargets);
            await Assert.ThrowsAsync<InvalidOperationException>(readonlySession.SaveAsync);
            readonlySession.Discard();
            Assert.Equal(0, readonlySession.StagedModuleCount);
            Assert.Throws<InvalidDataException>(() => UserMappingAdoption.RestoreOriginal(workspace, workspace.Diagrams.Single().ProfileId));
            var generic = Generic();
            generic = UserMappingAdoption.Apply(generic, generic.Diagrams.Single().ProfileId, Revision());
            Assert.Throws<InvalidOperationException>(() => UserMappingAdoption.RestoreOriginal(generic, generic.Diagrams.Single().ProfileId));
        }

        /// <summary>A partial failure reports saved modules, preserves remaining drafts and requires a fresh review.</summary>
        [Fact]
        public async Task Partial_save_failure_cannot_be_silently_retried_or_overwrite_stale_data()
        {
            var first = BuiltIn("Saved");
            var second = BuiltIn("Stale");
            var repository = new Fakes.DrawingReviewRepository([first, second]) { FailModule = second.Configuration.Module.Id };
            var session = new DrawingReplacementSession(Revision(), repository);
            await session.LoadAsync();
            session.Stage(session.CompatibleTargets);
            await Assert.ThrowsAsync<InvalidOperationException>(session.SaveAsync);
            Assert.Equal(1, session.SavedModuleCount);
            Assert.Equal(1, session.StagedModuleCount);
            Assert.True(session.RequiresReload);
            Assert.Null((await repository.GetAsync(second.Configuration.Module.Id))!.Diagrams.Single().CustomMapping);
            await Assert.ThrowsAsync<InvalidOperationException>(session.SaveAsync);
            session.Discard();
            Assert.Throws<InvalidOperationException>(() => session.Stage(session.Targets));
            await session.LoadAsync();
            Assert.False(session.RequiresReload);
            Assert.Equal(0, session.SavedModuleCount);
        }

        /// <summary>Corrupt original-artwork evidence cannot be persisted or used for rollback.</summary>
        [Theory]
        [InlineData("custom-path")]
        [InlineData("generic-path")]
        [InlineData("empty-svg")]
        [InlineData("width-nan")]
        [InlineData("height-nan")]
        [InlineData("width-zero")]
        [InlineData("height-zero")]
        [InlineData("duplicate-marker")]
        [InlineData("unknown-marker")]
        [InlineData("axis-kind")]
        [InlineData("input-key")]
        [InlineData("generic-evidence")]
        public void Corrupt_original_artwork_is_rejected(string corruption)
        {
            var workspace = BuiltIn("Integrity");
            var id = workspace.Diagrams.Single().ProfileId;
            var adopted = UserMappingAdoption.Apply(workspace, id, Revision());
            var diagram = adopted.Diagrams.Single();
            var original = diagram.OriginalDrawing!;
            var invalid = corruption switch
            {
                "custom-path" => original with { Path = "user-mappings/fake.svg" },
                "generic-path" => original with { Path = "generic/fake.svg" },
                "empty-svg" => original with { Svg = "" },
                "width-nan" => original with { Width = double.NaN },
                "height-nan" => original with { Height = double.NaN },
                "width-zero" => original with { Width = 0 },
                "height-zero" => original with { Height = 0 },
                "duplicate-marker" => original with { Controls = original.Controls.Append(original.Controls[0]).ToArray() },
                "unknown-marker" => original with { Controls = [original.Controls[0] with { Control = "unknown" }] },
                "axis-kind" => original with { Controls = [original.Controls[0] with { IsAxis = !original.Controls[0].IsAxis }] },
                "input-key" => original with { Controls = [original.Controls[0] with { InputKey = "JOY_BTN128" }] },
                _ => original
            };
            var corrupt = diagram with { OriginalDrawing = invalid,
                GenericCapabilities = corruption == "generic-evidence" ? GenericHardwareFixture.Definition().Capabilities : null };
            Assert.Throws<InvalidDataException>(() => UserMappingAdoption.Validate(corrupt));
            Assert.Throws<InvalidDataException>(() => UserMappingAdoption.RestoreOriginal(adopted with { Diagrams = [corrupt] }, id));
        }

        /// <summary>All shipped built-in drawings retain their stable inputs on first adoption and original rollback.</summary>
        [Theory]
        [MemberData(nameof(BundledDrawingImportTests.Drawings), MemberType = typeof(BundledDrawingImportTests))]
        public async Task Shipped_built_in_drawings_adopt_and_restore(string path)
        {
            var sources = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
            var hardware = JsonSerializer.Deserialize<WorkspaceHardware[]>(sources["config/ipia-hardware.json"])!
                .First(x => Path.GetFileNameWithoutExtension(x.Diagram.Path) == Path.GetFileNameWithoutExtension(path));
            var definition = BundledDrawingImportTests.Definition(hardware.Diagram.Controls.Select(x => x.InputKey));
            var inputs = hardware.Diagram.Controls.ToDictionary(x => x.Control, x => x.InputKey, StringComparer.Ordinal);
            var revision = DrawioMappingFileImport.Load(Path.Combine(BundledSharedCatalog.Root, path), definition, "test", inputs);
            var workspace = WorkspaceStructureEditor.AddDevice(GenericHardwareFixture.Workspace(), hardware, AssignmentLayer.Module,
                "Test controller {11111111-1111-1111-1111-111111111111}.diff.lua", "Test", "Pilot");
            var diagram = workspace.Diagrams.Single();
            var adopted = UserMappingAdoption.Apply(workspace, diagram.ProfileId, revision);
            Assert.NotNull(adopted.Diagrams.Single().OriginalDrawing);
            var restored = UserMappingAdoption.RestoreOriginal(adopted, diagram.ProfileId).Diagrams.Single();
            Assert.Equal(diagram.Svg, restored.Svg);
            Assert.Equal(diagram.Controls, restored.Controls);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static UserMappingRevision Revision(string color = "#dddddd") => DrawioMappingImporter.Import(
            DrawioMappingStarter.Export(GenericHardwareFixture.Definition()).Replace("fillColor=#ffffff", "fillColor=" + color, StringComparison.Ordinal),
            GenericHardwareFixture.Definition(), "controller.drawio", "test");
        private static EditorWorkspaceSnapshot Generic() => InventoryHardwareAuthoring.Add(GenericHardwareFixture.Workspace(),
            GenericHardwareFixture.Unit(GenericHardwareFixture.Definition(), "Unit"), AssignmentLayer.Module);
        private static EditorWorkspaceSnapshot BuiltIn(string name, int units = 1)
        {
            var workspace = Generic();
            if (units > 1) workspace = InventoryHardwareAuthoring.Add(workspace,
                GenericHardwareFixture.Unit(GenericHardwareFixture.Definition(), "Second unit"), AssignmentLayer.Module);
            var profiles = workspace.Configuration.Profiles.Select(x => new DeviceProfile(x.Id, "built-in-test", x.Instance, x.Role, x.SourceName)).ToArray();
            workspace = workspace with
            {
                Configuration = new ModuleConfiguration(new(workspace.Configuration.Module.Id, name, name), profiles, workspace.Configuration.Modifiers,
                    workspace.Configuration.Bindings, workspace.Configuration.Provenance),
                Diagrams = workspace.Diagrams.Select(x => x with { GenericCapabilities = null, Path = "assets/shared/hardware/test.svg" }).ToArray()
            };
            var profile = profiles[0];
            return workspace with { Assignments = [new(new(workspace.Configuration.Module.Id, profile.Id, AssignmentLayer.Module,
                BindingSection.Axis, "JOY_X"), new("axis", "Pitch", false, AssignmentLayer.Module)) { Filter = new(Deadzone: 0.1) }] };
        }
        #endregion END private helpers
    }
}
