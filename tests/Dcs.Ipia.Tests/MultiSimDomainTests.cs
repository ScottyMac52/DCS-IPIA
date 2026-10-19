using Dcs.Ipia.Domain;
using Dcs.Ipia.Domain.MultiSim;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Exercises multi-sim domain types.</summary>
    public sealed class MultiSimDomainTests
    {
        #region BEGIN Public

        /// <summary>Profile document round-trips basic structure.</summary>
        [Fact]
        public void Profile_document_holds_devices_and_metadata()
        {
            var profile = new ProfileDocument
            {
                Name = "Test",
                Metadata = { ["sim"] = "msfs2020" }
            };
            var device = new LogicalDevice
            {
                LogicalId = "Stick",
                Identity = new PhysicalDeviceIdentity
                {
                    WindowsName = "Warthog",
                    VendorId = 0x044F,
                    ProductId = 0x0402,
                    Guid = "{51FA39B0-CB32-11ED-800A-444553540000}",
                    CompositeId = 0,
                    SimSpecificIds = { ["msfs"] = "1026" }
                }
            };
            device.Axes.Add(new AxisBinding
            {
                SourceAxis = "X",
                CommandId = "AILERON",
                Sensitivity = 1f,
                Deadzone = 0.02f,
                Invert = true,
                Curve = CurveType.Exponential,
                SimSpecific = { ["flag"] = 2 }
            });
            device.Buttons.Add(new ButtonBinding
            {
                SourceButton = "Button1",
                CommandId = "FIRE",
                Mode = BindingMode.Press,
                Modifiers = { new Modifier { Source = "SHIFT" } },
                SimSpecific = { ["context"] = "COCKPIT" }
            });
            profile.Devices.Add(device);

            Assert.Equal("Test", profile.Name);
            Assert.Equal("msfs2020", profile.Metadata["sim"]);
            Assert.Single(profile.Devices);
            Assert.Equal("Stick", profile.Devices[0].LogicalId);
            Assert.Equal(0x044F, profile.Devices[0].Identity!.VendorId);
            Assert.Equal(CurveType.Exponential, profile.Devices[0].Axes[0].Curve);
            Assert.Equal(BindingMode.Press, profile.Devices[0].Buttons[0].Mode);
            Assert.Equal(CommandType.Axis, new SimCommand
            {
                Id = "AILERON",
                DisplayName = "Aileron",
                Category = "Flight",
                Type = CommandType.Axis
            }.Type);
        }

        #endregion END Public
    }
}
