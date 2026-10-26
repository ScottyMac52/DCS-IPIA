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
            Assert.Equal(source.Replace("GstInput.JoystickDevice10 Smoke Throttle 1\n", "", StringComparison.Ordinal), output);
            Assert.Equal(backend.ReadBindings(profile).Count, backend.ReadBindings(removed).Count);
            Assert.Throws<InvalidOperationException>(() => backend.RemoveUnusedCapturedDevice(profile, "squadrons.joystick.5"));
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
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
