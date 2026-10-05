using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Xunit;
using Dcs.Ipia.Infrastructure;

namespace Dcs.Ipia.Tests
{
    /// <summary>Tests physical transitions against generated-profile bindings without hardware.</summary>
    public sealed class InputTestSessionTests
    {
        private static readonly Guid Stick = Guid.Parse("10000000-0000-0000-0000-000000000001");
        private static readonly Guid Throttle = Guid.Parse("10000000-0000-0000-0000-000000000002");
        #region BEGIN Public
        /// <summary>Cross-device Hold chords replace base, with release retaining the pressed command.</summary>
        [Fact]
        public void ResolvesCrossDeviceChordsAndRelease()
        {
            var session = Session();
            Assert.Empty(session.Process([Frame(Stick, 0), Frame(Throttle, 0)]));
            var press = session.Process([Frame(Stick, 1), Frame(Throttle, 1)]);
            Assert.Equal("SHIFTED", Assert.Single(press.Single(x => x.PhysicalGuid == Stick).Matches).Binding.CommandId);
            Assert.Equal("S3", session.ActiveModifiers[AssignmentLayer.Module]);
            var release = session.Process([Frame(Stick, 0), Frame(Throttle, 0)]).Single(x => x.PhysicalGuid == Stick);
            Assert.Equal("SHIFTED", Assert.Single(release.Matches).Binding.CommandId);
            Assert.Equal("Release", release.Transition);
            Assert.Equal("BASE", Assert.Single(session.Process([Frame(Stick, 1), Frame(Throttle, 0)]).Single().Matches).Binding.CommandId);
        }
        /// <summary>Toggle modifiers react only to new rising edges and reset with session state.</summary>
        [Fact]
        public void ToggleDoesNotLatchOnInitialHeldInputOrRepeat()
        {
            var session = Session(ModifierMode.Toggle);
            session.Process([Frame(Stick, 0), Frame(Throttle, 1)]);
            Assert.Equal("", session.ActiveModifiers[AssignmentLayer.Module]);
            session.Process([Frame(Stick, 0), Frame(Throttle, 0)]);
            session.Process([Frame(Stick, 0), Frame(Throttle, 1)]);
            Assert.Equal("S3", session.ActiveModifiers[AssignmentLayer.Module]);
            Assert.Empty(session.Process([Frame(Stick, 0), Frame(Throttle, 1)]));
            session.Reset();
            Assert.Empty(session.ActiveModifiers);
            Assert.Empty(session.Process([Frame(Stick, 1), Frame(Throttle, 1)]));
        }
        /// <summary>Both scopes and duplicate matches remain visible; removed entries do not fire.</summary>
        [Fact]
        public void PreservesOverlapAndIdentity()
        {
            var bindings = new[] { Bound("A"), Bound("B"), Bound("UI", AssignmentLayer.UiLayer), Bound("OLD", disposition: BindingDisposition.Removed) };
            var session = new InputTestSession(bindings, []);
            session.Process([Frame(Stick, 0), Frame(Throttle, 0)]);
            Assert.Empty(session.Process([Frame(Stick, 0), Frame(Throttle, 1)]).Single().Matches);
            Assert.Equal(3, session.Process([Frame(Stick, 1), Frame(Throttle, 1)]).Single().Matches.Count);
        }
        /// <summary>POV aliases match physical directions and axis noise is suppressed without losing cumulative movement.</summary>
        [Fact]
        public void ResolvesPovAndAxes()
        {
            var session = new InputTestSession([new(Stick, AssignmentLayer.Module,
                new(Stick, BindingSection.Key, "HAT", "Hat", BindingDisposition.Added, "JOY_POV1_UR")),
                new(Stick, AssignmentLayer.Module, new(Stick, BindingSection.Axis, "PITCH", "Pitch", BindingDisposition.Changed, "JOY_X", filter: new(Invert: true)))], []);
            var initial = session.Process([new(Stick, new Dictionary<string, double> { ["JOY_BTN_POV1_UR"] = 0, ["JOY_X"] = 0.5 })]);
            Assert.Empty(initial);
            Assert.Empty(session.Process([new(Stick, new Dictionary<string, double> { ["JOY_BTN_POV1_UR"] = 0, ["JOY_X"] = 0.502 })]));
            var events = session.Process([new(Stick, new Dictionary<string, double> { ["JOY_BTN_POV1_UR"] = 1, ["JOY_X"] = 0.52 })]);
            Assert.Equal(2, events.Count);
            Assert.True(events.Single(x => x.InputKey == "JOY_X").Matches.Single().Binding.Filter!.Invert);
        }
        /// <summary>Incomplete polls and invalid values fail before updating modifier state.</summary>
        [Fact]
        public void RejectsIncompleteAndInvalidFrames()
        {
            var session = Session();
            session.Process([Frame(Stick, 0), Frame(Throttle, 0)]);
            Assert.Throws<InvalidDataException>(() => session.Process([Frame(Stick, 1)]));
            Assert.Throws<InvalidDataException>(() => session.Process([Frame(Stick, double.NaN), Frame(Throttle, 1)]));
            Assert.Throws<InvalidDataException>(() => session.Process([Frame(Stick, 0), Frame(Stick, 0)]));
            Assert.Equal("", session.ActiveModifiers[AssignmentLayer.Module]);
        }
        /// <summary>Keyboard modifiers and sorted chords are resolved independently in each scope.</summary>
        [Fact]
        public void ResolvesKeyboardAndSortedChord()
        {
            var session = new InputTestSession([Bound("CHORD", chord: ["Z", "A"])],
                [new(Guid.Empty, AssignmentLayer.Module, "Z", "LShift", ModifierMode.Hold), new(Throttle, AssignmentLayer.Module, "A", "JOY_BTN1", ModifierMode.Hold)]);
            session.Process([Frame(Stick, 0), Frame(Throttle, 0), new(Guid.Empty, new Dictionary<string, double> { ["LShift"] = 0 })]);
            var result = session.Process([Frame(Stick, 1), Frame(Throttle, 1), new(Guid.Empty, new Dictionary<string, double> { ["LShift"] = 1 })]);
            Assert.Equal("A+Z", result.Single(x => x.PhysicalGuid == Stick).Matches.Single().Chord);
        }
        /// <summary>The native decoder distinguishes POV diagonals, neutral, button indices and normalized axes.</summary>
        [Fact]
        public void DecodesNativeState()
        {
            var bytes = new byte[176];
            BitConverter.GetBytes(65535).CopyTo(bytes, 0);
            BitConverter.GetBytes(4500).CopyTo(bytes, 32);
            bytes[48] = 128;
            var result = DirectInputStateDecoder.Decode(bytes, new Dictionary<string, int> { ["JOY_X"] = 0, ["JOY_BTN_POV1"] = 32, ["JOY_BTN1"] = 48 });
            Assert.Equal(1, result["JOY_X"]);
            Assert.Equal(1, result["JOY_BTN1"]);
            Assert.Equal(1, result["JOY_BTN_POV1_UR"]);
            BitConverter.GetBytes(-1).CopyTo(bytes, 32);
            Assert.All(DirectInputStateDecoder.Decode(bytes, new Dictionary<string, int> { ["JOY_BTN_POV1"] = 32 }).Values, x => Assert.Equal(0, x));
            Assert.Throws<InvalidDataException>(() => DirectInputStateDecoder.Decode([], new Dictionary<string, int> { ["JOY_X"] = 0 }));
        }
        /// <summary>Generated Lua retains native GUIDs, effective modifiers and removal dispositions.</summary>
        [Fact]
        public void ReadsActualPayloadWithoutNameMatching()
        {
            var file = $"Stick {{{Stick:D}}}.diff.lua";
            var payload = new Dictionary<string, byte[]>
            {
                ["Config/Input/TEST/joystick/" + file] = System.Text.Encoding.UTF8.GetBytes("local diff = { [\"keyDiffs\"] = { [\"A\"] = { [\"name\"] = \"Action\", [\"added\"] = { [1] = { [\"key\"] = \"JOY_BTN1\" } } } } } return diff"),
                ["Config/Input/TEST/modifiers.lua"] = System.Text.Encoding.UTF8.GetBytes($"local modifiers = {{ [\"S3\"] = {{ [\"device\"] = \"Throttle {{{Throttle:D}}}\", [\"key\"] = \"JOY_BTN1\", [\"switch\"] = true }} }} return modifiers"),
                ["Config/Input/TEST/joystick/Unknown.diff.lua"] = System.Text.Encoding.UTF8.GetBytes("local diff = {} return diff")
            };
            var plan = GeneratedInputTestPlan.Read(payload);
            Assert.Equal(Stick, Assert.Single(plan.Bindings).PhysicalGuid);
            Assert.Equal(Throttle, Assert.Single(plan.Modifiers).PhysicalGuid);
            Assert.Equal(ModifierMode.Toggle, plan.Modifiers[0].Mode);
            Assert.Contains(plan.Diagnostics, x => x.Contains("Unknown", StringComparison.Ordinal));
        }
        /// <summary>A missing supported key is a lost sample, not a modifier release.</summary>
        [Fact]
        public void MissingKeyFailsWithoutClearingHold()
        {
            var session = Session();
            session.Process([Frame(Stick, 0), Frame(Throttle, 1)]);
            Assert.Throws<InvalidDataException>(() => session.Process([Frame(Stick, 0), new(Throttle, new Dictionary<string, double>())]));
            Assert.Equal("S3", session.ActiveModifiers[AssignmentLayer.Module]);
        }
        /// <summary>POV decoding covers all compass positions and neutral values.</summary>
        /// <param name="angle">Native hundredths of degrees.</param>
        /// <param name="direction">Expected DCS hat suffix.</param>
        [Theory]
        [InlineData(0, "U")]
        [InlineData(4500, "UR")]
        [InlineData(9000, "R")]
        [InlineData(13500, "DR")]
        [InlineData(18000, "D")]
        [InlineData(22500, "DL")]
        [InlineData(27000, "L")]
        [InlineData(31500, "UL")]
        public void DecodesEveryHatDirection(int angle, string direction)
        {
            var bytes = BitConverter.GetBytes(angle);
            var values = DirectInputStateDecoder.Decode(bytes, new Dictionary<string, int> { ["JOY_BTN_POV2"] = 0 });
            Assert.Equal(1, values["JOY_BTN_POV2_" + direction]);
            Assert.Equal(1, values.Values.Sum());
        }
        /// <summary>A second toggle edge returns to base while unrelated scope remains unchanged.</summary>
        [Fact]
        public void ToggleTurnsOffAndScopesRemainIndependent()
        {
            var session = Session(ModifierMode.Toggle);
            session.Process([Frame(Stick, 0), Frame(Throttle, 0)]);
            session.Process([Frame(Stick, 0), Frame(Throttle, 1)]);
            session.Process([Frame(Stick, 0), Frame(Throttle, 0)]);
            session.Process([Frame(Stick, 1), Frame(Throttle, 1)]);
            Assert.Equal("", session.ActiveModifiers[AssignmentLayer.Module]);
            Assert.Equal("", session.ActiveModifiers[AssignmentLayer.UiLayer]);
        }
        /// <summary>A SQLite-reopened workspace tests generated edits without changing persisted data or Lua.</summary>
        [Fact]
        public async Task TestingDoesNotSaveOrChangeGeneratedOutput()
        {
            var workspace = Workspace(Stick);
            var path = Path.Combine(Path.GetTempPath(), "ipia-input-test-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(workspace);
                var saved = (await repository.GetAsync(workspace.Configuration.Module.Id))!;
                var before = EditorSnapshotSerializer.Serialize(saved);
                var payload = SnapshotLuaPayloadRenderer.Generate(saved);
                var plan = GeneratedInputTestPlan.Create(saved);
                var session = new InputTestSession(plan.Bindings, plan.Modifiers);
                session.Process([Frame(Stick, 0)]);
                Assert.Equal("KEY", session.Process([Frame(Stick, 1)]).Single().Matches.Single().Binding.CommandId);
                var edited = saved with { Assignments = [saved.Assignments[0] with { Command = saved.Assignments[0].Command with { Name = "Pending edit" } }] };
                Assert.Equal("Pending edit", GeneratedInputTestPlan.Create(edited).Bindings.Single().Binding.CommandName);
                Assert.Equal("Test action", plan.Bindings.Single().Binding.CommandName);
                session.Reset();
                Assert.Equal(before, EditorSnapshotSerializer.Serialize((await repository.GetAsync(workspace.Configuration.Module.Id))!));
                foreach (var entry in payload) Assert.Equal(entry.Value, SnapshotLuaPayloadRenderer.Generate(saved)[entry.Key]);
            }
            finally { File.Delete(path); }
        }
        /// <summary>Other-device axes stay hidden while their Hold modifier resolves selected-device commands.</summary>
        [Fact]
        public void SelectedDeviceFeedbackKeepsCrossDeviceModifiers()
        {
            var session = new InputTestSession([Bound("SHIFTED", chord: ["S3"])],
                [new(Throttle, AssignmentLayer.Module, "S3", "JOY_BTN1", ModifierMode.Hold)]);
            var first = session.ProcessDevice([
                new(Stick, new Dictionary<string, double> { ["JOY_BTN1"] = 0, ["JOY_X"] = 0.5 }),
                new(Throttle, new Dictionary<string, double> { ["JOY_BTN1"] = 0, ["JOY_X"] = 0.7 })], Stick);
            Assert.Empty(first);
            var pressed = session.ProcessDevice([
                new(Stick, new Dictionary<string, double> { ["JOY_BTN1"] = 1, ["JOY_X"] = 0.5 }),
                new(Throttle, new Dictionary<string, double> { ["JOY_BTN1"] = 1, ["JOY_X"] = 0.9 })], Stick);
            Assert.All(pressed, x => Assert.Equal(Stick, x.PhysicalGuid));
            Assert.Equal("SHIFTED", Assert.Single(pressed.Single(x => x.Transition == "Press").Matches).Binding.CommandId);
            Assert.Equal("S3", session.ActiveModifiers[AssignmentLayer.Module]);
        }
        /// <summary>Identical axis keys remain scoped by GUID; unresolved selection yields no feedback.</summary>
        [Fact]
        public void SelectedDeviceFeedbackDistinguishesAxesAndNullSelection()
        {
            var session = Session(ModifierMode.Toggle);
            var frames = new[] {
                new DeviceInputFrame(Stick, new Dictionary<string, double> { ["JOY_BTN1"] = 0, ["JOY_X"] = 0.2 }),
                new DeviceInputFrame(Throttle, new Dictionary<string, double> { ["JOY_BTN1"] = 0, ["JOY_X"] = 0.8 }) };
            Assert.Empty(session.ProcessDevice(frames, null));
            frames[0] = new(Stick, new Dictionary<string, double> { ["JOY_BTN1"] = 0, ["JOY_X"] = 0.4 });
            frames[1] = new(Throttle, new Dictionary<string, double> { ["JOY_BTN1"] = 1, ["JOY_X"] = 0.6 });
            Assert.Equal(0.4, Assert.Single(session.ProcessDevice(frames, Stick)).Value);
            Assert.Equal("S3", session.ActiveModifiers[AssignmentLayer.Module]);
            frames[1] = new(Throttle, new Dictionary<string, double> { ["JOY_BTN1"] = 1, ["JOY_X"] = 0.9 });
            var switched = Assert.Single(session.ProcessDevice(frames, Throttle));
            Assert.Equal(Throttle, switched.PhysicalGuid);
            Assert.Equal(0.9, switched.Value);
        }
        /// <summary>Initially held buttons, POV directions and axis positions establish a silent baseline.</summary>
        [Fact]
        public void InitialStateAndUnchangedSamplesRemainSilent()
        {
            var session = new InputTestSession([], []);
            var values = new Dictionary<string, double> { ["JOY_BTN1"] = 1, ["JOY_BTN_POV1_U"] = 1, ["JOY_X"] = 0.5, ["JOY_Y"] = 1 };
            Assert.Empty(session.Process([new(Stick, values)]));
            Assert.Empty(session.Process([new(Stick, values)]));
            var released = new Dictionary<string, double>(values) { ["JOY_BTN1"] = 0, ["JOY_BTN_POV1_U"] = 0 };
            var changes = session.Process([new(Stick, released)]);
            Assert.Equal(2, changes.Count);
            Assert.All(changes, x => Assert.Equal("Release", x.Transition));
            Assert.Empty(session.Process([new(Stick, released)]));
            session.Reset();
            Assert.Empty(session.Process([new(Stick, values)]));
        }
        /// <summary>Hold and toggle changes do not manufacture movement on stationary axes.</summary>
        /// <param name="mode">Modifier activation mode.</param>
        [Theory]
        [InlineData(ModifierMode.Hold)]
        [InlineData(ModifierMode.Toggle)]
        public void ModifierChangesDoNotReportStationaryAxes(ModifierMode mode)
        {
            var session = Session(mode);
            var stick = new DeviceInputFrame(Stick, new Dictionary<string, double> { ["JOY_BTN1"] = 0, ["JOY_X"] = 0.5 });
            Assert.Empty(session.ProcessDevice([stick, Frame(Throttle, 0)], Stick));
            Assert.Empty(session.ProcessDevice([stick, Frame(Throttle, 1)], Stick));
            Assert.Equal("S3", session.ActiveModifiers[AssignmentLayer.Module]);
            var moved = stick with { Inputs = new Dictionary<string, double> { ["JOY_BTN1"] = 0, ["JOY_X"] = 0.52 } };
            Assert.Equal("Position", Assert.Single(session.ProcessDevice([moved, Frame(Throttle, 1)], Stick)).Transition);
        }
        /// <summary>Small axis movements accumulate from the baseline and from the last reported change.</summary>
        [Fact]
        public void AxisChangesAccumulateWithoutRepeatedPositions()
        {
            var session = new InputTestSession([], []);
            DeviceInputFrame Axis(double value) => new(Stick, new Dictionary<string, double> { ["JOY_X"] = value });
            Assert.Empty(session.Process([Axis(0.5)]));
            Assert.Empty(session.Process([Axis(0.504)]));
            Assert.Empty(session.Process([Axis(0.508)]));
            Assert.Equal(0.512, Assert.Single(session.Process([Axis(0.512)])).Value);
            Assert.Empty(session.Process([Axis(0.512)]));
            Assert.Equal(0.49, Assert.Single(session.Process([Axis(0.49)])).Value);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DeviceInputFrame Frame(Guid guid, double value) => new(guid, new Dictionary<string, double> { ["JOY_BTN1"] = value });
        private static InputTestBinding Bound(string command, AssignmentLayer layer = AssignmentLayer.Module,
            BindingDisposition disposition = BindingDisposition.Added, string[]? chord = null) =>
            new(Stick, layer, new(Stick, BindingSection.Key, command, command, disposition, "JOY_BTN1", chord));
        private static InputTestSession Session(ModifierMode mode = ModifierMode.Hold) => new([Bound("BASE"), Bound("SHIFTED", chord: ["S3"])],
            [new(Throttle, AssignmentLayer.Module, "S3", "JOY_BTN1", mode)]);
        private static EditorWorkspaceSnapshot Workspace(Guid physical)
        {
            var profile = new DeviceProfile(Guid.NewGuid(), "test", null, "Test", $"Test {{{physical:D}}}.diff.lua");
            var module = new DcsModule(Guid.NewGuid(), "INPUT_TEST", "Input test");
            var command = new DraftCommand("KEY", "Test action", false, AssignmentLayer.Module);
            var assignment = new DraftAssignment(new(module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1", ""), command);
            var configuration = new ModuleConfiguration(module, [profile], [], [], "Test fixture");
            var diagram = new UiLayerDiagram("Test", null, "test.svg", 100, 100, [], [])
            {
                ProfileId = profile.Id,
                Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><rect width='100' height='100' fill='#07111d'/></svg>",
                Controls = [new HardwareGraphicControl("button", "Button", "JOY_BTN1", 5, 5, 20, 20)]
            };
            return new(configuration, [], [], [diagram], [new("KEY", "Test action", "Keys", false)], [], [assignment], new Dictionary<string, string>(),
                new Dictionary<string, string>
                {
                    ["config/kneeboard.json"] = """{"profiles":{},"pages":[],"summaryPages":[]}""",
                    ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = "local modifiers = {} return modifiers"
                });
        }
        #endregion END private helpers
    }
}

