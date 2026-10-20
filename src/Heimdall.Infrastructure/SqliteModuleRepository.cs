using System.Text.Json;
using Heimdall.Application;
using Heimdall.Domain;
using Microsoft.Data.Sqlite;

using Heimdall.Application.Interfaces;

namespace Heimdall.Infrastructure
{
    /// <summary>Persists modules in SQLite.</summary>
    /// <param name="connectionString">The connection string.</param>
    public sealed class SqliteModuleRepository(string connectionString) : IModuleRepository
    {
        #region BEGIN Public
        /// <summary>Lists stored items.</summary>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>The result of the operation.</returns>
        public async Task<IReadOnlyList<DcsModule>> ListAsync(CancellationToken ct = default)
        {
            await using var c = await Open(ct);
            var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT id,dcs_id,display_name FROM modules ORDER BY dcs_id COLLATE NOCASE,id";
            await using var r = await cmd.ExecuteReaderAsync(ct);
            var result = new List<DcsModule>();
            while (await r.ReadAsync(ct))
                result.Add(Read(r));
            return result;
        }
        /// <summary>Gets an item by its stable identifier.</summary>
        /// <param name="id">The identifier.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>The result of the operation.</returns>
        public async Task<DcsModule?> GetAsync(Guid id, CancellationToken ct = default)
        {
            await using var c = await Open(ct);
            var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT id,dcs_id,display_name FROM modules WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id.ToString("D"));
            await using var r = await cmd.ExecuteReaderAsync(ct);
            return await r.ReadAsync(ct) ? Read(r) : null;
        }
        /// <summary>Inserts or updates an item.</summary>
        /// <param name="value">The value.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>A task that completes when the operation finishes</returns>
        public async Task UpsertAsync(DcsModule value, CancellationToken ct = default)
        {
            await using var c = await Open(ct);
            var cmd = c.CreateCommand();
            cmd.CommandText =
                "INSERT INTO modules(id,dcs_id,display_name) VALUES($id,$dcs,$name) ON CONFLICT(id) DO UPDATE SET dcs_id=excluded.dcs_id,display_name=excluded.display_name";
            cmd.Parameters.AddWithValue("$id", value.Id.ToString("D"));
            cmd.Parameters.AddWithValue("$dcs", value.DcsId);
            cmd.Parameters.AddWithValue("$name", value.DisplayName);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        /// <summary>Deletes an item by its stable identifier.</summary>
        /// <param name="id">The identifier.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>The result of the operation.</returns>
        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
        {
            await using var c = await Open(ct);
            var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM modules WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id.ToString("D"));
            return await cmd.ExecuteNonQueryAsync(ct) == 1;
        }
        #endregion END Public
        #region BEGIN private helpers
        private async Task<SqliteConnection> Open(CancellationToken ct)
        {
            var c = new SqliteConnection(connectionString);
            await c.OpenAsync(ct);
            var cmd = c.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_keys=ON";
            await cmd.ExecuteNonQueryAsync(ct);
            return c;
        }
        private static DcsModule Read(SqliteDataReader r) => new(Guid.Parse(r.GetString(0)), r.GetString(1),
                                                                 r.GetString(2));
        #endregion END private helpers
    }
}
