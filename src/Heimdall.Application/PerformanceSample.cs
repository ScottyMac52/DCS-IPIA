namespace Heimdall.Application
{
    /// <summary>One inclusive elapsed-time measurement with fixed operation and simulator tags.</summary>
    public sealed record PerformanceSample(string Operation, string Component, double DurationMs);
}
