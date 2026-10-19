using System.Text.Json;
using Dcs.Ipia.Domain.MultiSim;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Protects intermediate profile serialization and extension-data preservation.</summary>
    public sealed class MultiSimDomainTests
    {
        private static readonly int[] NativeCurve = [0, 50, 100];
        #region BEGIN Public
        /// <summary>Nested identity, bindings, modifiers and native extension values survive actual JSON serialization.</summary>
        [Fact]
        public void Profile_document_round_trips_nested_bindings_without_sharing_mutable_state()
        {
            var profile = new ProfileDocument
            {
                Name = "Carrier controls",
                Metadata = { ["sim"] = "example-sim", ["formatVersion"] = "1" },
                Devices =
                {
                    new LogicalDevice
                    {
                        LogicalId = "Stick",
                        Identity = new PhysicalDeviceIdentity
                        {
                            WindowsName = "Test stick", VendorId = 0x044F, ProductId = 0x0402,
                            InstanceId = "51fa39b0-cb32-11ed-800a-444553540000", CompositeId = 2,
                            SimSpecificIds = { ["example-sim:instance"] = "native-1026" }
                        },
                        Axes =
                        {
                            new AxisBinding
                            {
                                SourceAxis = "JOY_X", CommandId = "ROLL", Sensitivity = 0.75f,
                                Deadzone = 0.02f, Invert = true, Curve = CurveType.Exponential,
                                SimSpecific = { ["nativeScale"] = 2, ["curvePoints"] = NativeCurve }
                            }
                        },
                        Buttons =
                        {
                            new ButtonBinding
                            {
                                SourceButton = "JOY_BTN1", CommandId = "FIRE", Mode = BindingMode.Release,
                                Modifiers = { new Modifier { Source = "Keyboard:SHIFT", Value = "held" } },
                                SimSpecific = { ["context"] = "COCKPIT", ["nativeFlag"] = true }
                            }
                        }
                    }
                }
            };
            var json = JsonSerializer.Serialize(profile);
            var restored = JsonSerializer.Deserialize<ProfileDocument>(json)!;
            Assert.Equal(json, JsonSerializer.Serialize(restored));
            Assert.Equal("Carrier controls", restored.Name);
            Assert.Equal("example-sim", restored.Metadata["sim"]);
            var device = Assert.Single(restored.Devices);
            Assert.Equal("Stick", device.LogicalId);
            var identity = device.Identity!;
            Assert.Equal("Test stick", identity.WindowsName);
            Assert.Equal((ushort)0x044F, identity.VendorId);
            Assert.Equal((ushort)0x0402, identity.ProductId);
            Assert.Equal("51fa39b0-cb32-11ed-800a-444553540000", identity.InstanceId);
            Assert.Equal(2, identity.CompositeId);
            Assert.Equal("native-1026", identity.SimSpecificIds["example-sim:instance"]);
            var axis = Assert.Single(device.Axes);
            Assert.Equal("JOY_X", axis.SourceAxis);
            Assert.Equal("ROLL", axis.CommandId);
            Assert.Equal(0.75f, axis.Sensitivity);
            Assert.Equal(0.02f, axis.Deadzone);
            Assert.True(axis.Invert);
            Assert.Equal(CurveType.Exponential, axis.Curve);
            // Object-valued extension data returns as JsonElement, not its original boxed CLR type.
            Assert.Equal(2, Assert.IsType<JsonElement>(axis.SimSpecific["nativeScale"]).GetInt32());
            Assert.Equal(3, Assert.IsType<JsonElement>(axis.SimSpecific["curvePoints"]).GetArrayLength());
            var button = Assert.Single(device.Buttons);
            Assert.Equal("JOY_BTN1", button.SourceButton);
            Assert.Equal("FIRE", button.CommandId);
            Assert.Equal(BindingMode.Release, button.Mode);
            var modifier = Assert.Single(button.Modifiers);
            Assert.Equal("Keyboard:SHIFT", modifier.Source);
            Assert.Equal("held", modifier.Value);
            Assert.Equal("COCKPIT", Assert.IsType<JsonElement>(button.SimSpecific["context"]).GetString());
            Assert.True(Assert.IsType<JsonElement>(button.SimSpecific["nativeFlag"]).GetBoolean());
            modifier.Value = "changed";
            identity.SimSpecificIds["example-sim:instance"] = "another-unit";
            Assert.Equal("held", profile.Devices[0].Buttons[0].Modifiers[0].Value);
            Assert.Equal("native-1026", profile.Devices[0].Identity!.SimSpecificIds["example-sim:instance"]);
        }

        /// <summary>Command identity, context and extension metadata survive independently of aircraft naming conventions.</summary>
        [Theory]
        [InlineData(CommandType.Button)]
        [InlineData(CommandType.Axis)]
        [InlineData(CommandType.Hat)]
        [InlineData(CommandType.Other)]
        public void Commands_round_trip_catalog_context(CommandType type)
        {
            var command = new SimCommand
            {
                Id = "example:flight:roll",
                DisplayName = "Roll",
                Category = "Flight",
                Type = type,
                IsModuleSpecific = true,
                Context = "vehicle:example",
                Metadata = { ["nativeCommand"] = "ROLL_NATIVE" }
            };
            var restored = JsonSerializer.Deserialize<SimCommand>(JsonSerializer.Serialize(command))!;
            Assert.Equal(command.Id, restored.Id);
            Assert.Equal(command.DisplayName, restored.DisplayName);
            Assert.Equal(command.Category, restored.Category);
            Assert.Equal(type, restored.Type);
            Assert.True(restored.IsModuleSpecific);
            Assert.Equal(command.Context, restored.Context);
            Assert.Equal("ROLL_NATIVE", restored.Metadata["nativeCommand"]);
        }

        /// <summary>Omitted optional state stays distinct from a discovered device or configured native binding.</summary>
        [Fact]
        public void Defaults_preserve_empty_collections_and_optional_values()
        {
            var profile = JsonSerializer.Deserialize<ProfileDocument>("{}")!;
            Assert.Empty(profile.Name);
            Assert.Empty(profile.Devices);
            Assert.Empty(profile.Metadata);
            var device = JsonSerializer.Deserialize<LogicalDevice>("{}")!;
            Assert.Empty(device.LogicalId);
            Assert.Null(device.Identity);
            Assert.Empty(device.Axes);
            Assert.Empty(device.Buttons);
            var axis = JsonSerializer.Deserialize<AxisBinding>("{}")!;
            Assert.Equal(1f, axis.Sensitivity);
            Assert.Equal(CurveType.Linear, axis.Curve);
            var button = JsonSerializer.Deserialize<ButtonBinding>("{}")!;
            Assert.Equal(BindingMode.Press, button.Mode);
            Assert.Empty(button.Modifiers);
            Assert.Null(new Modifier().Value);
            Assert.Null(new PhysicalDeviceIdentity().InstanceId);
            Assert.Null(new SimCommand().Context);
        }
        #endregion END Public
    }
}
