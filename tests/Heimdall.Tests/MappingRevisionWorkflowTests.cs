using System.Text.Json;
using System.Xml.Linq;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises editable starters and reviewed portable revision changes against real persistence.</summary>
    public sealed class MappingRevisionWorkflowTests
    {
        private static readonly double[] FlatCurve = [0.0];
        #region BEGIN Public
        /// <summary>Every native button, axis and POV survives deterministic starter export and external geometry edits.</summary>
        /// <param name="highCount">Whether the controller reaches the supported button/hat limit.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Starter_round_trip_preserves_native_identity(bool highCount)
        {
            var definition = GenericHardwareFixture.Definition(highCount);
            var source = DrawioMappingStarter.Export(definition);
            Assert.Equal(source, DrawioMappingStarter.Export(definition));
            var document = XDocument.Parse(source);
            var label = document.Descendants("mxCell").First(x => x.Attribute("id")?.Value == "label-JOY_BTN1");
            label.Element("mxGeometry")!.SetAttributeValue("x", 30);
            var revision = DrawioMappingImporter.Import(document.ToString(), definition, "starter.drawio", "test");
            Assert.Equal(definition.Capabilities.DcsKeys.Order(StringComparer.Ordinal), revision.MappedInputs);
            Assert.Equal(definition.Capabilities.DcsKeys.Count, revision.Controls.Count);
            Assert.All(revision.Controls, x => Assert.Equal(x.InputKey, x.Control));
            Assert.Equal(30, revision.Controls.Single(x => x.Control == "JOY_BTN1").X);
            Assert.Contains(revision.Controls, x => x.IsAxis);
            Assert.Contains(revision.Controls, x => x.InputKey.StartsWith("JOY_BTN_POV", StringComparison.Ordinal));
        }

        /// <summary>Drafts, reviewed saves and rollback retain assignments and portable history without changing another module.</summary>
        [Fact]
        public async Task Revision_adoption_save_discard_rollback_and_snapshot_are_isolated()
        {
            var definition = GenericHardwareFixture.Definition();
            var workspace = InventoryHardwareAuthoring.Add(GenericHardwareFixture.Workspace(),
                GenericHardwareFixture.Unit(definition, "Pilot"), AssignmentLayer.Module);
            var profile = workspace.Configuration.Profiles.Single();
            workspace = WorkspaceStructureEditor.SetModifier(workspace, null,
                new(AssignmentLayer.Module, profile.Id, "SHIFT", "JOY_BTN1", "Shift", ModifierMode.Hold, ""));
            var assignments = new[] { new DraftAssignment(
                new(workspace.Configuration.Module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Axis, "JOY_X"),
                new("axis", "Pitch", false, AssignmentLayer.Module))
                { Filter = new(Deadzone: 0.1, SaturationX: 0.8, SaturationY: 0.9, Curvature: FlatCurve) } };
            workspace = workspace with { Assignments = assignments };
            var source = DrawioMappingStarter.Export(definition);
            var first = DrawioMappingImporter.Import(source, definition, "controller.drawio", "test");
            var second = DrawioMappingImporter.Import(source.Replace("fillColor=#ffffff", "fillColor=#eeeeee", StringComparison.Ordinal),
                definition, "controller.drawio", "test");
            var pinned = UserMappingAdoption.Apply(workspace, profile.Id, first);
            var review = MappingRevisionReview.Create(pinned.Diagrams.Single(), second);
            Assert.Empty(review.Conflicts);
            Assert.NotEmpty(review.Changes);
            var root = Path.Combine(Path.GetTempPath(), "ipia-revisions-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var connection = "Data Source=" + Path.Combine(root, "ipia.db") + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(pinned);
                var other = GenericHardwareFixture.Workspace();
                other = other with { Configuration = new ModuleConfiguration(new DcsModule(Guid.NewGuid(), "OTHER", "Other aircraft"),
                        other.Configuration.Profiles, other.Configuration.Modifiers, other.Configuration.Bindings, other.Configuration.Provenance) };
                other = InventoryHardwareAuthoring.Add(other, GenericHardwareFixture.Unit(definition, "Other pilot"), AssignmentLayer.Module);
                other = UserMappingAdoption.Apply(other, other.Configuration.Profiles.Single().Id, first);
                await repository.SaveAsync(other);
                var otherJson = JsonSerializer.Serialize(await repository.GetAsync(other.Configuration.Module.Id));
                var library = new FileUserMappingLibrary(root);
                await library.SaveAsync(first);
                await library.SaveAsync(second);
                Assert.Equal(first.Id, (await repository.GetAsync(workspace.Configuration.Module.Id))!.Diagrams.Single().CustomMapping!.Id);
                var expected = (await repository.GetAsync(workspace.Configuration.Module.Id))!;
                var draft = UserMappingAdoption.Apply(expected, profile.Id, second);
                Assert.Same(expected.Assignments, draft.Assignments);
                Assert.Equal(first.Id, (await repository.GetAsync(workspace.Configuration.Module.Id))!.Diagrams.Single().CustomMapping!.Id);
                await repository.SaveIfUnchangedAsync(expected, draft);
                await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveIfUnchangedAsync(expected, draft));
                var saved = (await repository.GetAsync(workspace.Configuration.Module.Id))!;
                Assert.Equal(second.Id, saved.Diagrams.Single().CustomMapping!.Id);
                Assert.Equal(first.Id, Assert.Single(saved.Diagrams.Single().MappingHistory!).Id);
                Assert.Equal(JsonSerializer.Serialize(expected.Configuration), JsonSerializer.Serialize(saved.Configuration));
                Assert.Equal(JsonSerializer.Serialize(expected.Assignments), JsonSerializer.Serialize(saved.Assignments));
                Assert.Equal(otherJson, JsonSerializer.Serialize(await repository.GetAsync(other.Configuration.Module.Id)));
                var legacySnapshot = EditorSnapshotSerializer.Serialize(expected);
                Assert.DoesNotContain("mappingHistory", legacySnapshot.Content, StringComparison.Ordinal);
                Assert.Equal(legacySnapshot.Content, EditorSnapshotSerializer.Serialize(EditorSnapshotSerializer.Deserialize(legacySnapshot)).Content);
                var snapshot = EditorSnapshotSerializer.Serialize(saved);
                var restored = EditorSnapshotSerializer.Deserialize(snapshot);
                var rollback = UserMappingAdoption.Apply(restored, profile.Id, first);
                Assert.Equal(first.Id, rollback.Diagrams.Single().CustomMapping!.Id);
                Assert.Contains(rollback.Diagrams.Single().MappingHistory!, x => x.Id == second.Id);
                Assert.Equal(JsonSerializer.Serialize(saved.Assignments), JsonSerializer.Serialize(rollback.Assignments));
                var backup = Path.Combine(root, "full.heimdall-backup.zip");
                // Backup destination is outside the active profile directory.
                var active = Path.Combine(root, "profile");
                Directory.CreateDirectory(active);
                var activeLibrary = new FileUserMappingLibrary(active);
                await activeLibrary.SaveAsync(first);
                await activeLibrary.SaveAsync(second);
                var activeConnection = "Data Source=" + Path.Combine(active, "ipia.db") + ";Pooling=False";
                await new SqliteMigrationRunner(activeConnection).MigrateAsync();
                await new SqliteEditorWorkspaceRepository(activeConnection).SaveAsync(saved);
                var recovery = new UserDataRecoveryService(active, "test");
                recovery.Backup(backup);
                recovery.Restore(backup);
                Assert.Equal(2, (await new FileUserMappingLibrary(active).ListAsync()).Count);
                Assert.Single((await new SqliteEditorWorkspaceRepository(activeConnection).GetAsync(workspace.Configuration.Module.Id))!.Diagrams.Single().MappingHistory!);
            }
            finally { SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
        }

        /// <summary>Changed stable markers or physical inputs block adoption and leave the original pin untouched.</summary>
        [Fact]
        public void Identity_changes_require_reconciliation()
        {
            var definition = GenericHardwareFixture.Definition();
            var workspace = InventoryHardwareAuthoring.Add(GenericHardwareFixture.Workspace(),
                GenericHardwareFixture.Unit(definition, "Pilot"), AssignmentLayer.Module);
            var profile = workspace.Configuration.Profiles.Single();
            var source = DrawioMappingStarter.Export(definition);
            var first = DrawioMappingImporter.Import(source, definition, "controller.drawio", "test");
            var pinned = UserMappingAdoption.Apply(workspace, profile.Id, first);
            var changed = DrawioMappingImporter.Import(source.Replace("label-JOY_BTN1", "label-renamed", StringComparison.Ordinal),
                definition, "controller.drawio", "test");
            Assert.Contains(MappingRevisionReview.Create(pinned.Diagrams.Single(), changed).Conflicts, x => x.Contains("JOY_BTN1", StringComparison.Ordinal));
            var error = Assert.Throws<InvalidOperationException>(() => UserMappingAdoption.Apply(pinned, profile.Id, changed));
            Assert.Contains("reconciliation", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(first.Id, pinned.Diagrams.Single().CustomMapping!.Id);
            var incompatible = DrawioMappingImporter.Import(DrawioMappingStarter.Export(GenericHardwareFixture.Definition(true)),
                GenericHardwareFixture.Definition(true), "other.drawio", "test");
            var incompatibleReview = MappingRevisionReview.Create(pinned.Diagrams.Single(), incompatible);
            Assert.NotEmpty(incompatibleReview.Conflicts);
            Assert.Contains(incompatibleReview.Conflicts, x => x.Contains("definition-only:", StringComparison.Ordinal) ||
                x.Contains("profile-only:", StringComparison.Ordinal));
        }

        /// <summary>Repeated physical units adopt independently while unsaved edits and physical identities remain intact.</summary>
        [Fact]
        public void A_selected_profile_changes_without_touching_another_unit_or_unsaved_edits()
        {
            var definition = GenericHardwareFixture.Definition();
            var workspace = InventoryHardwareAuthoring.Add(GenericHardwareFixture.Workspace(),
                GenericHardwareFixture.Unit(definition, "Left"), AssignmentLayer.Module);
            workspace = InventoryHardwareAuthoring.Add(workspace, GenericHardwareFixture.Unit(definition, "Right"), AssignmentLayer.Module);
            var ids = workspace.Diagrams.Select(x => x.ProfileId).ToArray();
            var source = DrawioMappingStarter.Export(definition);
            var first = DrawioMappingImporter.Import(source, definition, "panel.drawio", "test");
            var second = DrawioMappingImporter.Import(source.Replace("fillColor=#ffffff", "fillColor=#dddddd", StringComparison.Ordinal),
                definition, "panel.drawio", "test");
            foreach (var id in ids) workspace = UserMappingAdoption.Apply(workspace, id, first);
            workspace = InventoryHardwareAuthoring.RenameControl(workspace, ids[0], "JOY_BTN1", "Unsaved personal label");
            var untouched = JsonSerializer.Serialize(workspace.Diagrams.Single(x => x.ProfileId == ids[1]));
            var changed = UserMappingAdoption.Apply(workspace, ids[0], second);
            Assert.Same(workspace.Configuration, changed.Configuration);
            Assert.Same(workspace.Assignments, changed.Assignments);
            Assert.Same(workspace.Labels, changed.Labels);
            Assert.Equal("Unsaved personal label", changed.Diagrams.Single(x => x.ProfileId == ids[0]).Controls.Single(x => x.InputKey == "JOY_BTN1").Label);
            Assert.Equal(untouched, JsonSerializer.Serialize(changed.Diagrams.Single(x => x.ProfileId == ids[1])));
        }
        #endregion END Public
    }
}
