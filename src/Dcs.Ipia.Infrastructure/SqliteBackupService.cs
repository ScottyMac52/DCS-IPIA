using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Microsoft.Data.Sqlite;

namespace Dcs.Ipia.Infrastructure;

/// <summary>Creates and verifies SQLite backups.</summary>
/// <param name="connectionString">The connection string.</param>
public sealed class SqliteBackupService(string connectionString) {
 #region BEGIN Public
 /// <summary>Copies the database and verifies its integrity.</summary>
 /// <param name="path">The path.</param>
 public void Backup(string path){ArgumentException.ThrowIfNullOrWhiteSpace(path);using var source=new SqliteConnection(connectionString);using var target=new SqliteConnection($"Data Source={path}");source.Open();target.Open();source.BackupDatabase(target);using var cmd=target.CreateCommand();cmd.CommandText="PRAGMA integrity_check";if(!string.Equals((string?)cmd.ExecuteScalar(),"ok",StringComparison.Ordinal))throw new InvalidDataException("SQLite backup integrity verification failed.");}
 #endregion END Public
}
