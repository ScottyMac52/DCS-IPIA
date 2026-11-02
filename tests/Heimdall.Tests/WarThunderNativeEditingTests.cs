using System.Text.Json;
using Heimdall.Domain.MultiSim;
using Heimdall.WarThunder;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects occurrence-specific native writes and rejects ambiguous input ownership without partial changes.</summary>
    public sealed class WarThunderNativeEditingTests
    {
        #region BEGIN Public
        /// <summary>One chord member can move across saved device ranges while every other original character remains exact.</summary>
        [Fact]
        public void Rebind_one_repeated_scalar_preserves_the_rest_of_the_chord()
        {
            var backend = new WarThunderProfileBackend(); var profile = WarThunderBackendFixture.Import(backend);
            var frozen = JsonSerializer.Serialize(profile);
            var binding = WarThunderBackendFixture.Binding(profile, "ID_CHORD");
            var input = WarThunderBackendFixture.Field(binding, "joyButton");
            var other = WarThunderBackendFixture.Field(binding, "joyButton", 1);
            Assert.NotEqual(input.Address, other.Address); Assert.Equal(input.Path, other.Path);
            var changed = WarThunderProfileBackend.RebindInput(profile, binding.Id, input.Address, "war-thunder.joystick.1", CommandType.Button, 1);
            var expected = WarThunderBackendFixture.Source[..input.Start] + "5" + WarThunderBackendFixture.Source[input.End..];
            Assert.Equal(expected, WarThunderBackendFixture.Export(backend, changed));
            Assert.Equal(frozen, JsonSerializer.Serialize(profile));
            var reopened = WarThunderBackendFixture.Binding(changed, "ID_CHORD");
            Assert.Equal(binding.Id, reopened.Id);
            Assert.Equal("2", WarThunderBackendFixture.Field(reopened, "joyButton", 1).Value);
            Assert.Equal("4", WarThunderBackendFixture.Field(reopened, "keyboardKey").Value);
            Assert.Throws<InvalidOperationException>(() => WarThunderProfileBackend.RebindInput(profile, binding.Id, input.Address,
                "war-thunder.joystick.0", CommandType.Button, 2));
        }
        /// <summary>Axis routing changes only axisId; inversion, mouse mapping and unknown captured parameters survive.</summary>
        [Fact]
        public void Axis_rebind_does_not_rewrite_axis_parameters_or_other_commands()
        {
            var backend = new WarThunderProfileBackend(); var profile = WarThunderBackendFixture.Import(backend);
            var binding = WarThunderBackendFixture.Binding(profile, "pitch");
            var input = WarThunderBackendFixture.Field(binding, "axisId");
            var changed = WarThunderProfileBackend.RebindInput(profile, binding.Id, input.Address, "war-thunder.joystick.1", CommandType.Axis, 1);
            Assert.Equal(WarThunderBackendFixture.Source[..input.Start] + "3" + WarThunderBackendFixture.Source[input.End..],
                WarThunderBackendFixture.Export(backend, changed));
            Assert.Throws<InvalidOperationException>(() => WarThunderProfileBackend.RebindInput(profile, binding.Id, input.Address, "war-thunder.joystick.0", CommandType.Button, 1));
            Assert.Throws<InvalidOperationException>(() => WarThunderProfileBackend.RebindInput(profile, binding.Id, "missing", "war-thunder.joystick.0", CommandType.Axis, 1));
            Assert.Throws<InvalidOperationException>(() => WarThunderProfileBackend.RebindInput(profile, "missing", input.Address, "war-thunder.joystick.0", CommandType.Axis, 1));
        }
        /// <summary>Supported captured parameters retain their native type; ownership/system fields and unknown parameter semantics remain protected.</summary>
        [Fact]
        public void Parameter_edits_are_native_literals_and_do_not_infer_portable_tuning()
        {
            var backend = new WarThunderProfileBackend(); var profile = WarThunderBackendFixture.Import(backend);
            var pitch = WarThunderBackendFixture.Binding(profile, "pitch");
            var scale = WarThunderBackendFixture.Field(pitch, "kMul");
            var changed = WarThunderProfileBackend.EditAxisParameter(profile, pitch.Id, scale.Address, "2.5");
            Assert.Equal(WarThunderBackendFixture.Source[..scale.Start] + "2.5" + WarThunderBackendFixture.Source[scale.End..], WarThunderBackendFixture.Export(backend, changed));
            Assert.Throws<ArgumentException>(() => WarThunderProfileBackend.EditAxisParameter(profile, pitch.Id, scale.Address, "NaN"));
            Assert.Throws<InvalidOperationException>(() => WarThunderProfileBackend.EditAxisParameter(profile, pitch.Id, WarThunderBackendFixture.Field(pitch, "axisId").Address, "0"));
            Assert.Throws<InvalidOperationException>(() => WarThunderProfileBackend.EditAxisParameter(profile, pitch.Id, WarThunderBackendFixture.Field(pitch, "mouseAxisId").Address, "0"));
            Assert.Throws<InvalidOperationException>(() => WarThunderProfileBackend.EditAxisParameter(profile, pitch.Id, WarThunderBackendFixture.Field(pitch, "futureParam").Address, "0"));
            var throttle = WarThunderBackendFixture.Binding(profile, "throttle");
            Assert.Throws<ArgumentException>(() => WarThunderProfileBackend.EditAxisParameter(profile, throttle.Id, WarThunderBackendFixture.Field(throttle, "nonlinearity").Address, "1.5"));
        }
        /// <summary>The known native inverse flag supports captured yes/no or integer style and can be added without changing input ownership.</summary>
        [Theory]
        [InlineData("no", "yes")]
        [InlineData("0", "1")]
        public void Axis_inverse_preserves_captured_literal_style(string before, string after)
        {
            var backend = new WarThunderProfileBackend(); var source = WarThunderBackendFixture.Source.Replace("inverse:b=no", "inverse:b=" + before, StringComparison.Ordinal);
            var profile = WarThunderBackendFixture.Import(backend, source);
            var pitch = WarThunderBackendFixture.Binding(profile, "pitch");
            var changed = WarThunderProfileBackend.SetAxisInverse(profile, pitch.Id, true);
            Assert.Equal(source.Replace("inverse:b=" + before, "inverse:b=" + after, StringComparison.Ordinal), WarThunderBackendFixture.Export(backend, changed));
            Assert.Equal(source, WarThunderBackendFixture.Export(backend, WarThunderProfileBackend.SetAxisInverse(changed, pitch.Id, false)));
            var throttle = WarThunderBackendFixture.Binding(profile, "throttle");
            var added = WarThunderProfileBackend.SetAxisInverse(profile, throttle.Id, true);
            Assert.Equal("yes", WarThunderBackendFixture.Field(WarThunderBackendFixture.Binding(added, "throttle"), "inverse").Value);
            Assert.Equal("2", WarThunderBackendFixture.Field(WarThunderBackendFixture.Binding(added, "throttle"), "axisId").Value);
        }
        /// <summary>Clearing retains the record header so occurrence IDs and unbound catalog entries survive.</summary>
        [Fact]
        public void Clearing_one_joystick_alternative_retains_system_alternatives_and_stable_addresses()
        {
            var backend = new WarThunderProfileBackend(); var profile = WarThunderBackendFixture.Import(backend);
            var first = WarThunderBackendFixture.Binding(profile, "ID_FIRE");
            var keyboard = WarThunderBackendFixture.Binding(profile, "ID_FIRE", 1);
            var changed = WarThunderProfileBackend.ClearBinding(profile, first.Id);
            Assert.True(WarThunderBackendFixture.Binding(changed, "ID_FIRE").Empty);
            var after = WarThunderBackendFixture.Binding(changed, "ID_FIRE", 1);
            Assert.Equal(keyboard.Id, after.Id);
            var document = WarThunderBlkDocument.Parse(WarThunderBackendFixture.Source);
            var original = document.Blocks.Single(block => block.Address == first.Id);
            Assert.Equal(WarThunderBackendFixture.Source[..(original.Start + 1)] + WarThunderBackendFixture.Source[(original.End - 1)..],
                WarThunderBackendFixture.Export(backend, changed));
            Assert.Equal(keyboard.Fields.Select(field => (field.Address, field.Type, field.Value)),
                after.Fields.Select(field => (field.Address, field.Type, field.Value)));
            Assert.Equal(7, backend.GetCatalog(changed).Commands.Count);
            Assert.Throws<InvalidOperationException>(() => WarThunderProfileBackend.ClearBinding(profile, keyboard.Id));
            Assert.Throws<InvalidOperationException>(() => WarThunderProfileBackend.ClearBinding(profile, WarThunderBackendFixture.Binding(profile, "ID_CHORD").Id));
            Assert.Throws<InvalidOperationException>(() => WarThunderProfileBackend.ClearBinding(profile, WarThunderBackendFixture.Binding(profile, "pitch").Id));
        }
        /// <summary>New known hotkey alternatives and existing empty axis blocks use native offsets without invented activation codes.</summary>
        [Fact]
        public void Creation_uses_captured_commands_and_preserves_existing_records()
        {
            var backend = new WarThunderProfileBackend(); var profile = WarThunderBackendFixture.Import(backend);
            var catalog = backend.GetCatalog(profile);
            var empty = catalog.Commands.Single(command => command.Metadata["war-thunder.command"] == "ID_EMPTY");
            var filled = WarThunderProfileBackend.AddBinding(profile, empty.Id, "war-thunder.joystick.1", CommandType.Button, 2);
            Assert.Equal("6", Assert.Single(WarThunderBackendFixture.Binding(filled, "ID_EMPTY").Fields).Value);
            var fire = catalog.Commands.Single(command => command.Metadata["war-thunder.command"] == "ID_FIRE");
            var added = WarThunderProfileBackend.AddBinding(profile, fire.Id, "war-thunder.joystick.1", CommandType.Button, 3);
            Assert.Equal(3, WarThunderProfileBackend.ReadBindings(added).Count(binding => binding.Command == "ID_FIRE"));
            Assert.Equal("9", WarThunderBackendFixture.Field(WarThunderBackendFixture.Binding(added, "ID_FIRE"), "activationType").Value);
            Assert.Equal("7", Assert.Single(WarThunderBackendFixture.Binding(added, "ID_FIRE", 2).Fields).Value);
            var axis = catalog.Commands.Single(command => command.Metadata["war-thunder.command"] == "empty_axis");
            var axisAdded = WarThunderProfileBackend.AddBinding(profile, axis.Id, "war-thunder.joystick.0", CommandType.Axis, 0);
            Assert.Equal("0", Assert.Single(WarThunderBackendFixture.Binding(axisAdded, "empty_axis").Fields).Value);
            var pitch = catalog.Commands.Single(command => command.Metadata["war-thunder.command"] == "pitch");
            Assert.Throws<InvalidOperationException>(() => WarThunderProfileBackend.AddBinding(profile, pitch.Id, "war-thunder.joystick.0", CommandType.Axis, 0));
            Assert.Throws<InvalidOperationException>(() => WarThunderProfileBackend.AddBinding(profile, "missing", "war-thunder.joystick.0", CommandType.Button, 0));
            Assert.Throws<InvalidOperationException>(() => WarThunderProfileBackend.AddBinding(profile, empty.Id, "war-thunder.joystick.0", CommandType.Axis, 0));
            Assert.Equal(WarThunderBackendFixture.Source, WarThunderBackendFixture.Export(backend, profile));
        }
        /// <summary>Negative/out-of-range native indices, unknown devices and unsupported kinds cannot enter output.</summary>
        [Theory]
        [InlineData(-1)]
        [InlineData(4)]
        [InlineData(int.MaxValue)]
        public void Invalid_native_input_indices_leave_original_source_untouched(int index)
        {
            var backend = new WarThunderProfileBackend(); var profile = WarThunderBackendFixture.Import(backend);
            var binding = WarThunderBackendFixture.Binding(profile, "ID_FIRE");
            var input = WarThunderBackendFixture.Field(binding, "joyButton");
            Assert.Throws<InvalidOperationException>(() => WarThunderProfileBackend.RebindInput(profile, binding.Id, input.Address, "war-thunder.joystick.0", CommandType.Button, index));
            Assert.Throws<InvalidOperationException>(() => WarThunderProfileBackend.RebindInput(profile, binding.Id, input.Address, "missing", CommandType.Button, 0));
            Assert.Throws<InvalidOperationException>(() => WarThunderProfileBackend.RebindInput(profile, binding.Id, input.Address, "war-thunder.joystick.0", CommandType.Hat, 0));
            Assert.Equal(WarThunderBackendFixture.Source, WarThunderBackendFixture.Export(backend, profile));
        }
        /// <summary>Overlapping or malformed range declarations remain exportable evidence and block ambiguous writes.</summary>
        [Theory]
        [InlineData("axesOffset:i=2", "axesOffset:i=1")]
        [InlineData("axesCount:i=2", "axesCount:i=-1")]
        [InlineData("axesCount:i=2", "axesCount:i=2147483647")]
        public void Invalid_and_overlapping_ranges_fail_closed_for_edits(string before, string after)
        {
            var backend = new WarThunderProfileBackend(); var source = WarThunderBackendFixture.Source.Replace(before, after, StringComparison.Ordinal);
            var profile = WarThunderBackendFixture.Import(backend, source);
            var binding = WarThunderBackendFixture.Binding(profile, "pitch");
            var field = WarThunderBackendFixture.Field(binding, "axisId");
            Assert.Throws<InvalidOperationException>(() => WarThunderProfileBackend.RebindInput(profile, binding.Id, field.Address, "war-thunder.joystick.1", CommandType.Axis, 0));
            Assert.Equal(source, WarThunderBackendFixture.Export(backend, profile));
            Assert.NotEmpty(backend.Export(profile).Diagnostics);
        }
        /// <summary>Record capabilities reflect the actual supported native operations, including mixed-system preservation.</summary>
        [Fact]
        public void Capabilities_are_specific_to_native_record_shape()
        {
            var backend = new WarThunderProfileBackend(); var profile = WarThunderBackendFixture.Import(backend);
            var fire = WarThunderProfileBackend.GetBindingCapabilities(profile, WarThunderBackendFixture.Binding(profile, "ID_FIRE").Id);
            Assert.True(fire.RebindNativeInputs && fire.ClearJoystickAlternative);
            var chord = WarThunderProfileBackend.GetBindingCapabilities(profile, WarThunderBackendFixture.Binding(profile, "ID_CHORD").Id);
            Assert.True(chord.RebindNativeInputs); Assert.False(chord.ClearJoystickAlternative);
            var pitch = WarThunderBackendFixture.Binding(profile, "pitch");
            var caps = WarThunderProfileBackend.GetBindingCapabilities(profile, pitch.Id);
            Assert.True(caps.SetAxisInverse); Assert.False(caps.ClearJoystickAlternative);
            Assert.Contains(WarThunderBackendFixture.Field(pitch, "kMul").Address, caps.EditableParameterAddresses);
            Assert.DoesNotContain(WarThunderBackendFixture.Field(pitch, "futureParam").Address, caps.EditableParameterAddresses);
            var empty = WarThunderProfileBackend.GetBindingCapabilities(profile, WarThunderBackendFixture.Binding(profile, "ID_EMPTY").Id);
            Assert.True(empty.AddNativeInput);
        }
        #endregion END Public
    }
}
