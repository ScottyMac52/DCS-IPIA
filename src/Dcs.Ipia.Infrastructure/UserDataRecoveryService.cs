using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Microsoft.Data.Sqlite;

namespace Dcs.Ipia.Infrastructure
{
    /// <summary>Verifies portable user-data archives and journals same-volume directory replacement.</summary>
    public sealed class UserDataRecoveryService : IUserDataRecoveryService
    {
        private const string ManifestName = "ipia-backup.json";
        private const string ReviewName = "deployment-review.json";
        private const long MaximumBytes = 2L * 1024 * 1024 * 1024;
        private static readonly Dictionary<string, int> RequiredTables = new(StringComparer.Ordinal)
        {
            ["modules"] = 1, ["device_profiles"] = 2, ["modifiers"] = 2, ["bindings"] = 2,
            ["editor_workspaces"] = 3, ["app_preferences"] = 4, ["shared_ui_layer_revisions"] = 5,
            ["module_update_history"] = 6, ["fly_now_deployment"] = 7
        };
        private readonly string _version;
        private readonly Action<string>? _checkpoint;
        /// <summary>Creates recovery operations for one exclusively owned user-data profile.</summary>
        /// <param name="dataDirectory">Directory containing ipia.db and all per-user files.</param>
        /// <param name="applicationVersion">Creating application version.</param>
        /// <param name="checkpoint">Optional fault-injection observer for directory-swap tests.</param>
        public UserDataRecoveryService(string dataDirectory, string applicationVersion, Action<string>? checkpoint = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
            ArgumentException.ThrowIfNullOrWhiteSpace(applicationVersion);
            DataDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataDirectory));
            if (Path.GetDirectoryName(DataDirectory) is null) throw new ArgumentException("A filesystem root is not a user-data directory.", nameof(dataDirectory));
            _version = applicationVersion;
            _checkpoint = checkpoint;
        }
        #region BEGIN Public
        /// <inheritdoc/>
        public string DataDirectory { get; }
        /// <inheritdoc/>
        public string? DeploymentReview => File.Exists(Path.Combine(DataDirectory, ReviewName))
            ? File.ReadAllText(Path.Combine(DataDirectory, ReviewName)) : null;
        /// <inheritdoc/>
        public void Backup(string archive)
        {
            archive = Path.GetFullPath(archive);
            if (IsWithin(archive, DataDirectory)) throw new ArgumentException("Choose a backup destination outside the user-data folder.", nameof(archive));
            RejectLinks(archive);
            if (File.Exists(archive)) throw new IOException("Backup destination already exists. Choose a new filename.");
            var stage = DataDirectory + ".backup-" + Guid.NewGuid().ToString("N");
            var temporary = archive + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                RejectLinks(DataDirectory);
                Directory.CreateDirectory(stage);
                new SqliteBackupService(Connection(DataDirectory)).Backup(Path.Combine(stage, "ipia.db"));
                foreach (var file in EnumerateFiles(DataDirectory))
                {
                    RejectLinks(file);
                    var relative = Path.GetRelativePath(DataDirectory, file).Replace(Path.DirectorySeparatorChar, '/');
                    if (relative is "ipia.db" or "ipia.db-wal" or "ipia.db-shm" or "ipia.db.flynow.lock") continue;
                    ValidatePath(relative);
                    if (relative == ManifestName) throw new InvalidDataException("A reserved backup manifest occupies the user-data directory.");
                    var copy = Path.Combine(stage, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                    File.Copy(file, copy);
                }
                var schema = VerifyDatabase(stage);
                var files = Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories).ToDictionary(
                    path => Path.GetRelativePath(stage, path).Replace(Path.DirectorySeparatorChar, '/'), Describe, StringComparer.Ordinal);
                if (files.Count > 10000 || files.Values.Sum(x => x.Length) > MaximumBytes) throw new InvalidDataException("User data exceeds the backup size limit.");
                File.WriteAllText(Path.Combine(stage, ManifestName), JsonSerializer.Serialize(new UserDataBackupManifest(1, _version, schema, files)));
                ZipFile.CreateFromDirectory(stage, temporary, CompressionLevel.Optimal, false);
                Inspect(temporary);
                File.Move(temporary, archive);
            }
            finally
            {
                DeleteDirectory(stage);
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
        /// <inheritdoc/>
        public UserDataBackupInfo Inspect(string archive)
        {
            var stage = DataDirectory + ".inspect-" + Guid.NewGuid().ToString("N");
            try { return ExtractVerified(archive, stage); }
            finally { DeleteDirectory(stage); }
        }
        /// <inheritdoc/>
        public string Restore(string archive)
        {
            RecoverInterruptedRestore();
            var id = Guid.NewGuid().ToString("N");
            var stage = DataDirectory + ".restore-" + id;
            var previous = DataDirectory + ".before-restore-" + id;
            var journal = new UserDataRestoreJournal(stage, previous);
            var journalPath = DataDirectory + ".restore.json";
            try
            {
                ExtractVerified(archive, stage);
                new SqliteMigrationRunner(Connection(stage)).MigrateAsync().GetAwaiter().GetResult();
                MarkDeploymentReview(stage);
                RejectLinks(DataDirectory);
                Directory.CreateDirectory(DataDirectory);
                // Preserve a verified online safety archive when the current database is readable.
                try { if (File.Exists(Path.Combine(DataDirectory, "ipia.db"))) Backup(previous + ".ipia-backup.zip"); }
                catch (SqliteException) { /* Corrupt original bytes are retained in the previous directory instead. */ }
                catch (InvalidDataException) { /* An incompatible original is likewise retained without migration. */ }
                SqliteConnection.ClearAllPools();
                WriteJournal(journalPath, journal);
                _checkpoint?.Invoke("prepared");
                Directory.Move(DataDirectory, previous);
                _checkpoint?.Invoke("old-moved");
                Directory.Move(stage, DataDirectory);
                _checkpoint?.Invoke("installed");
                File.Delete(journalPath);
                return previous;
            }
            catch
            {
                if (Directory.Exists(previous))
                {
                    if (Directory.Exists(DataDirectory)) Directory.Move(DataDirectory, stage + ".failed");
                    Directory.Move(previous, DataDirectory);
                }
                if (File.Exists(journalPath)) File.Delete(journalPath);
                throw;
            }
            finally { DeleteDirectory(stage); }
        }
        /// <inheritdoc/>
        public void RecoverInterruptedRestore()
        {
            var path = DataDirectory + ".restore.json";
            if (!File.Exists(path)) return;
            RejectLinks(path);
            var journal = JsonSerializer.Deserialize<UserDataRestoreJournal>(File.ReadAllText(path))
                ?? throw new InvalidDataException("Invalid restore recovery journal.");
            ValidateJournal(journal);
            if (Directory.Exists(journal.Previous))
            {
                if (!Directory.Exists(DataDirectory)) Directory.Move(journal.Previous, DataDirectory);
                else if (Directory.Exists(journal.Stage)) throw new InvalidDataException("Ambiguous interrupted restore; retained data requires recovery review.");
                else
                {
                    try { VerifyDatabase(DataDirectory); } // Replacement completed before the journal was removed.
                    catch (Exception ex) when (ex is SqliteException or InvalidDataException)
                    {
                        Directory.Move(DataDirectory, journal.Stage + ".failed");
                        Directory.Move(journal.Previous, DataDirectory);
                    }
                }
            }
            else if (!Directory.Exists(DataDirectory)) throw new InvalidDataException("Interrupted restore has no original directory to recover.");
            DeleteDirectory(journal.Stage);
            File.Delete(path);
        }
        /// <inheritdoc/>
        public void ForgetReviewedDeployment()
        {
            if (DeploymentReview is null) return;
            using var db = new SqliteConnection(Connection(DataDirectory));
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = "DELETE FROM fly_now_deployment";
            command.ExecuteNonQuery();
            File.Delete(Path.Combine(DataDirectory, ReviewName));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static IEnumerable<string> EnumerateFiles(string directory)
        {
            RejectLinks(directory);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                RejectLinks(entry);
                if (Directory.Exists(entry))
                {
                    foreach (var file in EnumerateFiles(entry)) yield return file;
                }
                else yield return entry;
            }
        }
        private static UserDataBackupInfo ExtractVerified(string archive, string stage)
        {
            using var zip = ZipFile.OpenRead(archive);
            if (zip.Entries.Count > 10001 || zip.Entries.Sum(x => x.Length) > MaximumBytes) throw new InvalidDataException("Backup exceeds archive limits.");
            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in zip.Entries)
            {
                ValidatePath(entry.FullName);
                if (!entries.TryAdd(entry.FullName, entry)) throw new InvalidDataException("Duplicate archive path: " + entry.FullName);
            }
            if (!entries.TryGetValue(ManifestName, out var manifestEntry) || manifestEntry.Length > 2 * 1024 * 1024)
                throw new InvalidDataException("Backup manifest is missing or oversized.");
            using var manifestStream = manifestEntry.Open();
            var manifest = JsonSerializer.Deserialize<UserDataBackupManifest>(manifestStream)
                ?? throw new InvalidDataException("Invalid backup manifest.");
            if (manifest.FormatVersion != 1 || manifest.Files is null || string.IsNullOrWhiteSpace(manifest.ApplicationVersion) ||
                manifest.DatabaseSchemaVersion < 1 || manifest.DatabaseSchemaVersion > SqliteMigrationRunner.CurrentVersion)
                throw new InvalidDataException("Unsupported backup format or database schema.");
            if (!manifest.Files.ContainsKey("ipia.db") || entries.Count != manifest.Files.Count + 1)
                throw new InvalidDataException("Backup payload differs from its manifest.");
            Directory.CreateDirectory(stage);
            foreach (var (relative, expected) in manifest.Files)
            {
                ValidatePath(relative);
                if (relative is ManifestName or "ipia.db-wal" or "ipia.db-shm" or "ipia.db.flynow.lock" || expected is null ||
                    expected.Length < 0 || expected.Length > MaximumBytes || !entries.TryGetValue(relative, out var entry) || entry.FullName != relative || entry.Length != expected.Length)
                    throw new InvalidDataException("Invalid manifest entry: " + relative);
                var target = Path.Combine(stage, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using (var input = entry.Open())
                using (var output = File.Create(target)) CopyBounded(input, output, expected.Length);
                if (Describe(target) != expected) throw new InvalidDataException("Backup checksum differs: " + relative);
            }
            if (VerifyDatabase(stage) != manifest.DatabaseSchemaVersion) throw new InvalidDataException("Database schema differs from the manifest.");
            return new(manifest.ApplicationVersion, manifest.DatabaseSchemaVersion, manifest.Files.Count);
        }
        private static void CopyBounded(Stream input, Stream output, long length)
        {
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = input.Read(buffer)) > 0)
            {
                total += read;
                if (total > length) throw new InvalidDataException("Archive entry exceeds its declared length.");
                output.Write(buffer, 0, read);
            }
            if (total != length) throw new InvalidDataException("Truncated archive entry.");
        }
        private static int VerifyDatabase(string root)
        {
            using var db = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = Path.Combine(root, "ipia.db"), Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = "PRAGMA integrity_check";
            if (command.ExecuteScalar() as string != "ok") throw new InvalidDataException("Backup database integrity check failed.");
            command.CommandText = "SELECT max(version) FROM schema_migrations";
            var version = Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            if (version < 1 || version > SqliteMigrationRunner.CurrentVersion) throw new InvalidDataException("Database schema is not supported by this application.");
            command.CommandText = "SELECT count(*) FROM schema_migrations";
            if (Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != version)
                throw new InvalidDataException("Database migration history is incomplete.");
            foreach (var (table, introduced) in RequiredTables)
            {
                if (introduced > version) continue;
                command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name=$name";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$name", table);
                if (Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
                    throw new InvalidDataException("Backup database is missing required table: " + table);
            }
            command.CommandText = "PRAGMA foreign_key_check";
            command.Parameters.Clear();
            using var reader = command.ExecuteReader();
            if (reader.Read()) throw new InvalidDataException("Backup database has invalid foreign-key references.");
            return version;
        }
        private static void MarkDeploymentReview(string root)
        {
            using var db = new SqliteConnection(Connection(root));
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = "SELECT journal_json FROM fly_now_deployment WHERE id=1";
            if (command.ExecuteScalar() is string json) File.WriteAllText(Path.Combine(root, ReviewName), json);
        }
        private static string Connection(string root) => new SqliteConnectionStringBuilder
        { DataSource = Path.Combine(root, "ipia.db"), Pooling = false }.ToString();
        private static UserDataBackupFile Describe(string file)
        {
            using var stream = File.OpenRead(file);
            return new(stream.Length, Convert.ToHexString(SHA256.HashData(stream)));
        }
        private static bool IsWithin(string path, string root) => path.Equals(root, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        private static void ValidatePath(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || relative.Contains('\\') || relative.Contains(':') || relative.StartsWith('/') ||
                relative.Split('/').Any(x => x is "" or "." or ".." || x.EndsWith('.') || x.EndsWith(' ')))
                throw new InvalidDataException("Unsafe backup path: " + relative);
        }
        private static void RejectLinks(string path)
        {
            for (var current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if (new FileInfo(current).LinkTarget is not null || ((File.Exists(current) || Directory.Exists(current)) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)) throw new IOException("Linked user-data paths are not supported: " + current);
        }
        private void ValidateJournal(UserDataRestoreJournal journal)
        {
            var prefix = DataDirectory + ".restore-";
            if (journal.Stage is null || !journal.Stage.StartsWith(prefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(journal.Stage[prefix.Length..], "N", out _) ||
                journal.Previous != DataDirectory + ".before-restore-" + journal.Stage[prefix.Length..])
                throw new InvalidDataException("Restore journal references an unsafe directory.");
            RejectLinks(DataDirectory);
            RejectLinks(journal.Stage);
            RejectLinks(journal.Previous);
        }
        private static void WriteJournal(string path, UserDataRestoreJournal journal)
        {
            var temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, journal);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        #endregion END private helpers
    }
}
