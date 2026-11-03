namespace Heimdall.WarThunder
{
    /// <summary>Joins native slots to authored input aliases through reported object identity, independently of driver storage selection.</summary>
    internal static class WarThunderDrawingAliases
    {
        #region BEGIN Public
        internal static IReadOnlyList<string> ForSlot(WarThunderInputSlot slot, IReadOnlyDictionary<string, string> aliases)
        {
            if (!slot.Virtual) return aliases.TryGetValue(slot.InputId, out var key) ? [key] : [];
            var separator = slot.InputId.LastIndexOf('/');
            return separator >= 0 && aliases.ContainsKey(slot.InputId[..separator]) ? slot.StorageKeys : [];
        }
        #endregion END Public
    }
}
