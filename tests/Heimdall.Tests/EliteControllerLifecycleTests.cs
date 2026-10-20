using System.Text.Json;
using System.Xml.Linq;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects native ED controller removal/replacement, chords, indices, cancellation and saved state.</summary>
    public sealed class EliteControllerLifecycleTests
    {
        private const string Native = """
            <Root PresetName="Lifecycle" MajorVersion="4" MinorVersion="2">
              <PrimaryFire><Primary Device="06A30BAC" Key="Joy_1"/><Secondary Device="Keyboard" Key="Key_Space"><Modifier Device="06A30BAC" Key="Joy_2"/><Hold Value="1"/></Secondary><ToggleOn Value="0"/></PrimaryFire>
              <YawAxisRaw><Binding Device="06A30BAC" Key="Joy_XAxis"/><Inverted Value="1"/><Deadzone Value="0.125"/></YawAxisRaw>
              <SecondaryFire><Primary Device="Keyboard" Key="Key_F"/><Secondary Device="{NoDevice}" Key=""/></SecondaryFire>
              <FutureSetting Value="preserve"/>
            </Root>
            """;
        #region BEGIN Public
        /// <summary>Removal clears owned axes/buttons and whole dependent chords, retaining unrelated XML and selector bytes.</summary>
        [Fact]
        public void Removal_is_previewed_atomic_and_round_trips_without_dangling_modifiers()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var session = Session(Workspace(), issuer.Policy, []);
            var before = JsonSerializer.Serialize(session.Snapshot);
            var change = session.PreviewRemoveController(Source(session));
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
            Assert.Contains("cleared: 3", change.Summary);
            session.ApplyControllerChange(change);
            Assert.True(session.HasChanges);
            var files = session.Export().Files;
            Assert.Equal("Lifecycle\nLifecycle\nLifecycle\nLifecycle\n", files["StartPreset.4.start"]);
            var xml = XDocument.Parse(files["Lifecycle.4.2.binds"]);
            Assert.DoesNotContain(xml.Descendants(), n => (string?)n.Attribute("Device") == "06A30BAC");
            Assert.Equal("{NoDevice}", (string?)xml.Root!.Element("PrimaryFire")!.Element("Secondary")!.Attribute("Device"));
            Assert.Equal("Key_F", (string?)xml.Root.Element("SecondaryFire")!.Element("Primary")!.Attribute("Key"));
            Assert.Equal("preserve", (string?)xml.Root.Element("FutureSetting")!.Attribute("Value"));
            Assert.DoesNotContain(new EliteDangerousProfileBackend().Import("controls", files).Devices, x => x.Identity!.SimSpecificIds["ed.device"] == "06A30BAC");
            session.Discard(); Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
        }
        /// <summary>Supported keys move exactly, unsupported modifiers clear the entire chord, and persisted state can be reopened.</summary>
        [Fact]
        public void Replacement_moves_buttons_axes_and_only_supported_modifiers()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var device = Replacement();
            var session = Session(Workspace(), issuer.Policy, [device]);
            var before = JsonSerializer.Serialize(session.Snapshot);
            var plan = session.PreviewReplaceController(Source(session), device.PhysicalGuid, () => ["Joy_1", "Joy_XAxis"]);
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
            Assert.Contains("cleared: 1", plan.Summary);
            session.ApplyControllerChange(plan);
            var files = session.Export().Files;
            var xml = XDocument.Parse(files["Lifecycle.4.2.binds"]);
            Assert.Equal("044F1001", (string?)xml.Root!.Element("PrimaryFire")!.Element("Primary")!.Attribute("Device"));
            Assert.Equal("{NoDevice}", (string?)xml.Root.Element("PrimaryFire")!.Element("Secondary")!.Attribute("Device"));
            Assert.Equal("0.125", (string?)xml.Root.Element("YawAxisRaw")!.Element("Deadzone")!.Attribute("Value"));
            Assert.Equal("1", (string?)xml.Root.Element("YawAxisRaw")!.Element("Inverted")!.Attribute("Value"));
            Assert.DoesNotContain(xml.Descendants(), n => (string?)n.Attribute("Device") == "06A30BAC");
            var reimported = new EliteDangerousProfileBackend().Import("controls", files);
            Assert.Single(reimported.Devices.Where(x => x.Identity!.SimSpecificIds["ed.device"] == "044F1001"));
            var root = Path.Combine(Path.GetTempPath(), "heimdall-ed-lifecycle-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new FileGameWorkspaceStore(root); store.Save(session.Snapshot, null); session.AcceptSaved();
                var reopened = Session(store.Load(session.Snapshot.Id).Workspace, issuer.Policy, [device]);
                Assert.Equal(files["Lifecycle.4.2.binds"], reopened.Export().Files["Lifecycle.4.2.binds"]);
                Assert.False(reopened.HasChanges);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        /// <summary>Cross-device modifiers are rewritten along with their source controller, retaining hold mode.</summary>
        [Fact]
        public void Compatible_chords_keep_their_modifier_and_hold_semantics()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var device = Replacement();
            var session = Session(Workspace(), issuer.Policy, [device]);
            session.ApplyControllerChange(session.PreviewReplaceController(Source(session), device.PhysicalGuid, () => ["Joy_1", "Joy_2", "Joy_XAxis"]));
            var secondary = XDocument.Parse(session.Export().Files["Lifecycle.4.2.binds"]).Root!.Element("PrimaryFire")!.Element("Secondary")!;
            Assert.Equal("Keyboard", (string?)secondary.Attribute("Device"));
            Assert.Equal("044F1001", (string?)secondary.Element("Modifier")!.Attribute("Device"));
            Assert.Equal("Joy_2", (string?)secondary.Element("Modifier")!.Attribute("Key"));
            Assert.Equal("1", (string?)secondary.Element("Hold")!.Attribute("Value"));
        }
        /// <summary>A stale plan, foreign session, disconnect, changed product or changed inputs cannot overwrite current work.</summary>
        [Theory]
        [InlineData("stale")]
        [InlineData("foreign")]
        [InlineData("disconnect")]
        [InlineData("product")]
        [InlineData("inputs")]
        public void Changed_review_conditions_fail_without_mutation(string failure)
        {
            using var issuer = new OwnerLicenseTestIssuer(); var device = Replacement();
            IReadOnlyList<DetectedDcsDevice> observed = [device]; IReadOnlyCollection<string> keys = ["Joy_1", "Joy_2", "Joy_XAxis"];
            var session = new GameProfileSession(new EliteDangerousProfileBackend(), Workspace(), issuer.Policy, [], () => observed, new EliteDeviceReferenceMatcher());
            var plan = session.PreviewReplaceController(Source(session), device.PhysicalGuid, () => keys);
            if (failure == "stale") session.Clear(session.Snapshot.Profile.Devices.Single(x => x.LogicalId == Source(session)).Buttons[0].CommandId, "Primary");
            if (failure == "foreign") session = Session(session.Snapshot, issuer.Policy, observed);
            if (failure == "disconnect") observed = [];
            if (failure == "product") observed = [device with { ProductGuid = Guid.NewGuid() }];
            if (failure == "inputs") keys = ["Joy_1"];
            var before = JsonSerializer.Serialize(session.Snapshot);
            Assert.Throws<InvalidOperationException>(() => session.ApplyControllerChange(plan));
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
        }
        /// <summary>Identical replacement units require an explicit captured native index, never a guessed Windows ordinal.</summary>
        [Fact]
        public void Indexed_replacement_uses_the_explicit_reference_and_retains_other_assignments()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var first = Replacement(); var second = first with { PhysicalGuid = Guid.NewGuid() };
            var source = Native.Replace("<FutureSetting", "<LandingGearToggle><Primary Device='044F1001' DeviceIndex='0' Key='Joy_4'/><Secondary Device='044F1001' DeviceIndex='1' Key='Joy_5'/></LandingGearToggle><FutureSetting", StringComparison.Ordinal);
            var session = Session(Workspace(source), issuer.Policy, [first, second]);
            Assert.Throws<InvalidOperationException>(() => session.PreviewReplaceController(Source(session), first.PhysicalGuid, () => ["Joy_1"]));
            var id = session.Snapshot.Profile.Devices.Single(x => x.Identity!.SimSpecificIds.GetValueOrDefault("ed.deviceIndex") == "1").LogicalId;
            var plan = session.PreviewReplaceController(Source(session), first.PhysicalGuid, () => ["Joy_1", "Joy_2", "Joy_XAxis"], id);
            session.ApplyControllerChange(plan);
            var xml = XDocument.Parse(session.Export().Files["Lifecycle.4.2.binds"]);
            Assert.Equal("1", (string?)xml.Root!.Element("PrimaryFire")!.Element("Primary")!.Attribute("DeviceIndex"));
            Assert.Equal("1", (string?)xml.Root.Element("PrimaryFire")!.Element("Secondary")!.Element("Modifier")!.Attribute("DeviceIndex"));
            Assert.Equal("0", (string?)xml.Root.Element("LandingGearToggle")!.Element("Primary")!.Attribute("DeviceIndex"));
        }
        /// <summary>System inputs, opaque native references and unverified native extensions block destructive changes.</summary>
        [Theory]
        [InlineData("system")]
        [InlineData("opaque")]
        [InlineData("slot")]
        public void Unrepresentable_edits_are_rejected(string kind)
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var native = kind == "opaque" ? Native.Replace("Value=\"preserve\"", "Value=\"preserve\" Device=\"06A30BAC\"", StringComparison.Ordinal)
                : kind == "slot" ? Native.Replace("Key=\"Joy_1\"", "Key=\"Joy_1\" Future=\"keep\"", StringComparison.Ordinal) : Native;
            var session = Session(Workspace(native), issuer.Policy, []);
            var id = kind == "system" ? session.Snapshot.Profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "Keyboard").LogicalId : Source(session);
            var before = JsonSerializer.Serialize(session.Snapshot);
            Assert.Throws<InvalidOperationException>(() => session.PreviewRemoveController(id));
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
        }
        /// <summary>Removing an unsupported reference cannot grant access to another unsupported controller; replacement probing remains Owner-only.</summary>
        [Fact]
        public void Edition_boundaries_survive_cleanup_and_replacement()
        {
            var device = Replacement(); var session = Session(Workspace(), new(), [device]);
            Assert.Throws<InvalidOperationException>(() => session.PreviewReplaceController(Source(session), device.PhysicalGuid, () => ["Joy_1"]));
            session.ApplyControllerChange(session.PreviewRemoveController(Source(session)));
            Assert.Empty(session.AccessMessage); Assert.True(session.Export().Succeeded);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DetectedDcsDevice Replacement() => new(Guid.NewGuid(), "Replacement", true, ProductGuid: Guid.Parse("1001044f-0000-0000-0000-504944564944"));
        private static GameWorkspace Workspace(string native = Native) => new(1, Guid.NewGuid(), "Lifecycle", new EliteDangerousProfileBackend().Import("controls",
            new Dictionary<string, string> { ["Lifecycle.4.2.binds"] = native, ["StartPreset.4.start"] = "Lifecycle\nLifecycle\nLifecycle\nLifecycle\n" }), new());
        private static GameProfileSession Session(GameWorkspace workspace, EditionPolicy policy, IReadOnlyList<DetectedDcsDevice> observed) =>
            new(new EliteDangerousProfileBackend(), workspace, policy, [], () => observed, new EliteDeviceReferenceMatcher());
        private static string Source(GameProfileSession session) => session.Snapshot.Profile.Devices.Single(x => x.Identity!.SimSpecificIds["ed.device"] == "06A30BAC").LogicalId;
        #endregion END private helpers
    }
}
