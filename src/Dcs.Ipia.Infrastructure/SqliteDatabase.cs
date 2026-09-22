using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Microsoft.Data.Sqlite;

namespace Dcs.Ipia.Infrastructure;

public sealed class SqliteMigrationRunner(string connectionString) {
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
  await tx.CommitAsync(ct);
 }
 private static async Task<bool> Applied(SqliteConnection c,System.Data.Common.DbTransaction tx,int version,CancellationToken ct){var cmd=c.CreateCommand();cmd.Transaction=(SqliteTransaction)tx;cmd.CommandText="SELECT COUNT(*) FROM schema_migrations WHERE version=$version";cmd.Parameters.AddWithValue("$version",version);return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct))==1;}
 private static async Task Execute(SqliteConnection c,System.Data.Common.DbTransaction tx,string sql,CancellationToken ct){var cmd=c.CreateCommand();cmd.Transaction=(SqliteTransaction)tx;cmd.CommandText=sql;await cmd.ExecuteNonQueryAsync(ct);}
}

public sealed class SqliteModuleRepository(string connectionString):IModuleRepository {
 public async Task<IReadOnlyList<DcsModule>> ListAsync(CancellationToken ct=default){await using var c=await Open(ct);var cmd=c.CreateCommand();cmd.CommandText="SELECT id,dcs_id,display_name FROM modules ORDER BY dcs_id COLLATE NOCASE,id";await using var r=await cmd.ExecuteReaderAsync(ct);var result=new List<DcsModule>();while(await r.ReadAsync(ct))result.Add(Read(r));return result;}
 public async Task<DcsModule?> GetAsync(Guid id,CancellationToken ct=default){await using var c=await Open(ct);var cmd=c.CreateCommand();cmd.CommandText="SELECT id,dcs_id,display_name FROM modules WHERE id=$id";cmd.Parameters.AddWithValue("$id",id.ToString("D"));await using var r=await cmd.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?Read(r):null;}
 public async Task UpsertAsync(DcsModule value,CancellationToken ct=default){await using var c=await Open(ct);var cmd=c.CreateCommand();cmd.CommandText="INSERT INTO modules(id,dcs_id,display_name) VALUES($id,$dcs,$name) ON CONFLICT(id) DO UPDATE SET dcs_id=excluded.dcs_id,display_name=excluded.display_name";cmd.Parameters.AddWithValue("$id",value.Id.ToString("D"));cmd.Parameters.AddWithValue("$dcs",value.DcsId);cmd.Parameters.AddWithValue("$name",value.DisplayName);await cmd.ExecuteNonQueryAsync(ct);}
 public async Task<bool> DeleteAsync(Guid id,CancellationToken ct=default){await using var c=await Open(ct);var cmd=c.CreateCommand();cmd.CommandText="DELETE FROM modules WHERE id=$id";cmd.Parameters.AddWithValue("$id",id.ToString("D"));return await cmd.ExecuteNonQueryAsync(ct)==1;}
 private async Task<SqliteConnection> Open(CancellationToken ct){var c=new SqliteConnection(connectionString);await c.OpenAsync(ct);var cmd=c.CreateCommand();cmd.CommandText="PRAGMA foreign_keys=ON";await cmd.ExecuteNonQueryAsync(ct);return c;}
 private static DcsModule Read(SqliteDataReader r)=>new(Guid.Parse(r.GetString(0)),r.GetString(1),r.GetString(2));
}

public sealed class SqliteConfigurationRepository(string connectionString):IModuleConfigurationRepository {
 private static readonly JsonSerializerOptions JsonOptions=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
 public async Task ReplaceAsync(ModuleConfiguration configuration,CancellationToken ct=default) {
  ArgumentNullException.ThrowIfNull(configuration);await using var c=await Open(ct);await using var tx=await c.BeginTransactionAsync(ct);
  await Command(c,tx,"INSERT INTO modules(id,dcs_id,display_name,provenance) VALUES($id,$dcs,$name,$provenance) ON CONFLICT(id) DO UPDATE SET dcs_id=excluded.dcs_id,display_name=excluded.display_name,provenance=excluded.provenance",("$id",configuration.Module.Id.ToString("D")),("$dcs",configuration.Module.DcsId),("$name",configuration.Module.DisplayName),("$provenance",configuration.Provenance)).ExecuteNonQueryAsync(ct);
  await Command(c,tx,"DELETE FROM device_profiles WHERE module_id=$id",("$id",configuration.Module.Id.ToString("D"))).ExecuteNonQueryAsync(ct);
  await Command(c,tx,"DELETE FROM modifiers WHERE module_id=$id",("$id",configuration.Module.Id.ToString("D"))).ExecuteNonQueryAsync(ct);
  foreach(var profile in configuration.Profiles) await Command(c,tx,"INSERT INTO device_profiles VALUES($id,$module,$device,$instance,$role,$source)",("$id",profile.Id.ToString("D")),("$module",configuration.Module.Id.ToString("D")),("$device",profile.DeviceId),("$instance",profile.Instance is null?DBNull.Value:profile.Instance),("$role",profile.Role),("$source",profile.SourceName)).ExecuteNonQueryAsync(ct);
  foreach(var modifier in configuration.Modifiers) await Command(c,tx,"INSERT INTO modifiers VALUES($module,$name,$device,$key,$mode)",("$module",configuration.Module.Id.ToString("D")),("$name",modifier.Name),("$device",modifier.Device),("$key",modifier.Key),("$mode",(int)modifier.Mode)).ExecuteNonQueryAsync(ct);
  var ordinal=0;foreach(var binding in configuration.Bindings) await Command(c,tx,"INSERT INTO bindings VALUES($module,$ordinal,$profile,$section,$command,$name,$disposition,$key,$reformers,$filter)",("$module",configuration.Module.Id.ToString("D")),("$ordinal",ordinal++),("$profile",binding.ProfileId.ToString("D")),("$section",(int)binding.Section),("$command",binding.CommandId),("$name",binding.CommandName),("$disposition",(int)binding.Disposition),("$key",binding.InputKey),("$reformers",JsonSerializer.Serialize(binding.Reformers,JsonOptions)),("$filter",binding.Filter is null?DBNull.Value:JsonSerializer.Serialize(binding.Filter,JsonOptions))).ExecuteNonQueryAsync(ct);
  await tx.CommitAsync(ct);
 }
 public async Task<ModuleConfiguration?> GetAsync(Guid moduleId,CancellationToken ct=default) {await using var c=await Open(ct);return await Read(c,moduleId,ct);}
 public async Task<IReadOnlyList<ModuleConfiguration>> ListAsync(CancellationToken ct=default) {await using var c=await Open(ct);var cmd=c.CreateCommand();cmd.CommandText="SELECT id FROM modules ORDER BY dcs_id COLLATE NOCASE,id";var ids=new List<Guid>();await using(var r=await cmd.ExecuteReaderAsync(ct))while(await r.ReadAsync(ct))ids.Add(Guid.Parse(r.GetString(0)));var result=new List<ModuleConfiguration>();foreach(var id in ids)result.Add((await Read(c,id,ct))!);return result;}
 private static async Task<ModuleConfiguration?> Read(SqliteConnection c,Guid id,CancellationToken ct) {
  var moduleCommand=c.CreateCommand();moduleCommand.CommandText="SELECT dcs_id,display_name,provenance FROM modules WHERE id=$id";moduleCommand.Parameters.AddWithValue("$id",id.ToString("D"));string dcsId,name,provenance;await using(var r=await moduleCommand.ExecuteReaderAsync(ct)){if(!await r.ReadAsync(ct))return null;dcsId=r.GetString(0);name=r.GetString(1);provenance=r.GetString(2);}
  var profiles=new List<DeviceProfile>();var pc=c.CreateCommand();pc.CommandText="SELECT id,device_id,instance,role,source_name FROM device_profiles WHERE module_id=$id ORDER BY device_id,instance,id";pc.Parameters.AddWithValue("$id",id.ToString("D"));await using(var r=await pc.ExecuteReaderAsync(ct))while(await r.ReadAsync(ct))profiles.Add(new(Guid.Parse(r.GetString(0)),r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),r.GetString(3),r.GetString(4)));
  var modifiers=new List<DcsModifier>();var mc=c.CreateCommand();mc.CommandText="SELECT name,device,key,mode FROM modifiers WHERE module_id=$id ORDER BY name";mc.Parameters.AddWithValue("$id",id.ToString("D"));await using(var r=await mc.ExecuteReaderAsync(ct))while(await r.ReadAsync(ct))modifiers.Add(new(r.GetString(0),r.GetString(1),r.GetString(2),(ModifierMode)r.GetInt32(3)));
  var bindings=new List<DcsBinding>();var bc=c.CreateCommand();bc.CommandText="SELECT profile_id,section,command_id,command_name,disposition,input_key,reformers_json,filter_json FROM bindings WHERE module_id=$id ORDER BY ordinal";bc.Parameters.AddWithValue("$id",id.ToString("D"));await using(var r=await bc.ExecuteReaderAsync(ct))while(await r.ReadAsync(ct)){var reformers=JsonSerializer.Deserialize<string[]>(r.GetString(6),JsonOptions)??[];var filter=r.IsDBNull(7)?null:JsonSerializer.Deserialize<AxisFilter>(r.GetString(7),JsonOptions);bindings.Add(new(Guid.Parse(r.GetString(0)),(BindingSection)r.GetInt32(1),r.GetString(2),r.GetString(3),(BindingDisposition)r.GetInt32(4),r.GetString(5),reformers,filter));}
  return new(new(id,dcsId,name),profiles,modifiers,bindings,provenance);
 }
 private static SqliteCommand Command(SqliteConnection c,System.Data.Common.DbTransaction tx,string text,params (string Name,object Value)[] values){var cmd=c.CreateCommand();cmd.Transaction=(SqliteTransaction)tx;cmd.CommandText=text;foreach(var value in values)cmd.Parameters.AddWithValue(value.Name,value.Value);return cmd;}
 private async Task<SqliteConnection> Open(CancellationToken ct){var c=new SqliteConnection(connectionString);await c.OpenAsync(ct);var cmd=c.CreateCommand();cmd.CommandText="PRAGMA foreign_keys=ON";await cmd.ExecuteNonQueryAsync(ct);return c;}
}

public sealed class SqliteBackupService(string connectionString) {
 public void Backup(string path){ArgumentException.ThrowIfNullOrWhiteSpace(path);using var source=new SqliteConnection(connectionString);using var target=new SqliteConnection($"Data Source={path}");source.Open();target.Open();source.BackupDatabase(target);using var cmd=target.CreateCommand();cmd.CommandText="PRAGMA integrity_check";if(!string.Equals((string?)cmd.ExecuteScalar(),"ok",StringComparison.Ordinal))throw new InvalidDataException("SQLite backup integrity verification failed.");}
}
