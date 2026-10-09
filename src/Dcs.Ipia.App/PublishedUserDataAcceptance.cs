using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Dcs.Ipia.Rendering;
using Microsoft.Data.Sqlite;

namespace DcsIpia.Presentation
{
    /// <summary>Reopens actual bundled, generic and custom module data through the published executable.</summary>
    public static class PublishedUserDataAcceptance
    {
        #region BEGIN Public
        /// <summary>Seeds once and compares saved sources, assignments, graphics and native Lua on later installs.</summary>
        /// <param name="folder">Isolated acceptance profile.</param>
        /// <param name="revision">Verified custom drawing with captured editable source.</param>
        /// <returns>Completion after saved-state behavior matches the initial capture.</returns>
        public static async Task VerifyAsync(string folder, UserMappingRevision revision)
        {
            var connection = new SqliteConnectionStringBuilder { DataSource = Path.Combine(folder, "ipia.db"), Pooling = false }.ToString();
            var repository = new SqliteEditorWorkspaceRepository(connection);
            var manifest = Path.Combine(folder, "release-user-data.json");
            if (!File.Exists(manifest))
            {
                if ((await repository.ListAsync()).Count != 0) throw new InvalidDataException("Acceptance profile has modules but no baseline evidence.");
                var defaults = await new SqliteSharedUiLayerWorkspaceRepository(connection).SaveAsync(await SharedUiLayerWorkspaceFactory.CreateAsync());
                var hardware = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
                await repository.SaveAsync(NewModuleWorkspaceFactory.Create("RELEASE-BUNDLED", "Bundled acceptance", defaults, hardware));
                var unit = new HardwareInventoryInstance(Guid.NewGuid(), "Acceptance panel", revision.Definition.Id, "Pilot panel", "Pilot", true, []);
                await new SqliteHardwareInventoryRepository(connection).SaveAsync(new(1, [revision.Definition], [unit], []));
                var choice = new InventoryHardwareChoice(unit, revision.Definition);
                foreach (var custom in new[] { false, true })
                {
                    var workspace = NewModuleWorkspaceFactory.Create(custom ? "RELEASE-CUSTOM" : "RELEASE-GENERIC",
                        custom ? "Custom acceptance" : "Generic acceptance", defaults, hardware, true);
                    workspace = InventoryHardwareAuthoring.Add(workspace, choice, AssignmentLayer.Module);
                    var command = new DraftCommand("d3001pnilu3001cdnilvd1vpnilvunil", "Acceptance button", false, AssignmentLayer.Module);
                    workspace = workspace with
                    {
                        ModuleCommands = [new(command.Id, command.Name, "Acceptance", false)],
                        Assignments = [new(new(workspace.Configuration.Module.Id, workspace.Configuration.Profiles.Single().Id,
                            AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1"), command)]
                    };
                    if (custom) workspace = UserMappingAdoption.Apply(workspace, workspace.Configuration.Profiles.Single().Id, revision);
                    await repository.SaveAsync(workspace);
                }
                File.WriteAllText(manifest, JsonSerializer.Serialize(await Evidence(repository)));
            }
            var actual = await Evidence(repository);
            var expected = JsonSerializer.Deserialize<SortedDictionary<string, string>>(File.ReadAllText(manifest))
                ?? throw new InvalidDataException("Acceptance baseline could not be read.");
            if (JsonSerializer.Serialize(actual) != JsonSerializer.Serialize(expected))
                throw new InvalidDataException("Saved modules, native Lua or rendered drawings changed after reopen/install.");
            var readOnly = new SqliteConnectionStringBuilder(connection) { Mode = SqliteOpenMode.ReadOnly }.ToString();
            var reports = new SupportReportService(new SqliteSupportDatabaseInspector(readOnly),
                new SqliteHardwareInventoryRepository(readOnly), new SqliteEditorWorkspaceRepository(readOnly, false),
                new FileUserMappingLibrary(folder), typeof(App).Assembly.GetName().Version!, SqliteMigrationRunner.CurrentVersion);
            var report = JsonSerializer.Deserialize<SupportReport>(await reports.CaptureAsync())!;
            if (report.Failures.Count != 0 || report.Modules?.Count != 3 || report.CustomMappingRevisions != 1 ||
                report.Devices?.Count != 1 || report.Modules.Sum(x => x.GenericDrawings) != 1 || report.Modules.Sum(x => x.CustomDrawings) != 1)
                throw new InvalidDataException("Published support evidence differs from actual persisted fixtures.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static async Task<SortedDictionary<string, string>> Evidence(SqliteEditorWorkspaceRepository repository)
        {
            var evidence = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var id in await repository.ListAsync())
            {
                var workspace = await repository.GetAsync(id) ?? throw new InvalidDataException("Acceptance module disappeared.");
                GenericHardwareLayout.ValidateWorkspace(workspace);
                var name = workspace.Configuration.Module.DcsId;
                evidence[name + "/snapshot"] = Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(workspace)));
                var payload = SnapshotLuaPayloadRenderer.Generate(workspace);
                foreach (var (path, bytes) in payload) evidence[name + "/lua/" + path] = Hash(bytes);
                foreach (var diagram in workspace.Diagrams)
                    evidence[name + "/drawing/" + diagram.ProfileId] = Hash(CanonicalSvgRenderer.RenderComplete(diagram.Svg, diagram.Width, diagram.Height));
            }
            if (evidence.Count < 10) throw new InvalidDataException("Acceptance evidence lacks saved profiles or output.");
            return evidence;
        }
        private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
        #endregion END private helpers
    }
}

