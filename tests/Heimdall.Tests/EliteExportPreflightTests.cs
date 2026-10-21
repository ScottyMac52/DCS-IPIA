using System.Text.Json;
using System.Xml.Linq;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.EliteDangerous;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Reproduces missing yaw hardware, duplicate settings and lost preset names without storing user profiles.</summary>
    public sealed class EliteExportPreflightTests
    {
        private const string Native = "<Root PresetName='Custom' MajorVersion='4' MinorVersion='2'><MouseGUI Value='0'/><YawAxisRaw><Binding Device='044FB68F' Key='Joy_ZAxis'/><Inverted Value='0'/><Deadzone Value='0.00000000'/></YawAxisRaw><PrimaryFire><Primary Device='Keyboard' Key='Key_Space'><Modifier Device='044FB68F' Key='Joy_1'/></Primary><Secondary Device='{NoDevice}' Key=''/></PrimaryFire><MouseGUI Value='0'/></Root>";
        #region BEGIN Public
        /// <summary>Offline authoring works; game export identifies every affected slot and blocks missing hardware atomically.</summary>
        [Fact]
        public void Missing_controller_reports_yaw_and_modifier_without_exporting_files()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var session = Session(issuer.Policy, []); var before = JsonSerializer.Serialize(session.Snapshot);
            Assert.True(session.Export().Succeeded);
            var issue = Assert.Single(session.ReviewGameExport());
            Assert.Equal("044FB68F", issue.Reference); Assert.True(issue.CanRepair);
            Assert.Contains(issue.Assignments, x => x.Contains("Yaw Axis Raw", StringComparison.Ordinal) && x.Contains("Joy_ZAxis", StringComparison.Ordinal));
            Assert.Contains(issue.Assignments, x => x.Contains("Modifier", StringComparison.Ordinal));
            Assert.Throws<InvalidOperationException>(() => session.ExportForGame());
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
            session.ApplyControllerChange(session.PreviewRemoveController(issue.DeviceId));
            Assert.Empty(session.ReviewGameExport());
            var plan = session.ExportForGame(); Assert.True(plan.Succeeded);
            Assert.DoesNotContain("044FB68F", plan.Files.Values.Single());
        }
        /// <summary>Named output aligns native name, filename and matching selector lines, while retaining original evidence.</summary>
        [Fact]
        public void Named_export_normalizes_identical_settings_and_preserves_other_selector_choices()
        {
            var backend = new EliteDangerousProfileBackend();
            var files = new Dictionary<string, string> { ["Custom.4.2.binds"] = Native, ["StartPreset.4.start"] = "Custom\r\nOther\r\nCustom\r\nOther\r\n" };
            var profile = backend.Import("controls", files); var before = JsonSerializer.Serialize(profile);
            var plan = backend.ExportNamed(profile, "HEIMDALL & Vyper");
            Assert.True(plan.Succeeded); Assert.Equal(2, plan.Files.Count);
            var xml = XDocument.Parse(plan.Files["HEIMDALL & Vyper.4.2.binds"]);
            Assert.Equal("HEIMDALL & Vyper", (string?)xml.Root!.Attribute("PresetName")); Assert.Single(xml.Root.Elements("MouseGUI"));
            Assert.Equal("HEIMDALL & Vyper\r\nOther\r\nHEIMDALL & Vyper\r\nOther\r\n", plan.Files["StartPreset.4.start"]);
            Assert.Contains(plan.Diagnostics, x => x.Code == "ed.settings.normalized" && !x.IsError);
            Assert.Equal(before, JsonSerializer.Serialize(profile)); Assert.Equal(Native, backend.Export(profile).Files["Custom.4.2.binds"]);
            Assert.Equal("HEIMDALL & Vyper", backend.Import("controls", plan.Files).Name);
        }
        /// <summary>Conflicting duplicate settings are rejected rather than choosing an arbitrary value.</summary>
        [Theory]
        [InlineData("<MouseGUI Value='1'/>")]
        [InlineData("<MouseGUI Value='0' Extra='unknown'/>")]
        public void Conflicting_settings_are_blocked(string setting)
        {
            var position = Native.LastIndexOf("<MouseGUI", StringComparison.Ordinal);
            var source = Native[..position] + setting + "</Root>";
            var error = Assert.Throws<InvalidOperationException>(() => new EliteDangerousProfileBackend().Import("controls", new Dictionary<string, string> { ["Custom.4.2.binds"] = source }));
            Assert.Contains("MouseGUI", error.Message);
        }
        /// <summary>Unknown structured duplicates are reported, rather than silently retaining the first or dropping native data.</summary>
        [Fact]
        public void Structured_duplicate_settings_require_source_repair()
        {
            var native = Native.Replace("</Root>", "<Future><Value Key='one'/></Future><Future><Value Key='one'/></Future></Root>", StringComparison.Ordinal);
            var error = Assert.Throws<InvalidOperationException>(() => new EliteDangerousProfileBackend().Import("controls", new Dictionary<string, string> { ["Custom.4.2.binds"] = native }));
            Assert.Contains("Future", error.Message);
        }
        /// <summary>Unsafe or reserved native filenames fail with no output instead of being silently renamed.</summary>
        [Theory]
        [InlineData("")]
        [InlineData("../Custom")]
        [InlineData("Name.")]
        [InlineData("CON")]
        [InlineData("Custom:Flight")]
        [InlineData("Name\nOther")]
        public void Invalid_preset_names_produce_no_files(string name)
        {
            var backend = new EliteDangerousProfileBackend();
            var plan = backend.ExportNamed(backend.Import("controls", new Dictionary<string, string> { ["Custom.4.2.binds"] = Native }), name);
            Assert.False(plan.Succeeded); Assert.Empty(plan.Files);
        }
        /// <summary>System inputs bypass physical matching; unique HID references resolve without requiring artwork.</summary>
        [Fact]
        public void Connected_product_is_checked_fresh_at_each_export()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var device = Device();
            IReadOnlyList<DetectedDcsDevice> observed = [device];
            var session = new GameProfileSession(new EliteDangerousProfileBackend(), Workspace(), issuer.Policy, [], () => observed, new EliteDeviceReferenceMatcher());
            Assert.Empty(session.ReviewGameExport()); Assert.True(session.ExportForGame().Succeeded);
            observed = [device with { Connected = false }]; Assert.Single(session.ReviewGameExport());
            Assert.Throws<InvalidOperationException>(() => session.ExportForGame());
        }
        /// <summary>Repeated products require explicit indexed instance links; a missing or changed pin cannot be guessed.</summary>
        [Fact]
        public void Identical_products_require_index_and_exact_instance_evidence()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var first = Device(); var second = first with { PhysicalGuid = Guid.NewGuid() };
            var session = Session(issuer.Policy, [first, second]); Assert.Single(session.ReviewGameExport());
            var workspace = Workspace(Native.Replace("Device='044FB68F'", "Device='044FB68F' DeviceIndex='0'", StringComparison.Ordinal));
            session = new(new EliteDangerousProfileBackend(), workspace, issuer.Policy, [], () => [first, second], new EliteDeviceReferenceMatcher());
            Assert.Single(session.ReviewGameExport()); session.ConnectController(first.PhysicalGuid);
            Assert.Empty(session.ReviewGameExport()); Assert.True(session.ExportForGame().Succeeded);
            var pinned = session.Snapshot;
            var changed = first with { ProductGuid = Guid.NewGuid() };
            session = new(new EliteDangerousProfileBackend(), pinned, issuer.Policy, [], () => [changed, second], new EliteDeviceReferenceMatcher());
            Assert.Single(session.ReviewGameExport());
        }
        /// <summary>Unknown native device fields are also scanned and cannot be silently cleared through the portable projection.</summary>
        [Fact]
        public void Opaque_native_references_are_visible_but_require_native_repair()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var workspace = Workspace(Native.Replace("</Root>", "<Future Device='UnknownNative' Key='Joy_7'/></Root>", StringComparison.Ordinal));
            var session = new GameProfileSession(new EliteDangerousProfileBackend(), workspace, issuer.Policy, [], () => [Device()], new EliteDeviceReferenceMatcher());
            var issue = Assert.Single(session.ReviewGameExport()); Assert.Equal("UnknownNative", issue.Reference); Assert.False(issue.CanRepair);
            Assert.Throws<InvalidOperationException>(() => session.ExportForGame());
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DetectedDcsDevice Device() => new(Guid.NewGuid(), "Yaw controller", true, ProductGuid: Guid.Parse("b68f044f-0000-0000-0000-504944564944"));
        private static GameWorkspace Workspace(string source = Native) => new(1, Guid.NewGuid(), "Vyper Flight", new EliteDangerousProfileBackend().Import("controls", new Dictionary<string, string> { ["Custom.4.2.binds"] = source }), new());
        private static GameProfileSession Session(EditionPolicy policy, IReadOnlyList<DetectedDcsDevice> observed) => new(new EliteDangerousProfileBackend(), Workspace(), policy, [], () => observed, new EliteDeviceReferenceMatcher());
        #endregion END private helpers
    }
}
