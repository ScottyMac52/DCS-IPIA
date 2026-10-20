using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Microsoft.Data.Sqlite;

namespace Heimdall.Infrastructure
{
    /// <summary>Journals native deployment and exact-file cleanup in SQLite with cross-process exclusion.</summary>
    public sealed class SqliteFlyNowService : IFlyNowService
    {
        private readonly string _connectionString;
        private readonly string _backupRoot;
        private readonly string _lockPath;
        private readonly IFlyNowPayloadBuilder _builder;

        /// <summary>Creates deployment services for the migrated local database.</summary>
        /// <param name="connectionString">Local SQLite database.</param>
        /// <param name="backupRoot">Durable replacement and recovery backup location.</param>
        /// <param name="builder">Verified saved-state payload builder.</param>
        public SqliteFlyNowService(string connectionString, string backupRoot, IFlyNowPayloadBuilder builder)
        {
            _connectionString = connectionString;
            _backupRoot = Path.GetFullPath(backupRoot);
            _lockPath = Path.GetFullPath(new SqliteConnectionStringBuilder(connectionString).DataSource) + ".flynow.lock";
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        }

        #region BEGIN Public
        /// <inheritdoc/>
        public Task<FlyNowDeployment?> GetActiveAsync(CancellationToken ct = default) => Task.Run(() =>
        {
            using var lease = Acquire();
            Recover();
            return Read()?.Active;
        }, ct);

        /// <inheritdoc/>
        public async Task<FlyNowDeployment> DeployAsync(Guid moduleId, string destination, CancellationToken ct = default)
        {
            using var lease = Acquire();
            Recover();
            var previous = Read()?.Active;
            if (previous is not null && (previous.ModuleId != moduleId || previous.Clearing))
                throw new InvalidOperationException("Press Clear to remove the active Fly Now! module before deploying another module.");
            var root = ValidateRoot(destination);
            if (previous is not null && !string.Equals(previous.Destination, root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Press Clear before changing the active module's Saved Games destination.");
            var payload = await _builder.BuildAsync(moduleId, ct);
            var files = payload.Files.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            if (files.Count == 0)
                throw new InvalidDataException("The selected module produced no deployable files.");
            foreach (var path in files.Keys) ValidateFile(root, path);
            var folder = Path.Combine(_backupRoot, Guid.NewGuid().ToString("N"));
            var replaced = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var (path, bytes) in files)
            {
                ct.ThrowIfCancellationRequested();
                var target = ValidateFile(root, path);
                var existed = File.Exists(target);
                replaced.Add(path, existed);
                WriteBytes(Path.Combine(folder, "staged", path), bytes);
                if (existed) CopyFile(target, Path.Combine(folder, "original", path));
            }
            var manifest = (previous?.Files ?? []).Concat(files.Keys).Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.Ordinal).ToArray();
            var active = new FlyNowDeployment(moduleId, payload.DisplayName, root, manifest);
            Save(new FlyNowJournal(active, previous, folder, replaced, true));
            try
            {
                foreach (var path in files.Keys)
                {
                    ct.ThrowIfCancellationRequested();
                    CopyFile(Path.Combine(folder, "staged", path), ValidateFile(root, path), Path.GetFileName(folder));
                }
                Save(new FlyNowJournal(active, null, folder, replaced, false));
                return active;
            }
            catch (Exception copyError)
            {
                try { Recover(); }
                catch (Exception recoveryError)
                {
                    throw new IOException("Deployment interrupted; rollback needs attention. Retry Clear before deploying. "
                        + recoveryError.Message, copyError);
                }
                throw;
            }
        }

        /// <inheritdoc/>
        public Task ClearAsync(CancellationToken ct = default) => Task.Run(() =>
        {
            using var lease = Acquire();
            Recover();
            var journal = Read();
            if (journal is null) return;
            var active = journal.Active;
            ValidateRoot(active.Destination, false);
            foreach (var path in active.Files) ValidateFile(active.Destination, path);
            Save(journal with { Active = active with { Clearing = true } });
            var failures = new List<string>();
            foreach (var path in active.Files)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var target = ValidateFile(active.Destination, path);
                    if (File.Exists(target)) File.Delete(target);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { failures.Add(path + ": " + ex.Message); }
            }
            if (failures.Count > 0)
                throw new IOException("Clear is incomplete; the active module remains locked. Retry Clear. " + string.Join("; ", failures));
            Save(null);
        }, ct);
        #endregion END Public

        #region BEGIN private helpers
        private FileStream Acquire()
        {
            if (File.Exists(Path.Combine(Path.GetDirectoryName(_backupRoot)!, "deployment-review.json")))
                throw new InvalidOperationException("Restored deployment records need review under Data & recovery before Fly Now or Clear can run. DCS files have not been changed.");
            try { return new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException ex) { throw new IOException("Another Fly Now! or Clear operation is running. Try again when it finishes.", ex); }
        }

        private FlyNowJournal? Read()
        {
            using var db = new SqliteConnection(_connectionString);
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT journal_json FROM fly_now_deployment WHERE id=1";
            var json = cmd.ExecuteScalar() as string;
            return json is null ? null : JsonSerializer.Deserialize<FlyNowJournal>(json)
                ?? throw new InvalidDataException("The Fly Now! deployment record is invalid.");
        }

        private void Save(FlyNowJournal? journal)
        {
            using var db = new SqliteConnection(_connectionString);
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = journal is null ? "DELETE FROM fly_now_deployment WHERE id=1"
                : "INSERT INTO fly_now_deployment(id,journal_json) VALUES(1,$json) ON CONFLICT(id) DO UPDATE SET journal_json=excluded.journal_json";
            if (journal is not null) cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(journal));
            cmd.ExecuteNonQuery();
        }

        private void Recover()
        {
            var journal = Read();
            if (journal?.Writing != true) return;
            var root = ValidateRoot(journal.Active.Destination, false);
            var backup = Path.GetFullPath(journal.BackupFolder);
            if (!backup.StartsWith(_backupRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Deployment recovery backup is outside the configured backup folder.");
            RejectLinks(backup);
            foreach (var (path, existed) in journal.ReplacedFiles)
            {
                var target = ValidateFile(root, path);
                if (existed)
                {
                    var original = Path.Combine(backup, "original", path);
                    RejectLinks(original);
                    CopyFile(original, target, Path.GetFileName(backup));
                }
                else
                {
                    if (File.Exists(target)) File.Delete(target);
                    var temporary = target + "." + Path.GetFileName(backup) + ".ipia-tmp";
                    RejectLinks(temporary);
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
            }
            Save(journal.Previous is null ? null : new FlyNowJournal(journal.Previous, null, backup,
                new Dictionary<string, bool>(), false));
        }

        private static string ValidateRoot(string destination, bool mustExist = true)
        {
            if (string.IsNullOrWhiteSpace(destination) || !Path.IsPathFullyQualified(destination))
                throw new ArgumentException("Choose an existing DCS Saved Games instance folder in Settings.", nameof(destination));
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
            if (mustExist && !Directory.Exists(root))
                throw new DirectoryNotFoundException("The configured DCS Saved Games folder does not exist: " + root);
            RejectLinks(root);
            return root;
        }

        private static string ValidateFile(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || relative.Contains('\\') || relative.Contains(':') ||
                relative.StartsWith('/') || relative.Split('/').Any(x => x is "" or "." or "..") ||
                !(relative.StartsWith("Config/Input/", StringComparison.Ordinal) || relative.StartsWith("KNEEBOARD/", StringComparison.Ordinal)))
                throw new InvalidDataException("Unsafe DCS deployment path: " + relative);
            var target = Path.GetFullPath(Path.Combine(root, relative));
            if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("DCS file escapes the selected destination: " + relative);
            RejectLinks(target);
            if (Directory.Exists(target))
                throw new IOException("A directory occupies the deployment file path: " + target);
            return target;
        }

        private static void RejectLinks(string path)
        {
            for (var current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                var entry = new FileInfo(current);
                if (entry.LinkTarget is not null || ((File.Exists(current) || Directory.Exists(current)) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0))
                    throw new IOException("Linked deployment paths are not supported: " + current);
            }
        }

        private static void WriteBytes(string path, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
        }

        private static void CopyFile(string source, string target, string? operation = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var temporary = target + "." + (operation ?? Guid.NewGuid().ToString("N")) + ".ipia-tmp";
            RejectLinks(temporary);
            try
            {
                File.Copy(source, temporary, true);
                File.Move(temporary, target, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        #endregion END private helpers
    }
}
