using Heimdall.Application;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks Free hardware, authenticated owner access and brand-spoof rejection.</summary>
    public sealed class EditionPolicyTests
    {
        #region BEGIN Public
        /// <summary>Free accepts exact reviewed catalog IDs; aliases for other bases and unknown layouts are reserved.</summary>
        [Theory]
        [InlineData("tm-mfd", true)][InlineData("tm-tpr", true)][InlineData("tm-warthog-grip", true)]
        [InlineData("ava-base-f16c", true)][InlineData("ava-base-warthog-grip", true)][InlineData("warthog-base-warthog-grip", true)]
        [InlineData("tm-warthog-throttle", true)][InlineData("viper-tqs-mission-pack", true)]
        [InlineData("tm-sol-r-ava-r-buttons", true)][InlineData("tm-sol-r-ava-r-pov", true)]
        [InlineData("tm-sol-r-ava-l-buttons", true)][InlineData("tm-sol-r-ava-l-pov", true)]
        [InlineData("tm-sol-r-ava", false)][InlineData("tm-sol-r-base", false)]
        [InlineData("tm-sol-r-warthog-r-pov", false)][InlineData("tm-sol-r-moza-r-pov", false)]
        [InlineData("TM-SOL-R-AVA-R-POV", false)]
        [InlineData("grip-f18c", false)][InlineData("moza-ab9-warthog-grip", false)][InlineData("moza-ab9-hornet-grip", false)]
        [InlineData("moza-ab9", false)][InlineData("vkb-f14-gunfighter", false)][InlineData("winctrl-icp", false)]
        [InlineData("generic-anything", false)][InlineData("tm-fake", false)][InlineData("TM-MFD", false)]
        public void Free_uses_an_explicit_hardware_allowlist(string id, bool allowed)
        {
            var policy = new EditionPolicy();
            Assert.Equal(allowed, policy.AllowsHardware(id));
            if (allowed) policy.RequireHardware([id]);
            else Assert.Throws<InvalidOperationException>(() => policy.RequireHardware(["tm-mfd", id]));
        }
        /// <summary>Renaming a non-Thrustmaster controller never supplies vendor evidence.</summary>
        [Theory]
        [InlineData("0402044f-0000-0000-0000-504944564944", true)]
        [InlineData("1234044f-0000-0000-0000-504944564944", true)]
        [InlineData("0402046d-0000-0000-0000-504944564944", false)]
        [InlineData("0402044f-0000-0000-0000-000000000000", false)]
        [InlineData("00000000-0000-0000-0000-000000000000", false)]
        public void Free_requires_native_vendor_evidence(string product, bool allowed) =>
            Assert.Equal(allowed, new EditionPolicy().AllowsDevice(new(Guid.NewGuid(), "Thrustmaster renamed device", true, ProductGuid: Guid.Parse(product))));
        /// <summary>Only a signed entitlement unlocks every feature and preserves the owner's stored appearance.</summary>
        [Fact]
        public void Owner_and_Free_have_distinct_appearance_and_input_entitlements()
        {
            var free = new EditionPolicy();
            Assert.False(free.IsOwner); Assert.Contains("TBA", free.Label);
            Assert.Throws<InvalidOperationException>(free.RequirePremium);
            Assert.Equal(AppearanceTheme.NavyAmber, free.EffectiveTheme(AppearanceTheme.Light));
            using var issuer = new OwnerLicenseTestIssuer(); var owner = issuer.Policy;
            Assert.True(owner.IsOwner); Assert.Contains("Owner", owner.Label);
            owner.RequirePremium(); owner.RequireHardware(["anything"]);
            Assert.True(owner.AllowsDevice(new(Guid.NewGuid(), "Unknown hardware", true)));
            Assert.Equal(AppearanceTheme.CockpitGreen, owner.EffectiveTheme(AppearanceTheme.CockpitGreen));
            Assert.False(new EditionPolicy(issuer.License()).IsOwner);
        }
        #endregion END Public
    }
}
