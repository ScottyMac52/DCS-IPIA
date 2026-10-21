using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects native draft review, atomic assignment/toggle edits and exact-controller action readiness.</summary>
    public sealed class GameAssignmentReviewTests
    {
        #region BEGIN Public
        /// <summary>Representative editing, testing, missing selection and busy states follow the agreed action table.</summary>
        [Theory]
        [InlineData(false, false, false, false, false, false, false, false, false, false, false)]
        [InlineData(false, false, false, false, true, false, false, false, false, true, false)]
        [InlineData(true, true, false, true, true, false, false, false, true, true, false)]
        [InlineData(true, true, true, false, true, false, false, true, false, true, false)]
        [InlineData(true, true, true, true, true, false, false, true, true, true, false)]
        [InlineData(true, false, true, true, true, false, false, false, true, true, false)]
        [InlineData(true, true, true, true, true, true, false, false, false, false, true)]
        [InlineData(true, true, true, true, true, false, true, false, false, false, false)]
        [InlineData(false, false, false, false, false, true, true, false, false, false, true)]
        [InlineData(true, true, true, false, false, false, false, true, false, false, false)]
        public void Actions_follow_edit_test_and_busy_transitions(bool editable, bool valid, bool changed, bool assigned,
            bool ready, bool running, bool busy, bool apply, bool clear, bool start, bool stop) =>
            Assert.Equal(new GameAssignmentActions(apply, clear, start, stop),
                GameAssignmentActions.For(editable, valid, changed, assigned, ready, running, busy));
        /// <summary>Unchanged slots and combinations remain unchanged, and a complete valid edit stages once.</summary>
        [Fact]
        public void Draft_review_is_nonmutating_and_compares_native_meaning()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var workspace = Workspace();
            var backend = new EliteDangerousProfileBackend();
            var session = new GameProfileSession(backend, workspace, issuer.Policy, [], () => []);
            var rows = EliteWorkspaceProjection.Rows(workspace.Profile, session.Catalog);
            var before = JsonSerializer.Serialize(session.Snapshot); var changes = 0; session.Changed += (_, _) => changes++;
            foreach (var row in rows)
            {
                var draft = Draft(row);
                var review = session.ReviewAssignment(draft);
                Assert.True(review.Valid, review.Message); Assert.False(review.Changed);
            }
            var original = rows.First(x => x.CommandName == "Primary Fire" && x.Input == "Joy_1");
            var changed = Draft(original) with { Key = "Joy_8", Toggle = true };
            Assert.Equal(new GameAssignmentReview(true, true, ""), session.ReviewAssignment(changed));
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot)); Assert.Equal(0, changes); Assert.False(session.HasChanges);
            session.Assign(changed); Assert.Equal(1, changes);
            Assert.True(session.HasChanges); Assert.False(session.ReviewAssignment(changed).Changed);
            Assert.Equal("1", session.Snapshot.Profile.Metadata["ed.toggle:" + changed.CommandId]);
            session.Discard(); Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
            var toggleOnly = Draft(original) with { Toggle = true };
            Assert.True(session.ReviewAssignment(toggleOnly).Changed);
        }
        /// <summary>Invalid complete drafts cannot publish half a binding, a toggle or change notifications.</summary>
        [Theory]
        [InlineData("empty")][InlineData("device")][InlineData("command")][InlineData("slot")]
        [InlineData("mode")][InlineData("modifier")][InlineData("duplicate")][InlineData("toggle")]
        [InlineData("nan")][InlineData("deadzone")][InlineData("axisSlot")]
        public void Invalid_drafts_remain_atomic(string failure)
        {
            using var issuer = new OwnerLicenseTestIssuer(); var workspace = Workspace();
            var session = new GameProfileSession(new EliteDangerousProfileBackend(), workspace, issuer.Policy, [], () => []);
            var rows = EliteWorkspaceProjection.Rows(workspace.Profile, session.Catalog);
            var draft = Draft(rows.First(x => x.CommandName == "Secondary Fire"));
            var axis = Draft(rows.First(x => x.IsAxis));
            draft = failure switch
            {
                "empty" => draft with { Key = " " }, "device" => draft with { DeviceId = "missing" },
                "command" => draft with { CommandId = "missing" }, "slot" => draft with { Slot = "other" },
                "mode" => draft with { Mode = BindingMode.Toggle },
                "modifier" => draft with { Modifiers = [new() { Source = "missing", Value = "Joy_1" }] },
                "duplicate" => draft with { Modifiers = [draft.Modifiers![0], draft.Modifiers![0]] },
                "toggle" => draft with { Toggle = true }, "nan" => axis with { Deadzone = float.NaN },
                "deadzone" => axis with { Deadzone = 101 }, _ => axis with { Slot = "Primary" }
            };
            var before = JsonSerializer.Serialize(session.Snapshot); var changes = 0; session.Changed += (_, _) => changes++;
            Assert.False(session.ReviewAssignment(draft).Valid);
            Assert.ThrowsAny<Exception>(() => session.Assign(draft));
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot)); Assert.Equal(0, changes);
        }
        /// <summary>Read-only entitlement cannot gain authoring through draft review.</summary>
        [Fact]
        public void Review_enforces_edition_access()
        {
            var workspace = Workspace(); var backend = new EliteDangerousProfileBackend();
            var session = new GameProfileSession(backend, workspace, new EditionPolicy(), [], () => []);
            var draft = Draft(EliteWorkspaceProjection.Rows(workspace.Profile, session.Catalog)[0]);
            Assert.False(session.ReviewAssignment(draft).Valid);
            Assert.Throws<InvalidOperationException>(() => session.Assign(draft));
            Assert.False(session.HasChanges);
        }
        /// <summary>Ready means one fresh linked instance/product; drawings and stale connection flags are irrelevant.</summary>
        [Fact]
        public void Readiness_rejects_missing_duplicate_and_changed_hardware()
        {
            var physical = new DetectedDcsDevice(Guid.NewGuid(), "Controller", true, ProductGuid: Guid.NewGuid());
            var pin = new GameHardwarePin(null, physical with { Connected = false });
            Assert.True(GameInputReadiness.Ready(pin, [physical]));
            Assert.False(GameInputReadiness.Ready(null, [physical]));
            Assert.False(GameInputReadiness.Ready(new(null, physical with { PhysicalGuid = Guid.Empty }), [physical]));
            Assert.False(GameInputReadiness.Ready(new(null, physical with { ProductGuid = Guid.Empty }), [physical]));
            Assert.False(GameInputReadiness.Ready(pin, []));
            Assert.False(GameInputReadiness.Ready(pin, [physical with { Connected = false }]));
            Assert.False(GameInputReadiness.Ready(pin, [physical, physical]));
            Assert.False(GameInputReadiness.Ready(pin, [physical with { ProductGuid = Guid.NewGuid() }]));
            Assert.False(GameInputReadiness.Ready(pin, [physical with { PhysicalGuid = Guid.NewGuid() }]));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static GameWorkspace Workspace()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "EliteDangerous", "Synthetic.4.2.binds");
            var profile = new EliteDangerousProfileBackend().Import("controls", new Dictionary<string, string> { ["Synthetic.4.2.binds"] = File.ReadAllText(path) });
            return new(1, Guid.NewGuid(), "Draft Controls", profile, new());
        }
        private static GameAssignmentEdit Draft(EliteAssignmentRow row) => new(row.DeviceId, row.CommandId, row.Slot,
            row.Input, row.IsAxis, row.Invert, row.Deadzone, row.Mode, row.Modifiers);
        #endregion END private helpers
    }
}
