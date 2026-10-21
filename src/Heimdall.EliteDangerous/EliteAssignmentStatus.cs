namespace Heimdall.EliteDangerous
{
    /// <summary>Command ownership filters; combination-only references never own an assignment.</summary>
    public enum EliteAssignmentStatus
    {
        /// <summary>Every matching command.</summary>
        All,
        /// <summary>Owned by the selected controller.</summary>
        AssignedOnly,
        /// <summary>Not owned by the selected controller.</summary>
        NotAssigned,
        /// <summary>No controller, keyboard or mouse owns a binding.</summary>
        NotAssignedByAny,
        /// <summary>Owned anywhere in the profile.</summary>
        AssignedByAny
    }
}
