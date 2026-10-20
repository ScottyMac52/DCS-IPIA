namespace Dcs.Ipia.Application.Games
{
    /// <summary>An explicit native unit with optional shared artwork; game product tokens never supply manufacturer evidence.</summary>
    /// <param name="HardwareId">Optional reviewed shared catalog definition, independent of the physical unit.</param>
    /// <param name="Device">Native observation retained for offline authoring.</param>
    public sealed record GameHardwarePin(string? HardwareId, DetectedDcsDevice Device)
    {
        #region BEGIN Public
        /// <summary>Hides illustration independently of a retained reviewed catalog identity; older pins remain visible by default.</summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        public bool DrawingHidden { get; init; }
        #endregion END Public
    }
}
