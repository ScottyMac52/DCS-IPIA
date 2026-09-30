using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Microsoft.Data.Sqlite;

namespace Dcs.Ipia.Infrastructure
{
    /// <summary>Reads and writes complete editor snapshots without consulting source repositories.</summary>
    /// <param name="connectionString">SQLite connection string.</param>
    public sealed class SqliteEditorWorkspaceRepository(string connectionString) : IEditorWorkspaceRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
        #region BEGIN Public
        /// <inheritdoc/>
        public async Task<EditorWorkspaceSnapshot?> GetAsync(Guid moduleId, CancellationToken ct = default)
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT snapshot_json FROM editor_workspaces WHERE module_id=$id";
            command.Parameters.AddWithValue("$id", moduleId.ToString("D"));
            var json = await command.ExecuteScalarAsync(ct) as string;
            if (json is null)
                return null;
            var payload = JsonSerializer.Deserialize<EditorWorkspacePayload>(json, JsonOptions) ??
                          throw new InvalidDataException("Stored editor workspace could not be read.");
            var configuration =
                await new SqliteConfigurationRepository(connectionString).GetAsync(moduleId, ct) ??
                throw new InvalidDataException("The editor workspace has no stored module configuration.");
            var snapshot = new EditorWorkspaceSnapshot(configuration, payload.Pages, payload.Images, payload.Diagrams,
                                                       payload.ModuleCommands, payload.UiCommands, payload.Assignments,
                                                       payload.Labels, payload.Sources);
            if (payload.Version < 2)
            {
                snapshot = EditorWorkspaceUpgrade.RestoreShiftedControls(snapshot);
                snapshot = EditorWorkspaceUpgrade.RestoreMissingImportedAssignments(snapshot);
            }
            if (payload.Version < 3)
            {
                snapshot = EditorWorkspaceUpgrade.RestorePovAliases(snapshot);
            }
            var upgraded = MfdCategoryLabelUpgrade.Apply(snapshot);
            if (payload.Version < 3 || !ReferenceEquals(snapshot, upgraded))
                await SaveAsync(upgraded, ct);
            return upgraded;
        }
        /// <inheritdoc/>
        public async Task<IReadOnlyList<Guid>> ListAsync(CancellationToken ct = default)
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT module_id FROM editor_workspaces ORDER BY updated_utc DESC,module_id";
            var ids = new List<Guid>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                ids.Add(Guid.Parse(reader.GetString(0)));
            return ids;
        }
        /// <inheritdoc/>
        public async Task SaveAsync(EditorWorkspaceSnapshot snapshot, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (snapshot.Configuration.Module.Id == Guid.Empty)
                throw new ArgumentException("A module identity is required.", nameof(snapshot));
            if (snapshot.Assignments.Any(x => x.Slot.ModuleId != snapshot.Configuration.Module.Id))
                throw new ArgumentException("Assignments must belong to the stored module.", nameof(snapshot));
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(ct);
            await using (var foreignKeys = connection.CreateCommand())
            {
                foreignKeys.CommandText = "PRAGMA foreign_keys=ON";
                await foreignKeys.ExecuteNonQueryAsync(ct);
            }
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await SqliteConfigurationRepository.ReplaceInTransactionAsync(
                connection, (SqliteTransaction)transaction, snapshot.Configuration, ct);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText =
                "INSERT INTO editor_workspaces(module_id,snapshot_json,updated_utc) VALUES($id,$json,strftime('%Y-%m-%dT%H:%M:%fZ','now')) ON CONFLICT(module_id) DO UPDATE SET snapshot_json=excluded.snapshot_json,updated_utc=excluded.updated_utc";
            command.Parameters.AddWithValue("$id", snapshot.Configuration.Module.Id.ToString("D"));
            var payload = new EditorWorkspacePayload(snapshot.Pages, snapshot.Images, snapshot.Diagrams,
                                                     snapshot.ModuleCommands, snapshot.UiCommands, snapshot.Assignments,
                                                     snapshot.Labels, snapshot.Sources) { Version = 3 };
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(payload, JsonOptions));
            await command.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
        }
        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(Guid moduleId, CancellationToken ct = default)
        {
            if (moduleId == Guid.Empty)
                throw new ArgumentException("A module identity is required.", nameof(moduleId));
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(ct);
            await using (var foreignKeys = connection.CreateCommand())
            {
                foreignKeys.CommandText = "PRAGMA foreign_keys=ON";
                await foreignKeys.ExecuteNonQueryAsync(ct);
            }
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = "DELETE FROM modules WHERE id=$id";
            command.Parameters.AddWithValue("$id", moduleId.ToString("D"));
            var deleted = await command.ExecuteNonQueryAsync(ct) > 0;
            await transaction.CommitAsync(ct);
            return deleted;
        }
        #endregion END Public
    }
}
