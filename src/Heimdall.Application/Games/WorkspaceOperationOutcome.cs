
namespace Heimdall.Application.Games
{
    /// <summary>An explicit operation result that cannot mistake rejection for success.</summary>
    public enum WorkspaceOperationOutcome { Applied, NoChange, Blocked, Cancelled }
}
