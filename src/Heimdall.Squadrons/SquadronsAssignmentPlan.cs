namespace Heimdall.Squadrons
{
    /// <summary>Adapter-owned capability for command-first assignment; no UI or native index guesses.</summary>
    public sealed record SquadronsAssignmentPlan(string Concept, int CapturedRecords, int EditableRecords,
        IReadOnlyList<string> EditableRecordIds)
    {
        #region BEGIN Public
        /// <summary>Existing safe edit slots can be assigned through the established native record writer.</summary>
        public bool CanReplaceCapturedRecord => EditableRecords > 0;
        /// <summary>New native records must not be fabricated until their index/identifier rules are verified.</summary>
        public bool RequiresNewNativeRecord => EditableRecords == 0;
        #endregion END Public
    }
}
