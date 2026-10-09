using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Tests real saved-state diagnostics, privacy, read-only behavior and recoverable failures.</summary>
    public sealed class SupportReportTests : IDisposable
    {
        private const string Secret = "PRIVATE-user-artwork-and-path";
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "ipia-support-" + Guid.NewGuid().ToString("N"));
        private string Connection => new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString();
        private string DatabasePath => Path.Combine(_folder, "ipia.db");
        #region BEGIN Public
        /// <summary>Reports useful generic/custom/bundled evidence without exposing records or changing saved bytes.</summary>
        [Fact]
        public async Task Saved_evidence_is_allow_listed_and_read_only()
        {
            await Create();
            var definition = GenericHardwareFixture.Definition();
            var unit = GenericHardwareFixture.Unit(definition, Secret);
            var snapshot = new HardwareInventorySnapshot(1, [definition], [unit.Instance],
                [new(new(unit.Instance.PhysicalGuid, Secret, false), null, Secret)]);
            await new SqliteHardwareInventoryRepository(Connection).SaveAsync(snapshot);
            var workspace = InventoryHardwareAuthoring.Add(GenericHardwareFixture.Workspace(), unit, AssignmentLayer.Module);
            workspace = workspace with { Sources = workspace.Sources!.ToDictionary(x => x.Key, x => x.Value).Append(new KeyValuePair<string, string>(Secret, _folder)).ToDictionary() };
            await new SqliteEditorWorkspaceRepository(Connection).SaveAsync(workspace);
            var mappingDefinition = new HardwareDefinition("unused", ControllerCapabilityMapper.Map(
                [new ControllerObject(Guid.Parse("a36d02f0-c9f3-11cf-bfc7-444553540000"), 12, Secret)]));
            mappingDefinition = mappingDefinition with { Id = ControllerCapabilityMapper.Fingerprint(mappingDefinition.Capabilities) };
            var revision = DrawioMappingImporter.Import(Drawing(), mappingDefinition, Secret + ".drawio", "1.2.3.4");
            await new FileUserMappingLibrary(_folder).SaveAsync(revision);
            var custom = InventoryHardwareAuthoring.Add(Empty("CUSTOM"), GenericHardwareFixture.Unit(mappingDefinition, Secret), AssignmentLayer.Module);
            custom = UserMappingAdoption.Apply(custom, custom.Configuration.Profiles.Single().Id, revision);
            await new SqliteEditorWorkspaceRepository(Connection).SaveAsync(custom);
            var bundledProfile = new DeviceProfile(Guid.NewGuid(), "tm-mfd", null, Secret, Secret + ".diff.lua");
            var bundled = Empty("BUNDLED") with
            {
                Configuration = new(new(Guid.NewGuid(), "BUNDLED", Secret), [bundledProfile], [], [], Secret),
                Diagrams = [new(Secret, Secret, Secret, 100, 100, Encoding.UTF8.GetBytes(Secret), []) { ProfileId = bundledProfile.Id }]
            };
            await new SqliteEditorWorkspaceRepository(Connection).SaveAsync(bundled);
            var before = Hashes();
            var text = await Service().CaptureAsync();
            var report = JsonSerializer.Deserialize<SupportReport>(text)!;
            Assert.Equal("1.2.3.4", report.ApplicationVersion);
            Assert.Equal(SqliteMigrationRunner.CurrentVersion, report.Database!.SchemaVersion);
            Assert.True(report.Database.IntegrityPassed);
            Assert.Empty(report.Failures);
            Assert.Equal(1, report.CustomMappingRevisions);
            var device = Assert.Single(report.Devices!);
            Assert.Equal(definition.Capabilities.Inputs.Count, device.SupportedInputs);
            Assert.Equal(definition.Capabilities.Unsupported.Count, device.UnsupportedInputs);
            Assert.True(device.ObservationFailed);
            Assert.True(device.Selected);
            Assert.Equal(3, report.Modules!.Count);
            Assert.Equal(1, report.Modules.Sum(x => x.CustomDrawings));
            Assert.Equal(1, report.Modules.Sum(x => x.GenericDrawings));
            Assert.Equal(1, report.Modules.Sum(x => x.BundledDrawings));
            Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);
            Assert.DoesNotContain(_folder, text, StringComparison.Ordinal);
            Assert.DoesNotContain(unit.Instance.PhysicalGuid.ToString("D"), text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(revision.Id, text, StringComparison.Ordinal);
            Assert.DoesNotContain(workspace.Configuration.Module.Id.ToString("D"), text, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(before, Hashes());
        }

        /// <summary>Legacy snapshots can be projected without auto-saving their normal read upgrade.</summary>
        [Fact]
        public async Task Legacy_capture_does_not_persist_read_upgrades()
        {
            await Create();
            await new SqliteEditorWorkspaceRepository(Connection).SaveAsync(Empty("LEGACY"));
            Execute("UPDATE editor_workspaces SET snapshot_json=json_set(snapshot_json,'$.Version',1)");
            var before = File.ReadAllBytes(DatabasePath);
            Assert.Empty(JsonSerializer.Deserialize<SupportReport>(await Service().CaptureAsync())!.Failures);
            Assert.Equal(before, File.ReadAllBytes(DatabasePath));
        }

        /// <summary>Corrupt modules and drawings preserve independently readable evidence and hide raw errors.</summary>
        [Fact]
        public async Task Partial_failures_are_not_silently_empty()
        {
            await Create();
            var repository = new SqliteEditorWorkspaceRepository(Connection);
            await repository.SaveAsync(Empty("GOOD"));
            await repository.SaveAsync(Empty("BAD"));
            Execute("UPDATE editor_workspaces SET snapshot_json='PRIVATE invalid JSON' WHERE module_id=(SELECT id FROM modules WHERE dcs_id='BAD')");
            Directory.CreateDirectory(Path.Combine(_folder, "UserMappings"));
            File.WriteAllText(Path.Combine(_folder, "UserMappings", "broken.json"), "{PRIVATE");
            Execute("DROP TABLE hardware_inventory");
            var report = JsonSerializer.Deserialize<SupportReport>(await Service().CaptureAsync())!;
            Assert.Null(report.Devices);
            Assert.Null(report.CustomMappingRevisions);
            Assert.Single(report.Modules!);
            Assert.Equal(3, report.Failures.Count);
            Assert.Contains(report.Failures, x => x.Stage.StartsWith("module-", StringComparison.Ordinal) && x.Category == "invalid-data");
            Assert.Contains(report.Failures, x => x.Stage == "inventory");
            Assert.Contains(report.Failures, x => x.Stage == "custom-mappings");
            Assert.DoesNotContain("PRIVATE", JsonSerializer.Serialize(report), StringComparison.Ordinal);
            Execute("DROP TABLE editor_workspaces");
            Assert.Null(JsonSerializer.Deserialize<SupportReport>(await Service().CaptureAsync())!.Modules);
        }

        /// <summary>Fixed failure categories and advice replace exception messages, paths and stack details.</summary>
        /// <param name="category">Injected failure category.</param>
        [Theory]
        [InlineData("access-denied")]
        [InlineData("invalid-data")]
        [InlineData("file-unavailable")]
        [InlineData("read-failed")]
        public async Task Failure_evidence_is_sanitized(string category)
        {
            await Create();
            Exception error = category switch
            {
                "access-denied" => new UnauthorizedAccessException(Secret),
                "invalid-data" => new InvalidDataException(Secret),
                "file-unavailable" => new IOException(Secret),
                _ => new InvalidOperationException(Secret)
            };
            var text = await Service(new FailingSupportDatabaseInspector(error)).CaptureAsync();
            var report = JsonSerializer.Deserialize<SupportReport>(text)!;
            Assert.Null(report.Database);
            var failure = Assert.Single(report.Failures);
            Assert.Equal("database", failure.Stage);
            Assert.Equal(category, failure.Category);
            Assert.NotEmpty(failure.NextStep);
            Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);
        }

        /// <summary>Missing/corrupt startup databases are diagnosed without creating or repairing them.</summary>
        /// <param name="corrupt">Whether corrupt existing bytes replace an absent database.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Startup_failure_reports_do_not_create_or_repair_data(bool corrupt)
        {
            Directory.CreateDirectory(_folder);
            if (corrupt) File.WriteAllText(DatabasePath, Secret);
            var text = await Service().CaptureAsync();
            var report = JsonSerializer.Deserialize<SupportReport>(text)!;
            Assert.Null(report.Database);
            Assert.Null(report.Devices);
            Assert.Null(report.Modules);
            Assert.Equal(3, report.Failures.Count);
            if (corrupt) Assert.Equal(Secret, File.ReadAllText(DatabasePath));
            else Assert.False(File.Exists(DatabasePath));
            Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);
        }

        /// <summary>Newer schemas and invalid foreign keys offer actionable, non-destructive recovery evidence.</summary>
        [Fact]
        public async Task Schema_and_integrity_failures_remain_explicit()
        {
            await Create();
            Execute("INSERT INTO schema_migrations VALUES(99,'future'); PRAGMA foreign_keys=OFF; INSERT INTO device_profiles VALUES('orphan','missing','panel',NULL,'role','name')");
            var report = JsonSerializer.Deserialize<SupportReport>(await Service().CaptureAsync())!;
            Assert.Equal(99, report.Database!.SchemaVersion);
            Assert.False(report.Database.IntegrityPassed);
            Assert.Contains(report.Failures, x => x.Category == "unsupported-schema");
            Assert.Contains(report.Failures, x => x.Category == "integrity-failed");
        }

        /// <summary>Export matches the captured preview after saved data changes; cancellation and existing files stay safe.</summary>
        [Fact]
        public async Task Preview_is_exported_verbatim_without_overwrite()
        {
            await Create();
            var service = Service();
            var preview = await service.CaptureAsync();
            await new SqliteEditorWorkspaceRepository(Connection).SaveAsync(Empty("LATER"));
            var file = Path.Combine(_folder, "reviewed.json");
            SupportReportFile.WriteNew(file, preview);
            Assert.Equal(Encoding.UTF8.GetBytes(preview), File.ReadAllBytes(file));
            Assert.Throws<IOException>(() => SupportReportFile.WriteNew(file, "changed"));
            Assert.Equal(preview, File.ReadAllText(file));
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CaptureAsync(cancel.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(new FailingSupportDatabaseInspector(new OperationCanceledException())).CaptureAsync());
        }

        /// <summary>A failure after earlier migration statements rolls back every pending schema/data change.</summary>
        [Fact]
        public async Task Failed_migration_rolls_back_and_can_be_retried()
        {
            await Create();
            await new SqliteEditorWorkspaceRepository(Connection).SaveAsync(Empty("RETAINED"));
            Execute("DROP TABLE hardware_inventory; DROP TABLE hardware_definitions; DROP TABLE hardware_observations; DELETE FROM schema_migrations WHERE version>=8");
            await Assert.ThrowsAsync<SqliteException>(() => new SqliteMigrationRunner(Connection).MigrateAsync());
            using (var db = new SqliteConnection(Connection))
            {
                db.Open();
                using var query = db.CreateCommand();
                query.CommandText = "SELECT max(version) FROM schema_migrations";
                Assert.Equal(7L, query.ExecuteScalar());
                query.CommandText = "SELECT count(*) FROM sqlite_master WHERE name='hardware_inventory'";
                Assert.Equal(0L, query.ExecuteScalar());
            }
            Execute("DROP TABLE profile_setup");
            await new SqliteMigrationRunner(Connection).MigrateAsync();
            Assert.Single((await new SqliteEditorWorkspaceRepository(Connection).ListAsync()));
            Assert.Empty(JsonSerializer.Deserialize<SupportReport>(await Service().CaptureAsync())!.Failures);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
            GC.SuppressFinalize(this);
        }
        #endregion END Public
        #region BEGIN private helpers
        private async Task Create()
        {
            Directory.CreateDirectory(_folder);
            await new SqliteMigrationRunner(Connection).MigrateAsync();
        }
        private SupportReportService Service(ISupportDatabaseInspector? inspector = null)
        {
            var readOnly = new SqliteConnectionStringBuilder(Connection) { Mode = SqliteOpenMode.ReadOnly }.ToString();
            return new(inspector ?? new SqliteSupportDatabaseInspector(readOnly),
                new SqliteHardwareInventoryRepository(readOnly), new SqliteEditorWorkspaceRepository(readOnly, false),
                new FileUserMappingLibrary(_folder), new Version(1, 2, 3, 4), SqliteMigrationRunner.CurrentVersion);
        }
        private void Execute(string sql)
        {
            using var db = new SqliteConnection(Connection);
            db.Open();
            using var query = db.CreateCommand();
            query.CommandText = sql;
            query.ExecuteNonQuery();
        }
        private string[] Hashes() => Directory.EnumerateFiles(_folder, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
            .Select(path => Path.GetRelativePath(_folder, path) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))).ToArray();
        private static EditorWorkspaceSnapshot Empty(string id) => new(new(new(Guid.NewGuid(), id, Secret), [], [], [], Secret),
            [], [], [], [], [], [], new Dictionary<string, string>());
        private static string Drawing() => """
            <mxfile><diagram><mxGraphModel pageWidth="900" pageHeight="600"><root>
            <mxCell id="0"/><mxCell id="1" parent="0"/>
            <mxCell id="canvas" parent="1" vertex="1" style="fillColor=#ffffff;"><mxGeometry width="900" height="600"/></mxCell>
            <mxCell id="label-JOY_BTN1" parent="1" vertex="1" value="PRIVATE-user-artwork-and-path"><mxGeometry x="10" y="100" width="300" height="50"/></mxCell>
            <mxCell id="footer" parent="1" vertex="1" value="PRIVATE-user-artwork-and-path"><mxGeometry x="0" y="575" width="900" height="25"/></mxCell>
            </root></mxGraphModel></diagram></mxfile>
            """;
        #endregion END private helpers
    }
}

