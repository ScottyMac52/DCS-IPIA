using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.EliteDangerous;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects persisted presentation order, legacy documents and byte-identical native exports.</summary>
    public sealed class GameControllerOrderTests
    {
        #region BEGIN Public
        /// <summary>Moving display entries saves independently and never renumbers native devices or changes either export.</summary>
        [Fact]
        public void Reordering_persists_and_discards_without_changing_native_bytes()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var backend = new EliteDangerousProfileBackend(); var workspace = Workspace();
            var session = new GameProfileSession(backend, workspace, issuer.Policy, [], () => []);
            var ids = GameControllerOrder.For(workspace).ToArray();
            var native = JsonSerializer.Serialize(session.Snapshot.Profile);
            var raw = JsonSerializer.Serialize(session.Export().Files);
            var named = JsonSerializer.Serialize(backend.ExportNamed(session.Snapshot.Profile, workspace.Name).Files);
            var changes = 0; session.Changed += (_, _) => changes++;
            session.MoveController(ids[2], ids[0]);
            Assert.Equal(new[] { ids[2], ids[0], ids[1] }, GameControllerOrder.For(session.Snapshot));
            Assert.True(session.HasChanges); Assert.Equal(1, changes);
            var root = Path.Combine(Path.GetTempPath(), "heimdall-order-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new FileGameWorkspaceStore(root); store.Save(session.Snapshot, null); session.AcceptSaved();
                var reopened = new GameProfileSession(backend, store.Load(workspace.Id).Workspace, issuer.Policy, [], () => []);
                Assert.Equal(GameControllerOrder.For(session.Snapshot), GameControllerOrder.For(reopened.Snapshot));
                Assert.False(reopened.HasChanges);
                reopened.MoveController(ids[2], ids[1], after: true);
                Assert.Equal(ids, GameControllerOrder.For(reopened.Snapshot));
                Assert.True(reopened.HasChanges); reopened.Discard();
                Assert.Equal(GameControllerOrder.For(session.Snapshot), GameControllerOrder.For(reopened.Snapshot));
                Assert.Equal(native, JsonSerializer.Serialize(reopened.Snapshot.Profile));
                Assert.Equal(raw, JsonSerializer.Serialize(reopened.Export().Files));
                Assert.Equal(named, JsonSerializer.Serialize(backend.ExportNamed(reopened.Snapshot.Profile, workspace.Name).Files));
                var detached = reopened.Snapshot; detached.ControllerOrder.Clear();
                Assert.NotEmpty(reopened.Snapshot.ControllerOrder);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        /// <summary>Self drops, adjacent no-ops and invalid references never dirty an authorized session or raise change events.</summary>
        [Fact]
        public void Noop_and_invalid_moves_are_atomic_in_a_licensed_session()
        {
            var workspace = Workspace(); var ids = GameControllerOrder.For(workspace).ToArray();
            using var issuer = new OwnerLicenseTestIssuer();
            var session = new GameProfileSession(new EliteDangerousProfileBackend(), workspace, issuer.PremiumPolicy, [], () => []);
            Assert.Empty(session.AccessMessage);
            var changes = 0; session.Changed += (_, _) => changes++;
            session.MoveController(ids[0], ids[0]); session.MoveController(ids[0], ids[1]);
            session.MoveController(ids[1], ids[0], after: true);
            Assert.Throws<ArgumentException>(() => session.MoveController("missing", ids[0]));
            Assert.Throws<ArgumentException>(() => session.MoveController(ids[0], "missing"));
            Assert.False(session.HasChanges); Assert.Equal(0, changes);
            session.MoveController(ids[0], ids[2], after: true);
            Assert.True(session.HasChanges); Assert.Equal(1, changes);
        }
        /// <summary>Older envelopes need no migration; removed preferences are ignored and additions append deterministically.</summary>
        [Fact]
        public void Legacy_and_changed_device_lists_reconcile_without_touching_native_order()
        {
            var workspace = Workspace(); var ids = GameControllerOrder.For(workspace).ToArray();
            var json = JsonSerializer.Serialize(workspace).Replace(",\"ControllerOrder\":[]", "", StringComparison.Ordinal);
            var legacy = JsonSerializer.Deserialize<GameWorkspace>(json)!;
            GameWorkspaceContract.Validate(legacy); Assert.Empty(legacy.ControllerOrder);
            Assert.Equal(ids, GameControllerOrder.For(legacy));
            var preference = workspace with { ControllerOrder = [ids[2], "removed", ids[0]] };
            Assert.Equal(new[] { ids[2], ids[0], ids[1] }, GameControllerOrder.For(preference));
            Assert.Equal(ids, preference.Profile.Devices.Select(x => x.LogicalId));
            Assert.Throws<ArgumentNullException>(() => GameControllerOrder.For(null!));
            Assert.Throws<InvalidDataException>(() => GameWorkspaceContract.Validate(workspace with { ControllerOrder = null! }));
            Assert.Throws<InvalidDataException>(() => GameWorkspaceContract.Validate(workspace with { ControllerOrder = [" "] }));
            Assert.Throws<InvalidDataException>(() => GameWorkspaceContract.Validate(workspace with { ControllerOrder = [ids[0], ids[0]] }));
        }
        /// <summary>Removal prunes saved preferences, replacement keeps its slot, and new devices append.</summary>
        [Fact]
        public void Lifecycle_edits_reconcile_order_and_invalidate_stale_reviews()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var workspace = Workspace();
            var device = new DetectedDcsDevice(Guid.NewGuid(), "New controller", true,
                ProductGuid: Guid.Parse("1001044f-0000-0000-0000-504944564944"));
            var session = new GameProfileSession(new EliteDangerousProfileBackend(), workspace, issuer.Policy, [], () => [device], new EliteDeviceReferenceMatcher());
            var ids = GameControllerOrder.For(workspace).ToArray();
            var stale = session.PreviewRemoveController(ids[0]);
            session.MoveController(ids[0], ids[2], after: true);
            Assert.Throws<InvalidOperationException>(() => session.ApplyControllerChange(stale));
            session.ApplyControllerChange(session.PreviewReplaceController(ids[0], device.PhysicalGuid, () => ["Joy_1"]));
            var replaced = GameControllerOrder.For(session.Snapshot);
            Assert.Equal(new[] { ids[1], ids[2] }, replaced.Take(2));
            var added = replaced[2]; Assert.DoesNotContain(ids[0], replaced);
            session.ApplyControllerChange(session.PreviewRemoveController(added));
            Assert.Equal(new[] { ids[1], ids[2] }, session.Snapshot.ControllerOrder);
            session.ConnectController(device.PhysicalGuid);
            Assert.Equal(new[] { ids[1], ids[2] }, GameControllerOrder.For(session.Snapshot).Take(2));
            Assert.Equal(3, GameControllerOrder.For(session.Snapshot).Count);
        }
        /// <summary>An existing replacement takes the source position, including when it previously appeared earlier.</summary>
        [Fact]
        public void Existing_replacement_inherits_the_source_position_and_self_replacement_keeps_order()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var workspace = Workspace();
            var device = new DetectedDcsDevice(Guid.NewGuid(), "Indexed controller", true,
                ProductGuid: Guid.Parse("0bac06a3-0000-0000-0000-504944564944"));
            var session = new GameProfileSession(new EliteDangerousProfileBackend(), workspace, issuer.Policy, [], () => [device], new EliteDeviceReferenceMatcher());
            var ids = GameControllerOrder.For(workspace).ToArray();
            session.MoveController(ids[0], ids[2], after: true);
            session.ApplyControllerChange(session.PreviewReplaceController(ids[0], device.PhysicalGuid, () => ["Joy_1", "Joy_2", "Joy_3"], ids[1]));
            Assert.Equal(new[] { ids[2], ids[1] }, GameControllerOrder.For(session.Snapshot));
            var linked = JsonSerializer.Serialize(session.Snapshot);
            Assert.Throws<InvalidOperationException>(() => session.PreviewReplaceController(ids[1], device.PhysicalGuid,
                () => ["Joy_1", "Joy_2", "Joy_3"], ids[1]));
            Assert.Equal(linked, JsonSerializer.Serialize(session.Snapshot));
            session.ApplyControllerChange(session.PreviewUnlinkController(ids[1]));
            session.ApplyControllerChange(session.PreviewReplaceController(ids[1], device.PhysicalGuid, () => ["Joy_1", "Joy_2", "Joy_3"], ids[1]));
            Assert.Equal(new[] { ids[2], ids[1] }, GameControllerOrder.For(session.Snapshot));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static GameWorkspace Workspace()
        {
            var backend = new EliteDangerousProfileBackend();
            var profile = backend.Import("controls", new Dictionary<string, string> { ["Order.4.2.binds"] = """
                <Root PresetName="Order" MajorVersion="4" MinorVersion="2">
                 <PrimaryFire><Primary Device="06A30BAC" DeviceIndex="0" Key="Joy_1"/><Secondary Device="06A30BAC" DeviceIndex="1" Key="Joy_2"/></PrimaryFire>
                 <SecondaryFire><Primary Device="Keyboard" Key="Key_F"><Modifier Device="06A30BAC" DeviceIndex="1" Key="Joy_3"/></Primary><Secondary Device="{NoDevice}" Key=""/></SecondaryFire>
                </Root>
                """ });
            return new(1, Guid.NewGuid(), "Ordered Controls", profile, new());
        }
        #endregion END private helpers
    }
}
