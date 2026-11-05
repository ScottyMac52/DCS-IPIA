namespace Heimdall.Application
{
    /// <summary>One saved physical device and its reviewed replacement eligibility.</summary>
    /// <param name="Workspace">Expected saved module used for stale-write checking.</param>
    /// <param name="Diagram">Current physical device drawing.</param>
    /// <param name="Review">Identity conflicts and artwork differences.</param>
    /// <param name="AlreadyApplied">Whether the selected revision is already pinned.</param>
    public sealed record DcsDrawingReplacementTarget(DcsEditorWorkspaceSnapshot Workspace, UiLayerDiagram Diagram,
        DcsMappingRevisionReview Review, bool AlreadyApplied)
    {
        #region BEGIN Public
        /// <summary>Gets whether this profile can adopt the selected revision.</summary>
        public bool CanReplace => !AlreadyApplied && Review.Conflicts.Count == 0;
        /// <summary>Gets whether this profile can restore retained built-in artwork.</summary>
        public bool CanRestoreOriginal => Diagram.CustomMapping is not null && Diagram.OriginalDrawing is not null;
        /// <summary>Gets an unambiguous module and physical-unit display label.</summary>
        public string Title => Workspace.Configuration.Module.DisplayName + " / " + Diagram.Title + " · " +
            Workspace.Configuration.Profiles.Single(x => x.Id == Diagram.ProfileId).SourceName;
        #endregion END Public
    }
}
