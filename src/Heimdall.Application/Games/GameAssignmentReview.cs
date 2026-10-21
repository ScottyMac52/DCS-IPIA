namespace Heimdall.Application.Games
{
    /// <summary>Non-mutating native validation and semantic change detection for an assignment draft.</summary>
    /// <param name="Valid">Whether the adapter can represent the complete edit.</param>
    /// <param name="Changed">Whether native output differs from current output.</param>
    /// <param name="Message">Validation feedback or empty for a valid draft.</param>
    public sealed record GameAssignmentReview(bool Valid, bool Changed, string Message);
}
