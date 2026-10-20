namespace Heimdall.Domain.MultiSim
{
    /// <summary>Binding of a button or hat to a catalog command.</summary>
    public sealed class ButtonBinding
    {
        #region BEGIN Public

        /// <summary>Gets or sets the source button identifier.</summary>
        public string SourceButton { get; set; } = string.Empty;

        /// <summary>Gets or sets the catalog command identifier.</summary>
        public string CommandId { get; set; } = string.Empty;

        /// <summary>Gets or sets modifiers required for this binding.</summary>
        public List<Modifier> Modifiers { get; set; } = new();

        /// <summary>Gets or sets the binding mode.</summary>
        public BindingMode Mode { get; set; }

        /// <summary>Gets or sets optional sim-specific parameters.</summary>
        public Dictionary<string, object> SimSpecific { get; set; } =
            new();

        #endregion END Public
    }
}
