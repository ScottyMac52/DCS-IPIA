using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Heimdall.Domain;
using Microsoft.Data.Sqlite;

namespace Heimdall.Infrastructure
{
    /// <summary>Reads and writes complete editor snapshots without consulting source repositories.</summary>
    /// <param name="connectionString">SQLite connection string.</param>
    /// <param name="persistReadUpgrades">Whether opening legacy data writes upgraded snapshots.</param>
    public sealed class SqliteEditorWorkspaceRepository(string connectionString, bool persistReadUpgrades = true) : IEditorWorkspaceCreationRepository, IEditorWorkspaceUpdateRepository
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
                                                       payload.Labels, payload.Sources)
            {
                SharedDefault = payload.SharedDefault is null ? null : SharedUiLayerRevisionReference.Freeze(payload.SharedDefault)
            };
            if (payload.Version < 2)
            {
                snapshot = EditorWorkspaceUpgrade.RestoreShiftedControls(snapshot);
                snapshot = EditorWorkspaceUpgrade.RestoreMissingImportedAssignments(snapshot);
            }
            if (payload.Version < 3)
            {
                snapshot = EditorWorkspaceUpgrade.RestorePovAliases(snapshot);
            }
            GenericHardwareLayout.ValidateWorkspace(snapshot);
            var upgraded = MfdCategoryLabelUpgrade.Apply(snapshot);
            if (persistReadUpgrades && (payload.Version < 3 || !ReferenceEquals(snapshot, upgraded)))
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
        public Task SaveAsync(EditorWorkspaceSnapshot snapshot, CancellationToken ct = default) => SaveCoreAsync(snapshot, false, ct);
        /// <inheritdoc/>
        public Task CreateAsync(EditorWorkspaceSnapshot snapshot, CancellationToken ct = default) => SaveCoreAsync(snapshot, true, ct);
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
        /// <inheritdoc/>
        public async Task SaveIfUnchangedAsync(EditorWorkspaceSnapshot expected, EditorWorkspaceSnapshot updated, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(expected);
            ArgumentNullException.ThrowIfNull(updated);
            var id = expected.Configuration.Module.Id;
            if (id == Guid.Empty || updated.Configuration.Module.Id != id ||
                updated.Assignments.Any(x => x.Slot.ModuleId != id))
                throw new ArgumentException("Reviewed updates must belong to the same saved module.", nameof(updated));
            GenericHardwareLayout.ValidateWorkspace(updated);
            var reference = updated.SharedDefault is null ? null : SharedUiLayerRevisionReference.Freeze(updated.SharedDefault);
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(ct);
            await using (var foreignKeys = connection.CreateCommand())
            {
                foreignKeys.CommandText = "PRAGMA foreign_keys=ON";
                await foreignKeys.ExecuteNonQueryAsync(ct);
            }
            await using var transaction = connection.BeginTransaction();
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT snapshot_json FROM editor_workspaces WHERE module_id=$id";
            command.Parameters.AddWithValue("$id", id.ToString("D"));
            var currentJson = await command.ExecuteScalarAsync(ct) as string;
            var current = currentJson is null ? null : JsonSerializer.Deserialize<EditorWorkspacePayload>(currentJson, JsonOptions);
            var configuration = await new SqliteConfigurationRepository(connectionString).GetAsync(id, ct);
            if (current is null || JsonSerializer.Serialize(current, JsonOptions) != JsonSerializer.Serialize(Payload(expected), JsonOptions) ||
                JsonSerializer.Serialize(configuration, JsonOptions) != JsonSerializer.Serialize(expected.Configuration, JsonOptions))
                throw new InvalidOperationException("The module changed after review. Reopen and review it again.");
            var previousJson = JsonSerializer.Serialize(new ModuleUpdateHistoryPayload(
                JsonSerializer.SerializeToElement(expected.Configuration, JsonOptions), Payload(expected)), JsonOptions);
            command.CommandText = "INSERT INTO module_update_history(module_id,snapshot_json,content_sha256) VALUES($id,$previous,$hash)";
            command.Parameters.AddWithValue("$previous", previousJson);
            command.Parameters.AddWithValue("$hash", Hash(previousJson));
            await command.ExecuteNonQueryAsync(ct);
            await SqliteConfigurationRepository.ReplaceInTransactionAsync(connection, transaction, updated.Configuration, ct);
            command.CommandText = "UPDATE editor_workspaces SET snapshot_json=$json,updated_utc=strftime('%Y-%m-%dT%H:%M:%fZ','now') WHERE module_id=$id";
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(Payload(updated with { SharedDefault = reference }), JsonOptions));
            await command.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
        }
        /// <inheritdoc/>
        public async Task<EditorWorkspaceSnapshot?> GetPriorAsync(Guid moduleId, CancellationToken ct = default)
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT snapshot_json,content_sha256 FROM module_update_history WHERE module_id=$id ORDER BY id DESC LIMIT 1";
            command.Parameters.AddWithValue("$id", moduleId.ToString("D"));
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            var json = reader.GetString(0);
            if (Hash(json) != reader.GetString(1))
                throw new InvalidDataException("Prior module state failed its content check.");
            var prior = JsonSerializer.Deserialize<ModuleUpdateHistoryPayload>(json, JsonOptions) ??
                throw new InvalidDataException("The prior module state could not be read.");
            var editor = prior.Editor;
            return new(ReadConfiguration(prior.Configuration), editor.Pages, editor.Images, editor.Diagrams,
                editor.ModuleCommands, editor.UiCommands, editor.Assignments, editor.Labels, editor.Sources)
            {
                SharedDefault = editor.SharedDefault is null ? null : SharedUiLayerRevisionReference.Freeze(editor.SharedDefault)
            };
        }
        #endregion END Public
        #region BEGIN private helpers
        private async Task SaveCoreAsync(EditorWorkspaceSnapshot snapshot, bool createOnly, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (snapshot.Configuration.Module.Id == Guid.Empty)
                throw new ArgumentException("A module identity is required.", nameof(snapshot));
            if (snapshot.Assignments.Any(x => x.Slot.ModuleId != snapshot.Configuration.Module.Id))
                throw new ArgumentException("Assignments must belong to the stored module.", nameof(snapshot));
            GenericHardwareLayout.ValidateWorkspace(snapshot);
            var reference = snapshot.SharedDefault is null ? null : SharedUiLayerRevisionReference.Freeze(snapshot.SharedDefault);
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(ct);
            await using (var foreignKeys = connection.CreateCommand())
            {
                foreignKeys.CommandText = "PRAGMA foreign_keys=ON";
                await foreignKeys.ExecuteNonQueryAsync(ct);
            }
            await using var transaction = await connection.BeginTransactionAsync(ct);
            if (createOnly)
            {
                await using var duplicate = connection.CreateCommand();
                duplicate.Transaction = (SqliteTransaction)transaction;
                duplicate.CommandText = "SELECT COUNT(*) FROM modules WHERE id=$id OR dcs_id=$dcs COLLATE NOCASE";
                duplicate.Parameters.AddWithValue("$id", snapshot.Configuration.Module.Id.ToString("D"));
                duplicate.Parameters.AddWithValue("$dcs", snapshot.Configuration.Module.DcsId);
                if (Convert.ToInt64(await duplicate.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture) != 0)
                    throw new InvalidOperationException("That module identity or DCS folder is already saved. Restore into an empty database or remove the existing module first.");
            }
            await using (var existing = connection.CreateCommand())
            {
                existing.Transaction = (SqliteTransaction)transaction;
                existing.CommandText = "SELECT snapshot_json FROM editor_workspaces WHERE module_id=$id";
                existing.Parameters.AddWithValue("$id", snapshot.Configuration.Module.Id.ToString("D"));
                if (await existing.ExecuteScalarAsync(ct) is string currentJson)
                {
                    var previous = JsonSerializer.Deserialize<EditorWorkspacePayload>(currentJson, JsonOptions)?.SharedDefault;
                    if (previous is not null && (reference is null || previous.Revision != reference.Revision ||
                        previous.ContentSha256 != reference.ContentSha256 || previous.SourceCommit != reference.SourceCommit))
                        throw new InvalidOperationException("The module's shared default reference changed. Reopen it before saving; baseline changes require reviewed adoption.");
                }
            }
            await SqliteConfigurationRepository.ReplaceInTransactionAsync(
                connection, (SqliteTransaction)transaction, snapshot.Configuration, ct);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText =
                "INSERT INTO editor_workspaces(module_id,snapshot_json,updated_utc) VALUES($id,$json,strftime('%Y-%m-%dT%H:%M:%fZ','now')) ON CONFLICT(module_id) DO UPDATE SET snapshot_json=excluded.snapshot_json,updated_utc=excluded.updated_utc";
            command.Parameters.AddWithValue("$id", snapshot.Configuration.Module.Id.ToString("D"));
            var payload = new EditorWorkspacePayload(snapshot.Pages, snapshot.Images, snapshot.Diagrams,
                                                     snapshot.ModuleCommands, snapshot.UiCommands, snapshot.Assignments,
                                                     snapshot.Labels, snapshot.Sources) { Version = 3, SharedDefault = reference };
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(payload, JsonOptions));
            await command.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
        }
        private static EditorWorkspacePayload Payload(EditorWorkspaceSnapshot snapshot) =>
            new(snapshot.Pages, snapshot.Images, snapshot.Diagrams, snapshot.ModuleCommands, snapshot.UiCommands,
                snapshot.Assignments, snapshot.Labels, snapshot.Sources) { Version = 3, SharedDefault = snapshot.SharedDefault };
        private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        private static ModuleConfiguration ReadConfiguration(JsonElement configuration) =>
            new(configuration.GetProperty("Module").Deserialize<DcsModule>(JsonOptions)!,
                configuration.GetProperty("Profiles").Deserialize<DeviceProfile[]>(JsonOptions)!,
                configuration.GetProperty("Modifiers").Deserialize<DcsModifier[]>(JsonOptions)!,
                configuration.GetProperty("Bindings").EnumerateArray().Select(binding => new DcsBinding(
                    binding.GetProperty("ProfileId").GetGuid(), (BindingSection)binding.GetProperty("Section").GetInt32(),
                    binding.GetProperty("CommandId").GetString()!, binding.GetProperty("CommandName").GetString()!,
                    (BindingDisposition)binding.GetProperty("Disposition").GetInt32(), binding.GetProperty("InputKey").GetString()!,
                    binding.GetProperty("Reformers").Deserialize<string[]>(JsonOptions),
                    binding.GetProperty("Filter").Deserialize<AxisFilter>(JsonOptions))),
                configuration.GetProperty("Provenance").GetString()!);
        #endregion END private helpers
    }
}

