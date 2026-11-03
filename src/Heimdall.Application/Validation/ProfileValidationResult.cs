namespace Heimdall.Application.Validation
{
    /// <summary>Validation findings and the exact native payload checked by the adapter and shared rules.</summary>
    public sealed record ProfileValidationResult(IReadOnlyList<ProfileValidationIssue> Issues, SimProfileExportPlan? ExportPlan = null)
    {
        #region BEGIN Public
        /// <summary>Warnings retain readable native data; errors prevent publication.</summary>
        public bool IsValid => !Issues.Any(issue => issue.IsError);
        /// <summary>Formats actionable errors without discarding their field locations.</summary>
        public string ErrorMessage => string.Join("; ", Issues.Where(issue => issue.IsError).Select(issue => issue.Path + ": " + issue.Message + " Expected: " + issue.Expected + "; actual: " + issue.Actual));
        #endregion END Public
    }
}
