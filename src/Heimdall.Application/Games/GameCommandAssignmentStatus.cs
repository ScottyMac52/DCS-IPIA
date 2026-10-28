namespace Heimdall.Application.Games
{
    /// <summary>Display-only assignment ownership; combination inputs do not own a command.</summary>
    public enum GameCommandAssignmentStatus
    {
        All, AssignedToController, NotAssignedToController, UnassignedEverywhere, AssignedAnywhere
    }
}
