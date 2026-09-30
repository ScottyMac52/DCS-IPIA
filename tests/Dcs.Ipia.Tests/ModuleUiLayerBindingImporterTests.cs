using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks module-owned UI Layer composition across device and overlay schemas.</summary>
    public sealed class ModuleUiLayerBindingImporterTests
    {
        #region BEGIN Public
        /// <summary>An authored grip identity does not lose the canonical profile for its physical base filename.</summary>
        [Fact]
        public void ResolvesSamePhysicalFilenameAcrossDeviceClassification()
        {
            const string file = "MOZA AB9 FFB Base {71DA6210-432E-11f1-8001-444553540000}.diff.lua";
            var profile = new DeviceProfile(Guid.NewGuid(), "moza-ab9-hornet-grip", null, "Grip", file);
            var canonical = new DeviceProfile(Guid.NewGuid(), "moza-ab9", null, "UI", file);
            var configuration = new ModuleConfiguration(new DcsModule(Guid.NewGuid(), "UH-1H", "Huey"),
                [profile], [], [], "test");
            var imported = new ConsumerImportResult(configuration, [], [], [], [],
                ParseMetadata("""{"includeUiLayer":true}""", null), new Dictionary<string, string>(), [], []);
            var shared = new SharedLuaImportResult([], [canonical],
                [new(canonical.Id, BindingSection.Key, "UI", "Recenter VR", BindingDisposition.Added, "JOY_BTN4")],
                [], [], []);
            Assert.Equal(canonical, ModuleUiLayerBindingImporter.ResolveCanonicalProfile(imported, shared, profile));
            Assert.Equal(profile.Id, Assert.Single(ModuleUiLayerBindingImporter.Import(imported, shared, profile)).ProfileId);
            var different = new DeviceProfile(Guid.NewGuid(), canonical.DeviceId, null, "UI", "Other base.diff.lua");
            Assert.Null(ModuleUiLayerBindingImporter.ResolveCanonicalProfile(imported,
                new SharedLuaImportResult([], [different], [], [], [], []), profile));
            var duplicate = new DeviceProfile(Guid.NewGuid(), profile.DeviceId, null, "UI", file);
            Assert.Null(ModuleUiLayerBindingImporter.ResolveCanonicalProfile(imported,
                new SharedLuaImportResult([], [canonical, duplicate], [], [], [], []), profile));
        }
        /// <summary>Canonical assignments are selected per device and changed by authored overlay rows.</summary>
        [Fact]
        public void Composes_shared_bindings_with_module_clear_and_assignment()
        {
            var module = new DcsModule(Guid.NewGuid(), "TEST", "Test module");
            var profile = new DeviceProfile(Guid.NewGuid(), "test-panel", null, "Panel",
                                            "Panel {11111111-1111-1111-1111-111111111111}.diff.lua");
            var configuration = new ModuleConfiguration(module, [profile], [], [], "test@sha");
            var sharedProfile = new DeviceProfile(Guid.NewGuid(), "test-panel", null, "UI",
                                                  "Panel {22222222-2222-2222-2222-222222222222}.diff.lua");
            var shared =
                new SharedLuaImportResult([], [sharedProfile],
                                          [
                                              new(sharedProfile.Id, BindingSection.Key, "BASE", "Base command",
                                                  BindingDisposition.Added, "JOY_BTN1"),
                                              new(sharedProfile.Id, BindingSection.Key, "SHIFT", "Shift command",
                                                  BindingDisposition.Added, "JOY_BTN2", ["SHIFT"])
                                          ],
                                          [], [], []);
            var metadata = ParseMetadata("""
               {"includeUiLayer":true}
               """, """
                {"assignments":[
                 {"profileFile":"Panel {22222222-2222-2222-2222-222222222222}.diff.lua","section":"keyDiffs","key":"JOY_BTN1","reformers":[],"clear":true},
                 {"profileFile":"Panel {22222222-2222-2222-2222-222222222222}.diff.lua","section":"keyDiffs","key":"JOY_BTN3","reformers":["SHIFT"],"clear":false,"command":"NEW","name":"New command"}
                ],"removedModifiers":[]}
                """);
            var imported = new ConsumerImportResult(configuration, [], [], [], [], metadata,
                                                    new Dictionary<string, string>(), [], []);
            Assert.Equal(sharedProfile,
                         ModuleUiLayerBindingImporter.ResolveCanonicalProfile(imported, shared, profile));
            var effective = ModuleUiLayerBindingImporter.Import(imported, shared, profile);
            Assert.Equal(2, effective.Count);
            Assert.DoesNotContain(effective, x => x.CommandId == "BASE");
            Assert.Contains(effective, x => x.CommandId == "SHIFT" && x.ProfileId == profile.Id);
            Assert.Contains(effective, x => x.CommandId == "NEW" && x.InputKey == "JOY_BTN3" &&
                                            x.Reformers.SequenceEqual(["SHIFT"]));
            var disabled =
                new ConsumerImportResult(configuration, [], [], [], [], ParseMetadata("""{"includeUiLayer":false}""", null),
                                         new Dictionary<string, string>(), [], []);
            Assert.Empty(ModuleUiLayerBindingImporter.Import(disabled, shared, profile));
            Assert.Null(ModuleUiLayerBindingImporter.ResolveCanonicalProfile(disabled, shared, profile));
            Assert.Empty(ModuleUiLayerBindingImporter.Import(
                imported, shared, new DeviceProfile(Guid.NewGuid(), "other-panel", null, "Other", "Other.diff.lua")));
            var removedOverlay = JsonDocument.Parse("""
               {"assignments":[{"profileFile":"Panel {22222222-2222-2222-2222-222222222222}.diff.lua","section":"keyDiffs","key":"JOY_BTN1",
                "reformers":[],"clear":true}],"removedModifiers":["SHIFT"]}
               """).RootElement.Clone();
            var removedMetadata = metadata.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            removedMetadata["config/ui-layer-overlay.json"] = removedOverlay;
            var removedImport = new ConsumerImportResult(configuration, [], [], [], [], removedMetadata,
                                                         new Dictionary<string, string>(), [], []);
            Assert.Empty(ModuleUiLayerBindingImporter.Import(removedImport, shared, profile));
        }
        /// <summary>Ambiguous canonical candidates never select an arbitrary device instance.</summary>
        [Fact]
        public void ResolveCanonicalProfile_RejectsAmbiguousProfiles()
        {
            var module = new DcsModule(Guid.NewGuid(), "TEST", "Test module");
            var profile = new DeviceProfile(Guid.NewGuid(), "test-panel", null, "Panel", "Different.diff.lua");
            var configuration = new ModuleConfiguration(module, [profile], [], [], "test@sha");
            var candidates = new[] { new DeviceProfile(Guid.NewGuid(), "test-panel", null, "UI", "One.diff.lua"),
                                     new DeviceProfile(Guid.NewGuid(), "test-panel", null, "UI", "Two.diff.lua") };
            var shared = new SharedLuaImportResult([], candidates, [], [], [], []);
            var imported =
                new ConsumerImportResult(configuration, [], [], [], [], ParseMetadata("""{"includeUiLayer":true}""", null),
                                         new Dictionary<string, string>(), [], []);
            Assert.Null(ModuleUiLayerBindingImporter.ResolveCanonicalProfile(imported, shared, profile));
            var duplicateExact =
                candidates.Select(x => new DeviceProfile(x.Id, x.DeviceId, x.Instance, x.Role, profile.SourceName))
                    .ToArray();
            Assert.Null(ModuleUiLayerBindingImporter.ResolveCanonicalProfile(
                imported, new SharedLuaImportResult([], duplicateExact, [], [], [], []), profile));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static Dictionary<string, JsonElement> ParseMetadata(string kneeboard, string? overlay)
        {
            var result = new Dictionary<string, JsonElement> { ["config/kneeboard.json"] =
                                                                   JsonDocument.Parse(kneeboard).RootElement.Clone() };
            if (overlay is not null)
                result["config/ui-layer-overlay.json"] = JsonDocument.Parse(overlay).RootElement.Clone();
            return result;
        }
        #endregion END private helpers
    }
}
