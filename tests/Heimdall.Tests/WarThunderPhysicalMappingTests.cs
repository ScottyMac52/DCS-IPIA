using Heimdall.Application;
using Heimdall.Domain.MultiSim;
using Heimdall.WarThunder;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects native ranges, original object order, physical labels and the shared Apply/drop operation.</summary>
    public sealed class WarThunderPhysicalMappingTests
    {
        #region BEGIN Public
        /// <summary>Native axes follow original enumeration, buttons follow instances and POV buttons follow the physical button block.</summary>
        [Fact]
        public void Mapping_uses_raw_axis_order_and_pov_expansion_not_sorted_keys()
        {
            var source = WarThunderBackendFixture.Source.Replace("axesCount:i=2", "axesCount:i=4", StringComparison.Ordinal)
                .Replace("buttonsCount:i=4", "buttonsCount:i=6", StringComparison.Ordinal)
                .Replace("axesOffset:i=2", "axesOffset:i=4", StringComparison.Ordinal)
                .Replace("buttonsOffset:i=4", "buttonsOffset:i=6", StringComparison.Ordinal);
            var profile = WarThunderBackendFixture.Import(new(), source);
            var observation = Observe();
            var map = WarThunderPhysicalMap.Create(profile, "war-thunder.joystick.0", observation);
            Assert.Equal(12, map.Inputs.Count);
            Assert.Contains(new WarThunderPhysicalInput("JOY_BTN_POV1_R", CommandType.Axis, 2), map.Inputs);
            Assert.Contains(new WarThunderPhysicalInput("JOY_BTN_POV1_U", CommandType.Axis, 3), map.Inputs);
            Assert.Contains(new WarThunderPhysicalInput("JOY_Y", CommandType.Axis, 0), map.Inputs);
            Assert.Contains(new WarThunderPhysicalInput("JOY_X", CommandType.Axis, 1), map.Inputs);
            Assert.Contains(new WarThunderPhysicalInput("JOY_BTN2", CommandType.Button, 1), map.Inputs);
            Assert.Contains(new WarThunderPhysicalInput("JOY_BTN_POV1_U", CommandType.Button, 2), map.Inputs);
            Assert.Contains(new WarThunderPhysicalInput("JOY_BTN_POV1_L", CommandType.Button, 5), map.Inputs);
            var invalid = WarThunderPhysicalMap.Create(profile, "war-thunder.joystick.0", observation with { Capabilities = observation.Capabilities! with { NativeObjects = null } });
            Assert.Empty(invalid.Inputs); Assert.Contains("Rescan", invalid.Message, StringComparison.Ordinal);
            Assert.Empty(WarThunderPhysicalMap.Create(profile, "war-thunder.joystick.0", observation with { Device = observation.Device with { Connected = false } }).Inputs);
            Assert.Empty(WarThunderPhysicalMap.Create(WarThunderBackendFixture.Import(new()), "war-thunder.joystick.0", observation).Inputs);
        }
        /// <summary>Imported commands become callout labels, and physical assignment preserves system alternatives and activation fields.</summary>
        [Fact]
        public void Drawing_labels_and_assignment_share_native_offset_contract()
        {
            var backend = new WarThunderProfileBackend(); var profile = WarThunderBackendFixture.Import(backend);
            var drawing = new UiLayerDiagram("Synthetic", null, "", 300, 200, [], []) { Controls = [new("fire", "Fire", "JOY_BTN2", 0, 0, 50, 30)] };
            var map = new WarThunderPhysicalMap([new("JOY_BTN2", CommandType.Button, 1)], "verified test evidence");
            var page = WarThunderDrawingProjection.Diagram(drawing, profile, "war-thunder.joystick.0", map);
            Assert.Equal("FIRE", Assert.Single(page.Labels["fire"]).Text);
            var command = backend.GetCatalog(profile).Commands.Single(command => command.Metadata["war-thunder.command"] == "ID_FIRE");
            var edit = WarThunderDrawingProjection.Assignment(profile, command.Id, "war-thunder.joystick.1", new("JOY_BTN2", CommandType.Button, 1));
            var changed = edit.Apply(profile); var text = WarThunderBackendFixture.Export(backend, changed);
            Assert.Contains("joyButton:i=5", text, StringComparison.Ordinal); Assert.Contains("activationType:i=9", text, StringComparison.Ordinal);
            Assert.Contains("keyboardKey:i=30;keyboardKey:i=31", text, StringComparison.Ordinal);
            Assert.Throws<InvalidOperationException>(() => WarThunderDrawingProjection.Assignment(profile, command.Id, "war-thunder.joystick.0", new("JOY_X", CommandType.Axis, 0)));
            var chord = backend.GetCatalog(profile).Commands.Single(command => command.Metadata["war-thunder.command"] == "ID_CHORD");
            var added = WarThunderDrawingProjection.Assignment(profile, chord.Id, "war-thunder.joystick.0", new("JOY_BTN2", CommandType.Button, 1)).Apply(profile);
            Assert.Contains("joyButton:i=0;joyButton:i=2;keyboardKey:i=4", WarThunderBackendFixture.Export(backend, added), StringComparison.Ordinal);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static ControllerObservation Observe()
        {
            ControllerObject[] objects = [new(Guid.Parse("a36d02e1-c9f3-11cf-bfc7-444553540000"), 258u, "Y"),
                new(Guid.Parse("a36d02e0-c9f3-11cf-bfc7-444553540000"), 2u, "X"), new(Guid.NewGuid(), 4u, "Button 1"),
                new(Guid.NewGuid(), 260u, "Button 2"), new(Guid.NewGuid(), 16u, "POV")];
            return new(new(Guid.NewGuid(), "Synthetic", true, ProductGuid: Guid.Parse("00020001-0000-0000-0000-504944564944")), ControllerCapabilityMapper.Map(objects));
        }
        #endregion END private helpers
    }
}
