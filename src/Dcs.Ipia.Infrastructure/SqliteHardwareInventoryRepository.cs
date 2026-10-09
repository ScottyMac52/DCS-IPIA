using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Microsoft.Data.Sqlite;

namespace Dcs.Ipia.Infrastructure
{
    /// <summary>Stores reusable definitions, physical instances and scan evidence in independent profile-owned tables.</summary>
    public sealed class SqliteHardwareInventoryRepository : IHardwareInventoryRepository
    {
        private readonly string _connectionString;
        /// <summary>Creates persistence for an already migrated selected-profile database.</summary>
        /// <param name="connectionString">Selected profile's SQLite connection.</param>
        public SqliteHardwareInventoryRepository(string connectionString) => _connectionString = connectionString;
        #region BEGIN Public
        /// <inheritdoc/>
        public async Task<HardwareInventorySnapshot> ReadAsync(CancellationToken ct = default)
        {
            await using var db = new SqliteConnection(_connectionString);
            await db.OpenAsync(ct);
            await using var transaction = db.BeginTransaction(deferred: true);
            var snapshot = await ReadAsync(db, transaction, ct);
            await transaction.CommitAsync(ct);
            return snapshot;
        }
        /// <summary>Reads inventory within the caller's setup transaction.</summary>
        internal static async Task<HardwareInventorySnapshot> ReadAsync(SqliteConnection db, SqliteTransaction transaction, CancellationToken ct)
        {
            var definitions = await Read<HardwareDefinition>(db, transaction, "hardware_definitions", ct);
            var instances = await Read<HardwareInventoryInstance>(db, transaction, "hardware_inventory", ct);
            var observations = await Read<ControllerObservation>(db, transaction, "hardware_observations", ct);
            var snapshot = new HardwareInventorySnapshot(1, definitions, instances,
                observations.Select(x => x with { Device = x.Device with { Connected = false } }).ToArray());
            HardwareInventoryContract.Validate(snapshot);
            return snapshot;
        }
        /// <inheritdoc/>
        public async Task SaveAsync(HardwareInventorySnapshot snapshot, CancellationToken ct = default)
        {
            HardwareInventoryContract.Validate(snapshot);
            await using var db = new SqliteConnection(_connectionString);
            await db.OpenAsync(ct);
            await using var transaction = db.BeginTransaction();
            await WriteAsync(db, transaction, snapshot, ct);
            await transaction.CommitAsync(ct);
        }
        /// <summary>Writes reviewed inventory within the caller's setup transaction.</summary>
        internal static async Task WriteAsync(SqliteConnection db, SqliteTransaction transaction, HardwareInventorySnapshot snapshot, CancellationToken ct)
        {
            HardwareInventoryContract.Validate(snapshot);
            await using var command = db.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM hardware_inventory; DELETE FROM hardware_definitions; DELETE FROM hardware_observations";
            await command.ExecuteNonQueryAsync(ct);
            foreach (var definition in snapshot.Definitions)
                await Insert(db, transaction, "INSERT INTO hardware_definitions VALUES($id,$json)", definition.Id, definition, null, ct);
            foreach (var instance in snapshot.Instances)
                await Insert(db, transaction, "INSERT INTO hardware_inventory VALUES($id,$definition,$json)",
                    instance.PhysicalGuid.ToString("D"), instance, instance.DefinitionId, ct);
            foreach (var observation in snapshot.Observations)
                await Insert(db, transaction, "INSERT INTO hardware_observations VALUES($id,$json)", observation.Device.PhysicalGuid.ToString("D"), observation, null, ct);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static async Task<IReadOnlyList<T>> Read<T>(SqliteConnection db, SqliteTransaction transaction, string table, CancellationToken ct)
        {
            await using var command = db.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT payload_json FROM " + table + " ORDER BY 1";
            var rows = new List<T>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                rows.Add(JsonSerializer.Deserialize<T>(reader.GetString(0)) ?? throw new InvalidDataException("Incomplete hardware inventory row."));
            return rows;
        }
        private static async Task Insert<T>(SqliteConnection db, SqliteTransaction transaction, string sql,
            string id, T value, string? definition, CancellationToken ct)
        {
            await using var command = db.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(value));
            if (definition is not null) command.Parameters.AddWithValue("$definition", definition);
            await command.ExecuteNonQueryAsync(ct);
        }
        #endregion END private helpers
    }
}

