namespace Heimdall.App.Smoke
{
    /// <summary>One measured workflow phase, including failed and over-budget UI work.</summary>
    /// <param name="Name">Stable phase label.</param>
    /// <param name="DurationMs">Inclusive wall time.</param>
    /// <param name="AllocatedBytes">Process-wide managed allocation delta.</param>
    /// <param name="Interactive">Whether the UI responsiveness budget applies.</param>
    internal sealed record ConfigurationPerformanceStep(string Name, double DurationMs, long AllocatedBytes, bool Interactive);
}
