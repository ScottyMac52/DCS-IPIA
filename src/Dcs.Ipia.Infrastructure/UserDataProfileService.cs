using Microsoft.Data.Sqlite;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.Infrastructure
{
    /// <summary>Separates setup completion from a confirmed, verified, recoverable data reset.</summary>
    public sealed class UserDataProfileService : IUserDataProfileService
    {
        private readonly UserDataProfile _profile;
        private readonly string _version;
        /// <summary>Creates profile lifecycle operations under the application's exclusive profile lease.</summary>
        /// <param name="profile">Selected and validated profile.</param>
        /// <param name="version">Application version recorded in archives.</param>
        public UserDataProfileService(UserDataProfile profile, string version)
        {
            _profile = profile;
            _version = version;
        }
        #region BEGIN Public
        /// <summary>Gets whether setup was explicitly completed in this profile.</summary>
        public bool SetupCompleted
        {
            get
            {
                if (File.Exists(StatePath)) return true;
                using var db = OpenDatabase();
                using var command = db.CreateCommand();
                command.CommandText = "SELECT completed FROM profile_setup WHERE id=1";
                return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 1;
            }
        }
        /// <summary>Records successful setup, or clears completion without erasing modules, preferences or artwork.</summary>
        /// <param name="completed">True only after setup commits successfully; false to rerun setup.</param>
        public void SetSetupCompleted(bool completed)
        {
            _profile.Validate();
            using var db = OpenDatabase();
            using var command = db.CreateCommand();
            command.CommandText = "UPDATE profile_setup SET completed=$completed WHERE id=1";
            command.Parameters.AddWithValue("$completed", completed ? 1 : 0);
            if (command.ExecuteNonQuery() != 1) throw new InvalidDataException("Missing profile setup state.");
            if (File.Exists(StatePath)) File.Delete(StatePath);
        }
        /// <summary>Resets data only after explicit consent and a verified full backup; never accesses DCS destinations.</summary>
        /// <param name="confirmed">Explicit destructive-reset confirmation.</param>
        /// <param name="backupPath">New recoverable archive outside the profile.</param>
        /// <returns>Retained original directory; callers must restart before using database services.</returns>
        public string Reset(bool confirmed, string backupPath)
        {
            if (!confirmed) throw new InvalidOperationException("Reset cancelled; data is unchanged.");
            _profile.Validate();
            _profile.ValidateBackupDestination(backupPath);
            var recovery = new UserDataRecoveryService(_profile.DataDirectory, _version);
            recovery.RecoverInterruptedRestore();
            recovery.Backup(backupPath);
            recovery.Inspect(backupPath);
            var empty = _profile.DataDirectory + ".empty-" + Guid.NewGuid().ToString("N");
            var archive = empty + ".zip";
            try
            {
                Directory.CreateDirectory(empty);
                var connection = new SqliteConnectionStringBuilder { DataSource = Path.Combine(empty, "ipia.db"), Pooling = false }.ToString();
                new SqliteMigrationRunner(connection).MigrateAsync().GetAwaiter().GetResult();
                new UserDataRecoveryService(empty, _version).Backup(archive);
                _profile.Validate();
                return recovery.Restore(archive);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(empty)) Directory.Delete(empty, true);
                if (File.Exists(archive)) File.Delete(archive);
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private string StatePath => Path.Combine(_profile.DataDirectory, "onboarding-state.json");
        private SqliteConnection OpenDatabase()
        {
            _profile.Validate();
            var db = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = Path.Combine(_profile.DataDirectory, "ipia.db"), Mode = SqliteOpenMode.ReadWrite }.ToString());
            try { db.Open(); return db; }
            catch { db.Dispose(); throw; }
        }
        #endregion END private helpers
    }
}
