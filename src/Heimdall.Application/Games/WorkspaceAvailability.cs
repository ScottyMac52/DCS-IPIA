
namespace Heimdall.Application.Games
{
    /// <summary>Explicit eligibility and an explanation when an operation is unavailable.</summary>
    public sealed record WorkspaceAvailability(bool Enabled, string Reason);
}
