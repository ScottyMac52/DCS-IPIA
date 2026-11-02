namespace Heimdall.WarThunder
{
    /// <summary>One game-native local slot linked to neutral reported evidence and driver storage tags; storage tags are not physical control identities.</summary>
    public sealed record WarThunderInputSlot(string InputId, int NativeIndex, IReadOnlyList<string> StorageKeys, bool Virtual);
}
