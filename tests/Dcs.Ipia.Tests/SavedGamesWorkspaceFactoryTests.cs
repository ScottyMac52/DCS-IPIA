using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks editable Saved Games imports across independent module identities and physical units.</summary>
    public sealed class SavedGamesWorkspaceFactoryTests
    {
        #region BEGIN Public
        /// <summary>Removal-only and empty profiles remain devices; active commands, chords and tuning survive storage and export.</summary>
        /// <param name="module">An arbitrary module folder, unrelated to command or hardware identity.</param>
        [Theory]
        [InlineData("CUSTOM-AIRCRAFT")]
        [InlineData("Rotorcraft_Input")]
        public async Task Import_round_trips_devices_bindings_and_removals(string module)
        {
            var profile = new DeviceProfile(Guid.NewGuid(), "Offline controller", null, "unresolved device", FileName(1));
            var removed = new DeviceProfile(Guid.NewGuid(), "Offline controller", null, "unresolved device", FileName(2));
            var empty = new DeviceProfile(Guid.NewGuid(), "Empty panel", null, "unresolved device", FileName(3));
            DcsBinding[] bindings = [
                new(profile.Id, BindingSection.Axis, "a9001cdnil", "Custom axis", BindingDisposition.Added, "JOY_X", ["SHIFT"], new(Deadzone: 7)),
                new(profile.Id, BindingSection.Key, "d9002pnilunilcdnilvdnilvpnilvunil", "Custom key", BindingDisposition.Added, "JOY_BTN1"),
                new(removed.Id, BindingSection.Axis, "a9010cdnil", "Unwanted default", BindingDisposition.Removed, "JOY_Z")
            ];
            var folder = new DcsFolderImportResult(module, [profile, removed, empty], bindings,
                [new("SHIFT", "Keyboard", "LShift", ModifierMode.Hold)], [])
            {
                Sources = new Dictionary<string, string>
                {
                    ["keyboard/Keyboard.diff.lua"] = "local diff = { [\"keyDiffs\"] = { [\"KEYBOARD\"] = { [\"name\"] = \"Keyboard command\", [\"added\"] = { [1] = { [\"key\"] = \"K\", [\"reformers\"] = { [1] = \"SHIFT\" } } } } } } return diff"
                }
            };
            var hardware = Hardware();
            var workspace = SavedGamesWorkspaceFactory.Create(folder, module, "Imported aircraft", Defaults(), Sources(hardware),
                [new(profile.Id, hardware), new(removed.Id, hardware), new(empty.Id, hardware)]);
            Assert.Equal(3, WorkspaceStructureEditor.Describe(workspace).Devices.Count(x => x.Layer == AssignmentLayer.Module));
            Assert.Equal(2, workspace.Assignments.Count(x => x.Slot.Layer == AssignmentLayer.Module));
            Assert.Equal(3, workspace.Configuration.Profiles.Select(x => x.Id).Distinct().Count());
            var path = Path.Combine(Path.GetTempPath(), "ipia-saved-games-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(workspace);
                var reopened = (await repository.GetAsync(workspace.Configuration.Module.Id))!;
                var payload = SnapshotLuaPayloadRenderer.Generate(reopened);
                Assert.Equal(folder.Sources["keyboard/Keyboard.diff.lua"], Encoding.UTF8.GetString(payload["Config/Input/" + module + "/keyboard/Keyboard.diff.lua"]));
                var first = DcsLuaParser.ParseProfile(Encoding.UTF8.GetString(payload["Config/Input/" + module + "/joystick/" + profile.SourceName]), profile.Id);
                Assert.Contains(first, x => x.CommandId == "a9001cdnil" && x.Reformers.SequenceEqual(["SHIFT"]) && x.Filter?.Deadzone == 7 && x.Disposition != BindingDisposition.Removed);
                Assert.Contains(first, x => x.InputKey == "JOY_BTN1" && x.Disposition != BindingDisposition.Removed);
                var second = DcsLuaParser.ParseProfile(Encoding.UTF8.GetString(payload["Config/Input/" + module + "/joystick/" + removed.SourceName]), removed.Id);
                Assert.Equal(BindingDisposition.Removed, Assert.Single(second).Disposition);
                Assert.Empty(DcsLuaParser.ParseProfile(Encoding.UTF8.GetString(payload["Config/Input/" + module + "/joystick/" + empty.SourceName]), empty.Id));
                var portable = EditorSnapshotSerializer.Deserialize(EditorSnapshotSerializer.Serialize(reopened));
                Assert.Equal(payload.Keys, SnapshotLuaPayloadRenderer.Generate(portable).Keys);
                foreach (var pair in payload)
                    Assert.Equal(pair.Value, SnapshotLuaPayloadRenderer.Generate(portable)[pair.Key]);
            }
            finally { File.Delete(path); }
        }

        /// <summary>Unresolved hardware and erroneous sources cannot silently produce a partial module.</summary>
        [Fact]
        public void Import_requires_every_profile_to_be_reviewed()
        {
            var profile = new DeviceProfile(Guid.NewGuid(), "Unknown", null, "unresolved device", FileName(1));
            var folder = new DcsFolderImportResult("Some-module", [profile], [], [], []);
            var hardware = Hardware();
            Assert.Throws<InvalidDataException>(() => SavedGamesWorkspaceFactory.Create(folder, "Some-module", "Aircraft", Defaults(), Sources(hardware), []));
            Assert.Throws<InvalidDataException>(() => SavedGamesWorkspaceFactory.Create(folder, "Some-module", "Aircraft", Defaults(), Sources(hardware),
                [new(profile.Id, hardware), new(profile.Id, hardware)]));
            var invalid = new DcsFolderImportResult(folder.Folder, folder.Profiles, [], [], [new("invalid-profile", "Broken Lua", true)]);
            Assert.Throws<InvalidDataException>(() => SavedGamesWorkspaceFactory.Create(invalid, "Some-module", "Aircraft", Defaults(), Sources(hardware), [new(profile.Id, hardware)]));
            Assert.Throws<InvalidDataException>(() => SavedGamesWorkspaceFactory.Create(folder, "Some-module", "Aircraft", Defaults(), Sources(hardware), [new(Guid.NewGuid(), hardware)]));
            Assert.Throws<InvalidDataException>(() => SavedGamesWorkspaceFactory.Create(folder, "Some-module", "Aircraft", Defaults(), Sources(hardware),
                [new(profile.Id, hardware with { DeviceId = "outside-catalog" })]));
        }

        /// <summary>Unknown active axes block mismatched hardware, while unmapped keys are preserved in native exports.</summary>
        [Fact]
        public void Import_does_not_silently_remove_active_axes_or_unmapped_keys()
        {
            var profile = new DeviceProfile(Guid.NewGuid(), "Controller", null, "unresolved device", FileName(1));
            var hardware = Hardware();
            var key = new DcsBinding(profile.Id, BindingSection.Key, "KEY", "Unmapped key", BindingDisposition.Added, "JOY_BTN99");
            var axis = new DcsBinding(profile.Id, BindingSection.Axis, "a9000cdnil", "Unmapped axis", BindingDisposition.Added, "JOY_Z");
            var folder = new DcsFolderImportResult("Any-aircraft", [profile], [key, axis], [], []);
            Assert.Throws<InvalidDataException>(() => SavedGamesWorkspaceFactory.Create(folder, "Any-aircraft", "Aircraft", Defaults(), Sources(hardware), [new(profile.Id, hardware)]));
            folder = new(folder.Folder, folder.Profiles, [key], [], []);
            var imported = SavedGamesWorkspaceFactory.Create(folder, "Any-aircraft", "Aircraft", Defaults(), Sources(hardware), [new(profile.Id, hardware)]);
            var device = WorkspaceStructureEditor.Describe(imported).Devices.Single();
            Assert.Equal("JOY_BTN99", Assert.Single(DcsLuaParser.ParseProfile(ModuleLuaProfileRenderer.Render(imported, device.ProfileId), device.ProfileId)).InputKey);
        }

        /// <summary>Bundled mapping works for helicopter and piston-module fixtures, including repeated controller types.</summary>
        /// <param name="fixture">Existing module fixture directory.</param>
        /// <param name="module">DCS input folder within that fixture.</param>
        [Theory]
        [InlineData("Huey", "UH-1H")]
        [InlineData("F4U", "F4U-1D")]
        public async Task Existing_modules_use_catalog_rules_without_aircraft_special_cases(string fixture, string module)
        {
            var folder = await new DcsFolderImporter().ImportAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture, "src", "Config", "Input", module));
            // The existing Corsair fixture contains an undeclared legacy chord. Reject it, then provide an explicit corrected definition.
            if (fixture == "F4U")
            {
                Assert.Contains(folder.Diagnostics, x => x.Code == "unknown-modifier");
                folder = new(folder.Folder, folder.Profiles, folder.Bindings,
                    [.. folder.Modifiers, new("JOY_BTN3", "Keyboard", "CapsLock", ModifierMode.Hold)],
                    folder.Diagnostics.Where(x => x.Code != "unknown-modifier").ToArray())
                { Sources = folder.Sources };
            }
            Assert.True(folder.Succeeded, string.Join("; ", folder.Diagnostics.Select(x => x.Message)));
            var defaults = await SavedDefaultsAsync();
            var sources = await ModuleWorkspaceLoader.LoadHardwareAsync(BundledSharedCatalog.Root);
            var catalog = NewModuleWorkspaceFactory.Create("catalog-test", "Catalog", defaults, sources);
            var selections = folder.Profiles.Select(x => new SavedGamesDeviceSelection(x.Id,
                DetectedDeviceHardwareMatcher.Match(catalog, new(DcsPhysicalIdentity.FromFileName(x.SourceName)!.Value,
                    DcsPhysicalIdentity.NameFromFileName(x.SourceName), false, x.SourceName))!)).ToArray();
            Assert.All(selections, x => Assert.NotNull(x.Hardware));
            if (fixture == "F4U")
            {
                // This legacy fixture also assigns multiple commands to one slot. Production import must reject it.
                Assert.Throws<ArgumentException>(() => SavedGamesWorkspaceFactory.Create(folder, module, fixture, defaults, sources, selections));
                var corrected = folder.Bindings.Where(x => x.Disposition != BindingDisposition.Removed)
                    .GroupBy(x => DcsInputKey.PhysicalSlotKey(new(Guid.Empty, x.ProfileId, AssignmentLayer.Module,
                        x.Section, x.InputKey, string.Join("+", x.Reformers))))
                    .Select(x => x.First()).Concat(folder.Bindings.Where(x => x.Disposition == BindingDisposition.Removed)).ToArray();
                folder = new(folder.Folder, folder.Profiles, corrected, folder.Modifiers, folder.Diagnostics) { Sources = folder.Sources };
            }
            var imported = SavedGamesWorkspaceFactory.Create(folder, module, fixture, defaults, sources, selections);
            Assert.Equal(folder.Profiles.Count, WorkspaceStructureEditor.Describe(imported).Devices.Count(x => x.Layer == AssignmentLayer.Module));
            foreach (var source in folder.Profiles)
            {
                var device = WorkspaceStructureEditor.Describe(imported).Devices.Single(x => x.Layer == AssignmentLayer.Module && x.FileName == source.SourceName);
                var rendered = DcsLuaParser.ParseProfile(ModuleLuaProfileRenderer.Render(imported, device.ProfileId), device.ProfileId);
                foreach (var binding in folder.Bindings.Where(x => x.ProfileId == source.Id))
                    Assert.Contains(rendered, x => x.CommandId == binding.CommandId && x.InputKey == binding.InputKey &&
                        x.Disposition == binding.Disposition && x.Reformers.SequenceEqual(binding.Reformers));
            }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static async Task<SharedUiLayerWorkspace> SavedDefaultsAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-import-defaults-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                return await new SqliteSharedUiLayerWorkspaceRepository(connection).SaveAsync(await SharedUiLayerWorkspaceFactory.CreateAsync());
            }
            finally { File.Delete(path); }
        }
        private static string FileName(int unit) => "Offline controller {00000000-0000-0000-0000-" + unit.ToString("D12", System.Globalization.CultureInfo.InvariantCulture) + "}.diff.lua";
        private static WorkspaceHardware Hardware() => new("test-controller", "Controller", "Controller.diff.lua",
            new("Controller", null, "controller.svg", 100, 100, [], [])
            {
                Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"/>",
                Controls = [new("axis", "Axis", "JOY_X", 0, 0, 10, 10, true), new("key", "Key", "JOY_BTN1", 20, 0, 10, 10)]
            });
        private static Dictionary<string, string> Sources(WorkspaceHardware hardware) => new()
        {
            ["config/ipia-hardware.json"] = JsonSerializer.Serialize(new[] { hardware })
        };
        private static SharedUiLayerWorkspace Defaults()
        {
            var sources = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = "local modifiers = {} return modifiers",
                ["assets/shared/ui-layer/functions.json"] = """{"schemaVersion":1,"functions":[]}"""
            };
            var commit = new string('a', 40);
            return new(1, commit, sources, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(commit + "\n" + JsonSerializer.Serialize(sources)))));
        }
        #endregion END private helpers
    }
}
