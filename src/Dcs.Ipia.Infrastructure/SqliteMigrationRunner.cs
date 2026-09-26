using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Microsoft.Data.Sqlite;

namespace Dcs.Ipia.Infrastructure;

/// <summary>Applies SQLite schema migrations.</summary>
/// <param name="connectionString">The connection string.</param>
public sealed class SqliteMigrationRunner(string connectionString) {
 #region BEGIN Public
 /// <summary>Applies pending SQLite migrations.</summary>
 /// <param name="ct">Cancellation token for the operation.</param>
 /// <returns>A task that completes when the operation finishes</returns>
 public async Task MigrateAsync(CancellationToken ct=default) {
  await using var c=new SqliteConnection(connectionString);await c.OpenAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);
  await Execute(c,tx,"""CREATE TABLE IF NOT EXISTS schema_migrations(version INTEGER NOT NULL PRIMARY KEY,applied_utc TEXT NOT NULL); CREATE TABLE IF NOT EXISTS modules(id TEXT NOT NULL PRIMARY KEY,dcs_id TEXT NOT NULL UNIQUE COLLATE NOCASE,display_name TEXT NOT NULL); INSERT OR IGNORE INTO schema_migrations VALUES(1,strftime('%Y-%m-%dT%H:%M:%fZ','now'));""",ct);
  if(!await Applied(c,tx,2,ct)) {
   var migration2="ALTER TABLE modules ADD COLUMN provenance TEXT NOT NULL DEFAULT 'phase-0';\n"+
    "CREATE TABLE device_profiles(id TEXT NOT NULL PRIMARY KEY,module_id TEXT NOT NULL REFERENCES modules(id) ON DELETE CASCADE,device_id TEXT NOT NULL,instance TEXT NULL,role TEXT NOT NULL,source_name TEXT NOT NULL,UNIQUE(module_id,device_id,instance));\n"+
    "CREATE TABLE modifiers(module_id TEXT NOT NULL REFERENCES modules(id) ON DELETE CASCADE,name TEXT NOT NULL COLLATE NOCASE,device TEXT NOT NULL,key TEXT NOT NULL,mode INTEGER NOT NULL,PRIMARY KEY(module_id,name),UNIQUE(module_id,device,key));\n"+
    "CREATE TABLE bindings(module_id TEXT NOT NULL REFERENCES modules(id) ON DELETE CASCADE,ordinal INTEGER NOT NULL,profile_id TEXT NOT NULL REFERENCES device_profiles(id) ON DELETE CASCADE,section INTEGER NOT NULL,command_id TEXT NOT NULL,command_name TEXT NOT NULL,disposition INTEGER NOT NULL,input_key TEXT NOT NULL,reformers_json TEXT NOT NULL,filter_json TEXT NULL,PRIMARY KEY(module_id,ordinal));\n"+
    "INSERT INTO schema_migrations VALUES(2,strftime('%Y-%m-%dT%H:%M:%fZ','now'));";
   await Execute(c,tx,migration2,ct);
  }
  if(!await Applied(c,tx,3,ct)) {
   await Execute(c,tx,"""CREATE TABLE editor_workspaces(module_id TEXT NOT NULL PRIMARY KEY REFERENCES modules(id) ON DELETE CASCADE,snapshot_json TEXT NOT NULL,updated_utc TEXT NOT NULL); INSERT INTO schema_migrations VALUES(3,strftime('%Y-%m-%dT%H:%M:%fZ','now'));""",ct);
  }
  if(!await Applied(c,tx,4,ct)) {
   await Execute(c,tx,"""CREATE TABLE app_preferences(id INTEGER NOT NULL PRIMARY KEY CHECK(id=1),schema_version INTEGER NOT NULL,theme TEXT NOT NULL,occupied_destination_behavior TEXT NOT NULL); INSERT INTO schema_migrations VALUES(4,strftime('%Y-%m-%dT%H:%M:%fZ','now'));""",ct);
  }
  await tx.CommitAsync(ct);
 }
 #endregion END Public
 #region BEGIN private helpers
 private static async Task<bool> Applied(SqliteConnection c,System.Data.Common.DbTransaction tx,int version,CancellationToken ct){await using var cmd=c.CreateCommand();cmd.Transaction=(SqliteTransaction)tx;cmd.CommandText="SELECT COUNT(*) FROM schema_migrations WHERE version=$version";cmd.Parameters.AddWithValue("$version",version);return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct),System.Globalization.CultureInfo.InvariantCulture)==1;}
 private static async Task Execute(SqliteConnection c,System.Data.Common.DbTransaction tx,string sql,CancellationToken ct){await using var cmd=c.CreateCommand();cmd.Transaction=(SqliteTransaction)tx;cmd.CommandText=sql;await cmd.ExecuteNonQueryAsync(ct);}
 #endregion END private helpers
}
