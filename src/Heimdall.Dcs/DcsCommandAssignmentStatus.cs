namespace Heimdall.Dcs
{
    /// <summary>Native DCS palette ownership filters, including the all-controller intersection.</summary>
    public enum DcsCommandAssignmentStatus
    {
        All, AssignedHere, UnassignedHere, AssignedEverywhere, UnassignedEverywhere
    }
}
