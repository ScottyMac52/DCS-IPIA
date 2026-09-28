namespace Dcs.Ipia.Application
{
    /// <summary>One atomic, undoable draft operation and its before/after records.</summary>
    /// <param name="Name">Human-readable operation.</param>
    /// <param name="Changes">All affected slots, committed together.</param>
    public sealed record DraftOperation(string Name, IReadOnlyList<DraftChange> Changes);
}
