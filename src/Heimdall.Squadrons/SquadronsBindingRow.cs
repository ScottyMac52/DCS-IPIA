namespace Heimdall.Squadrons
{
    /// <summary>One native indexed record, including unsupported types and fields retained for lossless inspection.</summary>
    /// <param name="Concept">Fully scoped native input concept.</param>
    /// <param name="Index">Native record index, not an ED-style Primary/Secondary slot.</param>
    /// <param name="Fields">Original numeric fields, including identifier and negate.</param>
    public sealed record SquadronsBindingRow(string Concept, int Index, IReadOnlyDictionary<string, int> Fields)
    {
        #region BEGIN Public
        /// <summary>Gets the exact native address of this record.</summary>
        public string RecordId => Concept + "." + Index;
        /// <summary>Gets the native input type; three identifies a joystick record in captured version four files.</summary>
        public int Type => Fields["type"];
        /// <summary>Gets the zero-based saved device reference; it is not a physical instance identity.</summary>
        public int DeviceId => Fields["deviceid"];
        /// <summary>Gets a verified joystick interpretation, or null for unassigned and unsupported encodings.</summary>
        public SquadronsInput? Input => Type == 3 ? SquadronsInputCodec.Decode(Fields["axis"], Fields["button"]) : null;
        #endregion END Public
    }
}
