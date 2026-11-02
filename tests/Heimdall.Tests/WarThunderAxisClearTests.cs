using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.TestSupport;
using Heimdall.WarThunder;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Ensures axis clear is a reversible native input edit that preserves tuning and other input families.</summary>
    public sealed class WarThunderAxisClearTests
    {
        #region BEGIN Public
        /// <summary>Only the joystick axis scalar is removed; all other native characters remain unchanged.</summary>
        [Theory]
        [InlineData("\n", ";")]
        [InlineData("\r\n", ";")]
        [InlineData("\n", "")]
        public void Clear_preserves_tuning_comments_and_other_inputs(string newline, string terminator)
        {
            var backend = new WarThunderProfileBackend();
            var source = WarThunderBackendFixture.Source.Replace("axisId:i=1;", "/* keep */ axisId:i=1" + terminator + "\n", StringComparison.Ordinal)
                .Replace("mouseAxisId:i=-1", "mouseAxisId:i=2", StringComparison.Ordinal).Replace("\n", newline, StringComparison.Ordinal);
            var profile = WarThunderBackendFixture.Import(backend, source);
            var pitch = WarThunderBackendFixture.Binding(profile, "pitch");
            Assert.True(WarThunderProfileBackend.GetBindingCapabilities(profile, pitch.Id).ClearAssignment);
            var cleared = WarThunderProfileBackend.ClearBinding(profile, pitch.Id);
            var expected = source.Replace("axisId:i=1" + terminator, "", StringComparison.Ordinal);
            Assert.Equal(expected, WarThunderBackendFixture.Export(backend, cleared));
            var remaining = WarThunderBackendFixture.Binding(cleared, "pitch");
            Assert.DoesNotContain(remaining.Fields, field => WarThunderWorkspaceProjection.FieldName(field) == "axisId");
            var capabilities = WarThunderProfileBackend.GetBindingCapabilities(cleared, remaining.Id);
            Assert.False(capabilities.ClearAssignment); Assert.True(capabilities.AddNativeInput);
            Assert.Throws<InvalidOperationException>(() => WarThunderProfileBackend.ClearBinding(cleared, remaining.Id));
            var command = backend.GetCatalog(cleared).Commands.Single(item => item.Metadata["war-thunder.command"] == "pitch");
            var restored = WarThunderDrawingProjection.Assignment(cleared, command.Id, "war-thunder.joystick.1", new("JOY_X", CommandType.Axis, 1)).Apply(cleared);
            Assert.Equal("3", WarThunderBackendFixture.Field(WarThunderBackendFixture.Binding(restored, "pitch"), "axisId").Value);
            Assert.Contains("kMul:r=1.25;mouseAxisId:i=2;futureParam:r=2", WarThunderBackendFixture.Export(backend, restored), StringComparison.Ordinal);
            Assert.Equal(source, WarThunderBackendFixture.Export(backend, profile));
        }
        /// <summary>Session staging updates callout ownership and unassigned filters, and discard restores the exact saved source.</summary>
        [Fact]
        public void Clear_updates_projection_and_shared_draft_state()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var backend = new WarThunderProfileBackend(); var profile = WarThunderBackendFixture.Import(backend);
            var session = new GameProfileSession(backend, new(1, Guid.NewGuid(), "Clear axis", profile, new()), issuer.Policy, [], () => []);
            var before = JsonSerializer.Serialize(session.Snapshot);
            var pitch = WarThunderBackendFixture.Binding(profile, "pitch");
            session.ApplyNativeEdit(new WarThunderNativeEdit(WarThunderEditKind.Clear, pitch.Id));
            Assert.True(session.HasChanges);
            var cleared = session.Snapshot.Profile;
            var map = new WarThunderPhysicalMap([new("JOY_Y", CommandType.Axis, 1)], "test mapping");
            Assert.Empty(WarThunderDrawingProjection.Assignments(cleared, "war-thunder.joystick.0", "JOY_Y", map));
            var catalog = session.Catalog.Commands;
            var rows = WarThunderWorkspaceProjection.Rows(cleared, catalog);
            Assert.Equal("Unassigned", Assert.Single(rows, row => row.Binding.Command == "pitch").Inputs);
            Assert.Contains(WarThunderWorkspaceProjection.Filter(catalog, rows, null, "", null, CommandType.Axis,
                GameCommandAssignmentStatus.UnassignedEverywhere), command => command.Metadata["war-thunder.command"] == "pitch");
            session.Discard(); Assert.False(session.HasChanges); Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
        }
        /// <summary>Ambiguous, unsupported and unassigned axes stay unavailable instead of clearing unrelated native data.</summary>
        [Theory]
        [InlineData("axisId:i=1", "axisId:i=-1")]
        [InlineData("axisId:i=1", "axisId:t=\"one\"")]
        [InlineData("axisId:i=1", "axisId:i=1;axisId:i=0")]
        [InlineData("pitch{", "pitch{nested{}")]
        [InlineData("empty_axis{}", "pitch{}")]
        [InlineData("futureParam:r=2", "futureParam:p=2")]
        public void Unsupported_axis_shapes_are_not_clearable(string original, string replacement)
        {
            var backend = new WarThunderProfileBackend();
            var profile = WarThunderBackendFixture.Import(backend, WarThunderBackendFixture.Source.Replace(original, replacement, StringComparison.Ordinal));
            var pitch = WarThunderBackendFixture.Binding(profile, "pitch");
            Assert.False(WarThunderProfileBackend.GetBindingCapabilities(profile, pitch.Id).ClearAssignment);
            Assert.Throws<InvalidOperationException>(() => WarThunderProfileBackend.ClearBinding(profile, pitch.Id));
        }
        /// <summary>Low-level field removal cannot alter an unknown occurrence or opaque capture.</summary>
        [Fact]
        public void Scalar_removal_requires_editable_captured_evidence()
        {
            var document = WarThunderBlkDocument.Parse("root{axisId:i=1}");
            Assert.Equal("root{}", document.RemoveScalar(Assert.Single(document.Scalars).Address));
            Assert.Throws<InvalidOperationException>(() => document.RemoveScalar("missing"));
            var opaque = WarThunderBlkDocument.Parse("root{axisId:i=1;future:p=2;}");
            Assert.Throws<InvalidOperationException>(() => opaque.RemoveScalar(opaque.Scalars[0].Address));
        }
        #endregion END Public
    }
}
