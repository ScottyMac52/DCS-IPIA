namespace Heimdall.Application.Validation
{
    /// <summary>A stable, field-addressed validation finding with reviewable expected and actual values.</summary>
    public sealed record ProfileValidationIssue(string Code, string Path, string Expected, string Actual, string Message, bool IsError = true);
}
