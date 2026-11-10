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
        /// <summary>Offers explicit SOL-R artwork and repairs a generic unit without losing its native module state.</summary>
        [Fact]
        public async Task Generic_inventory_unit_can_adopt_explicit_sol_r_artwork_and_reopen()
        {
            var sources = await BundledHardwareTestFixture.LoadAsync();
            var catalog = JsonSerializer.Deserialize<WorkspaceHardware[]>(sources["config/ipia-hardware.json"])!;
            var definition = Definition(true);
            var choice = GenericHardwareFixture.Unit(definition, "Right SOL-R");
            var drawings = DcsInventoryHardwareAuthoring.Drawings(catalog, choice);
            var hardware = Assert.Single(drawings, x => x.DeviceId == "tm-sol-r-ava-r-pov");
            Assert.Contains(drawings, x => x.DeviceId == "tm-sol-r-ava-r-buttons");
            Assert.DoesNotContain(drawings, x => x.DeviceId.StartsWith("tm-sol-r-ava-l-", StringComparison.Ordinal));
            var workspace = DcsInventoryHardwareAuthoring.Add(GenericHardwareFixture.Workspace(), choice, AssignmentLayer.Module);
            var profile = Assert.Single(workspace.Configuration.Profiles);
            var command = new DcsDraftCommand("d3001pnilu3001cdnilvd1vpnilvunil", "Pause", false, AssignmentLayer.Module);
            workspace = workspace with
            {
                Assignments = [new(new(workspace.Configuration.Module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1"), command)],
                Labels = new Dictionary<string, string> { ["kept-label"] = "Personal label" }
            };
            workspace = DcsWorkspaceStructureEditor.SetModifier(workspace, null,
                new(AssignmentLayer.Module, profile.Id, "SHIFT", "JOY_BTN2", "Shift", ModifierMode.Hold, ""));
            var drawio = await BundledSharedCatalog.ReadTextAsync("assets/shared/hardware/drawio/tm-sol-r-ava-r-pov.drawio");
            var linked = workspace;
            Assert.Throws<InvalidOperationException>(() => DcsInventoryHardwareAuthoring.ApplyDrawing(linked, profile.Id, choice, hardware, drawio));
            workspace = DcsWorkspaceStructureEditor.SetDrawingLinked(workspace, profile.Id, false);
            var before = workspace;
            workspace = DcsInventoryHardwareAuthoring.ApplyDrawing(workspace, profile.Id, choice, hardware, drawio);
            var diagram = Assert.Single(workspace.Diagrams);
            Assert.False(diagram.DrawingUnlinked);
            Assert.NotNull(diagram.CustomMapping);
            Assert.Contains("data:image/", diagram.Svg, StringComparison.Ordinal);
            Assert.Equal(before.Assignments, workspace.Assignments);
            Assert.Equal(before.Labels, workspace.Labels);
            Assert.Equal(before.Configuration.Profiles, workspace.Configuration.Profiles);
            Assert.Equal(JsonSerializer.Serialize(DcsWorkspaceStructureEditor.Describe(before)),
                JsonSerializer.Serialize(DcsWorkspaceStructureEditor.Describe(workspace)));
            GenericHardwareLayout.ValidateWorkspace(workspace);
            var other = choice with { Instance = choice.Instance with { PhysicalGuid = Guid.NewGuid() } };
            Assert.Throws<InvalidOperationException>(() => DcsInventoryHardwareAuthoring.ApplyDrawing(before, profile.Id, other, hardware, drawio));
            var root = Path.Combine(Path.GetTempPath(), "ipia-sol-r-artwork-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var connection = "Data Source=" + Path.Combine(root, "module.db") + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new DcsSqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(workspace);
                var reopened = (await repository.GetAsync(workspace.Configuration.Module.Id))!;
                Assert.Equal(diagram.Svg, Assert.Single(reopened.Diagrams).Svg);
                Assert.Equal(profile.Id, Assert.Single(reopened.Configuration.Profiles).Id);
                Assert.Equal(workspace.Assignments, reopened.Assignments);
                Assert.Equal(workspace.Labels, reopened.Labels);
            }
            finally { Directory.Delete(root, true); }
        }

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
            Assert.Equal(hand == "r" ? 37 : 32, hardware.Diagram.Controls.Count);
            if (hand == "r")
                foreach (var key in new[] { "JOY_RX", "JOY_RY" })
                    Assert.Contains(hardware.Diagram.Controls, control => control.InputKey == key && control.IsAxis);
            Assert.Null(hardware.Diagram.CustomMapping);
            Assert.Contains(hardware.Diagram.Controls, x => x.InputKey == "JOY_RZ" && x.IsAxis);
            Assert.Contains(hardware.Diagram.Controls, x => x.InputKey == "JOY_BTN_POV1_UR");
            Assert.DoesNotContain(hardware.Diagram.Controls, x => x.InputKey == "JOY_BTN22" || x.InputKey == "JOY_SLIDER3");
            if (hand == "r")
                foreach (var key in new[] { "JOY_Z", "JOY_SLIDER1", "JOY_SLIDER2" })
                    Assert.Contains(hardware.Diagram.Controls, control => control.InputKey == key && control.IsAxis);
            var path = "assets/shared/hardware/drawio/" + id + ".drawio";
            var revision = DrawioMappingImporter.Import(await BundledSharedCatalog.ReadTextAsync(path), definition, id + ".drawio", "test");
            DrawioMappingImporter.Validate(revision);
            Assert.Equal(hand == "r" ? 37 : 32, revision.MappedInputs.Count);

            var instance = new HardwareInventoryInstance(native.PhysicalGuid, native.Name, definition.Id,
                "Sol-R", "Stick", true, candidates);
            var choice = new InventoryHardwareChoice(instance, definition);
            var workspace = GenericHardwareFixture.Workspace();
            var moduleSources = new Dictionary<string, string>(workspace.Sources!, StringComparer.Ordinal);
            foreach (var source in sources) moduleSources[source.Key] = source.Value;
            workspace = workspace with { Sources = moduleSources };
            Assert.NotNull(DcsDetectedDeviceHardwareMatcher.Match(workspace, native));
            workspace = DcsInventoryHardwareAuthoring.Add(workspace, choice, AssignmentLayer.Module, hardware);
            workspace = DcsModuleKneeboardPageEditor.Set(workspace, null,
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
                var repository = new DcsSqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(workspace);
                var reopened = (await repository.GetAsync(workspace.Configuration.Module.Id))!;
                var diagram = Assert.Single(reopened.Diagrams);
                Assert.Equal(hardware.Diagram.Path, diagram.Path);
                Assert.Null(diagram.CustomMapping);
                Assert.Equal(hardware.Diagram.Svg, diagram.Svg);
                Assert.NotEmpty(DcsSnapshotDeviceKneeboardRenderer.Generate(reopened));
                var portable = DcsModuleSnapshotWorkflow.Read(DcsModuleSnapshotWorkflow.Export(DcsModuleSnapshotWorkflow.Freeze(reopened, "1.0")));
                Assert.NotNull(portable);
            }
            finally { Directory.Delete(root, true); }
        }

        /// <summary>Keeps the reported unknown axis and collection explicit while allowing reviewed artwork for all known inputs.</summary>
        [Fact]
        public async Task Reported_right_ava_inputs_keep_unsupported_objects_unmapped()
        {
            var known = Definition(true).Capabilities;
            var capabilities = ControllerCapabilityMapper.Map(known.NativeObjects!.Concat(new[]
            {
                new ControllerObject(Guid.Empty, 192, "Collection 0 - Joystick"),
                new ControllerObject(Guid.Empty, 2050, "Axis 8")
            }));
            Assert.Equal(8, capabilities.Inputs.Count(input => input.Kind == ControllerInputKind.Axis));
            Assert.Equal(2, capabilities.Unsupported.Count);
            Assert.DoesNotContain(capabilities.DcsKeys, key => key == "JOY_SLIDER3" || key == "JOY_AXIS8");
            var sources = await BundledHardwareTestFixture.LoadAsync();
            var device = new DetectedDcsDevice(Guid.NewGuid(), "AVA [R] Sol-R", true);
            Assert.Empty(BundledHardwareCapabilityMatcher.Candidates(sources, new(device, capabilities)));
            var hardware = Assert.Single(JsonSerializer.Deserialize<WorkspaceHardware[]>(sources["config/ipia-hardware.json"])!,
                candidate => candidate.DeviceId == "tm-sol-r-ava-r-pov");
            var definition = new HardwareDefinition(ControllerCapabilityMapper.Fingerprint(capabilities), capabilities);
            var instance = new HardwareInventoryInstance(device.PhysicalGuid, device.Name, definition.Id, "Sol-R", "Stick", true, []);
            var choice = new InventoryHardwareChoice(instance, definition);
            var workspace = GenericHardwareFixture.Workspace();
            var mergedSources = new Dictionary<string, string>(workspace.Sources!, StringComparer.Ordinal);
            foreach (var source in sources) mergedSources[source.Key] = source.Value;
            workspace = DcsInventoryHardwareAuthoring.Add(workspace with { Sources = mergedSources }, choice, AssignmentLayer.Module, hardware);
            Assert.Equal(device.PhysicalGuid, DcsPhysicalIdentity.FromFileName(Assert.Single(workspace.Configuration.Profiles).SourceName));
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
            objects.Add(new(Guid.Parse("a36d02e3-c9f3-11cf-bfc7-444553540000"), miniStickAxes ? 1282u : 514u, "RZ"));
            objects.Add(new(Guid.Parse("a36d02f2-c9f3-11cf-bfc7-444553540000"), 16, "POV 1"));
            if (miniStickAxes)
            {
                objects.Add(new(Guid.Parse("a36d02f4-c9f3-11cf-bfc7-444553540000"), 770, "RX"));
                objects.Add(new(Guid.Parse("a36d02f5-c9f3-11cf-bfc7-444553540000"), 1026, "RY"));
                objects.Add(new(Guid.Parse("a36d02e2-c9f3-11cf-bfc7-444553540000"), 1794, "Z Axis"));
                objects.Add(new(Guid.Parse("a36d02e4-c9f3-11cf-bfc7-444553540000"), 514, "Dial"));
                objects.Add(new(Guid.Parse("a36d02e4-c9f3-11cf-bfc7-444553540000"), 1538, "Slider"));
            }
            var capabilities = ControllerCapabilityMapper.Map(objects);
            return new(ControllerCapabilityMapper.Fingerprint(capabilities), capabilities);
        }
        #endregion END private helpers
    }
}
