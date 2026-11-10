using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Domain.MultiSim;
using Heimdall.EliteDangerous;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks readable import evidence without inventing physical links or altering bindings.</summary>
    public sealed class EliteImportedControllerInfoTests
    {
        #region BEGIN Public
        /// <summary>Repeated products retain exact candidates and ignore historical connection flags.</summary>
        [Fact]
        public void Product_candidates_are_named_but_never_confirmed()
        {
            var native = Native();
            var second = native with { PhysicalGuid = Guid.NewGuid() };
            var inventory = Inventory(native);
            var device = Reference("044F0432");
            device.Buttons.Add(new() { SourceButton = "Joy_1" });
            device.Axes.Add(new() { SourceAxis = "Joy_XAxis" });
            var info = EliteImportedControllerInfo.Create(device, null, inventory, [native]);
            Assert.Equal("Right SOL-R", info.Name);
            Assert.Contains("Suggested product match", info.Summary);
            Assert.Contains("Needs identification", info.Summary);
            Assert.Contains("1 button/hat bindings · 1 axis bindings", info.Summary);
            Assert.Contains("044F0432 · ED index 0", info.Details);
            Assert.Null(device.Identity!.InstanceId);
            Assert.Equal("Joy_1", Assert.Single(device.Buttons).SourceButton);
            inventory = inventory with
            {
                Instances = [inventory.Instances[0], inventory.Instances[0] with { PhysicalGuid = second.PhysicalGuid, Label = "Left SOL-R" }],
                Observations = [new(native, null), new(second, null)]
            };
            info = EliteImportedControllerInfo.Create(device, null, inventory, [native]);
            Assert.Contains("2 matching units", info.Summary);
            Assert.Contains("Left SOL-R", info.Name);
            Assert.Contains(second.PhysicalGuid + " · Offline", info.Details);
            Assert.Contains(native.PhysicalGuid + " · Connected", info.Details);
        }

        /// <summary>An existing confirmed pin remains offline even if a similar product is connected.</summary>
        [Fact]
        public void Confirmed_link_uses_exact_instance_availability()
        {
            var native = Native();
            var pin = new GameHardwarePin(null, native);
            var device = Reference("044F0432");
            var info = EliteImportedControllerInfo.Create(device, pin, Inventory(native), [native with { PhysicalGuid = Guid.NewGuid() }]);
            Assert.Equal("Right SOL-R", info.Name);
            Assert.Contains("Confirmed link · Offline / unavailable", info.Summary);
            Assert.Contains(native.PhysicalGuid.ToString(), info.Details);
            Assert.Contains("Confirmed link · Connected", EliteImportedControllerInfo.Create(device, pin, null, [native]).Summary);
        }

        /// <summary>Unknown references retain their token and system inputs need no physical association.</summary>
        [Theory]
        [InlineData("Unknown", "Unidentified")]
        [InlineData("Keyboard", "System input")]
        [InlineData("Mouse", "System input")]
        public void Unknown_and_system_references_are_not_guessed(string token, string status)
        {
            var info = EliteImportedControllerInfo.Create(Reference(token), null, null, []);
            Assert.StartsWith(token, info.Name);
            Assert.StartsWith(status, info.Summary);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static LogicalDevice Reference(string token) => new()
        {
            LogicalId = "test", Identity = new() { SimSpecificIds = new() { ["ed.device"] = token, ["ed.deviceIndex"] = "0" } }
        };
        private static DetectedDcsDevice Native() => new(Guid.NewGuid(), "AVA [R] Sol-R", true,
            ProductGuid: Guid.Parse("0432044f-0000-0000-0000-504944564944"));
        private static HardwareInventorySnapshot Inventory(DetectedDcsDevice device) => new(1, [],
            [new(device.PhysicalGuid, device.Name, "definition", "Right SOL-R", "Pilot", true, [])], [new(device, null)]);
        #endregion END private helpers
    }
}
