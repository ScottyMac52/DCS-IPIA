namespace Heimdall.Dcs
{
    /// <summary>One editable shared UI Layer catalog entry with stable identity and DCS command code.</summary>
    /// <param name="Id">Stable catalog identity.</param>
    /// <param name="Command">DCS command code.</param>
    /// <param name="Label">Display name.</param>
    /// <param name="Category">Command category.</param>
    public sealed record SharedUiLayerCommand(string Id, string Command, string Label, string Category);
}
