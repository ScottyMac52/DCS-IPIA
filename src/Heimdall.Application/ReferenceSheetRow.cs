namespace Heimdall.Application
{
    /// <summary>One readable command/input/behaviour entry, independent of a game and output format.</summary>
    /// <param name="Command">Command label, including its activity when relevant.</param>
    /// <param name="Input">Native input and any explicitly identified modifier devices.</param>
    /// <param name="Details">Slots, modes and tuning values.</param>
    public sealed record ReferenceSheetRow(string Command, string Input, string Details);
}
