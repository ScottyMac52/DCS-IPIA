namespace Heimdall.Application.Games
{
    /// <summary>Durable receipt from a native import/export/configuration command: outcome, user status and native detail (for example an output location).</summary>
    public sealed record ProfileCommandReceipt(ProfileCommandOutcome Outcome, string Message, string Detail = "")
    {
        #region BEGIN Public
        /// <summary>Gets whether the native command completed its durable work.</summary>
        public bool Succeeded => Outcome == ProfileCommandOutcome.Succeeded;
        /// <summary>Creates a user-cancelled receipt; no native output was written.</summary>
        public static ProfileCommandReceipt Cancel(string message) => new(ProfileCommandOutcome.Cancelled, message);
        #endregion END Public
    }
}
