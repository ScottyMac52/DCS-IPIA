namespace Heimdall.Application
{
    /// <summary>Profile-owned completion and the confirmed default destination; null preserves legacy preferences.</summary>
    /// <param name="Completed">Whether a reviewed setup transaction committed.</param>
    /// <param name="Destination">Confirmed destination, empty for offline setup, or null before review.</param>
    public sealed record SetupState(bool Completed, string? Destination);
}
