namespace Heimdall.Application
{
    /// <summary>Names a validated command from one module or canonical UI Layer catalog.</summary>
    /// <param name="Id">Source command identifier.</param>
    /// <param name="Name">Human-readable name.</param>
    /// <param name="IsAxis">Whether this command requires an axis slot.</param>
    /// <param name="Layer">Catalog ownership scope.</param>
    public sealed record DraftCommand(string Id, string Name, bool IsAxis, AssignmentLayer Layer);
}
