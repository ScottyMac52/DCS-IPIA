using Dcs.Ipia.Application;
using Dcs.Ipia.Domain.MultiSim;
using Dcs.Ipia.EliteDangerous;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks native target compatibility independently from display names, catalog choices and instance resolution.</summary>
    public sealed class EliteDeviceReferenceMatcherTests
    {
        #region BEGIN Public
        /// <summary>Only reviewed aliases or matching numeric products can associate with a native observed unit.</summary>
        [Theory]
        [InlineData("044F1001", "1001044f-0000-0000-0000-504944564944", true)]
        [InlineData("044f1001", "1001044f-0000-0000-0000-504944564944", true)]
        [InlineData("044F1002", "1001044f-0000-0000-0000-504944564944", false)]
        [InlineData("06A30BAC", "1001044f-0000-0000-0000-504944564944", false)]
        [InlineData("ThrustMasterWarthogJoystick", "0402044f-0000-0000-0000-504944564944", true)]
        [InlineData("ThrustMasterWarthogThrottle", "0404044f-0000-0000-0000-504944564944", true)]
        [InlineData("ThrustMasterWarthogJoystick", "0404044f-0000-0000-0000-504944564944", false)]
        [InlineData("Thrustmaster-looking name", "1001044f-0000-0000-0000-504944564944", false)]
        [InlineData("ZZZZ1001", "1001044f-0000-0000-0000-504944564944", false)]
        [InlineData("00000000", "00000000-0000-0000-0000-000000000000", false)]
        [InlineData("", "1001044f-0000-0000-0000-504944564944", false)]
        public void Native_product_compatibility_is_explicit(string token, string product, bool expected)
        {
            var identity = new PhysicalDeviceIdentity { WindowsName = "Untrusted display label", SimSpecificIds = new() { ["ed.device"] = token } };
            var observed = new DetectedDcsDevice(Guid.NewGuid(), "Thrustmaster-looking name", true, ProductGuid: Guid.Parse(product));
            Assert.Equal(expected, new EliteDeviceReferenceMatcher().Matches(identity, observed));
            Assert.Null(identity.InstanceId);
            Assert.Equal(token, identity.SimSpecificIds["ed.device"]);
        }

        /// <summary>Missing native evidence and unsupported products cannot become eligible by naming them.</summary>
        [Fact]
        public void Missing_evidence_is_unresolved_and_product_matching_never_selects_a_unit()
        {
            var matcher = new EliteDeviceReferenceMatcher();
            var device = new DetectedDcsDevice(Guid.NewGuid(), "Controller", true, ProductGuid: Guid.Parse("1001044f-0000-0000-0000-504944564944"));
            Assert.False(matcher.Matches(new(), device));
            Assert.False(matcher.Matches(new() { SimSpecificIds = null! }, device));
            var identity = new PhysicalDeviceIdentity { SimSpecificIds = new() { ["ed.device"] = "044F1001" } };
            Assert.True(matcher.Matches(identity, device));
            Assert.True(matcher.Matches(identity, device with { PhysicalGuid = Guid.NewGuid() }));
            Assert.Equal("unresolved", new EliteDangerousProfileBackend().ResolveDevice(identity, []).Status);
            Assert.Throws<ArgumentNullException>(() => matcher.Matches(null!, device));
            Assert.Throws<ArgumentNullException>(() => matcher.Matches(identity, null!));
        }
        #endregion END Public
    }
}
