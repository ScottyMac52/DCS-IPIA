namespace Heimdall.Domain.MultiSim
{
    /// <summary>Identity information for a physical input device.</summary>
    public sealed class PhysicalDeviceIdentity
    {
        #region BEGIN Public

        /// <summary>Gets or sets the Windows device name.</summary>
        public string? WindowsName { get; set; }

        /// <summary>Gets or sets the USB vendor ID.</summary>
        public ushort VendorId { get; set; }

        /// <summary>Gets or sets the USB product ID.</summary>
        public ushort ProductId { get; set; }

        /// <summary>Gets or sets the native Windows or DirectInput instance identifier, distinct from a product or catalog identity.</summary>
        public string? InstanceId { get; set; }

        /// <summary>Gets or sets the composite ID.</summary>
        public int CompositeId { get; set; }

        /// <summary>Gets or sets sim-specific identifiers.</summary>
        public Dictionary<string, string> SimSpecificIds { get; set; } =
            new();

        #endregion END Public
    }
}

