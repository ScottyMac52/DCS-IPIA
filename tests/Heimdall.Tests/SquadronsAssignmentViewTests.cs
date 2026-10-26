using Heimdall.Domain.MultiSim;
using Heimdall.Squadrons;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Separates the normal assignment view from raw capture review without changing native evidence.</summary>
    public sealed class SquadronsAssignmentViewTests
    {
        #region BEGIN Public
        /// <summary>Normal flight/device filtering retains captured joystick inputs while review restores preserved system records.</summary>
        [Fact]
        public void Normal_view_shows_only_compatible_inputs_in_the_selected_context_and_device()
        {
            var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var rows = SquadronsWorkspaceProjection.ReviewInputs(SquadronsWorkspaceProjection.Rows(profile),
                new Dictionary<string, IReadOnlyCollection<string>> { ["squadrons.joystick.5"] = ["JOY_Z"] });
            var visible = SquadronsWorkspaceProjection.Filter(rows, "Flight", "All inputs", "", "squadrons.joystick.5", includeUnverified: false);
            Assert.Equal(2, visible.Count); Assert.All(visible, x => Assert.Equal("Supported physical input (slot unconfirmed)", x.HardwareStatus));
            Assert.Empty(SquadronsWorkspaceProjection.Filter(rows, "Flight", "All inputs", "", "squadrons.joystick.3", includeUnverified: false));
            Assert.Single(SquadronsWorkspaceProjection.Filter(rows, "Flight", "All inputs", "", "squadrons.joystick.3", includeUnverified: true));
            Assert.Empty(SquadronsWorkspaceProjection.Filter(rows, "Menus & General", "All inputs", "", "all", includeUnverified: false));
            Assert.NotEmpty(SquadronsWorkspaceProjection.Filter(rows, "All", "All inputs", "", "all", includeUnverified: true));
            Assert.Single(SquadronsWorkspaceProjection.Filter(rows, "Flight", "Axes", "Increase", "all", includeUnverified: false));
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
            Assert.DoesNotContain(rows, x => x.HardwareStatus == "Verified device input");
            Assert.Contains(rows, x => x.HardwareStatus == "Input absent on linked device" || x.HardwareStatus == "Hardware unverified");
        }
        /// <summary>Unverified native joystick assignments remain visible; flight context excludes camera and spectator controls.</summary>
        [Fact]
        public void Flight_view_hides_unverified_native_assignments_without_mixing_camera_contexts()
        {
            var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var rows = SquadronsWorkspaceProjection.ReviewInputs(SquadronsWorkspaceProjection.Rows(profile),
                new Dictionary<string, IReadOnlyCollection<string>>());
            var flight = SquadronsWorkspaceProjection.Filter(rows, "Flight", "All inputs", "", "squadrons.joystick.3", includeUnverified: false);
            Assert.Empty(flight);
            var advanced = SquadronsWorkspaceProjection.Filter(rows, "Flight", "All inputs", "", "squadrons.joystick.3", includeUnverified: true);
            Assert.Single(advanced);
            Assert.Equal("Hardware unverified", advanced[0].HardwareStatus);
            Assert.Equal("Fire", advanced[0].CommandName);
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Shared capability evidence identifies usable inputs, while identical native slot names remain separate.</summary>
        [Fact]
        public void Slot_diagnostics_distinguish_duplicate_names_and_current_grip_capabilities()
        {
            var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var baseOnly = SquadronsWorkspaceProjection.ReviewInputs(SquadronsWorkspaceProjection.Rows(profile),
                new Dictionary<string, IReadOnlyCollection<string>> { ["squadrons.joystick.3"] = ["JOY_X", "JOY_Y"], ["squadrons.joystick.5"] = ["JOY_Z"] });
            var stick = SquadronsWorkspaceProjection.SlotDiagnostic(profile, baseOnly, "squadrons.joystick.3");
            Assert.Contains("1 captured joystick records", stick);
            Assert.Contains("1 absent on linked hardware", stick);
            Assert.Contains("0 supported by current enumeration", stick);
            var throttle = SquadronsWorkspaceProjection.SlotDiagnostic(profile, baseOnly, "squadrons.joystick.5");
            Assert.Contains("2 captured joystick records", throttle);
            Assert.Contains("2 supported by current enumeration", throttle);
            Assert.Contains("2 slots share the name", throttle);
            var unused = SquadronsWorkspaceProjection.SlotDiagnostic(profile, baseOnly, "squadrons.joystick.10");
            Assert.Contains("0 captured joystick records", unused);
            Assert.DoesNotContain("unverified", unused, StringComparison.OrdinalIgnoreCase);
            var withGrip = SquadronsWorkspaceProjection.ReviewInputs(SquadronsWorkspaceProjection.Rows(profile),
                new Dictionary<string, IReadOnlyCollection<string>> { ["squadrons.joystick.3"] = ["JOY_X", "JOY_Y", "JOY_BTN1"] });
            Assert.Contains("1 supported by current enumeration",
                SquadronsWorkspaceProjection.SlotDiagnostic(profile, withGrip, "squadrons.joystick.3"));
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Only unreferenced slots can be removed without affecting bindings.</summary>
        [Fact]
        public void Unused_duplicate_slot_removal_preserves_every_native_binding()
        {
            var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var removed = backend.RemoveUnusedCapturedDevice(profile, "squadrons.joystick.10");
            var output = backend.Export(removed).Files[SquadronsProfileBackend.FileName];
            Assert.DoesNotContain("GstInput.JoystickDevice10 ", output);
            Assert.Equal(source.Replace("GstInput.JoystickDevice10 Smoke Throttle 1" + (source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n"), "", StringComparison.Ordinal), output);
            Assert.Equal(backend.ReadBindings(profile).Count, backend.ReadBindings(removed).Count);
            Assert.Throws<InvalidOperationException>(() => backend.RemoveUnusedCapturedDevice(profile, "squadrons.joystick.5"));
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Clearing one controller removes every native record, not merely records supported by its current grip.</summary>
        [Fact]
        public void Clear_all_joystick_records_keeps_other_controllers_and_non_joystick_settings()
        {
            var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            Assert.Equal(2, backend.CountCapturedAssignments(profile, "squadrons.joystick.5"));
            var cleared = backend.ClearCapturedAssignments(profile, "squadrons.joystick.5");
            var originalRows = backend.ReadBindings(profile);
            var remaining = backend.ReadBindings(cleared);
            Assert.Equal(originalRows.Count - 2, remaining.Count);
            Assert.DoesNotContain(remaining, row => row.Type == 3 && row.DeviceId == 4);
            Assert.Contains(remaining, row => row.Type == 3 && row.DeviceId == 2);
            Assert.Contains(remaining, row => row.Type == 2);
            var exported = backend.Export(cleared).Files[SquadronsProfileBackend.FileName];
            Assert.Contains("Future.Setting preserved", exported);
            Assert.Contains("GstInput.JoystickDevice5 Smoke Throttle 1", exported);
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
            Assert.Equal(0, backend.CountCapturedAssignments(cleared, "squadrons.joystick.5"));
        }
        /// <summary>Native permutation closes slot gaps and carries all joystick records to the correct new device IDs.</summary>
        [Fact]
        public void Drag_order_reindexes_slots_and_preserves_other_native_fields()
        {
            var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var reorder = new[] { "squadrons.joystick.10", "squadrons.joystick.3", "squadrons.joystick.5" };
            var changed = backend.ReorderCapturedSlots(profile, reorder);
            Assert.Equal("Smoke Throttle 1", changed.Devices.Single(x => x.LogicalId == "squadrons.joystick.1").Identity!.WindowsName);
            Assert.Equal("Smoke Stick 1", changed.Devices.Single(x => x.LogicalId == "squadrons.joystick.2").Identity!.WindowsName);
            Assert.Equal(2, backend.ReadBindings(changed).Count(x => x.Type == 3 && x.DeviceId == 2));
            Assert.Single(backend.ReadBindings(changed), x => x.Type == 3 && x.DeviceId == 1);
            Assert.Contains(backend.ReadBindings(changed), x => x.Type == 2 && x.DeviceId == -1);
            Assert.Contains("Future.Setting preserved", backend.Export(changed).Files[SquadronsProfileBackend.FileName]);
            Assert.Equal(profile.Devices.Sum(x => x.Buttons.Count + x.Axes.Count),
                changed.Devices.Sum(x => x.Buttons.Count + x.Axes.Count));
            var back = backend.ReorderCapturedSlots(changed, ["squadrons.joystick.2", "squadrons.joystick.3", "squadrons.joystick.1"]);
            Assert.Equal(profile.Devices.Sum(x => x.Buttons.Count + x.Axes.Count),
                back.Devices.Sum(x => x.Buttons.Count + x.Axes.Count));
            Assert.Throws<InvalidOperationException>(() => backend.ReorderCapturedSlots(profile,
                ["squadrons.joystick.3", "squadrons.joystick.5"]));
        }
        /// <summary>Deleting one native record cannot alter sibling records, other inputs or the command catalog.</summary>
        [Fact]
        public void Clear_single_record_preserves_other_bindings_and_command_catalog()
        {
            var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var command = backend.GetCatalog(profile).Commands.Single(x =>
                x.Metadata["squadrons.concept"] == "IncomStarshipInputConcepts.ConceptThrottle");
            Assert.Equal("Flight", command.Category);
            var original = backend.ReadBindings(profile);
            var cleared = backend.ClearCapturedRecord(profile, "IncomStarshipInputConcepts.ConceptThrottle.4");
            var remaining = backend.ReadBindings(cleared);
            Assert.Equal(original.Count - 1, remaining.Count);
            Assert.DoesNotContain(remaining, x => x.RecordId == "IncomStarshipInputConcepts.ConceptThrottle.4");
            Assert.Contains(remaining, x => x.RecordId == "IncomStarshipInputConcepts.ConceptThrottle.5");
            Assert.Contains(remaining, x => x.RecordId == "IncomStarshipInputConcepts.ConceptMenu.0");
            Assert.Contains("Future.Setting preserved", backend.Export(cleared).Files[SquadronsProfileBackend.FileName]);
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
            Assert.Throws<InvalidOperationException>(() =>
                backend.ClearCapturedRecord(profile, "IncomStarshipInputConcepts.ConceptMenu.0"));
        }
        /// <summary>Command-first authoring uses existing safe indexed records and does not invent missing ones.</summary>
        [Fact]
        public void Assignment_planner_distinguishes_existing_record_replacement_from_new_native_creation()
        {
            var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var throttle = SquadronsAssignmentPlanner.For(backend, profile, "IncomStarshipInputConcepts.ConceptThrottle");
            Assert.Equal(2, throttle.CapturedRecords);
            Assert.Equal(2, throttle.EditableRecords);
            Assert.True(throttle.CanReplaceCapturedRecord);
            Assert.False(throttle.RequiresNewNativeRecord);
            Assert.Contains("IncomStarshipInputConcepts.ConceptThrottle.4", throttle.EditableRecordIds);
            var cleared = backend.ClearCapturedAssignments(profile, "squadrons.joystick.5");
            var unassigned = SquadronsAssignmentPlanner.For(backend, cleared,
                "IncomStarshipInputConcepts.ConceptThrottle");
            Assert.Equal(0, unassigned.CapturedRecords);
            Assert.True(unassigned.RequiresNewNativeRecord);
            Assert.Throws<InvalidOperationException>(() => SquadronsAssignmentPlanner.For(backend, profile,
                "IncomStarshipInputConcepts.ConceptNotCaptured"));
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Unbound Pitch/Roll/Yaw remain discoverable even after removing the last joystick binding.</summary>
        [Fact]
        public void Command_catalog_is_independent_of_native_binding_records()
        {
            var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var original = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var cleared = backend.ClearCapturedAssignments(original, "squadrons.joystick.5");
            var commands = backend.GetCatalog(cleared).Commands;
            foreach (var name in new[] { "Pitch", "Roll", "Yaw", "Throttle", "Afterburner" })
            {
                var concept = "IncomStarshipInputConcepts.Concept" + name;
                Assert.Contains(commands, command => command.Metadata["squadrons.concept"] == concept
                    && command.Category == "Flight");
            }
            Assert.True(commands.Count >= 125);
            var planner = SquadronsAssignmentPlanner.For(backend, cleared, "IncomStarshipInputConcepts.ConceptYaw");
            Assert.Equal(0, planner.CapturedRecords);
            Assert.True(planner.RequiresNewNativeRecord);
            Assert.Equal(source, backend.Export(original).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Catalog filters preserve unbound native commands while distinguishing native ownership and input type.</summary>
        [Fact]
        public void Command_filters_intersect_context_type_status_search_and_controller()
        {
            var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var all = backend.GetCatalog(profile).Commands;
            var records = backend.ReadBindings(profile);
            var yaw = SquadronsCommandFilter.Apply(all, records, "Flight", "All types",
                "Unassigned everywhere", "yaw", null);
            Assert.Contains(yaw, x => x.Metadata["squadrons.concept"] == "IncomStarshipInputConcepts.ConceptYaw");
            Assert.DoesNotContain(yaw, x => x.DisplayName == "Throttle");
            var throttle = SquadronsCommandFilter.Apply(all, records, "Flight", "Axes",
                "Assigned to selected controller", "throttle", "squadrons.joystick.5");
            Assert.Single(throttle);
            Assert.Equal("Throttle", throttle[0].DisplayName);
            Assert.Empty(SquadronsCommandFilter.Apply(all, records, "Flight", "Axes",
                "Assigned to selected controller", "throttle", "squadrons.joystick.3"));
            Assert.Contains(SquadronsCommandFilter.Apply(all, records, "Flight", "All types",
                "Not assigned to selected controller", "throttle", "squadrons.joystick.3"),
                x => x.DisplayName == "Throttle");
            Assert.Contains(SquadronsCommandFilter.Apply(all, records, "All", "Unknown / unbound",
                "All", "yaw", null), x => x.DisplayName == "Yaw");
            Assert.Empty(SquadronsCommandFilter.Apply(all, records, "Hangar & Briefing Room",
                "All types", "All", "throttle", null));
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Native keyboard, mouse and gamepad records are inspectable without claiming an unverified key mapping.</summary>
        [Fact]
        public void System_inputs_are_visible_when_selected_and_remain_noneditable()
        {
            var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var rows = SquadronsWorkspaceProjection.ReviewInputs(SquadronsWorkspaceProjection.Rows(profile),
                new Dictionary<string, IReadOnlyCollection<string>>());
            var keyboard = SquadronsWorkspaceProjection.Filter(rows, "All", "All inputs", "",
                "system.2", includeUnverified: false);
            var capturedKey = Assert.Single(keyboard);
            Assert.False(capturedKey.Editable);
            Assert.Equal("Keyboard · Space", capturedKey.InputLabel);
            Assert.Equal("Preserved system input", capturedKey.HardwareStatus);
            Assert.Empty(SquadronsWorkspaceProjection.Filter(rows, "All", "All inputs", "", "system.0",
                includeUnverified: false));
            foreach (var (type, label) in new[] { (0, "Mouse"), (1, "Gamepad") })
            {
                var variant = source.Replace("IncomStarshipInputConcepts.ConceptMenu.0.type 2",
                    "IncomStarshipInputConcepts.ConceptMenu.0.type " + type, StringComparison.Ordinal);
                var testProfile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = variant });
                var testRows = SquadronsWorkspaceProjection.ReviewInputs(SquadronsWorkspaceProjection.Rows(testProfile),
                    new Dictionary<string, IReadOnlyCollection<string>>());
                var systemRow = Assert.Single(SquadronsWorkspaceProjection.Filter(testRows, "All", "All inputs", "",
                    "system." + type, includeUnverified: false));
                Assert.Equal(type == 0 ? "Mouse · Button 57" : "Gamepad · button code 57", systemRow.InputLabel);
                Assert.False(systemRow.Editable);
                Assert.Equal(variant, backend.Export(testProfile).Files[SquadronsProfileBackend.FileName]);
            }
            Assert.Throws<InvalidOperationException>(() =>
                backend.ClearCapturedRecord(profile, capturedKey.RecordId));
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Known native scan codes, mouse movements and gamepad controls have readable, noneditable labels.</summary>
        [Fact]
        public void System_input_decoder_labels_known_controls_and_preserves_unknown_codes()
        {
            static SquadronsBindingRow Row(int type, int axis, int button) =>
                new("IncomDefaultInputConcepts.ConceptMenu", 0, new Dictionary<string, int>
                {
                    ["type"] = type, ["axis"] = axis, ["button"] = button,
                    ["deviceid"] = -1, ["identifier"] = 123, ["modifier"] = 0,
                    ["altbutton"] = 0, ["negate"] = 0
                });
            Assert.Equal("Keyboard · Space", SquadronsSystemInputDisplay.Describe(Row(2, 26, 57)));
            Assert.Equal("Keyboard · W", SquadronsSystemInputDisplay.Describe(Row(2, 26, 17)));
            Assert.Equal("Keyboard · scan code 250", SquadronsSystemInputDisplay.Describe(Row(2, 26, 250)));
            Assert.Equal("Mouse · Wheel up", SquadronsSystemInputDisplay.Describe(Row(0, 14, 8)));
            Assert.Equal("Mouse · Wheel down", SquadronsSystemInputDisplay.Describe(Row(0, 15, 8)));
            Assert.Equal("Mouse · X axis (+)", SquadronsSystemInputDisplay.Describe(Row(0, 2, 8)));
            Assert.Equal("Mouse · Button 0", SquadronsSystemInputDisplay.Describe(Row(0, 26, 0)));
            Assert.Equal("Gamepad · A", SquadronsSystemInputDisplay.Describe(Row(1, 26, 5)));
            Assert.Equal("Gamepad · D-pad Up", SquadronsSystemInputDisplay.Describe(Row(1, 26, 0)));
            Assert.Equal("Gamepad · Left trigger", SquadronsSystemInputDisplay.Describe(Row(1, 26, 14)));
            Assert.Equal("Gamepad · Right stick X (-)", SquadronsSystemInputDisplay.Describe(Row(1, 10, 174)));
            Assert.Equal("Gamepad · button code 99", SquadronsSystemInputDisplay.Describe(Row(1, 26, 99)));
            Assert.False(new SquadronsWorkspaceRow(Row(2, 26, 57), "Menu", "Menus & General", "system.2", "Keyboard").Editable);
        }
        /// <summary>Mouse, gamepad, and keyboard selectors must not be parsed as joystick slot numbers.</summary>
        [Fact]
        public void Command_filter_accepts_read_only_system_sources_without_a_joystick_prefix()
        {
            var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var commands = backend.GetCatalog(profile).Commands;
            var records = backend.ReadBindings(profile);
            foreach (var system in new[] { "system.0", "system.1", "system.2" })
            {
                var selected = SquadronsCommandFilter.Apply(commands, records, "All", "All types",
                    "Assigned to selected controller", "", system);
                Assert.Equal(system == "system.2" ? 1 : 0, selected.Count);
                Assert.All(selected, command => Assert.Equal("Menu", command.DisplayName));
                var unassigned = SquadronsCommandFilter.Apply(commands, records, "All", "All types",
                    "Not assigned to selected controller", "yaw", system);
                Assert.Contains(unassigned, command => command.DisplayName == "Yaw");
            }
            Assert.NotEmpty(SquadronsCommandFilter.Apply(commands, records, "All", "All types",
                "Assigned to selected controller", "", "squadrons.joystick.5"));
            Assert.NotEmpty(SquadronsCommandFilter.Apply(commands, records, "All", "All types",
                "Assigned to selected controller", "", "all"));
            foreach (var invalid in new[] { "system.", "system.3", "squadrons.joystick.", "squadrons.joystick.11", "nonsense" })
                Assert.Throws<ArgumentException>(() => SquadronsCommandFilter.Apply(commands, records,
                    "All", "All types", "All", "", invalid));
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Native joystick assignments populate shared hardware callouts without reassigning commands.</summary>
        [Fact]
        public void Captured_joystick_assignments_render_on_matching_shared_drawing_controls()
        {
            var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var rows = SquadronsWorkspaceProjection.Rows(profile);
            var drawing = new Heimdall.Application.UiLayerDiagram("Example joystick", null, "fixture", 640, 360, [],
                [])
            {
                Controls =
                [
                    new Heimdall.Application.HardwareGraphicControl("throttle", "Throttle lever", "JOY_Z", 0, 0, 100, 50, true),
                    new Heimdall.Application.HardwareGraphicControl("trigger", "Trigger", "JOY_BTN1", 110, 0, 100, 50),
                    new Heimdall.Application.HardwareGraphicControl("empty", "Unassigned", "JOY_X", 220, 0, 100, 50)
                ]
            };
            var throttle = SquadronsDrawingProjection.Diagram("Test", drawing, rows, "squadrons.joystick.5", "Flight");
            Assert.Equal(2, throttle.Labels["throttle"].Count);
            Assert.All(throttle.Labels["throttle"], label => Assert.Contains("Throttle", label.Text));
            Assert.Empty(throttle.Labels["trigger"]);
            Assert.Empty(throttle.Labels["empty"]);
            var trigger = SquadronsDrawingProjection.Diagram("Test", drawing, rows, "squadrons.joystick.3", "Flight");
            Assert.Single(trigger.Labels["trigger"]);
            Assert.Contains("Fire", trigger.Labels["trigger"][0].Text);
            Assert.Empty(trigger.Labels["throttle"]);
            Assert.Equal(2, SquadronsDrawingProjection.ForControl(drawing.Controls[0], rows,
                "squadrons.joystick.5", "Flight").Count);
            Assert.Empty(SquadronsDrawingProjection.ForControl(drawing.Controls[0], rows,
                "squadrons.joystick.3", "Flight"));
            Assert.Empty(SquadronsDrawingProjection.ForControl(drawing.Controls[1], rows,
                "squadrons.joystick.3", "Menus & General"));
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Adding a second native binding retains all captured values and rejects unknown templates.</summary>
        [Fact]
        public void Native_record_creation_appends_a_verified_command_without_changing_existing_evidence()
        {
            var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var original = backend.ReadBindings(profile);
            var input = SquadronsInputCodec.Decode(26, 24)!;
            Assert.NotNull(input);
            var updated = backend.AddJoystickBinding(profile, "IncomStarshipInputConcepts.ConceptThrottle",
                "squadrons.joystick.5", input);
            var output = backend.Export(updated).Files[SquadronsProfileBackend.FileName];
            Assert.StartsWith(source.TrimEnd('\r', '\n'), output, StringComparison.Ordinal);
            var next = backend.ReadBindings(updated);
            Assert.Equal(original.Count + 1, next.Count);
            Assert.All(original, row => Assert.Contains(next, candidate =>
                candidate.RecordId == row.RecordId && candidate.Fields.Count == row.Fields.Count &&
                row.Fields.All(pair => candidate.Fields.TryGetValue(pair.Key, out var value) && value == pair.Value)));
            Assert.Contains(next, row => row.Concept == "IncomStarshipInputConcepts.ConceptThrottle" &&
                row.Index == 6 && row.DeviceId == 4 && row.Input == input);
            // A command with only a keyboard record can acquire its first joystick
            // assignment using the captured concept's identifier without touching the keyboard input.
            var menu = backend.AddJoystickBinding(profile, "IncomStarshipInputConcepts.ConceptMenu",
                "squadrons.joystick.5", input);
            var menuRows = backend.ReadBindings(menu);
            Assert.Contains(menuRows, row => row.Concept == "IncomStarshipInputConcepts.ConceptMenu" &&
                row.Type == 2 && row.Fields["button"] == 57);
            Assert.Contains(menuRows, row => row.Concept == "IncomStarshipInputConcepts.ConceptMenu" &&
                row.Type == 3 && row.DeviceId == 4 && row.Input == input);
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
            Assert.Throws<InvalidOperationException>(() => backend.AddJoystickBinding(profile,
                "IncomStarshipInputConcepts.ConceptYaw", "squadrons.joystick.5", input));
        }
        /// <summary>Controller display order is numeric even when the capture lists slot ten before slot three.</summary>
        [Fact]
        public void Controllers_are_ordered_by_native_slot_number_without_reordering_the_profile()
        {
            var backend = new SquadronsProfileBackend();
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            profile.Devices.Reverse();
            var order = SquadronsWorkspaceProjection.OrderedDevices(profile).Select(x => x.LogicalId).ToArray();
            Assert.Equal("squadrons.joystick.3", order[0]); Assert.Equal("squadrons.joystick.5", order[1]); Assert.Equal("squadrons.joystick.10", order[2]);
            Assert.Equal("squadrons.joystick.10", profile.Devices[0].LogicalId);
        }
        #endregion END Public
    }
}
