using System.Text.Json;
using Heimdall.Application;
using Heimdall.Domain;
using Microsoft.Data.Sqlite;

using Heimdall.Application.Interfaces;

namespace Heimdall.Infrastructure
{
    /// <summary>Persists module configurations in SQLite.</summary>
    /// <param name="connectionString">The connection string.</param>
    public sealed class SqliteConfigurationRepository(string connectionString) : IModuleConfigurationRepository
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        #region BEGIN Public
        /// <summary>Atomically replaces a module configuration.</summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>A task that completes when the operation finishes</returns>
        public async Task ReplaceAsync(ModuleConfiguration configuration, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            await using var c = await Open(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await ReplaceInTransactionAsync(c, (SqliteTransaction)tx, configuration, ct);
            await tx.CommitAsync(ct);
        }

        internal static async Task ReplaceInTransactionAsync(SqliteConnection c, SqliteTransaction tx,
                                                              ModuleConfiguration configuration,
                                                              CancellationToken ct)
        {
            await ExecuteCommandAsync(Command(
                c, tx,
                "INSERT INTO modules(id,dcs_id,display_name,provenance) VALUES($id,$dcs,$name,$provenance) ON CONFLICT(id) DO UPDATE SET dcs_id=excluded.dcs_id,display_name=excluded.display_name,provenance=excluded.provenance",
                ("$id", configuration.Module.Id.ToString("D")), ("$dcs", configuration.Module.DcsId),
                ("$name", configuration.Module.DisplayName), ("$provenance", configuration.Provenance)), ct);
            await ExecuteCommandAsync(Command(c, tx, "DELETE FROM device_profiles WHERE module_id=$id",
                          ("$id", configuration.Module.Id.ToString("D"))), ct);
            await ExecuteCommandAsync(Command(c, tx, "DELETE FROM modifiers WHERE module_id=$id",
                          ("$id", configuration.Module.Id.ToString("D"))), ct);
            foreach (var profile in configuration.Profiles)
                await ExecuteCommandAsync(Command(c, tx, "INSERT INTO device_profiles VALUES($id,$module,$device,$instance,$role,$source)",
                              ("$id", profile.Id.ToString("D")), ("$module", configuration.Module.Id.ToString("D")),
                              ("$device", profile.DeviceId),
                              ("$instance", profile.Instance is null ? DBNull.Value : profile.Instance),
                              ("$role", profile.Role), ("$source", profile.SourceName)), ct);
            foreach (var modifier in configuration.Modifiers)
                await ExecuteCommandAsync(Command(c, tx, "INSERT INTO modifiers VALUES($module,$name,$device,$key,$mode)",
                              ("$module", configuration.Module.Id.ToString("D")), ("$name", modifier.Name),
                              ("$device", modifier.Device), ("$key", modifier.Key), ("$mode", (int)modifier.Mode)), ct);
            var ordinal = 0;
            foreach (var binding in configuration.Bindings)
                await ExecuteCommandAsync(Command(
                    c, tx,
                    "INSERT INTO bindings VALUES($module,$ordinal,$profile,$section,$command,$name,$disposition,$key,$reformers,$filter)",
                    ("$module", configuration.Module.Id.ToString("D")), ("$ordinal", ordinal++),
                    ("$profile", binding.ProfileId.ToString("D")), ("$section", (int)binding.Section),
                    ("$command", binding.CommandId), ("$name", binding.CommandName),
                    ("$disposition", (int)binding.Disposition), ("$key", binding.InputKey),
                    ("$reformers", JsonSerializer.Serialize(binding.Reformers, JsonOptions)),
                    ("$filter",
                     binding.Filter is null ? DBNull.Value : JsonSerializer.Serialize(binding.Filter, JsonOptions))), ct);
        }
        /// <summary>Gets an item by its stable identifier.</summary>
        /// <param name="moduleId">The module identifier.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>The result of the operation.</returns>
        public async Task<ModuleConfiguration?> GetAsync(Guid moduleId, CancellationToken ct = default)
        {
            await using var c = await Open(ct);
            return await Read(c, moduleId, ct);
        }
        /// <summary>Lists stored items.</summary>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>The result of the operation.</returns>
        public async Task<IReadOnlyList<ModuleConfiguration>> ListAsync(CancellationToken ct = default)
        {
            await using var c = await Open(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT id FROM modules ORDER BY dcs_id COLLATE NOCASE,id";
            var ids = new List<Guid>();
            await using (var r = await cmd.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct))
                ids.Add(Guid.Parse(r.GetString(0)));
            var result = new List<ModuleConfiguration>();
            foreach (var id in ids)
                result.Add((await Read(c, id, ct))!);
            return result;
        }
        #endregion END Public
        #region BEGIN private helpers
        private static async Task<ModuleConfiguration?> Read(SqliteConnection c, Guid id, CancellationToken ct)
        {
            await using var moduleCommand = c.CreateCommand();
            moduleCommand.CommandText = "SELECT dcs_id,display_name,provenance FROM modules WHERE id=$id";
            moduleCommand.Parameters.AddWithValue("$id", id.ToString("D"));
            string dcsId, name, provenance;
            await using (var r = await moduleCommand.ExecuteReaderAsync(ct))
            {
                if (!await r.ReadAsync(ct))
                    return null;
                dcsId = r.GetString(0);
                name = r.GetString(1);
                provenance = r.GetString(2);
            }
            var profiles = new List<DeviceProfile>();
            await using var pc = c.CreateCommand();
            pc.CommandText =
                "SELECT id,device_id,instance,role,source_name FROM device_profiles WHERE module_id=$id ORDER BY device_id,instance,id";
            pc.Parameters.AddWithValue("$id", id.ToString("D"));
            await using (var r = await pc.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct))
                profiles.Add(new(Guid.Parse(r.GetString(0)), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2),
                                 r.GetString(3), r.GetString(4)));
            var modifiers = new List<DcsModifier>();
            await using var mc = c.CreateCommand();
            mc.CommandText = "SELECT name,device,key,mode FROM modifiers WHERE module_id=$id ORDER BY name";
            mc.Parameters.AddWithValue("$id", id.ToString("D"));
            await using (var r = await mc.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct))
                modifiers.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), (ModifierMode)r.GetInt32(3)));
            var bindings = new List<DcsBinding>();
            await using var bc = c.CreateCommand();
            bc.CommandText =
                "SELECT profile_id,section,command_id,command_name,disposition,input_key,reformers_json,filter_json FROM bindings WHERE module_id=$id ORDER BY ordinal";
            bc.Parameters.AddWithValue("$id", id.ToString("D"));
            await using (var r = await bc.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct))
            {
                var reformers = JsonSerializer.Deserialize<string[]>(r.GetString(6), JsonOptions) ?? [];
                var filter = r.IsDBNull(7) ? null : JsonSerializer.Deserialize<AxisFilter>(r.GetString(7), JsonOptions);
                bindings.Add(new(Guid.Parse(r.GetString(0)), (BindingSection)r.GetInt32(1), r.GetString(2),
                                 r.GetString(3), (BindingDisposition)r.GetInt32(4), r.GetString(5), reformers, filter));
            }
            return new(new(id, dcsId, name), profiles, modifiers, bindings, provenance);
        }
        private static async Task ExecuteCommandAsync(SqliteCommand command, CancellationToken ct)
        {
            await using (command)
                await command.ExecuteNonQueryAsync(ct);
        }
        private static SqliteCommand Command(SqliteConnection c, System.Data.Common.DbTransaction tx, string text,
                                             params(string Name, object Value)[] values)
        {
            var cmd = c.CreateCommand();
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = text;
            foreach (var value in values)
                cmd.Parameters.AddWithValue(value.Name, value.Value);
            return cmd;
        }
        private async Task<SqliteConnection> Open(CancellationToken ct)
        {
            var c = new SqliteConnection(connectionString);
            await c.OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_keys=ON";
            await cmd.ExecuteNonQueryAsync(ct);
            return c;
        }
        #endregion END private helpers
    }
}
