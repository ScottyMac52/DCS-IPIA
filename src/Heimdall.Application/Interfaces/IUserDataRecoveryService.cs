namespace Heimdall.Application.Interfaces
{
    /// <summary>Backs up saved user data and restores it without deploying DCS files.</summary>
    public interface IUserDataRecoveryService
    {
        #region BEGIN Public
        /// <summary>Gets the active user-data directory.</summary>
        string DataDirectory { get; }
        /// <summary>Gets the restored deployment record requiring explicit review, if any.</summary>
        string? DeploymentReview { get; }
        /// <summary>Creates a new verified full-user-data archive outside the active data directory.</summary>
        /// <param name="archive">New archive destination.</param>
        void Backup(string archive);
        /// <summary>Verifies all archive entries and database compatibility without replacing live data.</summary>
        /// <param name="archive">Archive to inspect.</param>
        /// <returns>Verified archive metadata.</returns>
        UserDataBackupInfo Inspect(string archive);
        /// <summary>Restores verified data after callers have closed their database services.</summary>
        /// <param name="archive">Archive to restore.</param>
        /// <returns>Retained previous-data directory for rollback or corruption diagnosis.</returns>
        string Restore(string archive);
        /// <summary>Recovers an interrupted directory replacement before SQLite is opened.</summary>
        void RecoverInterruptedRestore();
        /// <summary>Forgets a reviewed stale deployment record, leaving every DCS file untouched.</summary>
        void ForgetReviewedDeployment();
        #endregion END Public
    }
}
