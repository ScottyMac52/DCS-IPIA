using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Microsoft.Data.Sqlite;

namespace Heimdall.Infrastructure
{
    /// <summary>Stores append-only shared UI Layer revisions with optimistic concurrency and content checks.</summary>
    /// <param name="connectionString">Local editor database connection string.</param>
    public sealed class SqliteSharedUiLayerWorkspaceRepository(string connectionString) : ISharedUiLayerWorkspaceRepository
    {
        #region BEGIN Public
        /// <summary>Loads current or historical defaults independently of module data and source files.</summary>
        /// <param name="revision">Historical revision, or null for latest.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Verified saved workspace, or null when absent.</returns>
        public async Task<SharedUiLayerWorkspace?> GetAsync(int? revision = null, CancellationToken ct = default)
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT revision,source_commit,sources_json,content_sha256 FROM shared_ui_layer_revisions WHERE ($revision IS NULL OR revision=$revision) ORDER BY revision DESC LIMIT 1";
            command.Parameters.AddWithValue("$revision", (object?)revision ?? DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            var commit = reader.GetString(1);
            var json = reader.GetString(2);
            var hash = reader.GetString(3);
            if (Hash(commit, json) != hash) throw new InvalidDataException("Shared default UI Layer revision failed its content check.");
            return new(reader.GetInt32(0), commit, JsonSerializer.Deserialize<Dictionary<string, string>>(json)!, hash);
        }
        /// <summary>Appends defaults atomically and rejects stale baselines without altering history or modules.</summary>
        /// <param name="workspace">Sources and expected latest revision.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Persisted next revision.</returns>
        public async Task<SharedUiLayerWorkspace> SaveAsync(SharedUiLayerWorkspace workspace, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            if (workspace.Revision < 0 || workspace.SourceCommit.Length != 40 || !workspace.SourceCommit.All(Uri.IsHexDigit) ||
                workspace.Sources.Count == 0 || workspace.Sources.Any(x =>
                    !x.Key.StartsWith("assets/shared/ui-layer/", StringComparison.Ordinal) || x.Key.Contains('\\') ||
                    x.Key.Split('/').Any(part => part is "" or "." or "..") || string.IsNullOrWhiteSpace(x.Value)))
                throw new InvalidDataException("Shared default UI Layer sources or provenance are invalid.");
            var sorted = new SortedDictionary<string, string>(workspace.Sources.ToDictionary(x => x.Key, x => x.Value), StringComparer.Ordinal);
            var json = JsonSerializer.Serialize(sorted);
            var hash = Hash(workspace.SourceCommit, json);
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var transaction = connection.BeginTransaction();
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT COALESCE(MAX(revision),0) FROM shared_ui_layer_revisions";
            var latest = Convert.ToInt32(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
            if (latest != workspace.Revision) throw new InvalidOperationException("Shared defaults changed. Reopen the latest revision before saving.");
            command.CommandText = "INSERT INTO shared_ui_layer_revisions(revision,source_commit,sources_json,content_sha256) VALUES($revision,$commit,$sources,$hash)";
            command.Parameters.AddWithValue("$revision", latest + 1);
            command.Parameters.AddWithValue("$commit", workspace.SourceCommit);
            command.Parameters.AddWithValue("$sources", json);
            command.Parameters.AddWithValue("$hash", hash);
            await command.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
            return new(latest + 1, workspace.SourceCommit, sorted, hash);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Hash(string commit, string json) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(commit + "\n" + json)));
        #endregion END private helpers
    }
}
