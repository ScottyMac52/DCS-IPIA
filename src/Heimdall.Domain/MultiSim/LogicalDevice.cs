namespace Heimdall.Domain.MultiSim
{
    /// <summary>A logical input device independent of any specific sim.</summary>
    public sealed class LogicalDevice
    {
        #region BEGIN Public

        /// <summary>Gets or sets a stable logical identifier.</summary>
        public string LogicalId { get; set; } = string.Empty;

        /// <summary>Gets or sets the binding context, distinct from the physical instance and catalog.</summary>
        public string ContextId { get; set; } = string.Empty;

        /// <summary>Gets or sets the physical device identity.</summary>
        public PhysicalDeviceIdentity? Identity { get; set; }

        /// <summary>Gets or sets axis bindings for this device.</summary>
        public List<AxisBinding> Axes { get; set; } = new();

        /// <summary>Gets or sets button or hat bindings for this device.</summary>
        public List<ButtonBinding> Buttons { get; set; } = new();

        #endregion END Public
    }
}

