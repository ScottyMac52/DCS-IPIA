using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises the same saved-profile, native-edit and entitlement boundaries used by the Squadrons desktop.</summary>
    public sealed class SquadronsDesktopWorkflowTests
    {
        #region BEGIN Public
        /// <summary>Import, edit, rename, save, reopen, discard and isolated export preserve another input and native settings.</summary>
        [Fact]
        public void Desktop_session_round_trips_native_edits_and_saved_selection()
        {
            var root = Path.Combine(Path.GetTempPath(), "squadrons-desktop-" + Guid.NewGuid().ToString("N"));
            using var issuer = new OwnerLicenseTestIssuer();
            try
            {
                var backend = new SquadronsProfileBackend(); var workspace = Workspace(backend);
                var session = new GameProfileSession(backend, workspace, issuer.Policy, [], () => []);
                Assert.True(session.CanEditNativeRecords);
                session.RebindNative("IncomStarshipInputConcepts.ConceptThrottle.4", "squadrons.joystick.10", "JOY_Z", CommandType.Axis, 1);
                session.Rename("Space Controls"); Assert.True(session.HasChanges);
                var store = new FileGameWorkspaceStore(root); var revision = store.Save(session.Snapshot, null); session.AcceptSaved();
                store.SaveSelection(new("star-wars-squadrons", workspace.Id, DisplayName: "Space Controls"));
                Assert.Equal("star-wars-squadrons", store.ReadSelection().BackendId);
                Assert.Equal("Space Controls", Assert.Single(store.List()).Name);
                var reopened = new GameProfileSession(backend, store.Load(workspace.Id).Workspace, issuer.Policy, [], () => []);
                Assert.False(reopened.HasChanges);
                var rows = SquadronsWorkspaceProjection.Rows(reopened.Snapshot.Profile);
                Assert.Contains(rows, x => x.RecordId.EndsWith(".4", StringComparison.Ordinal) && x.DeviceLabel.Contains("10", StringComparison.Ordinal) && x.Editable);
                Assert.Contains(rows, x => x.DeviceLabel.Contains('3'));
                Assert.Single(SquadronsWorkspaceProjection.Filter(rows, "Flight", "Axes", "Throttle", "squadrons.joystick.10"));
                Assert.Empty(SquadronsWorkspaceProjection.Filter(rows, "Other", "Axes", "Throttle", "all"));
                Assert.Equal(2, SquadronsWorkspaceProjection.Filter(rows, "All", "All inputs", "", "all").Count);
                reopened.RebindNative("IncomStarshipInputConcepts.ConceptThrottle.4", "squadrons.joystick.3", "JOY_BTN2", CommandType.Button, 0);
                reopened.Discard(); Assert.False(reopened.HasChanges);
                var plan = reopened.Export();
                var directory = GameReviewExport.Write(root, plan.Files, [], backendId: "star-wars-squadrons");
                Assert.StartsWith("Squadrons-Review-", Path.GetFileName(directory), StringComparison.Ordinal);
                Assert.Equal(plan.Files[SquadronsProfileBackend.FileName], File.ReadAllText(Path.Combine(directory, SquadronsProfileBackend.FileName)));
                Assert.Equal(revision, store.Load(workspace.Id).Revision);
                Assert.Throws<InvalidOperationException>(() => store.Save(workspace, "stale"));
                Assert.Throws<InvalidOperationException>(() => GameReviewExport.Write(root, plan.Files, [root], backendId: "star-wars-squadrons"));
                Assert.Throws<InvalidDataException>(() => GameReviewExport.Write(root, new Dictionary<string, string> { ["bad.binds"] = "bad" }, [], backendId: "star-wars-squadrons"));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        /// <summary>Context/type/text/device filters retain each directional record and clearly preserve unsupported input types.</summary>
        [Fact]
        public void Desktop_projection_distinguishes_axes_buttons_and_preserved_inputs()
        {
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var backend = new SquadronsProfileBackend();
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var rows = SquadronsWorkspaceProjection.Rows(profile);
            Assert.Contains(rows, x => x.CommandName == "Throttle Increase");
            Assert.Contains(rows, x => x.CommandName == "Throttle Decrease");
            Assert.Equal(2, SquadronsWorkspaceProjection.Filter(rows, "All", "Axes", "", "all").Count);
            Assert.Single(SquadronsWorkspaceProjection.Filter(rows, "All", "Buttons and hats", "", "all"));
            var preserved = Assert.Single(SquadronsWorkspaceProjection.Filter(rows, "All", "Preserved inputs", "", "all"));
            Assert.False(preserved.Editable); Assert.Equal("Keyboard", preserved.DeviceLabel); Assert.Equal("Preserved native input", preserved.InputLabel);
            Assert.Single(SquadronsWorkspaceProjection.Filter(rows, "All", "All inputs", "Button 1", "all"));
            Assert.Equal(2, SquadronsWorkspaceProjection.Filter(rows, "All", "All inputs", "Throttle 1", "all").Count);
            Assert.Single(SquadronsWorkspaceProjection.Filter(rows, "All", "All inputs", "", "squadrons.joystick.3"));
            Assert.All(SquadronsInputCodec.Choices, choice => Assert.NotEmpty(choice.DisplayName));
            Assert.Contains(SquadronsInputCodec.Choices, x => x.DisplayName == "Hat 1 · U");
            Assert.Contains(SquadronsInputCodec.Choices, x => x.DisplayName == "Z axis (negative)");
            Assert.Contains(SquadronsInputCodec.Choices, x => x.DisplayName == "Z axis (positive)");
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Free cannot edit/export Squadrons, and invalid native operations never replace a staged profile.</summary>
        [Fact]
        public void Unsupported_edits_and_free_access_cannot_publish_changes()
        {
            var backend = new SquadronsProfileBackend(); var workspace = Workspace(backend);
            var free = new GameProfileSession(backend, workspace, new(), [], () => []);
            Assert.Contains("Premium", free.AccessMessage);
            Assert.Throws<InvalidOperationException>(() => free.RebindNative("missing", "squadrons.joystick.3", "JOY_BTN1", CommandType.Button, 0));
            Assert.Throws<InvalidOperationException>(() => free.Rename("New name"));
            Assert.Throws<InvalidOperationException>(() => free.Export());
            using var issuer = new OwnerLicenseTestIssuer(); var session = new GameProfileSession(backend, workspace, issuer.Policy, [], () => []);
            Assert.Throws<InvalidOperationException>(() => session.RebindNative("missing", "squadrons.joystick.3", "JOY_BTN1", CommandType.Button, 0));
            Assert.Throws<InvalidOperationException>(() => session.RebindNative("IncomStarshipInputConcepts.ConceptThrottle.4", "missing", "JOY_BTN1", CommandType.Button, 0));
            Assert.Throws<InvalidOperationException>(() => session.Rename("../invalid")); Assert.False(session.HasChanges);
            Assert.Empty(SquadronsWorkspaceProjection.Filter(SquadronsWorkspaceProjection.Rows(workspace.Profile), "All", "Buttons and hats", "missing", "all"));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static GameWorkspace Workspace(SquadronsProfileBackend backend)
        {
            var lines = new List<string> { "GstKeyBinding.InputDataVersion 4", "GstInput.JoystickDevice3 Stick 1", "GstInput.JoystickDevice5 Throttle 1", "GstInput.JoystickDevice10 Throttle 1", "Future.Setting retain" };
            foreach (var (concept, index, device, button) in new[] { ("ConceptThrottle", 4, 4, 41), ("ConceptFire", 2, 2, 22) })
                foreach (var (key, value) in new Dictionary<string, int> { ["type"] = 3, ["axis"] = 26, ["button"] = button, ["deviceid"] = device, ["identifier"] = 123, ["modifier"] = 0, ["altbutton"] = 0, ["negate"] = 0 })
                    lines.Add($"GstKeyBinding.IncomStarshipInputConcepts.{concept}.{index}.{key} {value}");
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = string.Join("\n", lines) + "\n" });
            return new(1, Guid.NewGuid(), "Squadrons Controls", profile, new());
        }
        #endregion END private helpers
    }
}
