namespace Dcs.Ipia.Application
{
    /// <summary>Report-local device ordinal and capability/mapping evidence; never a persistent identity.</summary>
    /// <param name="Number">One-based ordinal meaningful only inside this report.</param>
    /// <param name="Selected">Saved selection, not current connectivity.</param>
    /// <param name="SupportedInputs">Enumerated supported objects.</param>
    /// <param name="UnsupportedInputs">Rejected or ambiguous objects.</param>
    /// <param name="TemplateCandidates">Saved compatibility hints, not confirmed mappings.</param>
    /// <param name="ObservationFailed">Whether saved discovery evidence recorded a failure.</param>
    public sealed record SupportDeviceStatus(int Number, bool Selected, int SupportedInputs,
        int UnsupportedInputs, int TemplateCandidates, bool ObservationFailed);
}

