using Dcs.Ipia.Application;
using Dcs.Ipia.TestSupport;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Protects Free inventory retention without turning names, stale identities or missing evidence into vendor verification.</summary>
    public sealed class InventoryEditionPolicyTests
    {
        private static readonly Guid Physical = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid Other = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid Thrustmaster = Guid.Parse("0402044f-0000-0000-0000-504944564944");
        private static readonly Guid NonThrustmaster = Guid.Parse("0402046d-0000-0000-0000-504944564944");
        #region BEGIN Public
        /// <summary>Connected observations verify new units; offline units require matching saved native manufacturer evidence.</summary>
        [Theory]
        [InlineData(true, true, true, true)]
        [InlineData(true, false, true, false)]
        [InlineData(false, true, true, true)]
        [InlineData(false, false, true, false)]
        [InlineData(false, true, false, false)]
        [InlineData(false, false, false, false)]
        public void Selected_units_require_current_or_persisted_vendor_evidence(bool current, bool tm, bool adopted, bool allowed)
        {
            var observation = Observation(tm ? Thrustmaster : NonThrustmaster);
            var saved = Saved(adopted, observation);
            var candidate = new HardwareInventoryCandidate(Physical, saved.Instances.SingleOrDefault(), current ? observation : null, false);
            var edit = Edit(candidate);
            var policy = new EditionPolicy();
            if (allowed) policy.RequireInventory(saved, [edit]);
            else
            {
                var error = Assert.Throws<InvalidOperationException>(() => policy.RequireInventory(saved, [edit]));
                Assert.Contains("My panel", error.Message); Assert.Contains("Rescan", error.Message);
            }
        }
        /// <summary>Fresh discovery wins even when it contradicts a previous manufacturer identity.</summary>
        [Theory]
        [InlineData(true)][InlineData(false)]
        public void Current_observation_cannot_be_overridden_by_history(bool currentTm)
        {
            var saved = Saved(true, Observation(currentTm ? NonThrustmaster : Thrustmaster));
            var candidate = new HardwareInventoryCandidate(Physical, saved.Instances.Single(), Observation(currentTm ? Thrustmaster : NonThrustmaster), false);
            if (currentTm) new EditionPolicy().RequireInventory(saved, [Edit(candidate)]);
            else Assert.Throws<InvalidOperationException>(() => new EditionPolicy().RequireInventory(saved, [Edit(candidate)]));
        }
        /// <summary>Disconnected discovery and observations from another physical unit cannot authorize adoption.</summary>
        [Theory]
        [InlineData(true)][InlineData(false)]
        public void New_adoption_requires_connected_matching_identity(bool connected)
        {
            var observed = Observation(Thrustmaster) with { Device = Observation(Thrustmaster).Device with { PhysicalGuid = connected ? Other : Physical, Connected = connected } };
            var candidate = new HardwareInventoryCandidate(Physical, null, observed, false);
            Assert.Throws<InvalidOperationException>(() => new EditionPolicy().RequireInventory(Saved(false, observed), [Edit(candidate)]));
        }
        /// <summary>Selection alone, a matching name and unrelated historical observations are not vendor evidence.</summary>
        [Fact]
        public void Unverified_saved_units_need_a_rescan_and_blank_labels_use_the_identity()
        {
            var observation = Observation(Thrustmaster);
            var saved = Saved(true, observation);
            var candidate = new HardwareInventoryCandidate(Physical, saved.Instances.Single(), null, false);
            var edit = Edit(candidate) with { Label = " " };
            foreach (var history in new IReadOnlyList<ControllerObservation>[] { [], [observation with { Device = observation.Device with { PhysicalGuid = Other } }], [Observation(Guid.Empty)] })
            {
                var error = Assert.Throws<InvalidOperationException>(() => new EditionPolicy().RequireInventory(saved with { Observations = history }, [edit]));
                Assert.Contains(Physical.ToString("D"), error.Message);
            }
            Assert.Throws<InvalidOperationException>(() => new EditionPolicy().RequireInventory(saved with { Instances = [] }, [edit]));
        }
        /// <summary>Unselected reserved records remain manageable and Owner retains unrestricted access.</summary>
        [Fact]
        public void Deselection_and_owner_access_do_not_require_manufacturer_evidence()
        {
            var saved = Saved(true, Observation(NonThrustmaster));
            var edit = Edit(new(Physical, saved.Instances.Single(), null, false));
            new EditionPolicy().RequireInventory(saved, [edit with { Selected = false }]);
            new EditionPolicy().RequireInventory(saved, []);
            using var issuer = new OwnerLicenseTestIssuer();
            issuer.Policy.RequireInventory(saved, [edit]);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static ControllerObservation Observation(Guid product) => new(new(Physical, "Thrustmaster renamed panel", true, ProductGuid: product),
            ControllerCapabilityMapper.Map([new(Guid.NewGuid(), 12, "Button")]));
        private static HardwareInventorySnapshot Saved(bool adopted, ControllerObservation observation)
        {
            var caps = observation.Capabilities!;
            var definition = new HardwareDefinition(ControllerCapabilityMapper.Fingerprint(caps), caps);
            return new(1, adopted ? [definition] : [], adopted ? [new(Physical, "Thrustmaster renamed panel", definition.Id, "My panel", "", true, [])] : [],
                [observation with { Device = observation.Device with { Connected = false } }]);
        }
        private static HardwareInventoryEdit Edit(HardwareInventoryCandidate candidate) => new(candidate, true, "My panel", "", false, []);
        #endregion END private helpers
    }
}
