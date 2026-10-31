namespace Heimdall.Squadrons
{
    /// <summary>A desktop row retains its native address while presenting captured device ownership and decoded input.</summary>
    /// <param name="Native">Complete native binding evidence.</param>
    /// <param name="CommandName">Captured concept display name.</param>
    /// <param name="Section">Game context.</param>
    /// <param name="DeviceId">Slot-scoped logical reference, not a Windows instance.</param>
    /// <param name="DeviceLabel">Captured slot and name or native system device type.</param>
    public sealed record SquadronsWorkspaceRow(SquadronsBindingRow Native, string CommandName, string Section, string DeviceId, string DeviceLabel)
    {
        #region BEGIN Public
        /// <summary>Gets the catalog name shared by both native directions of one analog command.</summary>
        public string ConceptName { get; init; } = CommandName;
        /// <summary>Gets presentation-only capability review; native slot references are never rewritten by hardware inspection.</summary>
        public string HardwareStatus { get; init; } = "Hardware unverified";
        /// <summary>Gets the exact address used by the native edit boundary.</summary>
        public string RecordId => Native.RecordId;
        /// <summary>Gets a readable input rather than exposing the encoded button-field axis representation.</summary>
        public string InputLabel => Native.Input?.DisplayName ?? SquadronsSystemInputDisplay.Describe(Native);
        /// <summary>Gets a one-based presentation label without changing the native index.</summary>
        public string BindingLabel => "Binding " + (Native.Index + 1);
        /// <summary>Gets whether this record's semantics can be edited by the current adapter.</summary>
        public bool Editable => Native.Type == 3 && Native.Fields.Count == 8 && Native.Fields["modifier"] == 0 && Native.Fields["altbutton"] == 0;
        #endregion END Public
    }
}
