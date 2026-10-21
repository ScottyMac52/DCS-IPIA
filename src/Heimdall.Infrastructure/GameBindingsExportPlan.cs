namespace Heimdall.Infrastructure
{
    /// <summary>A reviewed single-preset write and the expected existing destination revision.</summary>
    /// <param name="Directory">User-selected absolute bindings directory.</param>
    /// <param name="FileName">Validated named preset basename.</param>
    /// <param name="Content">Validated native preset XML.</param>
    /// <param name="ExpectedHash">Existing bytes' revision, or null for a new preset.</param>
    public sealed record GameBindingsExportPlan(string Directory, string FileName, string Content, string? ExpectedHash);
}
