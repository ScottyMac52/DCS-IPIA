namespace Dcs.Ipia.Infrastructure
{
    /// <summary>Sibling directories involved in an interrupted user-data replacement.</summary>
    /// <param name="Stage">Verified replacement directory.</param>
    /// <param name="Previous">Original directory retained for recovery.</param>
    internal sealed record UserDataRestoreJournal(string Stage, string Previous);
}
