namespace Dcs.Ipia.Application.Interfaces
{
    /// <summary>Defines profile-scoped setup completion and verified destructive reset.</summary>
    public interface IUserDataProfileService
    {
        #region BEGIN Public
        /// <summary>Gets whether setup was already offered or completed in this profile.</summary>
        bool SetupOffered { get; }
        /// <summary>Remembers that setup was offered without completing setup or adopting devices.</summary>
        void MarkSetupOffered();
        /// <summary>Gets whether this profile completed setup.</summary>
        bool SetupCompleted { get; }
        /// <summary>Records or clears setup completion without removing saved user data.</summary>
        /// <param name="completed">Whether setup committed successfully.</param>
        void SetSetupCompleted(bool completed);
        /// <summary>Resets the current profile after consent and a mandatory verified full backup.</summary>
        /// <param name="confirmed">Explicit destructive-reset confirmation.</param>
        /// <param name="backupPath">New archive outside the active profile.</param>
        /// <returns>Retained original data directory.</returns>
        string Reset(bool confirmed, string backupPath);
        #endregion END Public
    }
}
