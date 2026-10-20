namespace Heimdall.Application
{
    /// <summary>Fixed diagnostic stage and recovery guidance; excludes exception messages and stacks.</summary>
    /// <param name="Stage">Application-owned stage code, optionally with a report-local ordinal.</param>
    /// <param name="Category">Application-owned failure category.</param>
    /// <param name="NextStep">Non-identifying guidance.</param>
    public sealed record SupportFailure(string Stage, string Category, string NextStep);
}

