using System.IO.Compression;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks safe supported-only imports and lossless compatible/Owner restores.</summary>
    public sealed class ModuleSnapshotEditionFilterTests
    {
        #region BEGIN Public
        /// <summary>Prunes both memberships and cross-device shifted bindings, retains Base/tuning, and persists a verified package.</summary>
        [Fact]
        public async Task Mixed_snapshot_keeps_supported_data_and_drops_dependent_chords()
        {
            var workspace = MixedSnapshotFixture.Create();
            var original = ModuleSnapshotWorkflow.Freeze(workspace, "1.2.3");
            var before = ModuleSnapshotWorkflow.Export(original);
            var filtered = ModuleSnapshotEditionFilter.KeepSupported(original, new());
            var result = EditorSnapshotSerializer.Deserialize(filtered.Workspace);
            Assert.Equal("1.2.3", filtered.Version); Assert.Equal(workspace.Configuration.Module, result.Configuration.Module);
            Assert.Equal(MixedSnapshotFixture.Supported, Assert.Single(result.Configuration.Profiles).Id);
            Assert.Equal(2, WorkspaceStructureEditor.Describe(result).Devices.Count);
            Assert.Empty(WorkspaceStructureEditor.Describe(result).Modifiers); Assert.Empty(result.Configuration.Modifiers);
            Assert.Single(result.Diagrams); Assert.Single(result.Pages, p => p.Kind == KneeboardPageKind.Device);
            Assert.Single(result.Pages, p => p.Kind == KneeboardPageKind.Summary); Assert.Equal(Enumerable.Range(0, 2), result.Pages.Select(p => p.Order));
            Assert.Equal(3, result.Assignments.Count); Assert.All(result.Assignments, a => Assert.Equal("", a.Slot.ModifierChord));
            Assert.DoesNotContain(result.Assignments, a => a.Slot.InputKey == "JOY_BTN2");
            Assert.Equal(0.07, result.Assignments.Single(a => a.Command.IsAxis).Filter!.Deadzone);
            Assert.Equal(3, result.Labels.Count); Assert.All(result.Labels, p => Assert.Equal(workspace.Labels[p.Key], p.Value));
            Assert.Equal(workspace.ModuleCommands.OrderBy(c => c.Id), result.ModuleCommands.OrderBy(c => c.Id));
            Assert.Equal(workspace.UiCommands.OrderBy(c => c.Id), result.UiCommands.OrderBy(c => c.Id));
            Assert.Equal(before, ModuleSnapshotWorkflow.Export(original));
            Assert.DoesNotContain(result.Sources!.Keys, p => p == "config/ipia-ui-" + MixedSnapshotFixture.Unsupported.ToString("N") + ".lua");
            using var archive = new ZipArchive(new MemoryStream(ModuleSnapshotWorkflow.Build(filtered).Bytes));
            Assert.DoesNotContain(archive.Entries, e => e.FullName.Contains("Other {", StringComparison.Ordinal));
            var path = Path.Combine(Path.GetTempPath(), "ipia-filter-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False"; await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await ModuleSnapshotWorkflow.RestoreAsync(filtered, repository);
                var reopened = (await repository.GetAsync(result.Configuration.Module.Id))!;
                Assert.Equal(filtered.Workspace, EditorSnapshotSerializer.Serialize(reopened));
            }
            finally { File.Delete(path); }
        }
        /// <summary>Owner and compatible snapshots retain their exact frozen input; unknown devices cannot leave an empty conversion.</summary>
        [Fact]
        public void Owner_and_compatible_inputs_are_lossless_and_empty_conversion_is_blocked()
        {
            var workspace = MixedSnapshotFixture.Create();
            var original = ModuleSnapshotWorkflow.Freeze(workspace, "Original");
            using var issuer = new OwnerLicenseTestIssuer();
            Assert.Same(original, ModuleSnapshotEditionFilter.KeepSupported(original, issuer.Policy));
            var allFree = workspace with { Configuration = new(workspace.Configuration.Module,
                workspace.Configuration.Profiles.Select(p => p.Id == MixedSnapshotFixture.Unsupported ? new DeviceProfile(p.Id, "tm-tpr", p.Instance, p.Role, p.SourceName) : p),
                workspace.Configuration.Modifiers, workspace.Configuration.Bindings, workspace.Configuration.Provenance) };
            var compatible = ModuleSnapshotWorkflow.Freeze(allFree, "Original");
            Assert.Same(compatible, ModuleSnapshotEditionFilter.KeepSupported(compatible, new()));
            var noneFree = workspace with { Configuration = new(workspace.Configuration.Module,
                workspace.Configuration.Profiles.Select(p => new DeviceProfile(p.Id, "reserved-" + p.DeviceId, p.Instance, p.Role, p.SourceName)),
                workspace.Configuration.Modifiers, workspace.Configuration.Bindings, workspace.Configuration.Provenance) };
            Assert.Throws<InvalidOperationException>(() => ModuleSnapshotEditionFilter.KeepSupported(ModuleSnapshotWorkflow.Freeze(noneFree, "Original"), new()));
            Assert.Same(workspace, WorkspaceStructureEditor.RemoveDevicesForSnapshotImport(workspace, new HashSet<Guid>()));
            Assert.Throws<ArgumentException>(() => WorkspaceStructureEditor.RemoveDevicesForSnapshotImport(workspace, new HashSet<Guid> { Guid.NewGuid() }));
        }
        /// <summary>Unmapped native bindings and captured default-axis removals follow the same dependency cleanup in both scopes.</summary>
        [Fact]
        public void Pruning_cleans_unmapped_chords_and_axis_evidence_without_clearing_unrelated_bindings()
        {
            var workspace = MixedSnapshotFixture.Create();
            var raw = new DcsBinding(MixedSnapshotFixture.Supported, BindingSection.Key, "d3004pnilu3004cdnilvd1vpnilvunil", "Unmapped", BindingDisposition.Added, "JOY_BTN99", ["SHIFT"]);
            workspace = workspace with { Configuration = new(workspace.Configuration.Module, workspace.Configuration.Profiles,
                workspace.Configuration.Modifiers, workspace.Configuration.Bindings.Append(raw).Append(new(raw.ProfileId, raw.Section, raw.CommandId, raw.CommandName, raw.Disposition, "JOY_BTN98", [])), workspace.Configuration.Provenance) };
            var sources = new Dictionary<string, string>(workspace.Sources!, StringComparer.Ordinal);
            var uiPath = "config/ipia-ui-" + MixedSnapshotFixture.Supported.ToString("N") + ".lua";
            sources[uiPath] = """local diff = { ["keyDiffs"] = { ["d3004pnilu3004cdnilvd1vpnilvunil"] = { ["name"] = "Unmapped", ["added"] = { [1] = { ["key"] = "JOY_BTN99", ["reformers"] = { [1] = "UI_SHIFT" } }, [2] = { ["key"] = "JOY_BTN98" } } } } } return diff""";
            workspace = workspace with { Sources = sources };
            foreach (var layer in Enum.GetValues<AssignmentLayer>())
            {
                var shift = layer == AssignmentLayer.Module ? "SHIFT" : "UI_SHIFT";
                var defaults = $$"""local diff = { ["axisDiffs"] = { ["a2002cdnil"] = { ["name"] = "Default", ["removed"] = { [1] = { ["key"] = "JOY_Z", ["reformers"] = { [1] = "{{shift}}" } } } }, ["a2003cdnil"] = { ["name"] = "Other default", ["removed"] = { [1] = { ["key"] = "JOY_Y" } } } } } return diff""";
                workspace = ModuleAxisDefaults.Import(workspace, MixedSnapshotFixture.Supported, layer, defaults);
                workspace = ModuleAxisDefaults.Import(workspace, MixedSnapshotFixture.Unsupported, layer, defaults);
            }
            var filtered = WorkspaceStructureEditor.RemoveDevicesForSnapshotImport(workspace, new HashSet<Guid> { MixedSnapshotFixture.Unsupported });
            var module = DcsLuaParser.ParseProfile(ModuleLuaProfileRenderer.Render(filtered, MixedSnapshotFixture.Supported), MixedSnapshotFixture.Supported);
            var ui = DcsLuaParser.ParseProfile(UiLayerLuaProfileRenderer.Render(filtered, MixedSnapshotFixture.Supported), MixedSnapshotFixture.Supported);
            foreach (var bindings in new[] { module, ui })
            {
                Assert.DoesNotContain(bindings, b => b.InputKey is "JOY_BTN99" or "JOY_Z");
                Assert.Contains(bindings, b => b.InputKey == "JOY_BTN98"); Assert.Contains(bindings, b => b.InputKey == "JOY_Y");
            }
            Assert.DoesNotContain(filtered.Sources!.Keys, key => key.Contains(MixedSnapshotFixture.Unsupported.ToString("N"), StringComparison.Ordinal));
        }
        /// <summary>Modifiers hosted on retained devices and their shifted assignments remain intact in each scope.</summary>
        [Fact]
        public void Supported_modifiers_and_unrelated_shifted_assignments_are_preserved()
        {
            var workspace = MixedSnapshotFixture.Create();
            foreach (var layer in Enum.GetValues<AssignmentLayer>())
            {
                var name = layer == AssignmentLayer.Module ? "KEEP" : "UI_KEEP";
                var device = WorkspaceStructureEditor.Describe(workspace).Devices.Single(d => d.ProfileId == MixedSnapshotFixture.Supported && d.Layer == layer);
                workspace = WorkspaceStructureEditor.SetModifier(workspace, null,
                    new(layer, device.ProfileId, name, "JOY_BTN3", name, ModifierMode.Hold, device.FileName[..^9]));
                var command = workspace.Assignments.First(a => a.Slot.Layer == layer).Command;
                workspace = workspace with { Assignments = [.. workspace.Assignments,
                    new(new(workspace.Configuration.Module.Id, device.ProfileId, layer, BindingSection.Key, "JOY_BTN2", name), command)] };
            }
            var filtered = ModuleSnapshotEditionFilter.KeepSupported(ModuleSnapshotWorkflow.Freeze(workspace, "1.2.3"), new());
            var result = EditorSnapshotSerializer.Deserialize(filtered.Workspace);
            Assert.Equal(5, result.Assignments.Count); Assert.Equal(2, WorkspaceStructureEditor.Describe(result).Modifiers.Count);
            Assert.Contains(result.Assignments, a => a.Slot.Layer == AssignmentLayer.Module && a.Slot.ModifierChord == "KEEP");
            Assert.Contains(result.Assignments, a => a.Slot.Layer == AssignmentLayer.UiLayer && a.Slot.ModifierChord == "UI_KEEP");
        }
        /// <summary>Legacy unmanaged imported Lua is materialized before pruning, so removed UI bindings cannot reappear from pinned sources.</summary>
        [Fact]
        public void Legacy_snapshot_profiles_are_materialized_and_pruned_in_both_scopes()
        {
            var workspace = MixedSnapshotFixture.Create();
            var sources = new Dictionary<string, string>(workspace.Sources!, StringComparer.Ordinal);
            var diagrams = workspace.Diagrams.Select(diagram =>
            {
                var profile = workspace.Configuration.Profiles.Single(p => p.Id == diagram.ProfileId);
                var path = "assets/shared/ui-layer/input/UiLayer/joystick/" + profile.SourceName;
                sources[path] = UiLayerLuaProfileRenderer.Render(workspace, profile.Id);
                return diagram with { CanonicalUiLayerSourcePath = path };
            }).ToArray();
            var native = workspace.Configuration.Profiles.Single(p => p.Id == MixedSnapshotFixture.Unsupported).SourceName[..^9];
            sources["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] = $$"""local modifiers = { ["UI_SHIFT"] = { ["device"] = "{{native}}", ["key"] = "JOY_BTN3", ["switch"] = true } } return modifiers""";
            sources.Remove(WorkspaceManagement.SourcePath);
            workspace = workspace with { Sources = sources, Diagrams = diagrams };
            var filtered = ModuleSnapshotEditionFilter.KeepSupported(ModuleSnapshotWorkflow.Freeze(workspace, "1.2.3"), new());
            var saved = EditorSnapshotSerializer.Deserialize(filtered.Workspace);
            Assert.Equal(3, saved.Assignments.Count); Assert.Empty(WorkspaceStructureEditor.Describe(saved).Modifiers);
            Assert.All(saved.Assignments, assignment => Assert.Equal(MixedSnapshotFixture.Supported, assignment.Slot.ProfileId));
            Assert.DoesNotContain(UiLayerLuaProfileRenderer.Render(saved, MixedSnapshotFixture.Supported), "UI_SHIFT", StringComparison.Ordinal);
        }
        /// <summary>The existing keyboard exception remains available even when no supported controller profile survives.</summary>
        [Fact]
        public void Keyboard_profiles_count_as_retained_supported_data()
        {
            var workspace = MixedSnapshotFixture.Create();
            var unsupported = workspace.Configuration.Profiles.Single(p => p.Id == MixedSnapshotFixture.Unsupported);
            var keyboard = new DeviceProfile(Guid.NewGuid(), "keyboard", null, "Keyboard", "Keyboard.diff.lua");
            workspace = workspace with { Configuration = new(workspace.Configuration.Module, [unsupported, keyboard], [], [], workspace.Configuration.Provenance) };
            var blocked = ModuleSnapshotEditionFilter.Unsupported(workspace, new());
            Assert.Equal(unsupported, Assert.Single(blocked));
            Assert.True(ModuleSnapshotEditionFilter.HasSupportedProfiles(workspace, blocked));
            Assert.False(ModuleSnapshotEditionFilter.HasSupportedProfiles(workspace, workspace.Configuration.Profiles));
        }
        /// <summary>Offline profiles follow the allowlist; connected conflicting native vendor evidence also requires review.</summary>
        [Fact]
        public void Review_matches_native_evidence_without_requiring_connected_hardware()
        {
            var workspace = MixedSnapshotFixture.Create();
            var free = new EditionPolicy();
            Assert.Single(ModuleSnapshotEditionFilter.Unsupported(workspace, free));
            var device = new DetectedDcsDevice(MixedSnapshotFixture.Supported, "Renamed TM", true);
            Assert.Equal(2, ModuleSnapshotEditionFilter.Unsupported(workspace, free, [device]).Count);
            Assert.Single(ModuleSnapshotEditionFilter.Unsupported(workspace, free, [device with { Connected = false }]));
            Assert.Single(ModuleSnapshotEditionFilter.Unsupported(workspace, free, [device with { ProductGuid = Guid.Parse("0402044f-0000-0000-0000-504944564944") }]));
            using var issuer = new OwnerLicenseTestIssuer();
            Assert.Empty(ModuleSnapshotEditionFilter.Unsupported(workspace, issuer.Policy, [device]));
        }
        #endregion END Public
    }
}
