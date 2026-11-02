using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.TestSupport;
using Heimdall.WarThunder;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects surviving native input ranges, complete alternatives and reviewed workspace associations during removal.</summary>
    public sealed class WarThunderControllerRemovalTests
    {
        #region BEGIN Public
        /// <summary>Removing a mapping clears dependent alternatives and retains surviving native values and independent system bindings.</summary>
        [Theory]
        [InlineData("\n")]
        [InlineData("\r\n")]
        public void Removal_preserves_surviving_offsets_and_native_text(string newline)
        {
            var backend = new WarThunderProfileBackend();
            var source = WarThunderBackendFixture.Source.Replace("\n", newline, StringComparison.Ordinal);
            var profile = WarThunderBackendFixture.Import(backend, source);
            var result = backend.RemoveController(profile, "war-thunder.joystick.0");
            var text = WarThunderBackendFixture.Export(backend, result.Profile);
            Assert.Equal("war-thunder.joystick.0", Assert.Single(result.RemainingIds).Value);
            Assert.Equal("war-thunder.joystick.1", Assert.Single(result.RemainingIds).Key);
            Assert.Contains("ID_FIRE{}", text, StringComparison.Ordinal);
            Assert.Contains("ID_CHORD{}", text, StringComparison.Ordinal);
            Assert.Contains("pitch{}", text, StringComparison.Ordinal);
            Assert.Contains("throttle{axisId:i=2;nonlinearity:i=1;}", text, StringComparison.Ordinal);
            Assert.Contains("axesOffset:i=2;axesCount:i=2;buttonsOffset:i=4;buttonsCount:i=4", text, StringComparison.Ordinal);
            Assert.Contains("keyboardKey:i=30;keyboardKey:i=31", text, StringComparison.Ordinal);
            Assert.Contains("future{setting:t=\"leave unchanged\";}", text, StringComparison.Ordinal);
            Assert.Contains(newline, text, StringComparison.Ordinal);
            Assert.Equal(source, WarThunderBackendFixture.Export(backend, profile));
            var last = backend.RemoveController(result.Profile, "war-thunder.joystick.0");
            Assert.Empty(last.Profile.Devices); Assert.Empty(last.RemainingIds);
            Assert.Contains("throttle{}", WarThunderBackendFixture.Export(backend, last.Profile), StringComparison.Ordinal);
        }
        /// <summary>Review does not mutate; apply remaps both physical and detached drawing links and discard restores exact state.</summary>
        [Fact]
        public void Reviewed_removal_follows_surviving_hardware_and_drawings()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var backend = new WarThunderProfileBackend();
            var one = new DetectedDcsDevice(Guid.NewGuid(), "One", true, ProductGuid: Guid.Parse("00020001-0000-0000-0000-504944564944"));
            var two = one with { PhysicalGuid = Guid.NewGuid(), Name = "Two" };
            var session = new GameProfileSession(backend, new(1, Guid.NewGuid(), "Removal", WarThunderBackendFixture.Import(backend), new()),
                issuer.Policy, ["drawing"], () => [one, two], new WarThunderDeviceReferenceMatcher());
            session.ApplyControllerChange(session.PreviewCapturedDeviceLink("war-thunder.joystick.0", one.PhysicalGuid));
            session.ApplyControllerChange(session.PreviewCapturedDeviceLink("war-thunder.joystick.1", two.PhysicalGuid));
            session.ChangeDrawing("war-thunder.joystick.1", "drawing");
            session.AcceptSaved(); var before = JsonSerializer.Serialize(session.Snapshot);
            var review = session.PreviewRemoveController("war-thunder.joystick.0");
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot)); Assert.Contains("dependent", review.Summary, StringComparison.Ordinal);
            session.ApplyControllerChange(review);
            Assert.True(session.HasChanges); Assert.Equal(two.PhysicalGuid, Assert.Single(session.Snapshot.Hardware).Value.Device.PhysicalGuid);
            Assert.Equal("drawing", session.DrawingId("war-thunder.joystick.0"));
            session.Discard(); Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
            session.ApplyControllerChange(session.PreviewUnlinkController("war-thunder.joystick.1"));
            session.ApplyControllerChange(session.PreviewRemoveController("war-thunder.joystick.0"));
            Assert.Empty(session.Snapshot.Hardware); Assert.Equal("drawing", session.DrawingId("war-thunder.joystick.0"));
            session.ApplyControllerChange(session.PreviewRemoveController("war-thunder.joystick.0"));
            Assert.Empty(session.Snapshot.UnlinkedDrawings);
        }
        /// <summary>Ambiguous or unsupported captures must fail before a native removal can be staged.</summary>
        [Theory]
        [InlineData("axesOffset:i=2", "axesOffset:i=1")]
        [InlineData("buttonsOffset:i=4", "buttonsOffset:i=3")]
        [InlineData("axesCount:i=2", "axesCount:i=-1")]
        [InlineData("ID_EMPTY{}", "ID_EMPTY{nested{joyButton:i=0;}}")]
        [InlineData("joyButton:i=1", "joyButton:t=\"one\"")]
        [InlineData("futureParam:r=2", "futureParam:p=1,2")]
        public void Unverifiable_dependencies_block_removal(string original, string replacement)
        {
            var backend = new WarThunderProfileBackend();
            var profile = WarThunderBackendFixture.Import(backend, WarThunderBackendFixture.Source.Replace(original, replacement, StringComparison.Ordinal));
            var before = JsonSerializer.Serialize(profile);
            Assert.Throws<InvalidOperationException>(() => backend.RemoveController(profile, "war-thunder.joystick.0"));
            Assert.Equal(before, JsonSerializer.Serialize(profile));
        }
        /// <summary>An unknown controller cannot remove any saved mapping.</summary>
        [Fact]
        public void Unknown_controller_is_rejected()
        {
            var backend = new WarThunderProfileBackend();
            Assert.Throws<InvalidOperationException>(() => backend.RemoveController(WarThunderBackendFixture.Import(backend), "missing"));
        }
        #endregion END Public
    }
}
