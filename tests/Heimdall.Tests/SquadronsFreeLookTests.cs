using Heimdall.Squadrons;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Distinguishes analog camera motion from the separate digital free-look commands.</summary>
    public sealed class SquadronsFreeLookTests
    {
        #region BEGIN Public
        /// <summary>Import retains the independent press, both mini-stick axes and keyboard-only directional concepts.</summary>
        [Fact]
        public void Import_preserves_camera_axes_and_does_not_relabel_them_as_digital_commands()
        {
            var source = "GstKeyBinding.InputDataVersion 4\nGstInput.JoystickDevice1 Smoke Stick 1\n";
            source += Record("FreeLook", 0, 3, 26, 27, 0);
            source += Record("CameraPitch", 0, 3, 26, 44, 0) + Record("CameraPitch", 1, 3, 26, 45, 0);
            source += Record("CameraYaw", 0, 3, 26, 42, 0) + Record("CameraYaw", 1, 3, 26, 43, 0);
            foreach (var (name, button) in new[] { ("Up", 72), ("Down", 76), ("Left", 75), ("Right", 77) })
                source += Record("FreeLookCamera" + name, 0, 2, 0, button, -1);
            var backend = new SquadronsProfileBackend();
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var records = backend.ReadBindings(profile);
            var rows = SquadronsWorkspaceProjection.Rows(profile);
            Assert.Equal(9, records.Count);
            Assert.Equal("JOY_BTN6", Assert.Single(records, row => row.Concept.EndsWith(".ConceptFreeLook", StringComparison.Ordinal)).Input!.Key);
            Assert.Equal(2, records.Count(row => row.Input?.PhysicalKey == "JOY_RX"));
            Assert.Equal(2, records.Count(row => row.Input?.PhysicalKey == "JOY_RY"));
            Assert.Contains(rows, row => row.CommandName == "Camera Pitch" && row.Native.Input?.PhysicalKey == "JOY_RY");
            Assert.Contains(rows, row => row.CommandName == "Camera Yaw" && row.Native.Input?.PhysicalKey == "JOY_RX");
            var commands = SquadronsCommandFilter.Apply(backend.GetCatalog(profile).Commands, records,
                "Flight", "All types", "All", "FreeLookCamera", "squadrons.joystick.1");
            Assert.Equal(4, commands.Count);
            Assert.All(commands, command => Assert.All(records.Where(row => row.Concept == command.Metadata["squadrons.concept"]), row => Assert.Equal(2, row.Type)));
            Assert.Equal(source, backend.Export(profile).Files[SquadronsProfileBackend.FileName]);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Record(string concept, int index, int type, int axis, int button, int device) =>
            string.Concat(new Dictionary<string, int>
            {
                ["altbutton"] = 0, ["axis"] = axis, ["button"] = button, ["deviceid"] = device,
                ["identifier"] = 100 + index, ["modifier"] = type == 2 ? 255 : 0, ["negate"] = index, ["type"] = type
            }.Select(field => "GstKeyBinding.IncomStarshipInputConcepts.Concept" + concept + "." + index + "." + field.Key + " " + field.Value + "\n"));
        #endregion END private helpers
    }
}
