using System.Text.Json;
using Heimdall.Domain.MultiSim;
using Heimdall.Squadrons;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects native Squadrons evidence, extended axis decoding and explicit device-slot edits.</summary>
    public sealed class SquadronsProfileBackendTests
    {
        #region BEGIN Public
        /// <summary>The native button field also contains axis directions, independent of physical button events.</summary>
        [Theory]
        [InlineData(26, 40, "JOY_Z", CommandType.Axis, 1)]
        [InlineData(26, 41, "JOY_Z", CommandType.Axis, -1)]
        [InlineData(8, 174, "JOY_X", CommandType.Axis, 1)]
        [InlineData(11, 174, "JOY_Y", CommandType.Axis, 1)]
        [InlineData(26, 22, "JOY_BTN1", CommandType.Button, 0)]
        [InlineData(26, 64, "JOY_BTN19", CommandType.Button, 0)]
        [InlineData(26, 173, "JOY_BTN128", CommandType.Button, 0)]
        [InlineData(26, 48, "JOY_POV1_U", CommandType.Hat, 0)]
        public void Decodes_native_input_without_treating_encoded_axes_as_buttons(int axis, int button, string key, CommandType type, int direction) =>
            Assert.Equal(new SquadronsInput(key, type, direction), SquadronsInputCodec.Decode(axis, button));
        /// <summary>Every supported input encoding has an exact inverse, including sliders and four hats.</summary>
        [Fact]
        public void Every_supported_input_round_trips()
        {
            foreach (var axis in new[] { 8, 9, 10, 11 })
            {
                var input = SquadronsInputCodec.Decode(axis, 174)!;
                Assert.Equal((axis, 174), SquadronsInputCodec.Encode(input));
            }
            foreach (var button in Enumerable.Range(14, 4).Concat(Enumerable.Range(22, 152)))
            {
                var input = SquadronsInputCodec.Decode(26, button)!;
                Assert.Equal((26, button), SquadronsInputCodec.Encode(input));
            }
            Assert.Null(SquadronsInputCodec.Decode(26, 174));
            Assert.Null(SquadronsInputCodec.Decode(99, 22));
            Assert.Null(SquadronsInputCodec.Decode(26, 18));
            Assert.Throws<InvalidOperationException>(() => SquadronsInputCodec.Encode(new("unknown", CommandType.Axis, 1)));
            Assert.Throws<ArgumentNullException>(() => SquadronsInputCodec.Encode(null!));
        }
        /// <summary>Unedited files retain line endings, blank lines, whitespace, unknown records and unassigned devices.</summary>
        [Theory]
        [InlineData("\n")][InlineData("\r\n")]
        public void Import_export_is_exact_and_native_evidence_survives_json(string newline)
        {
            var backend = new SquadronsProfileBackend(); var text = Source().Replace("\n", newline, StringComparison.Ordinal);
            var profile = Import(backend, text);
            profile = JsonSerializer.Deserialize<ProfileDocument>(JsonSerializer.Serialize(profile))!;
            var plan = backend.Export(profile);
            Assert.True(plan.Succeeded); Assert.Equal(text, plan.Files[SquadronsProfileBackend.FileName]);
            Assert.Equal("star-wars-squadrons", profile.BackendId);
            Assert.Equal(3, profile.Devices.Count);
            Assert.Equal(2, profile.Devices.Count(x => x.Identity!.WindowsName == "Viper TQS 1"));
            var axis = Assert.Single(profile.Devices.Single(x => x.LogicalId == "squadrons.joystick.5").Axes);
            Assert.Equal("JOY_Z", axis.SourceAxis); Assert.Equal("-1", axis.SimSpecific["squadrons.direction"].ToString());
            var rows = backend.ReadBindings(profile);
            Assert.Equal(5, rows.Count); Assert.Contains(rows, x => x.Type == 2 && x.Input is null);
            Assert.Contains(rows, x => x.Type == 3 && x.Input is null);
            var catalog = backend.GetCatalog(profile);
            Assert.Equal("star-wars-squadrons", catalog.SimId);
            Assert.Equal("Flight", catalog.Find("controls/IncomStarshipInputConcepts.ConceptThrottle")!.Category);
            Assert.Null(catalog.Find("missing"));
            Assert.Equal(CommandType.Axis, catalog.Find(axis.CommandId)!.Type);
            Assert.Equal("Throttle", catalog.Find(axis.CommandId)!.DisplayName);
            Assert.False(backend.Capabilities.DeploysToGame); Assert.False(backend.Capabilities.ModifierChords);
            Assert.Equal([BindingMode.Press], backend.Capabilities.ButtonModes);
        }
        /// <summary>Existing records can move between captured slots without rewriting another device or recalculating identifiers.</summary>
        [Fact]
        public void Rebinding_preserves_siblings_and_requires_explicit_slot()
        {
            var backend = new SquadronsProfileBackend(); var profile = Import(backend, Source());
            var before = backend.Export(profile).Files[SquadronsProfileBackend.FileName];
            var record = "IncomStarshipInputConcepts.ConceptThrottle.4";
            var edited = backend.RebindJoystick(profile, record, 10, new("JOY_Z", CommandType.Axis, 1));
            var result = backend.ReadBindings(edited).Single(x => x.RecordId == record);
            Assert.Equal(9, result.DeviceId); Assert.Equal(new("JOY_Z", CommandType.Axis, 1), result.Input);
            var output = backend.Export(edited).Files[SquadronsProfileBackend.FileName];
            Assert.Equal(before.Replace(record + ".deviceid 4", record + ".deviceid 9", StringComparison.Ordinal)
                .Replace(record + ".button 41", record + ".button 40", StringComparison.Ordinal), output);
            Assert.Equal(before, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
            Assert.Equal("-453871199", result.Fields["identifier"].ToString(System.Globalization.CultureInfo.InvariantCulture));
            var digital = backend.RebindJoystick(edited, record, 3, new("JOY_BTN3", CommandType.Button, 0));
            Assert.Equal(24, backend.ReadBindings(digital).Single(x => x.RecordId == record).Fields["button"]);
            Assert.Throws<InvalidOperationException>(() => backend.RebindJoystick(profile, record, 2, new("JOY_BTN1", CommandType.Button, 0)));
            Assert.Throws<InvalidOperationException>(() => backend.RebindJoystick(profile, "missing", 3, new("JOY_BTN1", CommandType.Button, 0)));
            Assert.Throws<InvalidOperationException>(() => backend.RebindJoystick(profile, "IncomDefaultInputConcepts.ConceptActivate.0", 3, new("JOY_BTN1", CommandType.Button, 0)));
            Assert.Throws<InvalidOperationException>(() => backend.RebindJoystick(Import(backend, Source().Replace(".modifier 0", ".modifier 29", StringComparison.Ordinal)), record, 3, new("JOY_BTN1", CommandType.Button, 0)));
        }
        /// <summary>Unknown numeric record extensions survive unchanged exports and block edits to their unverified semantics.</summary>
        [Fact]
        public void Unknown_extensions_are_preserved_and_not_rewritten()
        {
            var backend = new SquadronsProfileBackend();
            var source = Source() + "GstKeyBinding.IncomStarshipInputConcepts.ConceptThrottle.4.future 7\n";
            var profile = Import(backend, source);
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
            Assert.Throws<InvalidOperationException>(() => backend.RebindJoystick(profile,
                "IncomStarshipInputConcepts.ConceptThrottle.4", 3, new("JOY_Z", CommandType.Axis, 1)));
        }
        /// <summary>Malformed or ambiguous native evidence is rejected before any output can be produced.</summary>
        [Theory]
        [InlineData("version")][InlineData("duplicate")][InlineData("missing")][InlineData("integer")]
        [InlineData("index")][InlineData("record")][InlineData("line")][InlineData("empty")]
        public void Rejects_malformed_sources(string failure)
        {
            var source = failure switch
            {
                "version" => Source().Replace("InputDataVersion 4", "InputDataVersion 99", StringComparison.Ordinal),
                "duplicate" => Source() + "GstInput.JoystickDevice5 Other\n",
                "missing" => Source().Replace("GstKeyBinding.IncomStarshipInputConcepts.ConceptThrottle.4.type 3\n", "", StringComparison.Ordinal),
                "integer" => Source().Replace(".deviceid 4", ".deviceid nope", StringComparison.Ordinal),
                "index" => Source().Replace("ConceptThrottle.4.", "ConceptThrottle.-1.", StringComparison.Ordinal),
                "record" => Source() + "GstKeyBinding.broken 1\n",
                "line" => Source() + "broken\n", _ => ""
            };
            Assert.Throws<InvalidOperationException>(() => Import(new(), source));
        }
        /// <summary>Portable edits and foreign evidence are blocked rather than silently ignored by the starter backend.</summary>
        [Theory]
        [InlineData("backend")][InlineData("version")][InlineData("evidence")][InlineData("device")][InlineData("axis")][InlineData("null")]
        public void Export_blocks_unsupported_mutations(string change)
        {
            var backend = new SquadronsProfileBackend(); var profile = Import(backend, Source());
            switch (change)
            {
                case "backend": profile.BackendId = "elite-dangerous"; break;
                case "version": profile.SchemaVersion = 2; break;
                case "evidence": profile.NativeData.Clear(); break;
                case "device": profile.Devices[0].Identity!.WindowsName = "Another"; break;
                case "axis": profile.Devices.Single(x => x.LogicalId == "squadrons.joystick.5").Axes[0].Deadzone = 10; break;
                default: profile = null!; break;
            }
            var plan = backend.Export(profile); Assert.False(plan.Succeeded); Assert.Empty(plan.Files);
        }
        /// <summary>Only explicit instance identity can resolve a captured device; names and saved slot numbers are insufficient.</summary>
        [Fact]
        public void Device_resolution_never_guesses_and_source_contract_is_bounded()
        {
            var backend = new SquadronsProfileBackend(); var identity = new PhysicalDeviceIdentity { WindowsName = "Viper TQS 1" };
            Assert.Equal("unresolved", backend.ResolveDevice(identity, [identity]).Status);
            identity.InstanceId = "instance";
            Assert.Equal("disconnected", backend.ResolveDevice(identity, []).Status);
            Assert.Equal("resolved", backend.ResolveDevice(identity, [new() { InstanceId = "INSTANCE" }]).Status);
            Assert.Equal("ambiguous", backend.ResolveDevice(identity, [identity, identity]).Status);
            Assert.Throws<ArgumentNullException>(() => backend.ResolveDevice(null!, []));
            Assert.Throws<ArgumentNullException>(() => backend.ResolveDevice(identity, null!));
            Assert.Throws<ArgumentException>(() => backend.Import(" ", new Dictionary<string, string>()));
            Assert.Throws<ArgumentNullException>(() => backend.Import("controls", null!));
            Assert.Throws<InvalidOperationException>(() => backend.Import("controls", new Dictionary<string, string> { ["../bad"] = Source() }));
            Assert.Throws<InvalidOperationException>(() => Import(backend, new string('x', 4 * 1024 * 1024 + 1)));
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => backend.Import("controls", new Dictionary<string, string>(), cancellation.Token));
            Assert.Throws<OperationCanceledException>(() => backend.Export(Import(backend, Source()), cancellation.Token));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static ProfileDocument Import(SquadronsProfileBackend backend, string source) => backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
        private static string Source()
        {
            var lines = new List<string> { "GstKeyBinding.InputDataVersion 4", "GstInput.JoystickDevice3 Example Stick 1", "GstInput.JoystickDevice5 Viper TQS 1", "GstInput.JoystickDevice10 Viper TQS 1", "GstInput.JoystickDevice2 ", "Future.Setting keep me", "" };
            Add(lines, "IncomStarshipInputConcepts.ConceptThrottle.4", 3, 4, 26, 41);
            Add(lines, "IncomStarshipInputConcepts.ConceptFire.2", 3, 2, 26, 22);
            Add(lines, "IncomDefaultInputConcepts.ConceptActivate.0", 2, -1, 0, 57);
            Add(lines, "IncomSoldierInputConcepts.ConceptInteract.1", 3, 2, 26, 48);
            Add(lines, "FutureInputConcepts.ConceptUnknown.0", 3, 2, 999, 999);
            return string.Join("\n", lines) + "\n";
        }
        private static void Add(List<string> lines, string id, int type, int device, int axis, int button)
        {
            foreach (var (key, value) in new Dictionary<string, int> { ["altbutton"] = 0, ["axis"] = axis, ["button"] = button, ["deviceid"] = device, ["identifier"] = -453871199, ["modifier"] = 0, ["negate"] = 0, ["type"] = type })
                lines.Add("GstKeyBinding." + id + "." + key + " " + value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        #endregion END private helpers
    }
}
