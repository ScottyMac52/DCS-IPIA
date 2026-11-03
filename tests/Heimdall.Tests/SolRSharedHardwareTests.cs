using System.Text.Json;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Heimdall.Rendering;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects checkout-free Sol-R catalog discovery, native identity, and portable diagrams.</summary>
    public sealed class SolRSharedHardwareTests
    {
        #region BEGIN Public
        /// <summary>Both handedness and presentation modes ship as reusable hardware, not user revisions.</summary>
        [Theory]
        [InlineData("r", "buttons")]
        [InlineData("r", "pov")]
        [InlineData("l", "buttons")]
        [InlineData("l", "pov")]
        public async Task Bundled_definition_matches_inventory_and_survives_module_reopen(string hand, string mode)
        {
            var sources = await BundledHardwareTestFixture.LoadAsync();
            var choices = JsonSerializer.Deserialize<WorkspaceHardware[]>(sources["config/ipia-hardware.json"])!;
            var id = "tm-sol-r-ava-" + hand + "-" + mode;
            var hardware = Assert.Single(choices, x => x.DeviceId == id);
            var definition = Definition(hand == "r");
            var native = new DetectedDcsDevice(Guid.NewGuid(), "AVA [" + hand.ToUpperInvariant() + "] Sol-R", true);
            var candidates = BundledHardwareCapabilityMatcher.Candidates(sources, new(native, definition.Capabilities));
            Assert.Equal(2, candidates.Count);
            Assert.Contains(id, candidates);
            new EditionPolicy().RequireHardware(candidates);
            Assert.All(candidates, x => Assert.StartsWith("tm-sol-r-ava-" + hand + "-", x, StringComparison.Ordinal));
            Assert.True(definition.Capabilities.DcsKeys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(hardware.Diagram.Controls.Select(x => x.InputKey)));
            Assert.Equal(hand == "r" ? 34 : 32, hardware.Diagram.Controls.Count);
            if (hand == "r")
                foreach (var key in new[] { "JOY_RX", "JOY_RY" })
                    Assert.Contains(hardware.Diagram.Controls, control => control.InputKey == key && control.IsAxis);
            Assert.Null(hardware.Diagram.CustomMapping);
            Assert.Contains(hardware.Diagram.Controls, x => x.InputKey == "JOY_RZ" && x.IsAxis);
            Assert.Contains(hardware.Diagram.Controls, x => x.InputKey == "JOY_BTN_POV1_UR");
            Assert.DoesNotContain(hardware.Diagram.Controls, x => x.InputKey == "JOY_BTN22" || x.InputKey == "JOY_SLIDER1");
            var path = "assets/shared/hardware/drawio/" + id + ".drawio";
            var revision = DrawioMappingImporter.Import(await BundledSharedCatalog.ReadTextAsync(path), definition, id + ".drawio", "test");
            DrawioMappingImporter.Validate(revision);
            Assert.Equal(hand == "r" ? 34 : 32, revision.MappedInputs.Count);

            var instance = new HardwareInventoryInstance(native.PhysicalGuid, native.Name, definition.Id,
                "Sol-R", "Stick", true, candidates);
            var choice = new InventoryHardwareChoice(instance, definition);
            var workspace = GenericHardwareFixture.Workspace();
            var moduleSources = new Dictionary<string, string>(workspace.Sources!, StringComparer.Ordinal);
            foreach (var source in sources) moduleSources[source.Key] = source.Value;
            workspace = workspace with { Sources = moduleSources };
            Assert.NotNull(DetectedDeviceHardwareMatcher.Match(workspace, native));
            workspace = InventoryHardwareAuthoring.Add(workspace, choice, AssignmentLayer.Module, hardware);
            workspace = ModuleKneeboardPageEditor.Set(workspace, null,
                new(KneeboardPageKind.Summary, null, "00-OVERVIEW", "SOL-R", "AVA GRIP", 0,
                    [new("STICK", "Sol-R grip on AVA", "cyan")]));
            var profile = Assert.Single(workspace.Configuration.Profiles);
            Assert.Equal(id, profile.DeviceId);
            Assert.Equal(choice.FileName, profile.SourceName);
            Assert.Equal(native.PhysicalGuid, DcsPhysicalIdentity.FromFileName(profile.SourceName));
            var root = Path.Combine(Path.GetTempPath(), "ipia-sol-r-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var connection = "Data Source=" + Path.Combine(root, "module.db") + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(workspace);
                var reopened = (await repository.GetAsync(workspace.Configuration.Module.Id))!;
                var diagram = Assert.Single(reopened.Diagrams);
                Assert.Equal(hardware.Diagram.Path, diagram.Path);
                Assert.Null(diagram.CustomMapping);
                Assert.Equal(hardware.Diagram.Svg, diagram.Svg);
                Assert.NotEmpty(SnapshotDeviceKneeboardRenderer.Generate(reopened));
                var portable = ModuleSnapshotWorkflow.Read(ModuleSnapshotWorkflow.Export(ModuleSnapshotWorkflow.Freeze(reopened, "1.0")));
                Assert.NotNull(portable);
            }
            finally { Directory.Delete(root, true); }
        }

        /// <summary>A Sol-R base or an unrelated base/grip cannot acquire an AVA catalog candidate.</summary>
        [Theory]
        [InlineData("Sol-R 1 Flightstick")]
        [InlineData("Joystick - HOTAS Warthog")]
        [InlineData("MOZA AB9 Sol-R")]
        [InlineData("AVA [R] Viper")]
        public async Task Other_native_names_do_not_offer_sol_r_layouts(string name)
        {
            var sources = await BundledHardwareTestFixture.LoadAsync();
            var candidates = BundledHardwareCapabilityMatcher.Candidates(sources,
                new(new(Guid.NewGuid(), name, true), Definition().Capabilities));
            Assert.DoesNotContain(candidates, x => x.StartsWith("tm-sol-r-", StringComparison.Ordinal));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static HardwareDefinition Definition(bool miniStickAxes = false)
        {
            var objects = Enumerable.Range(0, 21).Select(i => new ControllerObject(
                Guid.Parse("a36d02f0-c9f3-11cf-bfc7-444553540000"), 12u | ((uint)i << 8), "Button " + (i + 1))).ToList();
            objects.Add(new(Guid.Parse("a36d02e0-c9f3-11cf-bfc7-444553540000"), 2, "X"));
            objects.Add(new(Guid.Parse("a36d02e1-c9f3-11cf-bfc7-444553540000"), 258, "Y"));
            objects.Add(new(Guid.Parse("a36d02e3-c9f3-11cf-bfc7-444553540000"), 514, "RZ"));
            objects.Add(new(Guid.Parse("a36d02f2-c9f3-11cf-bfc7-444553540000"), 16, "POV 1"));
            if (miniStickAxes)
            {
                objects.Add(new(Guid.Parse("a36d02f4-c9f3-11cf-bfc7-444553540000"), 770, "RX"));
                objects.Add(new(Guid.Parse("a36d02f5-c9f3-11cf-bfc7-444553540000"), 1026, "RY"));
            }
            var capabilities = ControllerCapabilityMapper.Map(objects);
            return new(ControllerCapabilityMapper.Fingerprint(capabilities), capabilities);
        }
        #endregion END private helpers
    }
}
