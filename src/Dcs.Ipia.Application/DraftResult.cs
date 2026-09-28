namespace Dcs.Ipia.Application
{
    /// <summary>Reports an atomic draft action without writing source files.</summary>
    /// <param name="Outcome">Applied, unchanged, blocked, or awaiting confirmation.</param>
    /// <param name="Message">Reason or next action for the UI.</param>
    /// <param name="Operation">Before/after record only when applied.</param>
    public sealed record DraftResult(DraftOutcome Outcome, string Message, DraftOperation? Operation = null);
}
