using System.Text.Json;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Heimdall.Rendering;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects AVA Hornet recognition, exact capabilities, native identity and reusable Hornet artwork.</summary>
    public sealed class AvaHornetHardwareTests
    {
        #region BEGIN Public
        /// <summary>The existing native rule resolves the AVA alias to Hornet art and exact physical controls.</summary>
        [Theory]
        [InlineData("AVA [R] Hornet")]
        [InlineData("Ava [R] Hornet")]
        [InlineData("AVA [R] Hornet USB")]
        public async Task Right_hornet_resolves_without_viper_or_foreign_defaults(string name)
        {
            var sources = await BundledHardwareTestFixture.LoadAsync();
            var hardware = Catalog(sources);
            var hornet = Assert.Single(hardware, item => item.DeviceId == "ava-base-f18c");
            Assert.Equal("assets/shared/hardware/svg/grip-f18c.svg", hornet.Diagram.Path);
            Assert.Equal(29, hornet.Diagram.Controls.Count);
            Assert.Contains(hornet.Diagram.Controls, control => control.InputKey == "JOY_BTN1" && control.Label.Contains("Trigger", StringComparison.Ordinal));
            Assert.Contains(hornet.Diagram.Controls, control => control.InputKey == "JOY_BTN6" && control.Label.Contains("Trigger", StringComparison.Ordinal));
            Assert.DoesNotContain(hornet.Diagram.Controls, control => control.InputKey is "JOY_RZ" or "JOY_BTN20");
            Assert.True(DcsInputKey.SamePhysicalInputs(Definition().Capabilities.DcsKeys, hornet.Diagram.Controls.Select(control => control.InputKey)));
            var observed = new ControllerObservation(Device(name), Definition().Capabilities);
            Assert.Equal("ava-base-f18c", Assert.Single(BundledHardwareCapabilityMatcher.Candidates(sources, observed)));
            new EditionPolicy().RequireHardware([hornet.DeviceId]);
            Assert.True(new EditionPolicy().AllowsDevice(observed.Device));
            Assert.False(new EditionPolicy().AllowsDevice(observed.Device with { ProductGuid = Guid.Parse("1234046d-0000-0000-0000-504944564944") }));
            Assert.Null(DcsPhysicalIdentity.FromFileName(hornet.DefaultFileName));
            Assert.Empty(hornet.Diagram.Callouts);
        }

        /// <summary>Unsupported handedness and different grips cannot acquire the right Hornet mapping from a generic AVA name.</summary>
        [Theory]
        [InlineData("AVA [L] Hornet")]
        [InlineData("AVA [R] Sol-R")]
        [InlineData("AVA [L] Sol-R")]
        [InlineData("AVA Base")]
        [InlineData("AVA [R] Viper")]
        public async Task Other_variants_never_fall_back_to_the_hornet_mapping(string name)
        {
            var sources = await BundledHardwareTestFixture.LoadAsync();
            var candidates = BundledHardwareCapabilityMatcher.Candidates(sources, new(Device(name), Definition().Capabilities));
            Assert.DoesNotContain("ava-base-f18c", candidates);
            if (name != "AVA [R] Viper") Assert.DoesNotContain("ava-base-f16c", candidates);
            if (name == "AVA [L] Hornet") Assert.Empty(candidates);
        }

        /// <summary>Recognition never substitutes for complete enumerated input evidence.</summary>
        [Theory]
        [InlineData("missing")]
        [InlineData("extra")]
        [InlineData("unsupported")]
        [InlineData("unavailable")]
        public async Task Changed_or_incomplete_controls_do_not_offer_hornet_artwork(string change)
        {
            var sources = await BundledHardwareTestFixture.LoadAsync();
            var capabilities = Definition(change == "missing" ? 18 : change == "extra" ? 20 : 19).Capabilities;
            if (change == "unsupported") capabilities = capabilities with
            {
                Unsupported = [new(new(Guid.NewGuid(), 1u, "Relative input"), "unsupported")]
            };
            Assert.Empty(BundledHardwareCapabilityMatcher.Candidates(sources,
                new(Device("AVA [R] Hornet"), change == "unavailable" ? null : capabilities)));
        }

        /// <summary>Generic and MOZA Hornet identities still resolve to the original art without inheriting AVA Free eligibility.</summary>
        [Fact]
        public async Task Existing_hornet_and_viper_catalogs_are_preserved()
        {
            var sources = await BundledHardwareTestFixture.LoadAsync();
            var hardware = Catalog(sources);
            var ava = Assert.Single(hardware, item => item.DeviceId == "ava-base-f18c");
            foreach (var id in new[] { "grip-f18c", "moza-ab9-hornet-grip" })
            {
                var original = Assert.Single(hardware, item => item.DeviceId == id);
                Assert.Equal(original.Diagram.Svg, ava.Diagram.Svg);
                Assert.True(DcsInputKey.SamePhysicalInputs(original.Diagram.Controls.Select(control => control.InputKey),
                    ava.Diagram.Controls.Select(control => control.InputKey)));
                Assert.False(new EditionPolicy().AllowsHardware(id));
            }
            Assert.Contains(hardware, item => item.DeviceId == "ava-base-f16c" && item.Diagram.Path.EndsWith("tm-warthog-grip.svg", StringComparison.Ordinal));
            Assert.Equal("moza-ab9-hornet-grip", Assert.Single(BundledHardwareCapabilityMatcher.Candidates(sources,
                new(Device("MOZA AB9 Hornet"), Definition().Capabilities))));
        }

        /// <summary>Explicit adoption preserves real instance filenames through save/reopen, snapshots and rendering.</summary>
        [Fact]
        public async Task Inventory_adoption_keeps_native_identity_and_portable_hornet_diagram()
        {
            var sources = await BundledHardwareTestFixture.LoadAsync();
            var hardware = Assert.Single(Catalog(sources), item => item.DeviceId == "ava-base-f18c");
            var definition = Definition(); var native = Device("AVA [R] Hornet");
            var instance = new HardwareInventoryInstance(native.PhysicalGuid, native.Name, definition.Id, "Hornet", "Stick", true, [hardware.DeviceId]);
            var choice = new InventoryHardwareChoice(instance, definition);
            var workspace = GenericHardwareFixture.Workspace();
            var moduleSources = new Dictionary<string, string>(workspace.Sources!, StringComparer.Ordinal);
            foreach (var source in sources) moduleSources[source.Key] = source.Value;
            workspace = workspace with { Sources = moduleSources };
            Assert.Equal("ava-base-f18c", DcsDetectedDeviceHardwareMatcher.Match(workspace, native)?.DeviceId);
            workspace = DcsInventoryHardwareAuthoring.Add(workspace, choice, AssignmentLayer.Module, hardware);
            workspace = DcsModuleKneeboardPageEditor.Set(workspace, null,
                new(KneeboardPageKind.Summary, null, "00-OVERVIEW", "HORNET", "AVA GRIP", 0,
                    [new("STICK", "Hornet grip on AVA", "cyan")]));
            var profile = Assert.Single(workspace.Configuration.Profiles);
            Assert.Equal("ava-base-f18c", profile.DeviceId);
            Assert.Equal(choice.FileName, profile.SourceName);
            Assert.Equal(native.PhysicalGuid, DcsPhysicalIdentity.FromFileName(profile.SourceName));
            var before = JsonSerializer.Serialize(workspace);
            Assert.Null(DcsDetectedDeviceHardwareMatcher.Match(workspace, native with { Name = "AVA [L] Hornet", Connected = false }));
            Assert.Equal(before, JsonSerializer.Serialize(workspace));
            var root = Path.Combine(Path.GetTempPath(), "heimdall-ava-hornet-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var connection = "Data Source=" + Path.Combine(root, "module.db") + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new DcsSqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(workspace);
                var reopened = (await repository.GetAsync(workspace.Configuration.Module.Id))!;
                var drawing = Assert.Single(reopened.Diagrams);
                Assert.Equal(hardware.Diagram.Path, drawing.Path);
                Assert.Equal(hardware.Diagram.Svg, drawing.Svg);
                Assert.Null(drawing.CustomMapping);
                Assert.Equal(native.PhysicalGuid, DcsPhysicalIdentity.FromFileName(Assert.Single(reopened.Configuration.Profiles).SourceName));
                Assert.NotEmpty(DcsSnapshotDeviceKneeboardRenderer.Generate(reopened));
                Assert.NotNull(DcsModuleSnapshotWorkflow.Read(DcsModuleSnapshotWorkflow.Export(DcsModuleSnapshotWorkflow.Freeze(reopened, "1.0"))));
            }
            finally { Directory.Delete(root, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static WorkspaceHardware[] Catalog(IReadOnlyDictionary<string, string> sources) =>
            JsonSerializer.Deserialize<WorkspaceHardware[]>(sources["config/ipia-hardware.json"])!;
        // Synthetic native identity proves vendor handling without publishing a personal instance or claiming an AVA PID capture.
        private static DetectedDcsDevice Device(string name) => new(Guid.NewGuid(), name, true,
            ProductGuid: Guid.Parse("1234044f-0000-0000-0000-504944564944"));
        private static HardwareDefinition Definition(int buttons = 19)
        {
            var objects = Enumerable.Range(0, buttons).Select(index => new ControllerObject(
                Guid.Parse("a36d02f0-c9f3-11cf-bfc7-444553540000"), 12u | ((uint)index << 8), "Button " + (index + 1))).ToList();
            objects.Add(new(Guid.Parse("a36d02e0-c9f3-11cf-bfc7-444553540000"), 2u, "X"));
            objects.Add(new(Guid.Parse("a36d02e1-c9f3-11cf-bfc7-444553540000"), 258u, "Y"));
            objects.Add(new(Guid.Parse("a36d02f2-c9f3-11cf-bfc7-444553540000"), 16u, "POV 1"));
            var capabilities = ControllerCapabilityMapper.Map(objects);
            return new(ControllerCapabilityMapper.Fingerprint(capabilities), capabilities);
        }
        #endregion END private helpers
    }
}
