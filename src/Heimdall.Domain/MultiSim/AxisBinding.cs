namespace Heimdall.Domain.MultiSim
{
    /// <summary>Binding of a physical axis to a catalog command.</summary>
    public sealed class AxisBinding
    {
        #region BEGIN Public

        /// <summary>Gets or sets the source axis name.</summary>
        public string SourceAxis { get; set; } = string.Empty;

        /// <summary>Gets or sets the catalog command identifier.</summary>
        public string CommandId { get; set; } = string.Empty;

        /// <summary>Gets or sets the modifier chord required for this axis.</summary>
        public List<Modifier> Modifiers { get; set; } = new();

        /// <summary>Gets or sets the sensitivity multiplier (1.0 = default); unsupported backends reject changes.</summary>
        public float Sensitivity { get; set; } = 1f;

        /// <summary>Gets or sets the deadzone in percentage points from 0 through 100.</summary>
        public float Deadzone { get; set; }

        /// <summary>Gets or sets a value indicating whether the axis is inverted.</summary>
        public bool Invert { get; set; }

        /// <summary>Gets or sets the response curve type.</summary>
        public CurveType Curve { get; set; }

        /// <summary>Gets or sets optional sim-specific axis parameters.</summary>
        public Dictionary<string, object> SimSpecific { get; set; } =
            new();

        #endregion END Public
    }
}

