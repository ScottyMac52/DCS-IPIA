using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Microsoft.Data.Sqlite;

namespace Dcs.Ipia.Infrastructure
{
    /// <summary>Stores reviewed onboarding in the same transaction as hardware adoption.</summary>
    public sealed class SqliteSetupRepository : ISetupRepository
    {
        private readonly string _connectionString;
        /// <summary>Creates setup storage for a migrated selected-profile database.</summary>
        /// <param name="connectionString">Selected profile's database connection.</param>
        public SqliteSetupRepository(string connectionString) => _connectionString = connectionString;
        #region BEGIN Public
        /// <inheritdoc/>
        public async Task<SetupState> ReadAsync(CancellationToken ct = default)
        {
            await using var db = new SqliteConnection(_connectionString);
            await db.OpenAsync(ct);
            await using var command = db.CreateCommand();
            command.CommandText = "SELECT completed,destination FROM profile_setup WHERE id=1";
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new InvalidDataException("Missing profile setup state.");
            return new(reader.GetInt32(0) == 1, reader.IsDBNull(1) ? null : reader.GetString(1));
        }
        /// <inheritdoc/>
        public async Task CommitAsync(HardwareInventorySnapshot expected, HardwareInventorySnapshot reviewed, string destination, CancellationToken ct = default)
        {
            HardwareInventoryContract.Validate(reviewed);
            if (destination.Length > 0 && !Path.IsPathFullyQualified(destination))
                throw new ArgumentException("Setup destination must be absolute.", nameof(destination));
            await using var db = new SqliteConnection(_connectionString);
            await db.OpenAsync(ct);
            await using var transaction = db.BeginTransaction();
            var current = await SqliteHardwareInventoryRepository.ReadAsync(db, transaction, ct);
            if (HardwareInventoryContract.Export(current) != HardwareInventoryContract.Export(expected))
                throw new InvalidOperationException("Inventory changed; rescan and review setup again.");
            await SqliteHardwareInventoryRepository.WriteAsync(db, transaction, reviewed, ct);
            await using var command = db.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE profile_setup SET completed=1,destination=$destination WHERE id=1";
            command.Parameters.AddWithValue("$destination", destination);
            if (await command.ExecuteNonQueryAsync(ct) != 1) throw new InvalidDataException("Missing profile setup state.");
            ct.ThrowIfCancellationRequested();
            await transaction.CommitAsync(ct);
        }
        #endregion END Public
    }
}
