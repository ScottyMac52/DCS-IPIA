namespace Dcs.Ipia.Application
{
    /// <summary>Defines the initial Free release and the authenticated all-feature owner entitlement.</summary>
    public sealed class EditionPolicy
    {
        private static readonly HashSet<string> FreeHardware = new(StringComparer.Ordinal) {
            "tm-warthog-grip", "ava-base-f16c", "ava-base-warthog-grip", "warthog-base-warthog-grip",
            "tm-mfd", "tm-tpr", "tm-warthog-throttle", "viper-tqs-mission-pack" };
        private readonly OwnerLicenseClaims? _owner;
        /// <summary>Constructs Free unless the supplied document authenticates against the issuer key.</summary>
        public EditionPolicy(string? license = null, OwnerLicenseVerifier? verifier = null) =>
            _owner = license is null ? null : (verifier ?? new OwnerLicenseVerifier()).Verify(license);
        #region BEGIN Public
        /// <summary>Gets whether all application features are unlocked for the owner.</summary>
        public bool IsOwner => _owner is not null;
        /// <summary>Gets a non-sensitive edition label for settings and release evidence.</summary>
        public string Label => IsOwner ? "Owner — all features unlocked" : "Free Edition — Premium availability and pricing TBA";
        /// <summary>Gets the fixed Free Edition theme without rewriting saved user preferences.</summary>
        public AppearanceTheme EffectiveTheme(AppearanceTheme saved) => IsOwner ? saved : AppearanceTheme.NavyAmber;
        /// <summary>Allows only explicitly reviewed Thrustmaster catalog identities in Free.</summary>
        public bool AllowsHardware(string hardwareId) => IsOwner || FreeHardware.Contains(hardwareId);
        /// <summary>Accepts native HID Thrustmaster vendor evidence, never a user-editable device name.</summary>
        public bool AllowsDevice(DetectedDcsDevice device)
        {
            var product = device.ProductGuid.ToString("D");
            return IsOwner || product.EndsWith("504944564944", StringComparison.OrdinalIgnoreCase) &&
                (device.ProductGuid.ToByteArray()[0] | device.ProductGuid.ToByteArray()[1] << 8) == 0x044F;
        }
        /// <summary>Rejects unlicensed hardware operations with a consistent actionable message.</summary>
        public void RequireHardware(IEnumerable<string> hardwareIds)
        {
            if (hardwareIds.Any(id => !AllowsHardware(id)))
                throw new InvalidOperationException("Free Edition supports the reviewed Thrustmaster catalog only. This workspace contains reserved or unknown hardware. Your saved data is unchanged. Premium availability TBA.");
        }
        /// <summary>Rejects reserved appearance or live-input operations before side effects.</summary>
        public void RequireOwner()
        {
            if (!IsOwner) throw new InvalidOperationException("This feature is reserved for the licensed edition. Premium availability and pricing TBA.");
        }
        #endregion END Public
    }
}
