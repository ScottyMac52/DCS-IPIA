using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Rendering;
using Heimdall.Infrastructure;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises module-owned device and modifier management through generated Lua and saved settings.</summary>
    public sealed class WorkspaceStructureEditorTests
    {
        private static readonly Guid ModuleId = Guid.Parse("115c5cf8-c3ac-4d80-a512-5d92bd36856a");
        private static readonly Guid ProfileId = Guid.Parse("b2da76ec-5d9a-497d-9712-43864c304939");
        #region BEGIN Public
        /// <summary>Deleting one physical unit keeps another unit of the same hardware type and its page.</summary>
        [Fact]
        public void KeepsOtherUnitsAndRemainingScopePages()
        {
            var workspace = Workspace();
            var hardware = new WorkspaceHardware("stick", "Stick", "Other.diff.lua", workspace.Diagrams[0]);
            workspace = WorkspaceStructureEditor.AddDevice(workspace, hardware, AssignmentLayer.Module, "Other.diff.lua", "SECOND", null);
            var other = workspace.Configuration.Profiles.Single(x => x.Id != ProfileId);
            workspace = WorkspaceStructureEditor.ClearDeviceAssignments(workspace, WorkspaceStructureEditor.Describe(workspace).Devices.First(x => x.ProfileId == ProfileId));
            foreach (var modifier in WorkspaceStructureEditor.Describe(workspace).Modifiers.ToArray())
                workspace = WorkspaceStructureEditor.RemoveModifier(workspace, modifier);
            var members = WorkspaceStructureEditor.Describe(workspace).Devices.Where(x => x.ProfileId == ProfileId).ToArray();
            workspace = WorkspaceStructureEditor.RemoveDevice(workspace, members[0]);
            Assert.Contains(workspace.Pages, x => x.StableKey == "01-STICK");
            workspace = WorkspaceStructureEditor.RemoveDevice(workspace, members[1]);
            Assert.Equal(other.Id, Assert.Single(workspace.Configuration.Profiles).Id);
            Assert.Equal("device-" + other.Id.ToString("N"), Assert.Single(workspace.Pages).StableKey);
            Assert.Equal(0, workspace.Pages[0].Order);
            Assert.True(ModuleKneeboardPageEditor.CanRemove(workspace, workspace.Pages[0]));
        }

        /// <summary>Deletion removes all filename aliases and compacts remaining page orders through SQLite reopening.</summary>
        [Theory]
        [InlineData("src/Config/Input/TEST/joystick/Stick.diff.lua")]
        [InlineData("src/Config/Input/TEST/joystick/STICK.DIFF.LUA")]
        [InlineData("src\\Config\\Input\\TEST\\joystick\\Stick.diff.lua")]
        public async Task RemovesAllDevicePageAliasesAndRenumbers(string mappedPath)
        {
            var saved = Workspace();
            var root = JsonNode.Parse(saved.Sources!["config/kneeboard.json"])!.AsObject();
            root["profiles"]!["stick"] = mappedPath;
            root["profiles"]!["alias"] = mappedPath;
            root["pages"]!.AsArray().Add(new JsonObject { ["profile"] = "alias", ["file"] = "02-ALIAS", ["title"] = "Alias" });
            var summary = new KneeboardPage(Guid.NewGuid(), ModuleId, null, KneeboardPageKind.Summary, "03-SUMMARY", "Summary", [], 9);
            saved = saved with
            {
                Pages = [.. saved.Pages, new(Guid.NewGuid(), ModuleId, null, KneeboardPageKind.Device, "02-ALIAS", "Alias", [], 5), summary],
                Images = [new("01-STICK", "old.png", [1], "old"), new("02-ALIAS", "alias.png", [2], "old")],
                Sources = new Dictionary<string, string>(saved.Sources!) { ["config/kneeboard.json"] = root.ToJsonString() }
            };
            saved = WorkspaceStructureEditor.ClearDeviceAssignments(saved, WorkspaceStructureEditor.Describe(saved).Devices[0]);
            foreach (var modifier in WorkspaceStructureEditor.Describe(saved).Modifiers.ToArray())
                saved = WorkspaceStructureEditor.RemoveModifier(saved, modifier);
            saved = WorkspaceStructureEditor.UpdateDevice(saved, WorkspaceStructureEditor.Describe(saved).Devices.First(x => x.Layer == AssignmentLayer.Module),
                "Renamed stick.diff.lua", null, null);
            var before = saved;
            foreach (var membership in WorkspaceStructureEditor.Describe(saved).Devices.ToArray())
                saved = WorkspaceStructureEditor.RemoveDevice(saved, membership);
            Assert.Equal(summary.Id, Assert.Single(saved.Pages).Id);
            Assert.Equal(0, saved.Pages[0].Order);
            Assert.Empty(saved.Images);
            using var manifest = JsonDocument.Parse(saved.Sources!["config/kneeboard.json"]);
            Assert.Empty(manifest.RootElement.GetProperty("profiles").EnumerateObject());
            Assert.Empty(manifest.RootElement.GetProperty("pages").EnumerateArray());
            var path = Path.Combine(Path.GetTempPath(), "ipia-deletion-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(before);
                await repository.SaveAsync(saved);
                var reopened = (await repository.GetAsync(ModuleId))!;
                Assert.Equal(summary.Id, Assert.Single(reopened.Pages).Id);
                Assert.Equal(0, reopened.Pages[0].Order);
                Assert.Empty(reopened.Configuration.Profiles);
                Assert.Empty(reopened.Images);
            }
            finally { File.Delete(path); }
        }

        /// <summary>Layer clearing removes mapped and unmapped exports without affecting the other scope or original draft.</summary>
        [Fact]
        public void ClearsSelectedDeviceScopeAndChord()
        {
            var baseline = Workspace();
            var moduleDevice = WorkspaceStructureEditor.Describe(baseline).Devices.Single(x => x.Layer == AssignmentLayer.Module);
            var cleared = WorkspaceStructureEditor.ClearLayerAssignments(baseline, moduleDevice, "SHIFT");
            Assert.Single(cleared.Assignments);
            Assert.Empty(cleared.Labels);
            Assert.Empty(DcsLuaParser.ParseProfile(ModuleLuaProfileRenderer.Render(cleared, ProfileId), ProfileId));
            Assert.Contains(DcsLuaParser.ParseProfile(UiLayerLuaProfileRenderer.Render(cleared, ProfileId), ProfileId), x => x.CommandId == "UNMAPPED");
            var uiDevice = WorkspaceStructureEditor.Describe(cleared).Devices.Single(x => x.Layer == AssignmentLayer.UiLayer);
            var unchanged = WorkspaceStructureEditor.ClearLayerAssignments(cleared, uiDevice, "");
            Assert.Single(unchanged.Assignments);
            cleared = WorkspaceStructureEditor.ClearLayerAssignments(cleared, uiDevice, " UI_SHIFT ");
            Assert.Empty(cleared.Assignments);
            Assert.Empty(DcsLuaParser.ParseProfile(UiLayerLuaProfileRenderer.Render(cleared, ProfileId), ProfileId));
            Assert.Equal(2, baseline.Assignments.Count);
        }
        /// <summary>New devices use editable filenames, optional identity fields, and independent scope membership.</summary>
        [Fact]
        public void AddsUpdatesAndExportsScopeSpecificDevices()
        {
            var saved = Workspace();
            var hardware = new WorkspaceHardware("other", "Other device", "Default.diff.lua", saved.Diagrams[0]);
            saved = WorkspaceStructureEditor.AddDevice(saved, hardware, AssignmentLayer.UiLayer, "Custom UI.diff.lua", null, null);
            var state = WorkspaceStructureEditor.Describe(saved);
            var added = state.Devices.Single(x => x.FileName == "Custom UI.diff.lua");
            Assert.Null(added.Instance);
            Assert.Null(added.Role);
            Assert.DoesNotContain(state.Devices, x => x.ProfileId == added.ProfileId && x.Layer == AssignmentLayer.Module);
            var payload = SnapshotLuaPayloadRenderer.Generate(saved);
            Assert.Contains("Config/Input/UiLayer/joystick/Custom UI.diff.lua", payload.Keys);
            Assert.DoesNotContain("Config/Input/TEST/joystick/Custom UI.diff.lua", payload.Keys);
            saved = WorkspaceStructureEditor.UpdateDevice(saved, added, "Renamed UI.diff.lua", "SECOND", "Auxiliary");
            Assert.Contains("Config/Input/UiLayer/joystick/Renamed UI.diff.lua", SnapshotLuaPayloadRenderer.Generate(saved).Keys);
            var json = saved.Sources![WorkspaceManagement.SourcePath];
            var restored = JsonSerializer.Deserialize<WorkspaceManagement>(json)!;
            Assert.Equal("Auxiliary", restored.Devices.Single(x => x.ProfileId == added.ProfileId).Role);
            saved = WorkspaceStructureEditor.RemoveDevice(saved, WorkspaceStructureEditor.Describe(saved).Devices.Single(x => x.ProfileId == added.ProfileId));
            Assert.DoesNotContain(saved.Configuration.Profiles, x => x.Id == added.ProfileId);
            Assert.DoesNotContain(saved.Diagrams, x => x.ProfileId == added.ProfileId);
            Assert.DoesNotContain(saved.Pages, x => x.StableKey == "device-" + added.ProfileId.ToString("N"));
        }
        /// <summary>Removal checks UI use and unmapped bindings, and explicit clearing makes removal possible.</summary>
        [Fact]
        public void BlocksUsedDevicesUntilBothScopesAndActivatorsAreCleared()
        {
            var saved = Workspace();
            var device = WorkspaceStructureEditor.Describe(saved).Devices.First(x => x.Layer == AssignmentLayer.Module);
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.RemoveDevice(saved, device));
            var onlyUi = saved with { Assignments = saved.Assignments.Where(x => x.Slot.Layer == AssignmentLayer.UiLayer).ToArray() };
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.RemoveDevice(onlyUi, device));
            saved = WorkspaceStructureEditor.ClearDeviceAssignments(saved, device);
            Assert.Empty(saved.Assignments);
            Assert.DoesNotContain(DcsLuaParser.ParseProfile(UiLayerLuaProfileRenderer.Render(saved, ProfileId), ProfileId),
                x => x.Disposition != BindingDisposition.Removed);
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.RemoveDevice(saved, device));
            foreach (var modifier in WorkspaceStructureEditor.Describe(saved).Modifiers.ToArray())
                saved = WorkspaceStructureEditor.RemoveModifier(saved, modifier);
            foreach (var membership in WorkspaceStructureEditor.Describe(saved).Devices.ToArray())
                saved = WorkspaceStructureEditor.RemoveDevice(saved, membership);
            Assert.Empty(saved.Configuration.Profiles);
        }
        /// <summary>Modifier updates preserve used references and labels, while removal checks native and semantic uses across scopes.</summary>
        [Fact]
        public void RenamesUsedModifiersAndChecksCrossScopeUsage()
        {
            var saved = Workspace();
            var shift = WorkspaceStructureEditor.Describe(saved).Modifiers.Single(x => x.Layer == AssignmentLayer.Module);
            saved = WorkspaceStructureEditor.SetModifier(saved, shift, shift with { Name = "RENAMED", SemanticLayer = "Combat", Mode = ModifierMode.Toggle });
            var renamed = WorkspaceStructureEditor.Describe(saved).Modifiers.Single(x => x.Layer == AssignmentLayer.Module);
            Assert.Contains(saved.Assignments, x => x.Slot.Layer == AssignmentLayer.Module && x.Slot.ModifierChord == "RENAMED");
            Assert.Contains(saved.Labels, x => x.Key == saved.Assignments.Single(a => a.Slot.Layer == AssignmentLayer.Module).Slot.Key && x.Value == "My label");
            var payload = SnapshotLuaPayloadRenderer.Generate(saved);
            Assert.Contains("RENAMED", Encoding.UTF8.GetString(payload["Config/Input/TEST/joystick/Stick.diff.lua"]));
            Assert.Contains("[\"switch\"] = true", Encoding.UTF8.GetString(payload["Config/Input/TEST/modifiers.lua"]));
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.RemoveModifier(saved, renamed));
            var ui = WorkspaceStructureEditor.Describe(saved).Modifiers.Single(x => x.Layer == AssignmentLayer.UiLayer);
            saved = WorkspaceStructureEditor.SetModifier(saved, ui, ui with { SemanticLayer = "Combat" });
            var moduleCleared = saved with { Assignments = saved.Assignments.Where(x => x.Slot.Layer != AssignmentLayer.Module).ToArray() };
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.RemoveModifier(moduleCleared, renamed));
            saved = WorkspaceStructureEditor.ClearDeviceAssignments(saved, WorkspaceStructureEditor.Describe(saved).Devices[0]);
            saved = WorkspaceStructureEditor.RemoveModifier(saved, renamed);
            Assert.DoesNotContain(WorkspaceStructureEditor.Describe(saved).Modifiers, x => x.Name == "RENAMED");
        }
        /// <summary>UI modifier renames migrate mapped and unmapped bindings without losing either.</summary>
        [Fact]
        public void RenamesUiModifierAndKeepsUnmappedBindings()
        {
            var saved = Workspace();
            var ui = WorkspaceStructureEditor.Describe(saved).Modifiers.Single(x => x.Layer == AssignmentLayer.UiLayer);
            saved = WorkspaceStructureEditor.SetModifier(saved, ui, ui with { Name = "VOICE_SHIFT", SemanticLayer = "Voice", Mode = ModifierMode.Toggle });
            var bindings = DcsLuaParser.ParseProfile(UiLayerLuaProfileRenderer.Render(saved, ProfileId), ProfileId);
            Assert.Contains(bindings, x => x.CommandId == "UNMAPPED");
            Assert.All(bindings, x => Assert.Contains("VOICE_SHIFT", x.Reformers));
            Assert.DoesNotContain(saved.Assignments, x => x.Slot.Layer == AssignmentLayer.UiLayer && x.Slot.ModifierChord == "UI_SHIFT");
        }
        /// <summary>Digital shared controls and valid unique identities are required before a change can be staged.</summary>
        [Fact]
        public void RejectsInvalidDevicesAndModifiers()
        {
            var saved = Workspace();
            var device = WorkspaceStructureEditor.Describe(saved).Devices[0];
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.UpdateDevice(saved, device, "../bad.diff.lua", null, null));
            var hardware = new WorkspaceHardware("other", "Other", "Other.diff.lua", saved.Diagrams[0]);
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.AddDevice(saved, hardware, AssignmentLayer.Module, "Stick.diff.lua", null, null));
            var modifier = new WorkspaceModifier(AssignmentLayer.Module, ProfileId, "NEW", "JOY_X", "Layer", ModifierMode.Hold, "");
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.SetModifier(saved, null, modifier));
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.SetModifier(saved, null, modifier with { Key = "JOY_BTN2", Name = "SHIFT" }));
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.SetModifier(saved, null, modifier with { Key = "JOY_BTN1", SemanticLayer = "" }));
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.SetModifier(saved, null, modifier with { Key = "JOY_BTN1", Name = "A+B" }));
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.UpdateDevice(saved, device, "Other.diff.lua", null, null, hardware: hardware));
        }
        /// <summary>New modifiers become valid assignment layers and exports use the edited activator filename.</summary>
        [Fact]
        public void AddsModifiersToBothScopesAndUpdatesNativeDeviceName()
        {
            var saved = Workspace();
            foreach (var layer in new[] { AssignmentLayer.Module, AssignmentLayer.UiLayer })
                saved = WorkspaceStructureEditor.SetModifier(saved, null,
                    new(layer, ProfileId, "EXTRA", "JOY_BTN1", "Extra Layer", ModifierMode.Hold, ""));
            Assert.Contains("EXTRA", WorkspaceStructureEditor.UiModifierNames(saved));
            var commands = saved.Assignments.Select(x => x.Command).Distinct();
            var draft = new DraftAssignmentService(saved.Configuration, commands, saved.Assignments, WorkspaceStructureEditor.UiModifierNames(saved));
            var slot = new DraftAssignmentSlot(ModuleId, ProfileId, AssignmentLayer.UiLayer, BindingSection.Key, "JOY_BTN1", "EXTRA");
            Assert.Equal(DraftOutcome.Applied, draft.Assign(slot, new("VOICE", "Voice", false, AssignmentLayer.UiLayer), OccupiedDestinationBehavior.ReplaceInDraft).Outcome);
            saved = saved with { Assignments = draft.Snapshot };
            Assert.Contains("EXTRA", UiLayerLuaProfileRenderer.Render(saved, ProfileId));

            var device = WorkspaceStructureEditor.Describe(saved).Devices.First(x => x.Layer == AssignmentLayer.Module);
            saved = WorkspaceStructureEditor.UpdateDevice(saved, device, "Renamed stick.diff.lua", null, null);
            var lua = Encoding.UTF8.GetString(SnapshotLuaPayloadRenderer.Generate(saved)["Config/Input/TEST/modifiers.lua"]);
            Assert.All(DcsLuaParser.ParseModifiers(lua).Where(x => x.Device != "Keyboard"),
                x => Assert.Equal("Renamed stick", x.Device));
            Assert.Contains("Config/Input/TEST/joystick/Renamed stick.diff.lua", SnapshotLuaPayloadRenderer.Generate(saved).Keys);
        }
        /// <summary>Saved device membership, semantic modifiers, and the hardware catalog reopen without source checkouts.</summary>
        /// <returns>Completion of the SQLite round trip.</returns>
        [Fact]
        public async Task ReopensManagementAndHardwareFromSqlite()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-managed-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var saved = Workspace();
                var hardware = new WorkspaceHardware("other", "Other", "Other.diff.lua", saved.Diagrams[0]);
                saved = WorkspaceStructureEditor.AddDevice(saved, hardware, AssignmentLayer.Module, "Custom.diff.lua", "AUX", null);
                var added = WorkspaceStructureEditor.Describe(saved).Devices.Single(x => x.FileName == "Custom.diff.lua");
                saved = WorkspaceStructureEditor.SetModifier(saved, null,
                    new(AssignmentLayer.Module, added.ProfileId, "AUX_SHIFT", "JOY_BTN2", "Auxiliary", ModifierMode.Toggle, ""));
                var sources = new Dictionary<string, string>(saved.Sources!) { ["config/ipia-hardware.json"] = JsonSerializer.Serialize(new[] { hardware }) };
                saved = saved with { Sources = sources };
                var connection = "Data Source=" + path + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(saved);
                var restored = (await repository.GetAsync(ModuleId))!;
                Assert.Equal("Custom.diff.lua", WorkspaceStructureEditor.Describe(restored).Devices.Single(x => x.ProfileId == added.ProfileId).FileName);
                var modifier = WorkspaceStructureEditor.Describe(restored).Modifiers.Single(x => x.Name == "AUX_SHIFT");
                Assert.Equal("Auxiliary", modifier.SemanticLayer);
                Assert.Equal(ModifierMode.Toggle, modifier.Mode);
                Assert.Equal("Other.diff.lua", Assert.Single(WorkspaceStructureEditor.Hardware(restored)).DefaultFileName);
                Assert.Contains("Config/Input/TEST/joystick/Custom.diff.lua", SnapshotLuaPayloadRenderer.Generate(restored).Keys);
                // Repository operations must release their native handles before returning.
                File.Delete(path);
                Assert.False(File.Exists(path));
            }
            finally { File.Delete(path); }
        }
        /// <summary>Export rejects a saved assignment whose device membership was removed outside the editor.</summary>
        [Fact]
        public void RejectsMembershipDriftRatherThanDroppingAssignments()
        {
            var saved = Workspace();
            var module = WorkspaceStructureEditor.Describe(saved).Modifiers.Single(x => x.Layer == AssignmentLayer.Module);
            saved = WorkspaceStructureEditor.SetModifier(saved, module, module with { SemanticLayer = "Flight" });
            var state = WorkspaceStructureEditor.Describe(saved);
            var invalid = state with { Devices = state.Devices.Where(x => x.Layer != AssignmentLayer.UiLayer).ToArray() };
            var sources = new Dictionary<string, string>(saved.Sources!) { [WorkspaceManagement.SourcePath] = JsonSerializer.Serialize(invalid) };
            Assert.Throws<InvalidDataException>(() => SnapshotLuaPayloadRenderer.Generate(saved with { Sources = sources }));
        }
        /// <summary>Module MFD edits reach the combined kneeboard, including its bottom callout.</summary>
        [Fact]
        public void UpdatesMfdCategoryLabels()
        {
            var saved = Workspace("tm-mfd");
            var device = WorkspaceStructureEditor.Describe(saved).Devices.First(x => x.Layer == AssignmentLayer.Module);
            saved = WorkspaceStructureEditor.UpdateDevice(saved, device, device.FileName, null, null,
                new Dictionary<string, string> { ["left"] = "COMM1", ["right"] = "COMM2", ["top"] = "UFC", ["bottom"] = "RADAR" });
            var page = KneeboardDraftProjection.CreateCombined(saved, saved.Diagrams[0], saved.Assignments, saved.Labels);
            Assert.Equal("COMM1", page.CategoryLabels["mfd-category-left"]);
            Assert.Equal("COMM2", page.CategoryLabels["mfd-category-right"]);
            Assert.Equal("UFC", page.CategoryLabels["mfd-category-top"]);
            Assert.Equal("RADAR", page.CategoryLabels["mfd-category-bottom"]);
            Assert.Contains("meta-mfd-category-bottom", saved.Diagrams[0].Svg);
            Assert.DoesNotContain("meta-mfd-category-middle", saved.Diagrams[0].Svg);
            var svg = XDocument.Parse(CompositeKneeboardRenderer.BuildDeviceSvg(saved.Diagrams[0], page));
            var bottom = svg.Descendants().Single(x => x.Attribute("id")?.Value == "meta-mfd-category-bottom");
            Assert.Equal("RADAR", bottom.Value);
            Assert.Equal("80", bottom.Attribute("y")?.Value);
        }
        /// <summary>Old MFD center metadata reopens as Bottom, retaining genuine Bottom labels and the authored footer.</summary>
        /// <param name="authoredBottom">Whether a genuine Bottom label already exists.</param>
        /// <param name="prefixed">Whether legacy category keys contain the MFD prefix.</param>
        /// <returns>Completion of the persisted upgrade round trip.</returns>
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task RepairsLegacyMfdCategoryToBottom(bool authoredBottom, bool prefixed)
        {
            var saved = Workspace("tm-mfd");
            var originalProfile = saved.Configuration.Profiles[0];
            var root = JsonNode.Parse(saved.Sources!["config/kneeboard.json"])!;
            var page = root["pages"]![0]!;
            var prefix = prefixed ? "mfd-category-" : "";
            page["categoryLabels"] = new JsonObject { [prefix + "middle"] = "LEGACY PAGE", ["top"] = "TOP" };
            if (authoredBottom) page["categoryLabels"]![prefix + "bottom"] = "AUTHORED BOTTOM";
            page["layers"] = new JsonArray(new JsonObject { ["categoryLabels"] = new JsonObject { [prefix + "middle"] = "LEGACY LAYER" } });
            var sources = new Dictionary<string, string>(saved.Sources) { ["config/kneeboard.json"] = root.ToJsonString() };
            var graphic = XDocument.Parse(saved.Diagrams[0].Svg);
            graphic.Root!.Add(new XElement(graphic.Root.Name.Namespace + "text", new XAttribute("id", "meta-mfd-category-middle"), new XAttribute("x", "50"), new XAttribute("y", "50")));
            graphic.Root.Add(new XElement(graphic.Root.Name.Namespace + "text", new XAttribute("id", "footer"), "KEEP FOOTER"));
            saved = saved with { Sources = sources, Diagrams = [saved.Diagrams[0] with { Svg = graphic.ToString() }] };
            var path = Path.Combine(Path.GetTempPath(), "ipia-bottom-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(saved);
                var restored = (await repository.GetAsync(ModuleId))!;
                var combined = KneeboardDraftProjection.CreateCombined(restored, restored.Diagrams[0], restored.Assignments, restored.Labels);
                Assert.Equal(authoredBottom ? "AUTHORED BOTTOM" : "LEGACY LAYER", combined.CategoryLabels["mfd-category-bottom"]);
                Assert.Equal("TOP", combined.CategoryLabels["mfd-category-top"]);
                Assert.DoesNotContain("mfd-category-middle", combined.CategoryLabels.Keys);
                Assert.DoesNotContain("middle", restored.Sources!["config/kneeboard.json"]);
                Assert.DoesNotContain("meta-mfd-category-middle", restored.Diagrams[0].Svg);
                var svg = XDocument.Parse(restored.Diagrams[0].Svg);
                Assert.Equal("KEEP FOOTER", svg.Descendants().Single(x => x.Attribute("id")?.Value == "footer").Value);
                Assert.Equal("80", svg.Descendants().Single(x => x.Attribute("id")?.Value == "meta-mfd-category-bottom").Attribute("y")?.Value);
                Assert.Equal(originalProfile, restored.Configuration.Profiles[0]);
                Assert.Equal(saved.Assignments.Select(x => x.Slot.Key), restored.Assignments.Select(x => x.Slot.Key));
                foreach (var source in saved.Sources.Where(x => x.Key != "config/kneeboard.json"))
                    Assert.Equal(source.Value, restored.Sources[source.Key]);
                var reopened = (await repository.GetAsync(ModuleId))!;
                Assert.Equal(restored.Sources["config/kneeboard.json"], reopened.Sources!["config/kneeboard.json"]);
                Assert.Equal(restored.Diagrams[0].Svg, reopened.Diagrams[0].Svg);
            }
            finally { File.Delete(path); }
        }
        /// <summary>An unrelated hardware template cannot block an MFD role edit or become an empty UI profile.</summary>
        /// <returns>Completion of the saved role round trip.</returns>
        [Fact]
        public async Task SavesMfdRoleWithUnboundUiHardwareTemplate()
        {
            var saved = Workspace("tm-mfd");
            var templateId = Guid.NewGuid();
            var configuration = saved.Configuration;
            saved = saved with {
                Configuration = new ModuleConfiguration(configuration.Module,
                    [.. configuration.Profiles, new DeviceProfile(templateId, "grip-f18c", null, "Stick", "Hornet grip.diff.lua")],
                    configuration.Modifiers, configuration.Bindings, configuration.Provenance),
                Diagrams = [.. saved.Diagrams, saved.Diagrams[0] with {
                    ProfileId = templateId, SupportsUiLayer = true, CanonicalUiLayerSourcePath = null, Callouts = [] }]
            };
            var importedPayload = SnapshotLuaPayloadRenderer.Generate(saved);
            var device = Assert.Single(WorkspaceStructureEditor.Describe(saved).Devices,
                x => x.ProfileId == ProfileId && x.Layer == AssignmentLayer.Module);
            saved = WorkspaceStructureEditor.UpdateDevice(saved, device, device.FileName, "MFD3", "AMPCD",
                new Dictionary<string, string> { ["right"] = "AMPCD", ["top"] = "UI Layer" });
            var path = Path.Combine(Path.GetTempPath(), "ipia-role-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(saved);
                var restored = (await repository.GetAsync(ModuleId))!;
                var state = WorkspaceStructureEditor.Describe(restored);
                var updated = Assert.Single(state.Devices, x => x.ProfileId == ProfileId && x.Layer == AssignmentLayer.Module);
                Assert.Equal("AMPCD", updated.Role);
                Assert.Equal("MFD3", updated.Instance);
                Assert.Equal(device.FileName, updated.FileName);
                Assert.Equal("AMPCD", restored.Configuration.Profiles.Single(x => x.Id == ProfileId).Role);
                Assert.DoesNotContain(state.Devices, x => x.ProfileId == templateId && x.Layer == AssignmentLayer.UiLayer);
                Assert.True(restored.Diagrams.Single(x => x.ProfileId == templateId).SupportsUiLayer);
                var page = KneeboardDraftProjection.CreateCombined(restored, restored.Diagrams[0], restored.Assignments, restored.Labels);
                Assert.Equal("AMPCD", page.CategoryLabels["mfd-category-right"]);
                var payload = SnapshotLuaPayloadRenderer.Generate(restored);
                Assert.Equal(importedPayload.Keys.Order(), payload.Keys.Order());
                foreach (var entry in importedPayload)
                    Assert.Equal(entry.Value, payload[entry.Key]);
                Assert.Contains(DcsLuaParser.ParseProfile(UiLayerLuaProfileRenderer.Render(restored, ProfileId), ProfileId),
                    x => x.CommandId == "UNMAPPED");
            }
            finally { File.Delete(path); }
        }
        /// <summary>Available physical sources recover missing links; genuinely missing sources reject actual UI usage.</summary>
        /// <param name="keepAssignments">Whether saved assignments or only callouts demonstrate UI Layer usage.</param>
        /// <param name="sourceAvailable">Whether the canonical source text is still pinned in the workspace.</param>
        [Theory]
        [InlineData(true, true)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public void RecoversLinksButRejectsMissingSourcesForActualUiUsage(bool keepAssignments, bool sourceAvailable)
        {
            var saved = Workspace("tm-mfd");
            var sourcePath = saved.Diagrams[0].CanonicalUiLayerSourcePath;
            saved = saved with {
                Sources = sourceAvailable ? saved.Sources : saved.Sources!.Where(x => x.Key != sourcePath)
                    .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
                Diagrams = [saved.Diagrams[0] with { CanonicalUiLayerSourcePath = null,
                    Callouts = keepAssignments ? [] : saved.Diagrams[0].Callouts }],
                Assignments = keepAssignments ? saved.Assignments : saved.Assignments.Where(x => x.Slot.Layer == AssignmentLayer.Module).ToArray()
            };
            var device = WorkspaceStructureEditor.Describe(saved).Devices.First(x => x.Layer == AssignmentLayer.Module);
            if (sourceAvailable)
            {
                var recovered = WorkspaceStructureEditor.UpdateDevice(saved, device, device.FileName, "MFD3", "AMPCD");
                Assert.NotEmpty(SnapshotLuaPayloadRenderer.Generate(recovered));
            }
            else
                Assert.Throws<InvalidDataException>(() => WorkspaceStructureEditor.UpdateDevice(saved, device, device.FileName, "MFD3", "AMPCD"));
        }
        /// <summary>Any module device can save a page heading independently of hardware, role, and other pages.</summary>
        /// <param name="deviceId">MFD or ordinary shared hardware.</param>
        /// <returns>Completion of the saved title round trip.</returns>
        [Theory]
        [InlineData("stick")]
        [InlineData("tm-mfd")]
        public async Task SavesKneeboardTitleForAnyModuleDevice(string deviceId)
        {
            var original = Workspace(deviceId);
            var hardware = new WorkspaceHardware("other", "Other hardware", "Other.diff.lua", original.Diagrams[0]);
            var saved = WorkspaceStructureEditor.AddDevice(original, hardware, AssignmentLayer.Module, "Other.diff.lua", "AUX", "Other role");
            var payloadBefore = SnapshotLuaPayloadRenderer.Generate(saved);
            var pngBefore = SnapshotDeviceKneeboardRenderer.Generate(saved);
            var device = WorkspaceStructureEditor.Describe(saved).Devices.First(x => x.ProfileId == ProfileId && x.Layer == AssignmentLayer.Module);
            var originalProfile = saved.Configuration.Profiles.Single(x => x.Id == ProfileId);
            saved = WorkspaceStructureEditor.UpdateDevice(saved, device, device.FileName, device.Instance, device.Role,
                kneeboardTitle: "  F/A-18 AMPCD & Controls  ");
            var path = Path.Combine(Path.GetTempPath(), "ipia-title-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(saved);
                var restored = (await repository.GetAsync(ModuleId))!;
                var combined = KneeboardDraftProjection.CreateCombined(restored, restored.Diagrams[0], restored.Assignments, restored.Labels);
                Assert.Equal("F/A-18 AMPCD & Controls", combined.Title);
                Assert.Equal("Controls", combined.Kicker);
                Assert.Equal("F/A-18 AMPCD & Controls", restored.Pages.Single(x => x.StableKey == "01-STICK").Title);
                Assert.Equal("other", restored.Pages.Single(x => x.StableKey != "01-STICK").Title);
                Assert.Equal(originalProfile, restored.Configuration.Profiles.Single(x => x.Id == ProfileId));
                Assert.Equal(original.Diagrams[0].Title, restored.Diagrams[0].Title);
                Assert.Equal("Stick", KneeboardDraftProjection.CreateCombined(original, original.Diagrams[0], original.Assignments, original.Labels).Title);
                Assert.Contains("F/A-18 AMPCD &amp; Controls", CompositeKneeboardRenderer.BuildPageSvg(restored.Diagrams[0], combined, ""));
                var pngAfter = SnapshotDeviceKneeboardRenderer.Generate(restored);
                Assert.NotEqual(pngBefore["KNEEBOARD/TEST/01-STICK.png"], pngAfter["KNEEBOARD/TEST/01-STICK.png"]);
                Assert.Equal(pngBefore.Keys.Order(), pngAfter.Keys.Order());
                var payloadAfter = SnapshotLuaPayloadRenderer.Generate(restored);
                foreach (var entry in payloadBefore)
                    Assert.Equal(entry.Value, payloadAfter[entry.Key]);
            }
            finally { File.Delete(path); }
        }
        /// <summary>New module devices accept an authored title while UI Layer edits preserve module headings.</summary>
        [Fact]
        public void AddsKneeboardTitleAndPreservesItDuringUiEdits()
        {
            var saved = Workspace();
            var hardware = new WorkspaceHardware("other", "Other", "Other.diff.lua", saved.Diagrams[0]);
            saved = WorkspaceStructureEditor.AddDevice(saved, hardware, AssignmentLayer.Module, "Other.diff.lua", "AUX", null,
                kneeboardTitle: "Auxiliary controls");
            Assert.Equal("Auxiliary controls", saved.Pages.Single(x => x.StableKey != "01-STICK").Title);
            var ui = WorkspaceStructureEditor.Describe(saved).Devices.Single(x => x.ProfileId == ProfileId && x.Layer == AssignmentLayer.UiLayer);
            saved = WorkspaceStructureEditor.UpdateDevice(saved, ui, ui.FileName, ui.Instance, ui.Role, kneeboardTitle: "UI title");
            Assert.Equal("Stick", KneeboardDraftProjection.CreateCombined(saved, saved.Diagrams[0], saved.Assignments, saved.Labels).Title);
        }
        /// <summary>Blank or multiline page headings fail validation without changing the original snapshot.</summary>
        /// <param name="title">An invalid title.</param>
        [Theory]
        [InlineData(" ")]
        [InlineData("Title\nLine")]
        [InlineData("Title\0")]
        public void RejectsInvalidKneeboardTitles(string title)
        {
            var saved = Workspace();
            var device = WorkspaceStructureEditor.Describe(saved).Devices.First(x => x.Layer == AssignmentLayer.Module);
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.UpdateDevice(saved, device, device.FileName,
                device.Instance, device.Role, kneeboardTitle: title));
            Assert.Equal("Stick", KneeboardDraftProjection.CreateCombined(saved, saved.Diagrams[0], saved.Assignments, saved.Labels).Title);
        }
        /// <summary>Any device subtitle persists independently of its title, identity, bindings, and other pages.</summary>
        /// <param name="deviceId">An MFD or ordinary shared device.</param>
        /// <returns>Completion of the SQLite round trip.</returns>
        [Theory]
        [InlineData("stick")]
        [InlineData("tm-mfd")]
        public async Task SavesAndClearsKneeboardSubtitle(string deviceId)
        {
            var original = Workspace(deviceId);
            var hardware = new WorkspaceHardware("other", "Other", "Other.diff.lua", original.Diagrams[0]);
            var saved = WorkspaceStructureEditor.AddDevice(original, hardware, AssignmentLayer.Module, "Other.diff.lua", "AUX", null,
                kneeboardSubtitle: "Auxiliary panel");
            var before = SnapshotDeviceKneeboardRenderer.Generate(saved)["KNEEBOARD/TEST/01-STICK.png"];
            var luaBefore = SnapshotLuaPayloadRenderer.Generate(saved);
            var device = WorkspaceStructureEditor.Describe(saved).Devices.First(x => x.ProfileId == ProfileId && x.Layer == AssignmentLayer.Module);
            saved = WorkspaceStructureEditor.UpdateDevice(saved, device, device.FileName, device.Instance, device.Role,
                kneeboardSubtitle: "  Center display & controls  ");
            var path = Path.Combine(Path.GetTempPath(), "ipia-subtitle-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(saved);
                var restored = (await repository.GetAsync(ModuleId))!;
                var page = KneeboardDraftProjection.CreateCombined(restored, restored.Diagrams[0], restored.Assignments, restored.Labels);
                Assert.Equal("Center display & controls", page.Kicker);
                Assert.Equal("Stick", page.Title);
                Assert.Equal("Auxiliary panel", KneeboardDraftProjection.CreateCombined(restored, restored.Diagrams[1], restored.Assignments, restored.Labels).Kicker);
                Assert.Equal(original.Configuration.Profiles[0], restored.Configuration.Profiles.Single(x => x.Id == ProfileId));
                Assert.Contains("Center display &amp; controls", CompositeKneeboardRenderer.BuildPageSvg(restored.Diagrams[0], page, ""));
                Assert.NotEqual(before, SnapshotDeviceKneeboardRenderer.Generate(restored)["KNEEBOARD/TEST/01-STICK.png"]);
                foreach (var entry in luaBefore)
                    Assert.Equal(entry.Value, SnapshotLuaPayloadRenderer.Generate(restored)[entry.Key]);
                var ui = WorkspaceStructureEditor.Describe(restored).Devices.Single(x => x.ProfileId == ProfileId && x.Layer == AssignmentLayer.UiLayer);
                restored = WorkspaceStructureEditor.UpdateDevice(restored, ui, ui.FileName, ui.Instance, ui.Role, kneeboardSubtitle: "UI change");
                Assert.Equal(page.Kicker, KneeboardDraftProjection.CreateCombined(restored, restored.Diagrams[0], restored.Assignments, restored.Labels).Kicker);
                device = WorkspaceStructureEditor.Describe(restored).Devices.First(x => x.ProfileId == ProfileId && x.Layer == AssignmentLayer.Module);
                restored = WorkspaceStructureEditor.UpdateDevice(restored, device, device.FileName, device.Instance, device.Role, kneeboardSubtitle: "");
                await repository.SaveAsync(restored);
                restored = (await repository.GetAsync(ModuleId))!;
                Assert.Equal("", KneeboardDraftProjection.CreateCombined(restored, restored.Diagrams[0], restored.Assignments, restored.Labels).Kicker);
                Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.UpdateDevice(restored, device, device.FileName,
                    device.Instance, device.Role, kneeboardSubtitle: "Invalid\nsubtitle"));
            }
            finally { File.Delete(path); }
        }
        /// <summary>Adding an existing physical device to the other scope reuses its profile and permits a shared filename.</summary>
        [Fact]
        public void ReusesPhysicalProfilesAcrossScopes()
        {
            var saved = Workspace();
            saved = WorkspaceStructureEditor.ClearDeviceAssignments(saved, WorkspaceStructureEditor.Describe(saved).Devices[0]);
            foreach (var modifier in WorkspaceStructureEditor.Describe(saved).Modifiers.ToArray())
                saved = WorkspaceStructureEditor.RemoveModifier(saved, modifier);
            var ui = WorkspaceStructureEditor.Describe(saved).Devices.Single(x => x.Layer == AssignmentLayer.UiLayer);
            saved = WorkspaceStructureEditor.RemoveDevice(saved, ui);
            var hardware = WorkspaceStructureEditor.Hardware(saved)[0];
            saved = WorkspaceStructureEditor.AddDevice(saved, hardware, AssignmentLayer.UiLayer, "Stick.diff.lua", null, null);
            Assert.Single(saved.Configuration.Profiles);
            Assert.True(saved.Diagrams[0].SupportsUiLayer);
            Assert.Equal(2, WorkspaceStructureEditor.Describe(saved).Devices.Count);
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.AddDevice(saved, hardware, AssignmentLayer.UiLayer, "Duplicate.diff.lua", null, null));
            saved = WorkspaceStructureEditor.RemoveDevice(saved, WorkspaceStructureEditor.Describe(saved).Devices.Single(x => x.Layer == AssignmentLayer.Module));
            Assert.Single(saved.Configuration.Profiles);
            saved = WorkspaceStructureEditor.AddDevice(saved, hardware, AssignmentLayer.Module, "MODULE.DIFF.LUA", "  ", "  ");
            Assert.Equal("MODULE.DIFF.LUA", saved.Configuration.Profiles[0].SourceName);
            Assert.Contains("Config/Input/TEST/joystick/MODULE.DIFF.LUA", SnapshotLuaPayloadRenderer.Generate(saved).Keys);
        }
        /// <summary>Unused hardware replacement regenerates its device page rather than retaining old graphic metadata.</summary>
        [Fact]
        public void ReplacesUnusedHardwareAndResetsAuthoredPage()
        {
            var saved = Workspace();
            saved = WorkspaceStructureEditor.ClearDeviceAssignments(saved, WorkspaceStructureEditor.Describe(saved).Devices[0]);
            foreach (var modifier in WorkspaceStructureEditor.Describe(saved).Modifiers.ToArray())
                saved = WorkspaceStructureEditor.RemoveModifier(saved, modifier);
            var device = WorkspaceStructureEditor.Describe(saved).Devices.First(x => x.Layer == AssignmentLayer.Module);
            var hardware = new WorkspaceHardware("replacement", "Replacement", "Replacement.diff.lua",
                saved.Diagrams[0] with { Device = "Replacement", Controls = [new("new-button", "New button", "JOY_BTN4", 0, 0, 10, 10, false)] });
            saved = WorkspaceStructureEditor.UpdateDevice(saved, device, "Replacement.diff.lua", "SECOND", "Aux", hardware: hardware);
            Assert.Equal("replacement", saved.Configuration.Profiles[0].DeviceId);
            Assert.Equal("new-button", Assert.Single(saved.Diagrams[0].Controls).Control);
            Assert.DoesNotContain(saved.Pages, x => x.StableKey == "01-STICK");
            Assert.Equal("Replacement · SECOND", KneeboardDraftProjection.CreateCombined(saved, saved.Diagrams[0], saved.Assignments, saved.Labels).Title);
            Assert.Contains("Config/Input/TEST/joystick/Replacement.diff.lua", SnapshotLuaPayloadRenderer.Generate(saved).Keys);
        }
        /// <summary>Device identity edits cannot merge separate instances or collide across scope filenames.</summary>
        [Fact]
        public void RejectsDuplicateInstancesAndCrossScopeFilenames()
        {
            var saved = Workspace();
            var hardware = WorkspaceStructureEditor.Hardware(saved)[0];
            saved = WorkspaceStructureEditor.AddDevice(saved, hardware, AssignmentLayer.Module, "Aux.diff.lua", "SECOND", "Aux");
            var added = WorkspaceStructureEditor.Describe(saved).Devices.Single(x => x.FileName == "Aux.diff.lua");
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.UpdateDevice(saved, added, "Aux.diff.lua", null, null));
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.AddDevice(saved, hardware, AssignmentLayer.UiLayer, "Stick.diff.lua", "SECOND", null));
            saved = WorkspaceStructureEditor.UpdateDevice(saved, added, "Aux.diff.lua", " SECOND ", " Aux ");
            Assert.Equal("SECOND", WorkspaceStructureEditor.Describe(saved).Devices.Single(x => x.FileName == "Aux.diff.lua").Instance);
        }
        /// <summary>Invalid native filenames are rejected before save or export.</summary>
        /// <param name="file">Unsafe or incomplete native filename.</param>
        [Theory]
        [InlineData("")]
        [InlineData("Stick.lua")]
        [InlineData("a\\b.diff.lua")]
        [InlineData("a:b.diff.lua")]
        [InlineData("a..b.diff.lua")]
        [InlineData("a\tb.diff.lua")]
        [InlineData("a?b.diff.lua")]
        public void RejectsUnsafeNativeFilenames(string file)
        {
            var saved = Workspace();
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.AddDevice(saved, WorkspaceStructureEditor.Hardware(saved)[0], AssignmentLayer.Module, file, "SECOND", null));
        }
        /// <summary>Modifier validation rejects missing devices, malformed names, duplicate controls, and scope moves.</summary>
        [Fact]
        public void RejectsInvalidModifierDefinitionsAndScopeMoves()
        {
            var saved = Workspace();
            var candidate = new WorkspaceModifier(AssignmentLayer.Module, ProfileId, "NEW", "JOY_BTN1", "New", ModifierMode.Hold, "");
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.SetModifier(saved, null, candidate with { ProfileId = Guid.NewGuid() }));
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.SetModifier(saved, null, candidate with { Name = " " }));
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.SetModifier(saved, null, candidate with { Name = "A\tB" }));
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.SetModifier(saved, null, candidate with { Mode = (ModifierMode)99 }));
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.SetModifier(saved, null, candidate with { Key = "JOY_BTN2" }));
            var existing = WorkspaceStructureEditor.Describe(saved).Modifiers.First(x => x.Layer == AssignmentLayer.Module);
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.SetModifier(saved, existing, candidate with { Layer = AssignmentLayer.UiLayer }));
            saved = WorkspaceStructureEditor.SetModifier(saved, null, candidate with { Name = " NEW ", SemanticLayer = " New " });
            Assert.Equal("New", WorkspaceStructureEditor.Describe(saved).Modifiers.Single(x => x.Name == "NEW").SemanticLayer);
            var ui = WorkspaceStructureEditor.Describe(saved).Modifiers.Single(x => x.Layer == AssignmentLayer.UiLayer);
            saved = WorkspaceStructureEditor.SetModifier(saved, ui, ui with { Key = "JOY_BTN1" });
            Assert.Equal("JOY_BTN1", WorkspaceStructureEditor.Describe(saved).Modifiers.Single(x => x.Layer == AssignmentLayer.UiLayer).Key);
        }
        /// <summary>Managed exports reject duplicate memberships, unknown profiles, missing sources, and duplicate output paths.</summary>
        [Fact]
        public void RejectsCorruptManagedExports()
        {
            var saved = Workspace();
            var modifier = WorkspaceStructureEditor.Describe(saved).Modifiers[0];
            saved = WorkspaceStructureEditor.SetModifier(saved, modifier, modifier with { SemanticLayer = "Flight" });
            var state = WorkspaceStructureEditor.Describe(saved);
            EditorWorkspaceSnapshot WithState(WorkspaceManagement invalid) => saved with {
                Sources = new Dictionary<string, string>(saved.Sources!) { [WorkspaceManagement.SourcePath] = JsonSerializer.Serialize(invalid) }
            };
            Assert.Throws<InvalidDataException>(() => SnapshotLuaPayloadRenderer.Generate(WithState(state with { Devices = [.. state.Devices, state.Devices[0]] })));
            Assert.Throws<InvalidDataException>(() => SnapshotLuaPayloadRenderer.Generate(WithState(state with { Devices = [state.Devices[0] with { ProfileId = Guid.NewGuid() }] })));
            Assert.Throws<InvalidDataException>(() => SnapshotLuaPayloadRenderer.Generate(WithState(state with { Devices = [state.Devices[0] with { Layer = (AssignmentLayer)99 }] })));
            Assert.Throws<InvalidDataException>(() => UiLayerLuaProfileRenderer.Render(WithState(state with { Devices = [] }), ProfileId));
            var sources = new Dictionary<string, string>(saved.Sources!);
            sources.Remove("config/ipia-ui-" + ProfileId.ToString("N") + ".lua");
            Assert.Throws<InvalidDataException>(() => UiLayerLuaProfileRenderer.Render(saved with { Sources = sources }, ProfileId));
            var duplicate = new DeviceProfile(Guid.NewGuid(), "other", null, "Other", "Other.diff.lua");
            var configuration = new ModuleConfiguration(saved.Configuration.Module, [.. saved.Configuration.Profiles, duplicate],
                saved.Configuration.Modifiers, saved.Configuration.Bindings, saved.Configuration.Provenance);
            var badPaths = WithState(state with { Devices = [.. state.Devices, new(duplicate.Id, AssignmentLayer.Module, "Stick.diff.lua", null, null)] });
            Assert.Throws<InvalidDataException>(() => SnapshotLuaPayloadRenderer.Generate(badPaths with { Configuration = configuration }));
        }
        /// <summary>Module MFD edits take precedence over earlier per-layer authored category labels and permit clearing them.</summary>
        [Fact]
        public void MfdLabelEditsOverrideLayerMetadataAndCanBeCleared()
        {
            var saved = Workspace("tm-mfd");
            var sources = new Dictionary<string, string>(saved.Sources!);
            sources["config/kneeboard.json"] = sources["config/kneeboard.json"].Replace("\"categoryLabels\":{}", "\"categoryLabels\":{},\"layers\":[{\"categoryLabels\":{\"top\":\"OLD\"}},{\"labels\":{}}]", StringComparison.Ordinal);
            saved = saved with { Sources = sources };
            var device = WorkspaceStructureEditor.Describe(saved).Devices.First(x => x.Layer == AssignmentLayer.Module);
            saved = WorkspaceStructureEditor.UpdateDevice(saved, device, device.FileName, null, null, new Dictionary<string, string> { ["top"] = "NEW", ["bottom"] = "MID" });
            saved = WorkspaceStructureEditor.UpdateDevice(saved, device, device.FileName, null, null, new Dictionary<string, string> { ["top"] = "NEWER", ["bottom"] = "MID" });
            Assert.Equal("NEWER", KneeboardDraftProjection.CreateCombined(saved, saved.Diagrams[0], saved.Assignments, saved.Labels).CategoryLabels["mfd-category-top"]);
            saved = WorkspaceStructureEditor.UpdateDevice(saved, device, device.FileName, null, null, new Dictionary<string, string>());
            Assert.Equal("", KneeboardDraftProjection.CreateCombined(saved, saved.Diagrams[0], saved.Assignments, saved.Labels).CategoryLabels["mfd-category-bottom"]);
        }
        /// <summary>Default keyboard modifiers remain protected in both scopes even after every assignment is cleared.</summary>
        /// <param name="key">Built-in keyboard modifier key.</param>
        [Theory]
        [InlineData("LAlt")]
        [InlineData("LCtrl")]
        [InlineData("LShift")]
        [InlineData("LWin")]
        [InlineData("RAlt")]
        [InlineData("RCtrl")]
        [InlineData("RShift")]
        [InlineData("RWin")]
        public void ProtectsDefaultKeyboardModifiersEvenWhenUnused(string key)
        {
            var saved = Workspace();
            saved = saved with {
                Configuration = new ModuleConfiguration(saved.Configuration.Module, saved.Configuration.Profiles,
                    [.. saved.Configuration.Modifiers, new(key, "Keyboard", key, ModifierMode.Hold)],
                    saved.Configuration.Bindings, saved.Configuration.Provenance)
            };
            var sources = new Dictionary<string, string>(saved.Sources!);
            sources["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] =
                "local modifiers = { [\"UI_SHIFT\"] = { [\"device\"] = \"Stick\", [\"key\"] = \"JOY_BTN2\", [\"switch\"] = false }, " +
                "[\"" + key + "\"] = { [\"device\"] = \"Keyboard\", [\"key\"] = \"" + key + "\", [\"switch\"] = false } } return modifiers";
            saved = saved with { Sources = sources };
            Assert.Equal(2, WorkspaceStructureEditor.Describe(saved).Modifiers.Count(x => x.Name == key));
            saved = WorkspaceStructureEditor.ClearDeviceAssignments(saved, WorkspaceStructureEditor.Describe(saved).Devices[0]);
            Assert.Empty(saved.Assignments);
            foreach (var modifier in WorkspaceStructureEditor.Describe(saved).Modifiers.Where(x => x.Name == key))
            {
                Assert.True(WorkspaceStructureEditor.IsDefaultKeyboardModifier(modifier));
                var error = Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.RemoveModifier(saved, modifier));
                Assert.Contains("Default keyboard modifiers", error.Message);
                Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.SetModifier(saved, modifier,
                    modifier with { ProfileId = ProfileId, Key = "JOY_BTN1", Name = "MOVED" }));
            }
            var payload = SnapshotLuaPayloadRenderer.Generate(saved);
            Assert.Contains("\"" + key + "\"", Encoding.UTF8.GetString(payload["Config/Input/TEST/modifiers.lua"]));
            Assert.Contains("\"" + key + "\"", Encoding.UTF8.GetString(payload["Config/Input/UiLayer/modifiers.lua"]));
            Assert.False(WorkspaceStructureEditor.IsDefaultKeyboardModifier(new(AssignmentLayer.Module, ProfileId, key, key, key, ModifierMode.Hold, "Stick")));
            Assert.False(WorkspaceStructureEditor.IsDefaultKeyboardModifier(new(AssignmentLayer.Module, Guid.Empty, "CUSTOM", "K", "Custom", ModifierMode.Hold, "Keyboard")));
        }
        /// <summary>Rebinding preserves authored data while changing exact native filenames and modifiers in both scopes.</summary>
        /// <returns>Completion of real SQLite preservation checks.</returns>
        [Fact]
        public async Task RebindsBothScopesWithoutLosingProfileData()
        {
            var original = Workspace();
            original = original with { Configuration = new(original.Configuration.Module, original.Configuration.Profiles,
                original.Configuration.Modifiers, original.Configuration.Bindings.Concat([
                    new DcsBinding(ProfileId, BindingSection.Axis, "AXIS", "Pitch", BindingDisposition.Added, "JOY_X", filter: new AxisFilter(Deadzone: 4, Curvature: [12], Invert: true)),
                    new DcsBinding(ProfileId, BindingSection.Key, "OLD", "Removed action", BindingDisposition.Removed, "JOY_BTN3")
                ]), original.Configuration.Provenance),
                Assignments = [.. original.Assignments,
                    new DraftAssignment(new(ModuleId, ProfileId, AssignmentLayer.Module, BindingSection.Axis, "JOY_X", ""),
                        new("AXIS", "Pitch", true, AssignmentLayer.Module)) { Filter = new AxisFilter(Deadzone: 4, Curvature: [12], Invert: true) }]
            };
            var device = WorkspaceStructureEditor.Describe(original).Devices.First(x => x.Layer == AssignmentLayer.Module);
            var file = new DetectedDcsDevice(Guid.Parse("11223344-1234-11F1-8001-444553540000"), "Actual Stick", false).FileName;
            var rebound = WorkspaceStructureEditor.RebindDevice(original, device, file, "Pilot", "Grip");
            Assert.Equal(original.Assignments, rebound.Assignments);
            Assert.Equal(original.Labels, rebound.Labels);
            Assert.Equal(JsonSerializer.Serialize(original.Configuration.Bindings.OrderBy(x => x.CommandId)), JsonSerializer.Serialize(rebound.Configuration.Bindings.OrderBy(x => x.CommandId)));
            Assert.Equal(original.Pages.Select(x => x.Id), rebound.Pages.Select(x => x.Id));
            Assert.Equal(original.Sources![original.Diagrams[0].CanonicalUiLayerSourcePath!], rebound.Sources![original.Diagrams[0].CanonicalUiLayerSourcePath!]);
            Assert.Equal(UiLayerLuaProfileRenderer.Render(original, ProfileId), UiLayerLuaProfileRenderer.Render(rebound, ProfileId));
            Assert.Equal(ModuleLuaProfileRenderer.Render(original, ProfileId), ModuleLuaProfileRenderer.Render(rebound, ProfileId));
            Assert.All(WorkspaceStructureEditor.Describe(rebound).Devices, x => Assert.Equal(file, x.FileName));
            Assert.All(WorkspaceStructureEditor.Describe(rebound).Modifiers, x => Assert.Equal(file[..^9], x.NativeDevice));
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.RebindDevice(rebound, device, file, "Pilot", null));
            Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.RebindDevice(original, device, "Generic.diff.lua", null, null));
            var path = Path.Combine(Path.GetTempPath(), "ipia-rebind-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(rebound);
                var restored = (await repository.GetAsync(ModuleId))!;
                var output = SnapshotLuaPayloadRenderer.Generate(restored);
                Assert.Contains("Config/Input/TEST/joystick/" + file, output.Keys);
                Assert.Contains("Config/Input/UiLayer/joystick/" + file, output.Keys);
                foreach (var scope in new[] { "TEST", "UiLayer" })
                    Assert.Contains(file[..^9], Encoding.UTF8.GetString(output["Config/Input/" + scope + "/modifiers.lua"]));
                Assert.Equal("Stick.diff.lua", original.Configuration.Profiles[0].SourceName);
                var updated = WorkspaceStructureEditor.Describe(restored).Devices.First(x => x.Layer == AssignmentLayer.Module);
                Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.UpdateDevice(restored, updated,
                    new DetectedDcsDevice(Guid.NewGuid(), "Other", true).FileName, null, null));
                var other = new WorkspaceHardware("other", "Other", "Other.diff.lua", restored.Diagrams[0]);
                var withOther = WorkspaceStructureEditor.AddDevice(restored, other, AssignmentLayer.Module,
                    new DetectedDcsDevice(Guid.NewGuid(), "Other", true).FileName, "Second", null);
                var otherDevice = WorkspaceStructureEditor.Describe(withOther).Devices[^1];
                Assert.Throws<InvalidOperationException>(() => WorkspaceStructureEditor.RebindDevice(withOther, otherDevice, file, null, null));
            }
            finally { File.Delete(path); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static EditorWorkspaceSnapshot Workspace(string deviceId = "stick")
        {
            var profile = new DeviceProfile(ProfileId, deviceId, null, "Stick", "Stick.diff.lua");
            var config = new ModuleConfiguration(new DcsModule(ModuleId, "TEST", "Test"), [profile],
                [new DcsModifier("SHIFT", "Stick", "JOY_BTN2", ModifierMode.Hold)],
                [new DcsBinding(ProfileId, BindingSection.Key, "FLIGHT", "Flight", BindingDisposition.Added, "JOY_BTN1", ["SHIFT"])], "fixture");
            var assignments = new[] {
                new DraftAssignment(new(ModuleId, ProfileId, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1", "SHIFT"), new("FLIGHT", "Flight", false, AssignmentLayer.Module)),
                new DraftAssignment(new(ModuleId, ProfileId, AssignmentLayer.UiLayer, BindingSection.Key, "JOY_BTN1", "UI_SHIFT"), new("VOICE", "Voice", false, AssignmentLayer.UiLayer))
            };
            var diagram = new UiLayerDiagram("Stick", null, "stick.svg", 100, 100, [],
                [new(1, "one", "Voice", "UI_SHIFT", 0, 0, 10, 10) { CommandId = "VOICE" }]) {
                ProfileId = ProfileId, CanonicalUiLayerSourcePath = "assets/shared/ui-layer/input/UiLayer/joystick/Stick.diff.lua",
                Svg = """<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" viewBox="0 0 100 100"><text id="meta-mfd-category-top" x="50" y="20"/><text id="meta-mfd-category-bottom" x="50" y="80"/></svg>""",
                Controls = [new("one", "Button 1", "JOY_BTN1", 0, 0, 10, 10, false), new("two", "Button 2", "JOY_BTN2", 10, 0, 10, 10, false), new("axis", "Axis", "JOY_X", 20, 0, 10, 10, true)]
            };
            var sources = new Dictionary<string, string> {
                ["config/kneeboard.json"] = """{"includeUiLayer":true,"profiles":{"stick":"src/Config/Input/TEST/joystick/Stick.diff.lua"},"pages":[{"file":"01-STICK","profile":"stick","title":"Stick","kicker":"Controls","categoryLabels":{}}],"summaryPages":[]}""",
                ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = """local modifiers = { ["UI_SHIFT"] = { ["device"] = "Stick", ["key"] = "JOY_BTN2", ["switch"] = false } } return modifiers""",
                ["assets/shared/ui-layer/input/UiLayer/joystick/Stick.diff.lua"] = """local diff = { ["keyDiffs"] = { ["VOICE"] = { ["name"] = "Voice", ["added"] = { [1] = { ["key"] = "JOY_BTN1", ["reformers"] = { [1] = "UI_SHIFT" } } } }, ["UNMAPPED"] = { ["name"] = "Unmapped", ["added"] = { [1] = { ["key"] = "JOY_BTN99", ["reformers"] = { [1] = "UI_SHIFT" } } } } } } return diff"""
            };
            return new(config, [new KneeboardPage(Guid.NewGuid(), ModuleId, null, KneeboardPageKind.Device, "01-STICK", "Stick", [], 0)],
                [], [diagram], [new CommandPaletteEntry("FLIGHT", "Flight", "Flight", false)],
                [new DcsCommandDefinition(Guid.NewGuid(), Guid.NewGuid(), "VOICE", "Voice", "UI", false)], assignments, new Dictionary<string, string> { [assignments[0].Slot.Key] = "My label" }, sources);
        }
        #endregion END private helpers
    }
}

