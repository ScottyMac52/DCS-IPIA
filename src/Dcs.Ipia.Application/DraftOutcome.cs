namespace Dcs.Ipia.Application
{
    /// <summary>Describes whether a draft action applied or requires a decision.</summary>
    public enum DraftOutcome
    {
        Applied,
        NoChange,
        ConfirmationRequired,
        Blocked
    }
}
