using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;
using Heimdall.WarThunder;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises native UI operations through the shared draft, identity, link and persistence boundaries.</summary>
    public sealed class WarThunderWorkspaceTests
    {
        #region BEGIN Public
        /// <summary>Native edits preserve unrelated fields, stage atomically and restore the exact saved capture on discard.</summary>
        [Fact]
        public void Native_session_stages_rebind_inverse_parameters_clear_and_add()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var session = Open(issuer.Policy);
            var original = JsonSerializer.Serialize(session.Snapshot);
            var notifications = 0; session.Changed += (_, _) => notifications++;
            var pitch = WarThunderBackendFixture.Binding(session.Snapshot.Profile, "pitch");
            var axis = WarThunderBackendFixture.Field(pitch, "axisId");
            session.ApplyNativeEdit(new WarThunderNativeEdit(WarThunderEditKind.Rebind, pitch.Id, axis.Address, "war-thunder.joystick.1", Type: CommandType.Axis, NativeIndex: 1));
            session.ApplyNativeEdit(new WarThunderNativeEdit(WarThunderEditKind.Inverse, pitch.Id, Inverted: true));
            var multiplier = WarThunderBackendFixture.Field(pitch, "kMul");
            session.ApplyNativeEdit(new WarThunderNativeEdit(WarThunderEditKind.Parameter, pitch.Id, multiplier.Address, Literal: "1.5"));
            var fire = WarThunderBackendFixture.Binding(session.Snapshot.Profile, "ID_FIRE");
            session.ApplyNativeEdit(new WarThunderNativeEdit(WarThunderEditKind.Clear, fire.Id));
            var empty = session.Catalog.Commands.Single(command => command.Metadata["war-thunder.command"] == "ID_FIRE");
            session.ApplyNativeEdit(new WarThunderNativeEdit(WarThunderEditKind.Add, CommandId: empty.Id, DeviceId: "war-thunder.joystick.0", NativeIndex: 3));
            Assert.Equal(5, notifications); Assert.True(session.HasChanges);
            var text = Assert.Single(session.Export().Files).Value;
            Assert.Contains("axisId:i=3", text, StringComparison.Ordinal);
            Assert.Contains("inverse:b=yes", text, StringComparison.Ordinal);
            Assert.Contains("kMul:r=1.5", text, StringComparison.Ordinal);
            Assert.Contains("keyboardKey:i=30;keyboardKey:i=31;", text, StringComparison.Ordinal);
            Assert.Contains("futureParam:r=2", text, StringComparison.Ordinal);
            session.Discard(); Assert.False(session.HasChanges); Assert.Equal(original, JsonSerializer.Serialize(session.Snapshot));
        }
        /// <summary>Invalid indices, literal injection and Free access fail before any current or saved state changes.</summary>
        [Fact]
        public void Failed_native_edits_and_edition_restrictions_preserve_draft()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var session = Open(issuer.Policy);
            var pitch = WarThunderBackendFixture.Binding(session.Snapshot.Profile, "pitch");
            var before = JsonSerializer.Serialize(session.Snapshot);
            Assert.Throws<InvalidOperationException>(() => session.ApplyNativeEdit(new WarThunderNativeEdit(WarThunderEditKind.Rebind,
                pitch.Id, WarThunderBackendFixture.Field(pitch, "axisId").Address, "war-thunder.joystick.0", Type: CommandType.Axis, NativeIndex: 2)));
            Assert.Throws<ArgumentException>(() => session.ApplyNativeEdit(new WarThunderNativeEdit(WarThunderEditKind.Parameter,
                pitch.Id, WarThunderBackendFixture.Field(pitch, "kMul").Address, Literal: "2;future:i=4")));
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot)); Assert.False(session.HasChanges);
            var free = Open(new EditionPolicy());
            Assert.Throws<InvalidOperationException>(() => free.ApplyNativeEdit(new WarThunderNativeEdit(WarThunderEditKind.Clear, "unused")));
        }
        /// <summary>Matching products still require distinct, explicitly selected instances and unlink before either replacement.</summary>
        [Fact]
        public void Native_links_preserve_ranges_and_require_product_and_instance_ownership()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var one = new DetectedDcsDevice(Guid.NewGuid(), "Same name", true, ProductGuid: Guid.Parse("00020001-0000-0000-0000-504944564944"));
            var two = one with { PhysicalGuid = Guid.NewGuid() };
            var wrong = one with { PhysicalGuid = Guid.NewGuid(), ProductGuid = Guid.Parse("00030001-0000-0000-0000-504944564944") };
            var session = Open(issuer.Policy, [one, two, wrong]);
            var native = Assert.Single(session.Export().Files).Value;
            Assert.True(session.CanLinkCapturedDevices);
            Assert.Throws<InvalidOperationException>(() => session.PreviewCapturedDeviceLink("war-thunder.joystick.0", wrong.PhysicalGuid));
            session.ApplyControllerChange(session.PreviewCapturedDeviceLink("war-thunder.joystick.0", one.PhysicalGuid));
            Assert.Throws<InvalidOperationException>(() => session.PreviewCapturedDeviceLink("war-thunder.joystick.1", one.PhysicalGuid));
            Assert.Throws<InvalidOperationException>(() => session.PreviewCapturedDeviceLink("war-thunder.joystick.0", two.PhysicalGuid));
            session.ChangeDrawing("war-thunder.joystick.0", "drawing-a");
            Assert.Throws<InvalidOperationException>(() => session.ChangeDrawing("war-thunder.joystick.0", "drawing-b"));
            session.ApplyControllerChange(session.PreviewUnlinkController("war-thunder.joystick.0"));
            Assert.Equal("drawing-a", session.DrawingId("war-thunder.joystick.0"));
            session.ApplyControllerChange(session.PreviewCapturedDeviceLink("war-thunder.joystick.0", two.PhysicalGuid));
            session.ChangeDrawing("war-thunder.joystick.0", null); session.ChangeDrawing("war-thunder.joystick.0", "drawing-b");
            Assert.Equal(native, Assert.Single(session.Export().Files).Value);
        }
        /// <summary>Saved profiles retain native evidence, export protections and recent selection across reopening.</summary>
        [Fact]
        public void Persistence_and_review_export_preserve_source_and_reject_wrong_payloads()
        {
            var root = Path.Combine(Path.GetTempPath(), "wt-workspace-" + Guid.NewGuid().ToString("N"));
            try
            {
                using var issuer = new OwnerLicenseTestIssuer(); var session = Open(issuer.Policy);
                var source = Path.Combine(root, "source"); Directory.CreateDirectory(source);
                var workspace = session.Snapshot with { SourceDirectory = source };
                var store = new FileGameWorkspaceStore(Path.Combine(root, "store"));
                var revision = store.Save(workspace, null); var reopened = store.Load(workspace.Id);
                Assert.Equal(revision, reopened.Revision); Assert.Equal(source, reopened.Workspace.SourceDirectory);
                store.SaveSelection(new("war-thunder", workspace.Id, DisplayName: workspace.Name)); Assert.Equal("war-thunder", store.ReadSelection().BackendId);
                var backend = new WarThunderProfileBackend(); var plan = backend.Export(reopened.Workspace.Profile);
                Assert.Throws<InvalidOperationException>(() => GameReviewExport.Write(source, plan.Files, [source], "war-thunder"));
                var folder = GameReviewExport.Write(root, plan.Files, [source], "war-thunder");
                Assert.Equal(WarThunderBackendFixture.Source, File.ReadAllText(Path.Combine(folder, "Test controls.blk")));
                Assert.Empty(Directory.GetFiles(source));
                Assert.Throws<InvalidDataException>(() => GameReviewExport.Write(root, new Dictionary<string, string> { ["wrong.binds"] = "x" }, [], "war-thunder"));
                Assert.Throws<InvalidDataException>(() => GameReviewExport.Write(root, new Dictionary<string, string> { ["a.blk"] = "x", ["b.blk"] = "x" }, [], "war-thunder"));
                var copy = GameConfigurations.Create(reopened.Workspace, "Independent", false);
                Assert.NotEqual(workspace.Id, copy.Id); Assert.Equal(source, copy.SourceDirectory);
                Assert.Equal(JsonSerializer.Serialize(workspace.Profile), JsonSerializer.Serialize(copy.Profile));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        /// <summary>Catalog ownership filters retain unassigned, mixed and system alternatives without physical aliases.</summary>
        [Fact]
        public void Native_projection_and_reference_sheet_preserve_occurrence_details()
        {
            var backend = new WarThunderProfileBackend(); var profile = WarThunderBackendFixture.Import(backend);
            var catalog = backend.GetCatalog(profile).Commands; var rows = WarThunderWorkspaceProjection.Rows(profile, catalog);
            Assert.Equal(WarThunderProfileBackend.ReadBindings(profile).Count, rows.Count);
            Assert.Contains(rows, row => row.Inputs.Contains("native Axis 1", StringComparison.Ordinal));
            Assert.Contains(rows, row => row.Inputs == "System / other native fields");
            var filter = WarThunderWorkspaceProjection.Filter(catalog, rows, "war-thunder.joystick.1", "", null, CommandType.Axis, GameCommandAssignmentStatus.AssignedToController);
            Assert.Equal("throttle", Assert.Single(filter).DisplayName);
            Assert.Contains("keyboardKey:i=30", WarThunderWorkspaceProjection.ReferenceText(rows), StringComparison.Ordinal);
            Assert.Contains("activationType:i=9", WarThunderWorkspaceProjection.ReferenceText(rows), StringComparison.Ordinal);
            Assert.DoesNotContain("JOY_", WarThunderWorkspaceProjection.ReferenceText(rows), StringComparison.Ordinal);
        }
        /// <summary>Overlapping saved ranges stay unresolved, while each ownership/status/category filter follows native evidence.</summary>
        [Fact]
        public void Filters_do_not_claim_ambiguous_ownership_or_parameter_only_assignments()
        {
            var backend = new WarThunderProfileBackend();
            var source = WarThunderBackendFixture.Source.Replace("axesOffset:i=2", "axesOffset:i=1", StringComparison.Ordinal)
                .Replace("empty_axis{}", "empty_axis{nonlinearity:i=1;}", StringComparison.Ordinal);
            var profile = WarThunderBackendFixture.Import(backend, source); var catalog = backend.GetCatalog(profile).Commands;
            var rows = WarThunderWorkspaceProjection.Rows(profile, catalog);
            var pitch = Assert.Single(rows, row => row.Binding.Command == "pitch");
            Assert.Empty(pitch.DeviceIds); Assert.Contains("Unresolved native Axis", pitch.Inputs, StringComparison.Ordinal);
            var unassigned = WarThunderWorkspaceProjection.Filter(catalog, rows, null, "", "Native axes", CommandType.Axis, GameCommandAssignmentStatus.UnassignedEverywhere);
            Assert.Equal("empty axis", Assert.Single(unassigned).DisplayName);
            Assert.NotEmpty(WarThunderWorkspaceProjection.Filter(catalog, rows, null, "", null, null, GameCommandAssignmentStatus.AssignedAnywhere));
            Assert.NotEmpty(WarThunderWorkspaceProjection.Filter(catalog, rows, "missing", "FIRE", "Native hotkeys", CommandType.Button, GameCommandAssignmentStatus.NotAssignedToController));
            Assert.Empty(WarThunderWorkspaceProjection.Filter(catalog, rows, null, "missing", null, null, GameCommandAssignmentStatus.All));
            Assert.Empty(WarThunderWorkspaceProjection.Filter(catalog, rows, null, "", "missing", null, GameCommandAssignmentStatus.All));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static GameProfileSession Open(EditionPolicy policy, IReadOnlyList<DetectedDcsDevice>? observed = null)
        {
            var backend = new WarThunderProfileBackend(); var profile = WarThunderBackendFixture.Import(backend);
            return new(backend, new(1, Guid.NewGuid(), "Synthetic", profile, new()), policy, ["drawing-a", "drawing-b"], () => observed ?? [], new WarThunderDeviceReferenceMatcher());
        }
        #endregion END private helpers
    }
}
