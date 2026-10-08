using System.IO.Compression;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Dcs.Ipia.Packaging;
using Dcs.Ipia.Rendering;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Verifies bounded source import, immutable persistence and assignment-preserving portable custom output.</summary>
    public sealed class DrawioMappingTests
    {
        #region BEGIN Public
        /// <summary>Native and compressed diagrams share the same deterministic derived graphic and control mapping.</summary>
        [Fact]
        public void Native_and_compressed_sources_reproduce_the_same_graphics()
        {
            var definition = GenericHardwareFixture.Definition();
            var native = DrawioMappingFixture.Revision();
            var compressed = DrawioMappingImporter.Import(DrawioMappingFixture.Compress(native.Source), definition, "compressed.drawio", "1.0");
            Assert.Equal(native.Svg, compressed.Svg);
            Assert.Equal(native.Controls, compressed.Controls);
            Assert.NotEqual(native.Id, compressed.Id);
            Assert.Equal(definition.Capabilities.DcsKeys.Count, native.Controls.Count);
            Assert.Equal(new[] { "JOY_BTN1", "JOY_X" }, native.MappedInputs);
            DrawioMappingImporter.Validate(compressed);
            Assert.Equal(JsonSerializer.Serialize(native), JsonSerializer.Serialize(
                DrawioMappingImporter.Import(native.Source, native.Definition, native.SourceName, "1.0")));
        }

        /// <summary>Unsafe, unsupported or ambiguous input never publishes a revision.</summary>
        /// <param name="failure">Malformed construct.</param>
        [Theory]
        [InlineData("unknown")][InlineData("duplicate-id")][InlineData("duplicate-input")]
        [InlineData("dimensions")][InlineData("nan")][InlineData("outside")][InlineData("group")]
        [InlineData("script")][InlineData("html")][InlineData("doctype")][InlineData("multi")]
        [InlineData("canvas")][InlineData("footer")][InlineData("connector")][InlineData("rotation")]
        [InlineData("shape")][InlineData("color")][InlineData("font")][InlineData("url")]
        public void Rejects_invalid_sources_before_publication(string failure)
        {
            var source = DrawioMappingFixture.Source();
            source = failure switch
            {
                "unknown" => source.Replace("inputKey=\"JOY_BTN1\"", "inputKey=\"JOY_BTN999\"", StringComparison.Ordinal),
                "duplicate-id" => source.Replace("id=\"anchor-trigger\"", "id=\"label-JOY_X\"", StringComparison.Ordinal),
                "duplicate-input" => source.Replace("id=\"label-JOY_X\"", "id=\"label-extra\" inputKey=\"JOY_BTN1\"", StringComparison.Ordinal),
                "dimensions" => source.Replace("pageWidth=\"900\"", "pageWidth=\"0\"", StringComparison.Ordinal),
                "nan" => source.Replace("x=\"10\"", "x=\"NaN\"", StringComparison.Ordinal),
                "outside" => source.Replace("x=\"400\"", "x=\"9999\"", StringComparison.Ordinal),
                "group" => source.Replace("parent=\"1\" vertex", "parent=\"canvas\" vertex", StringComparison.Ordinal),
                "script" => source.Replace("label=\"Trigger\"", "label=\"Trigger\" onclick=\"evil\"", StringComparison.Ordinal),
                "html" => source.Replace("label=\"Trigger\"", "label=\"&lt;img src=x&gt;\"", StringComparison.Ordinal),
                "doctype" => "<!DOCTYPE mxfile [<!ENTITY bad SYSTEM 'file:///secret'>]>" + source,
                "multi" => source.Replace("</mxfile>", "<diagram>extra</diagram></mxfile>", StringComparison.Ordinal),
                "canvas" => source.Replace("id=\"canvas\"", "id=\"unknown\"", StringComparison.Ordinal),
                "footer" => source.Replace("id=\"footer\"", "id=\"unknown\"", StringComparison.Ordinal),
                "connector" => source.Replace("target=\"anchor-trigger\"", "target=\"missing\"", StringComparison.Ordinal),
                "rotation" => source.Replace("fillColor=#ffffff;", "rotation=30;fillColor=#ffffff;", StringComparison.Ordinal),
                "shape" => source.Replace("fillColor=#ffffff;", "shape=cylinder;fillColor=#ffffff;", StringComparison.Ordinal),
                "color" => source.Replace("#ffffff", "url(external)", StringComparison.Ordinal),
                "font" => source.Replace("fontColor=#111111;", "fontSize=1000;fontColor=#111111;", StringComparison.Ordinal),
                "url" => DrawioMappingFixture.Source("https://example.com/image.png"),
                _ => throw new InvalidOperationException()
            };
            Assert.ThrowsAny<Exception>(() => DrawioMappingImporter.Import(source, GenericHardwareFixture.Definition(), "bad.drawio", "1.0"));
        }

        /// <summary>Source, inflated graph and captured image bounds reject resource exhaustion before rendering.</summary>
        [Fact]
        public void Rejects_oversized_truncated_and_unsupported_sources()
        {
            var definition = GenericHardwareFixture.Definition();
            Assert.Throws<InvalidDataException>(() => DrawioMappingImporter.Import(new string('x', 2_000_001), definition, "big.drawio", "1.0"));
            Assert.Throws<InvalidDataException>(() => DrawioMappingImporter.Import(DrawioMappingFixture.CompressGraph(new string('x', 4_000_001)), definition, "bomb.drawio", "1.0"));
            Assert.ThrowsAny<Exception>(() => DrawioMappingImporter.Import("<mxfile><diagram>not-base64</diagram></mxfile>", definition, "bad.drawio", "1.0"));
            Assert.Throws<InvalidDataException>(() => DrawioMappingImporter.Import(DrawioMappingFixture.Source("data:image/svg+xml,evil"), definition, "bad.drawio", "1.0"));
            Assert.Throws<InvalidDataException>(() => DrawioMappingImporter.Import(DrawioMappingFixture.Source("../outside.png"), definition, "bad.drawio", "1.0"));
            Assert.Throws<InvalidDataException>(() => DrawioMappingImporter.Import(DrawioMappingFixture.Source("C:/outside.png"), definition, "bad.drawio", "1.0"));
            Assert.Throws<InvalidDataException>(() => DrawioMappingImporter.Import(DrawioMappingFixture.Source("missing.png"), definition, "bad.drawio", "1.0"));
            Assert.Throws<InvalidDataException>(() => RasterImageContract.Validate(new byte[30], "png"));
        }

        /// <summary>Embedded and explicitly captured local images render identically and survive source deletion.</summary>
        [Fact]
        public async Task Local_images_and_library_revisions_are_self_contained_and_immutable()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-custom-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var path = Path.Combine(root, "custom.drawio");
                await File.WriteAllTextAsync(path, DrawioMappingFixture.Source("image.png"));
                await File.WriteAllBytesAsync(Path.Combine(root, "image.png"), DrawioMappingFixture.Png);
                var local = DrawioMappingFileImport.Load(path, GenericHardwareFixture.Definition(), "1.0");
                var embedded = DrawioMappingImporter.Import(DrawioMappingFixture.Source("data:image/png;base64," +
                    Convert.ToBase64String(DrawioMappingFixture.Png)), local.Definition, "embedded.drawio", "1.0");
                Assert.Equal(local.Svg, embedded.Svg);
                Assert.True(CanonicalSvgRenderer.RenderComplete(local.Svg, local.Width, local.Height).Length > 1000);
                var library = new FileUserMappingLibrary(Path.Combine(root, "profile"));
                await library.SaveAsync(local);
                await library.SaveAsync(local);
                File.Delete(path); File.Delete(Path.Combine(root, "image.png"));
                var reopened = Assert.Single(await new FileUserMappingLibrary(Path.Combine(root, "profile")).ListAsync());
                Assert.Equal(JsonSerializer.Serialize(local), JsonSerializer.Serialize(reopened));
                Assert.Equal(UserMappingBundle.Export(local), UserMappingBundle.Export(reopened));
                using var zip = new ZipArchive(new MemoryStream(UserMappingBundle.Export(reopened)));
                Assert.Equal(3, zip.Entries.Count);
                Assert.NotNull(zip.GetEntry("mapping.drawio")); Assert.NotNull(zip.GetEntry("image.png"));
                await Assert.ThrowsAsync<InvalidDataException>(() => library.SaveAsync(local with { Svg = "forged" }));
                await File.WriteAllTextAsync(Path.Combine(root, "profile", "UserMappings", local.Id + ".json"), "broken");
                await Assert.ThrowsAnyAsync<Exception>(() => library.ListAsync());
            }
            finally { Directory.Delete(root, true); }
        }

        /// <summary>Module adoption retains profile/page identity, labels, modifiers, tuned axes and independent repeated units.</summary>
        [Fact]
        public async Task Adoption_reopens_and_rebuilds_after_full_and_portable_restore()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-custom-module-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var data = Path.Combine(root, "data"); Directory.CreateDirectory(data);
                var connection = Connection(data);
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var revision = DrawioMappingFixture.Revision();
                var left = GenericHardwareFixture.Unit(revision.Definition, "Left");
                var right = GenericHardwareFixture.Unit(revision.Definition, "Right");
                var workspace = InventoryHardwareAuthoring.Add(GenericHardwareFixture.Workspace(), left, AssignmentLayer.Module);
                workspace = InventoryHardwareAuthoring.Add(workspace, left, AssignmentLayer.UiLayer);
                workspace = InventoryHardwareAuthoring.Add(workspace, right, AssignmentLayer.Module);
                var profile = workspace.Configuration.Profiles.Single(x => x.SourceName == left.FileName);
                workspace = WorkspaceStructureEditor.SetModifier(workspace, null, new(AssignmentLayer.Module, profile.Id, "SHIFT", "JOY_BTN2", "Shift", ModifierMode.Hold, ""));
                workspace = InventoryHardwareAuthoring.RenameControl(workspace, profile.Id, "JOY_BTN1", "My trigger");
                workspace = ModuleCommandCatalogImport.Apply(workspace, ModuleCommandCatalogImport.Review(workspace,
                    [new("commands.json", """{"schemaVersion":1,"moduleId":"GENERIC","commands":[{"bindingKey":"a2001cdnil","name":"Pitch","categoryPath":["Flight"],"type":"axis"}]}""")], false));
                var module = workspace.Configuration.Module.Id;
                var axis = new DraftAssignment(new(module, profile.Id, AssignmentLayer.Module, BindingSection.Axis, "JOY_X", "SHIFT"),
                    new("a2001cdnil", "Pitch", true, AssignmentLayer.Module)) { Filter = new(Deadzone: 3, Curvature: [12], Invert: true) };
                var ui = new DraftAssignment(new(module, profile.Id, AssignmentLayer.UiLayer, BindingSection.Key, "JOY_BTN1"),
                    new(workspace.UiCommands.Single().DcsCommandId, "Pause", false, AssignmentLayer.UiLayer));
                workspace = workspace with { Assignments = [axis, ui], Labels = new Dictionary<string, string> { [axis.Slot.Key] = "Pitch custom" } };
                workspace = ModuleKneeboardPageEditor.Set(workspace, null, new(KneeboardPageKind.Summary, null, "00-OVERVIEW", "OVERVIEW", "CONTROLS", 0, [new("CONTROLS", "Custom", "cyan")]));
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(workspace);
                var library = new FileUserMappingLibrary(data); await library.SaveAsync(revision);
                var original = EditorSnapshotSerializer.Serialize(workspace);
                var adopted = UserMappingAdoption.Apply(workspace, profile.Id, revision);
                Assert.Same(workspace.Configuration, adopted.Configuration);
                Assert.Same(workspace.Assignments, adopted.Assignments); Assert.Same(workspace.Pages, adopted.Pages);
                Assert.Same(workspace.Labels, adopted.Labels); Assert.Same(workspace.Sources, adopted.Sources);
                Assert.Equal(original, EditorSnapshotSerializer.Serialize(workspace));
                Assert.Equal(workspace.Diagrams.Single(x => x.ProfileId != profile.Id), adopted.Diagrams.Single(x => x.ProfileId != profile.Id));
                Assert.Equal("My trigger", adopted.Diagrams.Single(x => x.ProfileId == profile.Id).Controls.Single(x => x.InputKey == "JOY_BTN1").Label);
                await repository.SaveAsync(adopted);
                var saved = (await repository.GetAsync(module))!;
                var snapshot = ModuleSnapshotWorkflow.Freeze(saved, "1.0");
                var package = ModuleSnapshotWorkflow.Build(snapshot);
                var generated = SnapshotDeviceKneeboardRenderer.Generate(saved);
                Assert.Contains(generated.Keys, x => x.EndsWith("-002.png", StringComparison.Ordinal));
                var target = Path.Combine(root, "portable"); Directory.CreateDirectory(target);
                await new SqliteMigrationRunner(Connection(target)).MigrateAsync();
                var targetRepository = new SqliteEditorWorkspaceRepository(Connection(target));
                await ModuleSnapshotWorkflow.RestoreAsync(ModuleSnapshotWorkflow.Read(ModuleSnapshotWorkflow.Export(snapshot)), targetRepository);
                Assert.False(Directory.Exists(Path.Combine(target, "UserMappings")));
                Assert.Equal(package.Bytes, ModuleSnapshotWorkflow.Build(ModuleSnapshotWorkflow.Freeze((await targetRepository.GetAsync(module))!, "1.0")).Bytes);
                var archive = Path.Combine(root, "backup.zip");
                new UserDataRecoveryService(data, "1.0").Backup(archive);
                var recovered = Path.Combine(root, "recovered");
                new UserDataRecoveryService(recovered, "1.0").Restore(archive);
                Assert.Single(await new FileUserMappingLibrary(recovered).ListAsync());
                var recoveredWorkspace = (await new SqliteEditorWorkspaceRepository(Connection(recovered)).GetAsync(module))!;
                Assert.Equal(package.Bytes, ModuleSnapshotWorkflow.Build(ModuleSnapshotWorkflow.Freeze(recoveredWorkspace, "1.0")).Bytes);
                Assert.Throws<InvalidDataException>(() => EditorSnapshotSerializer.Serialize(adopted with
                { Diagrams = adopted.Diagrams.Select(x => x.ProfileId == profile.Id ? x with { Svg = "forged" } : x).ToArray() }));
            }
            finally { SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
        }

        /// <summary>A real controller photo imports and produces a reviewable image without Node or original paths.</summary>
        [Fact]
        public void Real_custom_diagram_renders_for_visual_review()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CustomMappings", "mfd-custom.drawio");
            var revision = DrawioMappingFileImport.Load(path, GenericHardwareFixture.Definition(), "1.0");
            Assert.Equal(2, revision.MappedInputs.Count);
            var png = CanonicalSvgRenderer.RenderComplete(revision.Svg, revision.Width, revision.Height);
            Assert.True(png.Length > 10_000);
            if (Environment.GetEnvironmentVariable("IPIA_MAPPING_PREVIEW") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, "custom-mapping-preview.png"), png);
                File.WriteAllText(Path.Combine(output, "custom-mapping.svg"), revision.Svg);
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Connection(string data) => new SqliteConnectionStringBuilder
        { DataSource = Path.Combine(data, "ipia.db"), Pooling = false }.ToString();
        #endregion END private helpers
    }
}
