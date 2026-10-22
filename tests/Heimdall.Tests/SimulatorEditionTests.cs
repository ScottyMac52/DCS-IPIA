using Heimdall.Application;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects the Free DCS contract and distinct signed Premium/Owner identities.</summary>
    public sealed class SimulatorEditionTests
    {
        #region BEGIN Public
        /// <summary>Only the exact DCS identifier is Free; future integrations default to paid access.</summary>
        [Theory]
        [InlineData("Free", "dcs", true)][InlineData("Free", "elite-dangerous", false)]
        [InlineData("Free", "future-simulator", false)][InlineData("Free", "DCS", false)]
        [InlineData("Free", "", false)][InlineData("Free", " ", false)]
        [InlineData("Premium", "dcs", true)][InlineData("Premium", "elite-dangerous", true)]
        [InlineData("Premium", "future-simulator", true)][InlineData("Premium", "", false)]
        [InlineData("Owner", "dcs", true)][InlineData("Owner", "elite-dangerous", true)]
        [InlineData("Owner", "future-simulator", true)][InlineData("Owner", "", false)]
        public void Simulator_access_defaults_to_Premium(string edition, string simulator, bool allowed)
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var policy = edition == "Free" ? new EditionPolicy() : new EditionPolicy(
                issuer.License(new(1, "HEIMDALL", edition, Guid.NewGuid(), "Contract test")), issuer.Verifier);
            Assert.Equal(allowed, policy.AllowsSimulator(simulator));
            if (allowed) policy.RequireSimulator(simulator);
            else Assert.Throws<InvalidOperationException>(() => policy.RequireSimulator(simulator));
            Assert.Equal(edition == "Owner", policy.IsOwner);
            Assert.Equal(edition == "Premium", policy.IsPremium);
            Assert.Equal(edition != "Free", policy.HasPremium);
        }

        /// <summary>Customer Premium unlocks paid features without impersonating the application owner.</summary>
        [Fact]
        public void Premium_has_feature_access_but_is_not_Owner()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var premium = issuer.PremiumPolicy;
            Assert.False(premium.IsOwner); Assert.True(premium.IsPremium); Assert.Contains("Premium Edition", premium.Label);
            premium.RequirePremium(); premium.RequireHardware(["generic-controller", "moza-ab9"]);
            Assert.True(premium.AllowsDevice(new(Guid.NewGuid(), "Unknown controller", true)));
            Assert.Equal(AppearanceTheme.CockpitGreen, premium.EffectiveTheme(AppearanceTheme.CockpitGreen));
            var signed = issuer.License(new(1, "HEIMDALL", "Premium", Guid.NewGuid(), "Customer"));
            Assert.False(new EditionPolicy(signed).HasPremium);
        }

        /// <summary>Premium installation survives reload; invalid imports preserve it and removing it safely restores Free.</summary>
        [Fact]
        public void Signed_Premium_and_Owner_can_be_switched_without_a_test_bypass()
        {
            var root = Path.Combine(Path.GetTempPath(), "heimdall-edition-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                using var issuer = new OwnerLicenseTestIssuer();
                var source = Path.Combine(root, "source.json"); var target = Path.Combine(root, "accepted.json");
                var store = new FileOwnerLicenseStore(target, issuer.Verifier);
                var premium = issuer.License(new(1, "HEIMDALL", "Premium", Guid.NewGuid(), "Customer"));
                File.WriteAllText(source, premium); Assert.True(store.Import(source));
                var reloaded = new FileOwnerLicenseStore(target, issuer.Verifier).Load();
                Assert.True(reloaded.IsPremium); Assert.False(reloaded.IsOwner); reloaded.RequireSimulator("elite-dangerous");
                File.WriteAllText(source, "{\"Edition\":\"Owner\"}"); Assert.False(store.Import(source));
                Assert.Equal(premium, File.ReadAllText(target)); Assert.True(store.Load().IsPremium);
                File.Delete(target); Assert.False(store.Load().HasPremium); Assert.True(store.Load().AllowsSimulator("dcs"));
                File.WriteAllText(source, issuer.License()); Assert.True(store.Import(source));
                Assert.True(store.Load().IsOwner); Assert.False(store.Load().IsPremium);
            }
            finally { Directory.Delete(root, true); }
        }
        #endregion END Public
    }
}
