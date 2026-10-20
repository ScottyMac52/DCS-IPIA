using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Heimdall.Rendering;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Imports the shipped editable drawings through the same native mapping path as the application.</summary>
    public sealed class BundledDrawingImportTests
    {
        #region BEGIN Public
        /// <summary>Gets every shipped source from provenance so newly bundled drawings are automatically covered.</summary>
        public static IEnumerable<object[]> Drawings => BundledSharedCatalog.Paths("assets/shared/hardware/drawio")
            .Where(x => x.EndsWith(".drawio", StringComparison.Ordinal)).Select(x => new object[] { x });

        /// <summary>Actual bundled sources preview, persist and export with their images, layout and footer intact.</summary>
        /// <param name="path">Original bundled draw.io source.</param>
        [Theory]
        [MemberData(nameof(Drawings))]
        public async Task Every_bundled_drawing_imports_renders_and_reopens(string path)
        {
            var sources = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
            var choices = JsonSerializer.Deserialize<WorkspaceHardware[]>(sources["config/ipia-hardware.json"])!;
            var hardware = choices.First(x => Path.GetFileNameWithoutExtension(x.Diagram.Path) == Path.GetFileNameWithoutExtension(path));
            var inputs = hardware.Diagram.Controls.ToDictionary(x => x.Control, x => x.InputKey, StringComparer.Ordinal);
            var definition = Definition(inputs.Values);
            var original = await BundledSharedCatalog.ReadTextAsync(path);
            var revision = DrawioMappingFileImport.Load(Path.Combine(BundledSharedCatalog.Root, path), definition, "test", inputs);
            Assert.Equal("ipia-drawio-2", revision.ExporterVersion);
            Assert.Equal(inputs.Keys.Order(StringComparer.Ordinal), revision.Controls.Where(x => revision.MappedInputs.Contains(x.InputKey))
                .Select(x => x.Control).Order(StringComparer.Ordinal));
            Assert.True(revision.Controls.Select(x => x.InputKey).ToHashSet(StringComparer.Ordinal).SetEquals(definition.Capabilities.DcsKeys));
            var graph = XDocument.Parse(revision.Source).Descendants("mxGraphModel").Single();
            var svg = XDocument.Parse(revision.Svg);
            XNamespace ns = "http://www.w3.org/2000/svg";
            var footer = graph.Descendants("mxCell").Single(x => x.Attribute("id")?.Value == "footer").Attribute("value")!.Value;
            Assert.Contains(svg.Descendants(ns + "text"), x => x.Value == footer);
            Assert.Equal(hardware.Diagram.Width, double.Parse(graph.Attribute("pageWidth")!.Value, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(hardware.Diagram.Height, double.Parse(graph.Attribute("pageHeight")!.Value, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(graph.Descendants("mxCell").Count(x => (x.Attribute("id")?.Value ?? "").StartsWith("hardware-image-", StringComparison.Ordinal)),
                svg.Descendants(ns + "image").Count());
            foreach (var control in hardware.Diagram.Controls)
            {
                var actual = revision.Controls.Single(x => x.Control == control.Control);
                Assert.True(DcsInputKey.SamePhysicalInput(control.InputKey, actual.InputKey));
                // Published catalog SVGs can differ from their editable sources; preserve the authored geometry.
                var cell = graph.Descendants("mxCell").Single(x =>
                    (x.Attribute("id")?.Value ?? (x.Parent?.Name.LocalName == "UserObject" ? x.Parent.Attribute("id")?.Value : null)) == "label-" + control.Control);
                var geometry = cell.Element("mxGeometry")!;
                double Coordinate(string name) => double.Parse(geometry.Attribute(name)?.Value ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                Assert.Equal(Coordinate("x"), actual.X);
                Assert.Equal(Coordinate("y"), actual.Y);
                Assert.Equal(Coordinate("width"), actual.Width);
                Assert.Equal(Coordinate("height"), actual.Height);
            }
            var compressed = DrawioMappingImporter.Import(DrawioMappingFixture.Compress(revision.Source), definition, Path.GetFileName(path), "test");
            Assert.Equal(revision.Svg, compressed.Svg);
            UserMappingImageVerifier.Validate(revision);
            Assert.True(CanonicalSvgRenderer.RenderComplete(revision.Svg, revision.Width, revision.Height).Length > 10_000);
            var root = Path.Combine(Path.GetTempPath(), "ipia-bundled-drawing-" + Guid.NewGuid().ToString("N"));
            try
            {
                var library = new FileUserMappingLibrary(root);
                await library.SaveAsync(revision);
                var reopened = Assert.Single(await library.ListAsync());
                Assert.Equal(revision.Id, reopened.Id);
                Assert.Equal(revision.Source, reopened.Source);
                using var archive = new ZipArchive(new MemoryStream(UserMappingBundle.Export(reopened)));
                using var reader = new StreamReader(archive.GetEntry("mapping.drawio")!.Open());
                Assert.Equal(revision.Source, await reader.ReadToEndAsync());
                var output = Environment.GetEnvironmentVariable("IPIA_MAPPING_PREVIEW");
                if (!string.IsNullOrWhiteSpace(output))
                {
                    Directory.CreateDirectory(output);
                    await File.WriteAllBytesAsync(Path.Combine(output, Path.GetFileNameWithoutExtension(path) + ".png"),
                        CanonicalSvgRenderer.RenderComplete(revision.Svg, revision.Width, revision.Height));
                }
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
            Assert.Equal(original, await BundledSharedCatalog.ReadTextAsync(path));
        }

        /// <summary>Shifted MFD labels retain distinct marker identities through adoption, SQLite reopen and drawing replacement.</summary>
        [Fact]
        public async Task Shifted_mfd_pairs_keep_assignments_and_labels_after_save_and_readoption()
        {
            var sources = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
            var hardware = JsonSerializer.Deserialize<WorkspaceHardware[]>(sources["config/ipia-hardware.json"])!.First(x => x.DeviceId == "tm-mfd");
            var inputs = hardware.Diagram.Controls.ToDictionary(x => x.Control, x => x.InputKey, StringComparer.Ordinal);
            var definition = Definition(inputs.Values.Append("JOY_BTN29")); // Exercise continuation rendering alongside shifted pairs.
            var revision = DrawioMappingFileImport.Load(Path.Combine(BundledSharedCatalog.Root, "assets/shared/hardware/drawio/tm-mfd.drawio"), definition, "test", inputs);
            var unit = GenericHardwareFixture.Unit(definition, "MFD3");
            var workspace = InventoryHardwareAuthoring.Add(GenericHardwareFixture.Workspace(), unit, AssignmentLayer.Module);
            var profile = workspace.Configuration.Profiles.Single();
            workspace = WorkspaceStructureEditor.SetModifier(workspace, null, new(AssignmentLayer.Module, profile.Id, "SHIFT", "JOY_BTN28", "Shift", ModifierMode.Hold, ""));
            var key = revision.Controls.First(x => x.Control.EndsWith("-shifted", StringComparison.Ordinal)).InputKey;
            var assignments = new[] {
                new DraftAssignment(new(workspace.Configuration.Module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Key, key),
                    new("d3001pnilu3001cdnilvd1vpnilvunil", "Base action", false, AssignmentLayer.Module)),
                new DraftAssignment(new(workspace.Configuration.Module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Key, key, "SHIFT"),
                    new("d3002pnilu3002cdnilvd1vpnilvunil", "Shifted action", false, AssignmentLayer.Module))
            };
            workspace = workspace with { Assignments = assignments };
            var adopted = UserMappingAdoption.Apply(workspace, profile.Id, revision);
            Assert.Same(workspace.Assignments, adopted.Assignments);
            var diagram = adopted.Diagrams.Single();
            var pair = diagram.Controls.Where(x => x.InputKey == key).ToArray();
            Assert.Equal(2, pair.Length);
            Assert.Contains(pair, x => x.Control.EndsWith("-shifted", StringComparison.Ordinal));
            var composite = KneeboardDraftProjection.CreateCombined(adopted, diagram, adopted.Assignments, adopted.Labels);
            Assert.Contains(composite.Labels[pair.Single(x => x.Control.EndsWith("-shifted", StringComparison.Ordinal)).Control],
                x => x.Text == "Shifted action");
            Assert.True(DeviceKneeboardPages.Render(diagram, composite).Count > 1);
            var renamed = InventoryHardwareAuthoring.RenameControl(adopted, profile.Id, key, "OSB custom");
            Assert.All(renamed.Diagrams.Single().Controls.Where(x => x.InputKey == key), x => Assert.Equal("OSB custom", x.Label));
            var root = Path.Combine(Path.GetTempPath(), "ipia-mfd-pairs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var connection = "Data Source=" + Path.Combine(root, "ipia.db") + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(renamed);
                var saved = (await repository.GetAsync(workspace.Configuration.Module.Id))!;
                Assert.Equal(assignments, saved.Assignments);
                var reapplied = UserMappingAdoption.Apply(saved, profile.Id, revision);
                Assert.All(reapplied.Diagrams.Single().Controls.Where(x => x.InputKey == key), x => Assert.Equal("OSB custom", x.Label));
            }
            finally { SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
        }

        /// <summary>Old immutable rendering semantics remain reproducible after upgrading the importer.</summary>
        [Fact]
        public void Previously_saved_version_one_revisions_still_validate()
        {
            var old = DrawioMappingImporter.Import(DrawioMappingFixture.Source(), GenericHardwareFixture.Definition(), "old.drawio", "old",
                exporterVersion: "ipia-drawio-1");
            DrawioMappingImporter.Validate(old);
            var current = DrawioMappingImporter.Import(old.Source, old.Definition, old.SourceName, old.ApplicationVersion);
            Assert.Equal("ipia-drawio-2", current.ExporterVersion);
            Assert.NotEqual(old.Id, current.Id);
            Assert.Throws<InvalidDataException>(() => DrawioMappingImporter.Validate(old with { ExporterVersion = "ipia-drawio-2" }));
        }
        #endregion END Public
        #region BEGIN private helpers
        internal static HardwareDefinition Definition(IEnumerable<string> keys)
        {
            var all = keys.Select(x => x.StartsWith("JOY_POV", StringComparison.Ordinal) ? "JOY_BTN_POV" + x[7..] : x)
                .Distinct(StringComparer.Ordinal).ToArray();
            var objects = all.Where(x => Regex.IsMatch(x, "^JOY_BTN[0-9]+$", RegexOptions.CultureInvariant))
                .Select(x => new ControllerObject(Guid.Parse("a36d02f0-c9f3-11cf-bfc7-444553540000"),
                    12u | ((uint)(int.Parse(x[7..], System.Globalization.CultureInfo.InvariantCulture) - 1) << 8), x)).ToList();
            var axes = new[] { ("JOY_X", "a36d02e0"), ("JOY_Y", "a36d02e1"), ("JOY_Z", "a36d02e2"),
                ("JOY_RX", "a36d02f4"), ("JOY_RY", "a36d02f5"), ("JOY_RZ", "a36d02e3"),
                ("JOY_SLIDER1", "a36d02e4"), ("JOY_SLIDER2", "a36d02e4") };
            for (var i = 0; i < axes.Length; i++)
                if (all.Contains(axes[i].Item1, StringComparer.Ordinal) || i == 6 && all.Contains("JOY_SLIDER2", StringComparer.Ordinal))
                    objects.Add(new(Guid.Parse(axes[i].Item2 + "-c9f3-11cf-bfc7-444553540000"), 2u | ((uint)i << 8), axes[i].Item1));
            for (var i = 0; i < 4; i++)
                if (all.Any(x => x.StartsWith("JOY_BTN_POV" + (i + 1) + "_", StringComparison.Ordinal)))
                    objects.Add(new(Guid.Parse("a36d02f2-c9f3-11cf-bfc7-444553540000"), 16u | ((uint)i << 8), "POV"));
            var capabilities = ControllerCapabilityMapper.Map(objects);
            return new(ControllerCapabilityMapper.Fingerprint(capabilities), capabilities);
        }
        #endregion END private helpers
    }
}
