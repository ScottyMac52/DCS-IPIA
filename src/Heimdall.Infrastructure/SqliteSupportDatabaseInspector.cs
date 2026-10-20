using System.Globalization;
using Heimdall.Application;
using Heimdall.Application.Interfaces;
using Microsoft.Data.Sqlite;

namespace Heimdall.Infrastructure
{
    /// <summary>Checks existing database evidence using a non-pooled, read-only connection.</summary>
    public sealed class SqliteSupportDatabaseInspector : ISupportDatabaseInspector
    {
        private readonly string _connection;
        /// <summary>Forces read-only mode even when the application uses a writable connection string.</summary>
        /// <param name="connectionString">Selected profile database connection.</param>
        public SqliteSupportDatabaseInspector(string connectionString)
        {
            var builder = new SqliteConnectionStringBuilder(connectionString) { Mode = SqliteOpenMode.ReadOnly, Pooling = false };
            _connection = builder.ToString();
        }
        #region BEGIN Public
        /// <inheritdoc/>
        public async Task<SupportDatabaseStatus> InspectAsync(CancellationToken ct = default)
        {
            using var db = new SqliteConnection(_connection);
            await db.OpenAsync(ct);
            using var command = db.CreateCommand();
            command.CommandText = "PRAGMA quick_check";
            var integrity = await command.ExecuteScalarAsync(ct) as string == "ok";
            command.CommandText = "PRAGMA foreign_key_check";
            using (var reader = await command.ExecuteReaderAsync(ct))
                integrity &= !await reader.ReadAsync(ct);
            command.CommandText = "SELECT max(version) FROM schema_migrations";
            var raw = await command.ExecuteScalarAsync(ct);
            return new(raw is null or DBNull ? null : Convert.ToInt32(raw, CultureInfo.InvariantCulture), integrity);
        }
        #endregion END Public
    }
}

