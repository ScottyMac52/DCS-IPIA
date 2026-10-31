namespace Heimdall.Application
{
    /// <summary>Versioned supported controls and rejected object evidence for one controller.</summary>
    /// <param name="SchemaVersion">Capability interpretation format, currently 1.</param>
    /// <param name="Inputs">Supported native objects in deterministic order.</param>
    /// <param name="Unsupported">Unsupported or ambiguous native objects.</param>
    public sealed record ControllerCapabilities(int SchemaVersion, IReadOnlyList<ControllerInput> Inputs,
        IReadOnlyList<UnsupportedControllerObject> Unsupported)
    {
        #region BEGIN Public
        /// <summary>Retains original native enumeration order for adapters whose axis indices depend on it; absent in older captures.</summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<ControllerObject>? NativeObjects { get; init; }
        /// <summary>Gets the complete live-test key set, including eight directions per hat.</summary>
        public IReadOnlyCollection<string> DcsKeys => DirectInputStateDecoder.Decode(new byte[176],
            Inputs.ToDictionary(x => x.DcsKey, x => x.Offset, StringComparer.Ordinal)).Keys.ToArray();
        #endregion END Public
    }
}
