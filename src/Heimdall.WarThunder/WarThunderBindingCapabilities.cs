namespace Heimdall.WarThunder
{
    /// <summary>Native record operations are separate from portable binding modes and physical control translation.</summary>
    public sealed record WarThunderBindingCapabilities(bool RebindNativeInputs, bool ClearJoystickAlternative,
        bool AddNativeInput, bool SetAxisInverse, IReadOnlyList<string> EditableParameterAddresses);
}
