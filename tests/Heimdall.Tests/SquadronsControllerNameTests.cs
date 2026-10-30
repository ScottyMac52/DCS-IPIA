using System.Text.Json;
using System.Text.RegularExpressions;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.EliteDangerous;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Regresses native occurrence names and repair of device declarations scattered below bindings.</summary>
    public sealed class SquadronsControllerNameTests
    {
        private const string Slot = "squadrons.joystick.2";
        #region BEGIN Public
        /// <summary>Occurrence numbers are separate from the reported product/model name and the selected native slot.</summary>
        [Theory]
        [InlineData("Viper TQS", "Viper TQS 1")]
        [InlineData("F16 MFD 1", "F16 MFD 1 1")]
        [InlineData("AVA [R] Sol-R", "AVA [R] Sol-R 1")]
        public void Fresh_names_receive_the_first_occurrence_suffix(string reported, string expected)
        {
            var backend = new SquadronsProfileBackend();
            var profile = Import(backend, "GstKeyBinding.InputDataVersion 4\nFuture.Setting retained\n");
            Assert.Equal(expected, backend.SuggestNativeControllerName(profile, reported));
            var added = backend.AddNativeController(profile, expected);
            Assert.StartsWith("GstInput.JoystickDevice1 " + expected + "\n", backend.Export(added).Files[SquadronsProfileBackend.FileName], StringComparison.Ordinal);
            Assert.Equal("GstKeyBinding.InputDataVersion 4\nFuture.Setting retained\n", backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Existing native occurrences remain stable; a new identical name receives the next free occurrence.</summary>
        [Fact]
        public void Existing_names_are_preserved_and_new_occurrences_avoid_duplicates()
        {
            var backend = new SquadronsProfileBackend();
            var profile = Import(backend, "GstInput.JoystickDevice1 Viper TQS 1\nGstInput.JoystickDevice2 Viper TQS 2\n");
            Assert.Equal("Viper TQS 3", backend.SuggestNativeControllerName(profile, "Viper TQS"));
            Assert.Equal("Viper TQS 2", backend.SuggestNativeControllerName(profile, "Viper TQS", Slot));
            Assert.Equal("Viper TQS 2", backend.SuggestNativeControllerName(profile, "Different Windows alias", Slot));
            Assert.Throws<ArgumentException>(() => backend.SuggestNativeControllerName(profile, " "));
            Assert.Throws<InvalidOperationException>(() => backend.SuggestNativeControllerName(profile, "Viper TQS", "missing"));
        }
        /// <summary>A bare name saved by the prior build can be corrected without changing physical ownership or either axis direction.</summary>
        [Theory]
        [InlineData("\n")]
        [InlineData("\r\n")]
        public void Reviewed_repair_groups_native_names_and_preserves_all_other_bytes(string newline)
        {
            using var issuer = new OwnerLicenseTestIssuer(); var backend = new SquadronsProfileBackend();
            var source = Scattered(newline); var profile = Import(backend, source);
            var device = new DetectedDcsDevice(Guid.NewGuid(), "Viper TQS", true, ProductGuid: Guid.NewGuid());
            var workspace = new GameWorkspace(1, Guid.NewGuid(), "Repair", profile, new() { [Slot] = new(null, device) });
            var session = new GameProfileSession(backend, workspace, issuer.Policy, [], () => []);
            var beforePins = JsonSerializer.Serialize(session.Snapshot.Hardware);
            var beforeRows = JsonSerializer.Serialize(backend.ReadBindings(profile));
            Assert.True(session.CanRenameNativeControllers);
            var proposal = backend.SuggestNativeControllerName(profile, device.Name, Slot);
            Assert.Equal("Viper TQS 1", proposal);
            var review = session.PreviewRenameNativeController(Slot, proposal);
            Assert.False(session.HasChanges);
            Assert.Equal(source, session.Export().Files[SquadronsProfileBackend.FileName]);
            var notifications = 0; session.Changed += (_, _) => notifications++;
            session.ApplyControllerChange(review); Assert.Equal(1, notifications);
            var text = session.Export().Files[SquadronsProfileBackend.FileName];
            Assert.Equal(proposal, session.Snapshot.Profile.Devices.Single(x => x.LogicalId == Slot).Identity!.WindowsName);
            Assert.Equal(beforePins, JsonSerializer.Serialize(session.Snapshot.Hardware));
            Assert.Equal(beforeRows, JsonSerializer.Serialize(backend.ReadBindings(session.Snapshot.Profile)));
            Assert.Equal(WithoutDevices(source), WithoutDevices(text));
            AssertGrouped(text);
            var root = Path.Combine(Path.GetTempPath(), "squadrons-name-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new FileGameWorkspaceStore(root); store.Save(session.Snapshot, null);
                var reopened = store.Load(workspace.Id).Workspace;
                Assert.Equal(text, backend.Export(reopened.Profile).Files[SquadronsProfileBackend.FileName]);
                Assert.Equal(beforePins, JsonSerializer.Serialize(reopened.Hardware));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
            session.Discard(); Assert.Equal(source, session.Export().Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Adding another controller and renumbering native slots both maintain the grouped device block.</summary>
        [Fact]
        public void Add_and_reorder_group_previously_scattered_declarations()
        {
            var backend = new SquadronsProfileBackend(); var source = Scattered("\n");
            var profile = Import(backend, source);
            var added = backend.AddNativeController(profile, "New stick 1");
            AssertGrouped(backend.Export(added).Files[SquadronsProfileBackend.FileName]);
            var reordered = backend.ReorderCapturedSlots(added, ["squadrons.joystick.3", Slot, "squadrons.joystick.1"]);
            AssertGrouped(backend.Export(reordered).Files[SquadronsProfileBackend.FileName]);
            Assert.Equal(2, backend.ReadBindings(reordered).Count);
            Assert.All(backend.ReadBindings(reordered), row => Assert.Equal(1, row.DeviceId));
        }
        /// <summary>An already-correct name can still explicitly repair a scattered declaration block.</summary>
        [Fact]
        public void Updating_the_same_name_repairs_layout_without_reassigning_controls()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var backend = new SquadronsProfileBackend();
            var source = Scattered("\n").Replace("JoystickDevice2 Viper TQS\n", "JoystickDevice2 Viper TQS 1\n", StringComparison.Ordinal);
            var session = new GameProfileSession(backend, new(1, Guid.NewGuid(), "Layout", Import(backend, source), new()), issuer.Policy, [], () => []);
            var rows = JsonSerializer.Serialize(backend.ReadBindings(session.Snapshot.Profile));
            session.ApplyControllerChange(session.PreviewRenameNativeController(Slot, "Viper TQS 1"));
            AssertGrouped(session.Export().Files[SquadronsProfileBackend.FileName]);
            Assert.Equal(rows, JsonSerializer.Serialize(backend.ReadBindings(session.Snapshot.Profile)));
            Assert.Equal(WithoutDevices(source), WithoutDevices(session.Export().Files[SquadronsProfileBackend.FileName]));
        }
        /// <summary>Device-only settings and trailing declarations without a newline remain well separated after grouping.</summary>
        [Theory]
        [InlineData("GstInput.JoystickDevice2 Viper TQS")]
        [InlineData("GstKeyBinding.InputDataVersion 4\r\nGstInput.JoystickDevice2 Viper TQS")]
        public void Repair_handles_a_final_declaration_without_line_ending(string source)
        {
            var backend = new SquadronsProfileBackend(); var profile = Import(backend, source);
            var renamed = backend.RenameNativeController(profile, Slot, "Viper TQS 1");
            var text = backend.Export(renamed).Files[SquadronsProfileBackend.FileName];
            Assert.Contains("GstInput.JoystickDevice2 Viper TQS 1", text, StringComparison.Ordinal);
            Assert.Equal(WithoutDevices(source), WithoutDevices(text));
        }
        /// <summary>Names and session revision checks reject invalid repair attempts before any mutation.</summary>
        [Fact]
        public void Duplicate_missing_and_stale_name_repairs_are_rejected()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var backend = new SquadronsProfileBackend();
            var source = Scattered("\n"); var profile = Import(backend, source);
            Assert.Throws<InvalidOperationException>(() => backend.RenameNativeController(profile, Slot, "Existing 1"));
            Assert.Throws<InvalidOperationException>(() => backend.RenameNativeController(profile, "missing", "TQS 1"));
            Assert.Throws<InvalidOperationException>(() => backend.RenameNativeController(profile, "squadrons.joystick.10", "TQS 1"));
            var session = new GameProfileSession(backend, new(1, Guid.NewGuid(), "Names", profile, new()), issuer.Policy, [], () => []);
            var review = session.PreviewRenameNativeController(Slot, "Viper TQS 1");
            session.Rename("Changed"); var before = JsonSerializer.Serialize(session.Snapshot);
            Assert.Throws<InvalidOperationException>(() => session.ApplyControllerChange(review));
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot));
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Name editing remains optional and cannot bypass simulator entitlement.</summary>
        [Fact]
        public void Free_and_unsupported_adapters_cannot_rename_native_slots()
        {
            var backend = new SquadronsProfileBackend(); var profile = Import(backend, Scattered("\n"));
            var free = new GameProfileSession(backend, new(1, Guid.NewGuid(), "Free", profile, new()), new(), [], () => []);
            Assert.Throws<InvalidOperationException>(() => free.PreviewRenameNativeController(Slot, "Viper TQS 1"));
            using var issuer = new OwnerLicenseTestIssuer(); var elite = new EliteDangerousProfileBackend();
            var ed = elite.Import("controls", new Dictionary<string, string> { ["controls.binds"] = "<Root PresetName=\"Test\" MajorVersion=\"4\" MinorVersion=\"0\" />" });
            var session = new GameProfileSession(elite, new(1, Guid.NewGuid(), "ED", ed, new()), issuer.Policy, [], () => []);
            Assert.False(session.CanRenameNativeControllers);
            Assert.Throws<InvalidOperationException>(() => session.PreviewRenameNativeController("missing", "Name 1"));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static Heimdall.Domain.MultiSim.ProfileDocument Import(SquadronsProfileBackend backend, string source) =>
            backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
        private static string Scattered(string newline)
        {
            var lines = new List<string> { "GstInput.DeadzoneCenter 0.220000", "GstInput.JoystickDevice1 Existing 1", "GstInput.JoystickDevice10 ", "Future.Setting  keep  exactly", "GstKeyBinding.InputDataVersion 4" };
            foreach (var (index, button, identifier, negate) in new[] { (4, 41, -453871199, 0), (5, 40, 973687430, 1) })
            {
                var fields = new Dictionary<string, int> { ["altbutton"] = 0, ["axis"] = 26, ["button"] = button, ["deviceid"] = 1, ["identifier"] = identifier, ["modifier"] = 0, ["negate"] = negate, ["type"] = 3 };
                lines.AddRange(fields.Select(x => "GstKeyBinding.IncomStarshipInputConcepts.ConceptThrottle." + index + "." + x.Key + " " + x.Value));
            }
            lines.Add("GstInput.JoystickDevice2 Viper TQS");
            return string.Join(newline, lines) + newline;
        }
        private static string WithoutDevices(string text) => Regex.Replace(text, @"(?m)^GstInput\.JoystickDevice\d+ [^\n]*(?:\n|$)", "");
        private static void AssertGrouped(string text)
        {
            var lines = text.Split('\n');
            var rows = lines.Select((line, index) => (line, index)).Where(x => x.line.StartsWith("GstInput.JoystickDevice", StringComparison.Ordinal)).ToArray();
            Assert.Equal(rows.Length, rows[^1].index - rows[0].index + 1);
            var keys = rows.Select(x => x.line[..x.line.IndexOf(' ')]).ToArray();
            Assert.Equal(keys.Order(StringComparer.Ordinal), keys);
            Assert.True(rows[^1].index < Array.FindIndex(lines, line => line.StartsWith("GstKeyBinding.", StringComparison.Ordinal)));
        }
        #endregion END private helpers
    }
}
