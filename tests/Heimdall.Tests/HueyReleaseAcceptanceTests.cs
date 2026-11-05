using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Heimdall.Packaging;
using Heimdall.Rendering;
using SkiaSharp;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Measures real Huey release coverage and source-free SQLite snapshot rebuild and rollback.</summary>
    public sealed class HueyReleaseAcceptanceTests
    {
        private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };
        private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "HueyParity");
        #region BEGIN Public
        /// <summary>Real released inputs preserve every native binding and page through Complete export and two clean restores.</summary>
        /// <returns>Completion of SQLite, renderer, package and rollback verification.</returns>
        [Fact]
        public async Task Released_huey_roundtrips_and_reports_complete_output_differences()
        {
            var workspace = Workspace();
            var adapter = new DcsSimProfileBackend();
            var originalWorkspace = DcsEditorSnapshotSerializer.Serialize(workspace);
            var originalPackage = DcsModuleSnapshotWorkflow.Build(DcsModuleSnapshotWorkflow.Freeze(workspace, "3.1.1"));
            var portable = adapter.Capture(workspace);
            portable = JsonSerializer.Deserialize<global::Heimdall.Domain.MultiSim.ProfileDocument>(JsonSerializer.Serialize(portable))!;
            var portableCatalog = adapter.GetCatalog(portable).Commands;
            foreach (var command in workspace.ModuleCommands)
                Assert.Contains(portableCatalog, x => x.Context == portable.ContextId && x.Metadata["nativeId"] == command.Id && x.Category == command.Category && x.DisplayName == command.Name);
            workspace = DcsSimProfileBackend.Restore(portable);
            Assert.Equal(originalWorkspace.Sha256, DcsEditorSnapshotSerializer.Serialize(workspace).Sha256);
            Assert.Equal(originalPackage.Bytes, DcsModuleSnapshotWorkflow.Build(DcsModuleSnapshotWorkflow.Freeze(workspace, "3.1.1")).Bytes);
            var editedPortable = JsonSerializer.Deserialize<global::Heimdall.Domain.MultiSim.ProfileDocument>(JsonSerializer.Serialize(portable))!;
            var axisDevice = editedPortable.Devices.First(x => x.ContextId != "UiLayer" && x.Axes.Count > 0);
            axisDevice.Axes[0].Deadzone = 2.5f;
            var uiDevice = editedPortable.Devices.First(x => x.ContextId == "UiLayer" && x.Buttons.Count > 0);
            uiDevice.Buttons.RemoveAt(0);
            var editedWorkspace = DcsSimProfileBackend.Restore(editedPortable);
            Assert.Equal(workspace.Assignments.Count(x => x.Slot.Layer == AssignmentLayer.Module), editedWorkspace.Assignments.Count(x => x.Slot.Layer == AssignmentLayer.Module));
            Assert.Equal(workspace.Assignments.Count(x => x.Slot.Layer == AssignmentLayer.UiLayer) - 1, editedWorkspace.Assignments.Count(x => x.Slot.Layer == AssignmentLayer.UiLayer));
            Assert.Contains(editedWorkspace.Assignments, x => x.Slot.Layer == AssignmentLayer.Module && x.Filter?.Deadzone == 2.5);
            Assert.Equal(originalWorkspace.Sha256, DcsEditorSnapshotSerializer.Serialize(workspace).Sha256);
            Assert.NotEqual(originalPackage.Bytes, DcsModuleSnapshotWorkflow.Build(DcsModuleSnapshotWorkflow.Freeze(editedWorkspace, "3.1.1")).Bytes);
            Assert.True(adapter.Export(editedPortable).Succeeded);
            var unknownSources = new Dictionary<string, string>(workspace.Sources!, StringComparer.Ordinal);
            var modifierSource = unknownSources.Keys.First(x => x.EndsWith("/modifiers.lua", StringComparison.Ordinal));
            unknownSources[modifierSource] = unknownSources[modifierSource].Replace("local modifiers = {", "local modifiers = { [\"futureFlag\"] = true,", StringComparison.Ordinal);
            var unsafePlan = adapter.Export(adapter.Capture(workspace with { Sources = unknownSources }));
            Assert.False(unsafePlan.Succeeded);
            Assert.Empty(unsafePlan.Files);
            Assert.Contains("futureFlag", Assert.Single(unsafePlan.Diagnostics).Message);
            var export = adapter.Export(portable);
            Assert.True(export.Succeeded, string.Join("; ", export.Diagnostics.Select(x => x.Message)));
            Assert.Equal(DcsSnapshotLuaPayloadRenderer.Generate(workspace).OrderBy(x => x.Key).Select(x => (x.Key, Encoding.UTF8.GetString(x.Value))),
                export.Files.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)));
            Assert.Equal(5, workspace.Configuration.Profiles.Count);
            Assert.Equal(6, workspace.Pages.Count);
            Assert.Equal(3, workspace.Configuration.Profiles.Count(x => x.DeviceId == "tm-mfd"));
            var root = Path.Combine(Path.GetTempPath(), "ipia-huey-acceptance-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var repository = await RepositoryAsync(Path.Combine(root, "original.db"));
                await repository.SaveAsync(workspace);
                var reopened = (await repository.GetAsync(workspace.Configuration.Module.Id))!;
                Assert.Equal(DcsEditorSnapshotSerializer.Serialize(workspace), DcsEditorSnapshotSerializer.Serialize(reopened));
                var lua = DcsSnapshotLuaPayloadRenderer.Generate(reopened);
                var reference = Directory.GetFiles(Path.Combine(FixtureRoot, "Expected"), "*.lua", SearchOption.AllDirectories);
                foreach (var file in reference)
                {
                    var path = Path.GetRelativePath(Path.Combine(FixtureRoot, "Expected"), file).Replace('\\', '/');
                    Assert.True(lua.ContainsKey(path), "Missing released native path: " + path);
                    if (path.EndsWith(".diff.lua", StringComparison.Ordinal))
                    {
                        var expectedBindings = Bindings(File.ReadAllText(file));
                        var actualBindings = Bindings(Encoding.UTF8.GetString(lua[path]));
                        Assert.Equal(expectedBindings, actualBindings);
                    }
                    else
                    {
                        var expected = DcsLuaParser.ParseModifiers(File.ReadAllText(file));
                        var actual = DcsLuaParser.ParseModifiers(Encoding.UTF8.GetString(lua[path]));
                        foreach (var modifier in expected)
                            Assert.Contains(actual, x => x.Name == modifier.Name && x.Device == modifier.Device && x.Key == modifier.Key && x.Mode == modifier.Mode);
                    }
                }
                var original = DcsModuleSnapshotWorkflow.Freeze(reopened, "3.1.1");
                var ovgme = DcsModuleSnapshotWorkflow.Build(original);
                var complete = DcsModuleCompleteReleaseWorkflow.Build(original);
                Assert.Equal(original.Workspace, DcsModuleCompleteReleaseWorkflow.Verify(complete).Workspace);
                Assert.Equal(complete.Bytes, DcsModuleCompleteReleaseWorkflow.Build(original).Bytes);
                var payload = Files(ovgme.Bytes);
                var rendered = DcsSnapshotDeviceKneeboardRenderer.Generate(reopened)
                    .Concat(DcsSnapshotSummaryKneeboardRenderer.Generate(reopened)).ToDictionary(x => x.Key, x => x.Value);
                var legacyPages = Directory.GetFiles(Path.Combine(FixtureRoot, "Expected", "KNEEBOARD"), "*.png", SearchOption.AllDirectories);
                Assert.Equal(6, legacyPages.Length);
                Assert.Equal(legacyPages.Select(x => Path.GetRelativePath(Path.Combine(FixtureRoot, "Expected"), x).Replace('\\', '/')).Order(StringComparer.Ordinal),
                    rendered.Keys.Order(StringComparer.Ordinal));
                var pageReport = legacyPages.Select(file =>
                {
                    var path = Path.GetRelativePath(Path.Combine(FixtureRoot, "Expected"), file).Replace('\\', '/');
                    var expectedBytes = File.ReadAllBytes(file);
                    using var oldImage = SKBitmap.Decode(expectedBytes);
                    using var newImage = SKBitmap.Decode(rendered[path]);
                    Assert.NotNull(oldImage);
                    Assert.NotNull(newImage);
                    return new
                    {
                        path,
                        legacyWidth = oldImage.Width,
                        legacyHeight = oldImage.Height,
                        generatedWidth = newImage.Width,
                        generatedHeight = newImage.Height,
                        legacySha256 = Hash(expectedBytes),
                        generatedSha256 = Hash(rendered[path]),
                        exact = expectedBytes.SequenceEqual(rendered[path])
                    };
                }).ToArray();
                var guides = DcsModuleDocumentationWorkflow.Generate(original);
                var integration = File.ReadAllText(Path.Combine(FixtureRoot, "Source", "docs", "OPENKNEEBOARD-VAICOM.md"));
                Assert.Equal(integration, Assert.Single(DcsModuleIntegrationImport.Instructions(reopened)).Instructions);
                foreach (var line in integration.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
                    Assert.Contains("    " + line, guides["INTEGRATIONS.md"], StringComparison.Ordinal);
                Assert.Contains("INSTALLATION.md", guides.Keys);
                Assert.Contains("CONTROL-MAPPINGS.md", guides.Keys);
                Assert.Contains("THIRD-PARTY-ASSETS.md", guides.Keys);
                var restoredRepository = await RepositoryAsync(Path.Combine(root, "restored.db"));
                var restored = await DcsModuleSnapshotWorkflow.RestoreAsync(DcsModuleSnapshotWorkflow.Read(DcsModuleSnapshotWorkflow.Export(original)), restoredRepository);
                Assert.Equal(original.Workspace, DcsEditorSnapshotSerializer.Serialize(restored));
                Assert.Equal(ovgme.Bytes, DcsModuleSnapshotWorkflow.Build(DcsModuleSnapshotWorkflow.Freeze(restored, "3.1.1")).Bytes);
                Assert.Equal(complete.Bytes, DcsModuleCompleteReleaseWorkflow.Build(DcsModuleSnapshotWorkflow.Freeze(restored, "3.1.1")).Bytes);
                var page = restored.Pages.First(x => x.Kind == KneeboardPageKind.Device);
                var edited = DcsModuleKneeboardPageEditor.Set(restored, page,
                    DcsModuleKneeboardPageEditor.Describe(restored, page) with { Title = "EDITED HUEY CONTROLS" });
                await restoredRepository.SaveAsync(edited);
                Assert.NotEqual(ovgme.Bytes, DcsModuleSnapshotWorkflow.Build(DcsModuleSnapshotWorkflow.Freeze(edited, "3.1.1")).Bytes);
                var rollbackRepository = await RepositoryAsync(Path.Combine(root, "rollback.db"));
                var rollback = await DcsModuleSnapshotWorkflow.RestoreAsync(original, rollbackRepository);
                Assert.Equal(complete.Bytes, DcsModuleCompleteReleaseWorkflow.Build(DcsModuleSnapshotWorkflow.Freeze(rollback, "3.1.1")).Bytes);
                Assert.Equal("EDITED HUEY CONTROLS", (await restoredRepository.GetAsync(edited.Configuration.Module.Id))!.Pages.Single(x => x.Id == page.Id).Title);
                var report = new
                {
                    nativeUiParity = true,
                    module = "UH-1H",
                    sourceCommit = "77d924cce220355860e8f026cff3524bac531e29",
                    release = "v3.1.1",
                    originalDigest = original.Workspace.Sha256,
                    ovgmeSha256 = Hash(ovgme.Bytes),
                    completeSha256 = Hash(complete.Bytes),
                    nativePaths = lua.Keys.Order(StringComparer.Ordinal),
                    pages = pageReport,
                    packageEntries = payload.Keys.Order(StringComparer.Ordinal),
                    documentation = guides.Keys.Order(StringComparer.Ordinal),
                    cleanRestore = true,
                    rollback = true
                };
                var reportRoot = Environment.GetEnvironmentVariable("HEIMDALL_PARITY_OUTPUT") ?? Environment.GetEnvironmentVariable("IPIA_PARITY_OUTPUT") ?? Path.Combine(Path.GetTempPath(), "ipia-huey-parity");
                Directory.CreateDirectory(reportRoot);
                File.WriteAllText(Path.Combine(reportRoot, "huey-parity.json"), JsonSerializer.Serialize(report, ReportOptions));
                foreach (var (path, bytes) in rendered)
                {
                    var output = Path.Combine(reportRoot, "Generated", path);
                    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                    File.WriteAllBytes(output, bytes);
                }
            }
            finally { Directory.Delete(root, true); }
        }
        /// <summary>Checks every captured release and source byte against independent provenance.</summary>
        [Fact]
        public void Pinned_release_evidence_matches_recorded_hashes()
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureRoot, "PROVENANCE.json")));
            var entries = manifest.RootElement.GetProperty("files").EnumerateArray().ToArray();
            Assert.Equal(entries.Select(x => x.GetProperty("path").GetString()).Order(StringComparer.Ordinal),
                Directory.GetFiles(FixtureRoot, "*", SearchOption.AllDirectories)
                    .Select(x => Path.GetRelativePath(FixtureRoot, x).Replace('\\', '/'))
                    .Where(x => x != "PROVENANCE.json").Order(StringComparer.Ordinal));
            foreach (var item in entries)
            {
                var bytes = File.ReadAllBytes(Path.Combine(FixtureRoot, item.GetProperty("path").GetString()!));
                Assert.Equal(item.GetProperty("bytes").GetInt32(), bytes.Length);
                Assert.Equal(item.GetProperty("sha256").GetString(), Hash(bytes));
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static DcsEditorWorkspaceSnapshot Workspace()
        {
            var commit = BundledSharedCatalog.SourceCommit();
            string Read(string path) => BundledSharedCatalog.ReadTextAsync(path).GetAwaiter().GetResult();
            var catalog = new DcsCommonCatalogImporter().Import(new(commit,
                "assets/shared/hardware/manifest.json", Read("assets/shared/hardware/manifest.json"),
                "assets/shared/ui-layer/functions.json", Read("assets/shared/ui-layer/functions.json"),
                "assets/shared/ui-layer/hardware-overlays.json", Read("assets/shared/ui-layer/hardware-overlays.json"),
                "assets/shared/hardware/scaffold-device-map.json", Read("assets/shared/hardware/scaffold-device-map.json")));
            Assert.True(catalog.Succeeded);
            var paths = BundledSharedCatalog.Paths("assets/shared/hardware/lua", "assets/shared/ui-layer/input/UiLayer");
            var shared = new DcsCommonLuaImporter().Import(new(commit, catalog, paths.Select(x => new DcsSharedLuaSource(x, Read(x))).ToArray()));
            Assert.True(shared.Succeeded);
            var sourceRoot = Path.Combine(FixtureRoot, "Source");
            var sources = Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories)
                .Select(x => new DcsConsumerSource(Path.GetRelativePath(sourceRoot, x).Replace('\\', '/'), File.ReadAllText(x)))
                .Where(x => x.Path != "LICENSE").ToArray();
            var imported = new DcsConsumerSourceImporter().Import(new("77d924cce220355860e8f026cff3524bac531e29", catalog, shared, sources));
            Assert.True(imported.Succeeded, string.Join("; ", imported.Diagnostics.Select(x => x.Message)));
            var configuration = imported.Configuration!;
            var graphics = BundledSharedCatalog.Paths("assets/shared/hardware/svg").ToDictionary(x => x, Read);
            var diagrams = DcsUiLayerDiagramFactory.Create(imported, catalog, graphics, shared);
            var assignments = configuration.Bindings.Where(x => x.Disposition != BindingDisposition.Removed &&
                    diagrams.Any(d => d.ProfileId == x.ProfileId && d.Controls.Any(c => DcsInputKey.SamePhysicalInput(c.InputKey, x.InputKey))))
                .Select(x => Assignment(x, configuration.Module.Id, AssignmentLayer.Module))
                .Concat(diagrams.SelectMany(d => d.Callouts.Where(c => d.Controls.Any(x => x.Control == c.Control)).Select(c =>
                    new DcsDraftAssignment(new(configuration.Module.Id, d.ProfileId, AssignmentLayer.UiLayer, BindingSection.Key,
                        DcsInputKey.Resolve(configuration, d.ProfileId, BindingSection.Key, d.Controls.Single(x => x.Control == c.Control).InputKey),
                        c.Modifier.Replace(" + ", "+", StringComparison.Ordinal)), new(c.CommandId, c.Function, false, AssignmentLayer.UiLayer)))))
                .GroupBy(x => x.Slot.Key, StringComparer.Ordinal).Select(x => x.First()).ToArray();
            var pinned = imported.Sources.Concat(paths.Select(x => new KeyValuePair<string, string>(x, Read(x)))).ToDictionary(x => x.Key, x => x.Value);
            var workspace = new DcsEditorWorkspaceSnapshot(configuration, imported.Pages, [], diagrams,
                DcsCommandPalette.FromBindings(configuration.Bindings), catalog.Commands, assignments,
                DcsKneeboardLabelImporter.Import(imported.Metadata, configuration), pinned);
            return DcsModuleIntegrationImport.Apply(workspace, DcsModuleIntegrationImport.ReadFile(workspace,
                Path.Combine(sourceRoot, "docs", "OPENKNEEBOARD-VAICOM.md")));
        }
        private static DcsDraftAssignment Assignment(DcsBinding binding, Guid module, AssignmentLayer layer) =>
            new(new(module, binding.ProfileId, layer, binding.Section, binding.InputKey, string.Join("+", binding.Reformers)),
                new(binding.CommandId, binding.CommandName, binding.Section == BindingSection.Axis, layer))
            { Filter = binding.Filter };
        private static string[] Bindings(string source) => DcsLuaParser.ParseProfile(source, Guid.Parse("3835a442-bfcc-4c2d-b1f0-b26fef3cde9a"))
            .Select(x => $"{x.Section}|{x.CommandId}|{x.Disposition}|{x.InputKey}|{string.Join("+", x.Reformers.Order(StringComparer.Ordinal))}|{JsonSerializer.Serialize(x.Filter)}")
            .Order(StringComparer.Ordinal).ToArray();
        private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
        private static async Task<DcsSqliteEditorWorkspaceRepository> RepositoryAsync(string path)
        {
            var connection = "Data Source=" + path + ";Pooling=False";
            await new SqliteMigrationRunner(connection).MigrateAsync();
            return new DcsSqliteEditorWorkspaceRepository(connection);
        }
        private static Dictionary<string, byte[]> Files(byte[] archive)
        {
            using var stream = new MemoryStream(archive);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            return zip.Entries.Where(x => !x.FullName.EndsWith('/')).ToDictionary(x => x.FullName, x =>
            {
                using var entry = x.Open();
                using var output = new MemoryStream();
                entry.CopyTo(output);
                return output.ToArray();
            });
        }
        #endregion END private helpers
    }
}

