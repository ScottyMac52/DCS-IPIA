using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Heimdall.Packaging;
using Heimdall.Rendering;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Exercises generic controllers through inventory, editing, persistence and portable release output.</summary>
    public sealed class GenericHardwareTests
    {
        #region BEGIN Public
        /// <summary>Unbundled repeated units retain distinct bindings, labels, POVs, chords and filters across SQLite and portable restore.</summary>
        /// <returns>Completion of end-to-end authoring and packaging.</returns>
        [Fact]
        public async Task Inventory_to_edit_save_preview_restore_and_package_is_portable()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-generic-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var source = "Data Source=" + Path.Combine(root, "source.db") + ";Pooling=False";
                await new SqliteMigrationRunner(source).MigrateAsync();
                var definition = GenericHardwareFixture.Definition();
                var left = GenericHardwareFixture.Unit(definition, "Left");
                var right = GenericHardwareFixture.Unit(definition, "Right");
                var inventory = new SqliteHardwareInventoryRepository(source);
                await inventory.SaveAsync(new(1, [definition], [left.Instance, right.Instance], []));
                var manager = new HardwareInventoryManager(inventory, new FakeControllerCapabilityDiscovery());
                var choices = InventoryHardwareAuthoring.Choices(await manager.ReadSnapshotAsync());
                Assert.Equal(2, choices.Count);
                var workspace = GenericHardwareFixture.Workspace();
                foreach (var choice in choices) workspace = InventoryHardwareAuthoring.Add(workspace, choice, AssignmentLayer.Module);
                workspace = InventoryHardwareAuthoring.Add(workspace, left, AssignmentLayer.UiLayer);
                Assert.Equal(2, workspace.Configuration.Profiles.Count);
                Assert.Single(WorkspaceStructureEditor.Hardware(workspace));
                var profile = workspace.Configuration.Profiles.Single(x => x.SourceName == left.FileName);
                var other = workspace.Configuration.Profiles.Single(x => x.SourceName == right.FileName);
                workspace = WorkspaceStructureEditor.SetModifier(workspace, null, new(AssignmentLayer.Module, profile.Id,
                    "SHIFT", "JOY_BTN2", "Shift", ModifierMode.Hold, ""));
                workspace = WorkspaceStructureEditor.SetModifier(workspace, null, new(AssignmentLayer.UiLayer, profile.Id,
                    "UI-SHIFT", "JOY_BTN2", "UI shift", ModifierMode.Toggle, ""));
                workspace = ModuleCommandCatalogImport.Apply(workspace, ModuleCommandCatalogImport.Review(workspace,
                    [new("commands.json", """{"schemaVersion":1,"moduleId":"GENERIC","commands":[{"bindingKey":"d3002pnilu3002cdnilvd1vpnilvunil","name":"Aircraft action","categoryPath":["Systems"],"type":"button"},{"bindingKey":"a2001cdnil","name":"Pitch","categoryPath":["Flight"],"type":"axis"}]}""")], false));
                var key = new DraftCommand(workspace.ModuleCommands.Single(x => !x.IsAxis).Id, "Aircraft action", false, AssignmentLayer.Module);
                var axis = new DraftCommand("a2001cdnil", "Pitch", true, AssignmentLayer.Module);
                var ui = new DraftCommand(workspace.UiCommands.Single().DcsCommandId, "Pause", false, AssignmentLayer.UiLayer);
                var draft = new DraftAssignmentService(workspace.Configuration, [key, axis, ui], [], WorkspaceStructureEditor.UiModifierNames(workspace));
                var module = workspace.Configuration.Module.Id;
                var first = new DraftAssignmentSlot(module, profile.Id, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1");
                var moved = first with { InputKey = "JOY_BTN3" };
                Assert.Equal(DraftOutcome.Applied, draft.Assign(first, key, OccupiedDestinationBehavior.ReplaceInDraft).Outcome);
                Assert.Equal(DraftOutcome.Applied, draft.Move(first, moved, OccupiedDestinationBehavior.ReplaceInDraft).Outcome);
                Assert.Equal(DraftOutcome.Applied, draft.Copy(moved, first, OccupiedDestinationBehavior.ReplaceInDraft).Outcome);
                Assert.Equal(DraftOutcome.Applied, draft.Assign(first with { ProfileId = other.Id }, key, OccupiedDestinationBehavior.ReplaceInDraft).Outcome);
                var hat = first with { InputKey = "JOY_BTN_POV1_UR", ModifierChord = "SHIFT" };
                Assert.Equal(DraftOutcome.Applied, draft.Assign(hat, key, OccupiedDestinationBehavior.ReplaceInDraft).Outcome);
                var tuned = first with { Section = BindingSection.Axis, InputKey = "JOY_X", ModifierChord = "SHIFT" };
                Assert.Equal(DraftOutcome.Applied, draft.Assign(tuned, axis, OccupiedDestinationBehavior.ReplaceInDraft).Outcome);
                var filter = new AxisFilter(Deadzone: 4, Curvature: [12], Invert: true, Slider: false);
                Assert.Equal(DraftOutcome.Applied, draft.TuneAxis(tuned, filter).Outcome);
                Assert.Equal(DraftOutcome.Applied, draft.Assign(first with { Layer = AssignmentLayer.UiLayer, ModifierChord = "UI-SHIFT" }, ui, OccupiedDestinationBehavior.ReplaceInDraft).Outcome);
                workspace = workspace with { Assignments = draft.Snapshot, Labels = new Dictionary<string, string> { [tuned.Key] = "Pitch custom" } };
                workspace = InventoryHardwareAuthoring.RenameControl(workspace, profile.Id, "JOY_BTN3", "Trigger");
                Assert.Equal("JOY_BTN3", workspace.Diagrams.Single(x => x.ProfileId == profile.Id).Controls.Single(x => x.Label == "Trigger").InputKey);
                Assert.DoesNotContain(workspace.Diagrams.Single(x => x.ProfileId == other.Id).Controls, x => x.Label == "Trigger");
                workspace = ModuleKneeboardPageEditor.Set(workspace, null, new(KneeboardPageKind.Summary, null, "00-OVERVIEW", "OVERVIEW", "CONTROLS", 0,
                    [new("CONTROLS", "Aircraft and UI Layer", "cyan")]));
                var repository = new SqliteEditorWorkspaceRepository(source);
                await repository.SaveAsync(workspace);
                var reopened = (await repository.GetAsync(module))!;
                Assert.Equal(System.Text.Json.JsonSerializer.Serialize(filter), System.Text.Json.JsonSerializer.Serialize(reopened.Assignments.Single(x => x.Slot == tuned).Filter));
                Assert.Null(reopened.SharedDefault);
                var plan = GeneratedInputTestPlan.Create(reopened);
                Assert.Empty(plan.Diagnostics);
                var session = new InputTestSession(plan.Bindings, plan.Modifiers);
                var leftInputs = definition.Capabilities.DcsKeys.ToDictionary(x => x, _ => 0.0, StringComparer.Ordinal);
                var rightInputs = new Dictionary<string, double>(leftInputs, StringComparer.Ordinal);
                Assert.Empty(session.Process([new(left.Instance.PhysicalGuid, leftInputs), new(right.Instance.PhysicalGuid, rightInputs)]));
                leftInputs["JOY_BTN2"] = 1;
                leftInputs["JOY_BTN1"] = 1;
                leftInputs["JOY_BTN_POV1_UR"] = 1;
                leftInputs["JOY_X"] = 0.5;
                var changes = session.Process([new(left.Instance.PhysicalGuid, leftInputs), new(right.Instance.PhysicalGuid, rightInputs)]);
                Assert.Contains(changes, x => x.InputKey == "JOY_BTN_POV1_UR" && x.Matches.Any(b => b.Layer == AssignmentLayer.Module && b.Chord == "SHIFT"));
                Assert.Contains(changes, x => x.InputKey == "JOY_BTN1" && x.Matches.Any(b => b.Layer == AssignmentLayer.UiLayer && b.Chord == "UI-SHIFT"));
                Assert.Contains(changes, x => x.InputKey == "JOY_X" && x.Matches.Any(b => b.Binding.Filter?.Deadzone == 4));
                var rendered = SnapshotDeviceKneeboardRenderer.Generate(reopened);
                Assert.True(rendered.Count >= 2);
                Assert.All(rendered.Values, x => Assert.True(x.Length > 1000));
                var snapshot = ModuleSnapshotWorkflow.Freeze(reopened, "1.0");
                var portable = ModuleSnapshotWorkflow.Read(ModuleSnapshotWorkflow.Export(snapshot));
                var target = "Data Source=" + Path.Combine(root, "target.db") + ";Pooling=False";
                await new SqliteMigrationRunner(target).MigrateAsync();
                var restored = new SqliteEditorWorkspaceRepository(target);
                await ModuleSnapshotWorkflow.RestoreAsync(portable, restored);
                Assert.Empty((await new SqliteHardwareInventoryRepository(target).ReadAsync()).Instances);
                var after = (await restored.GetAsync(module))!;
                Assert.Equal(snapshot.Workspace, EditorSnapshotSerializer.Serialize(after));
                var package = ModuleSnapshotWorkflow.Build(snapshot);
                Assert.Equal(package.Bytes, ModuleSnapshotWorkflow.Build(ModuleSnapshotWorkflow.Freeze(after, "1.0")).Bytes);
                Assert.Equal(package.Sha256, OvGmePackageVerifier.Verify(package));
                var lua = SnapshotLuaPayloadRenderer.Generate(after);
                var aircraft = Encoding.UTF8.GetString(lua["Config/Input/GENERIC/joystick/" + left.FileName]);
                Assert.Contains("JOY_BTN_POV1_UR", aircraft, StringComparison.Ordinal);
                Assert.Contains("curvature", aircraft, StringComparison.Ordinal);
                Assert.Contains("SHIFT", aircraft, StringComparison.Ordinal);
                Assert.Contains("UI-SHIFT", Encoding.UTF8.GetString(lua["Config/Input/UiLayer/joystick/" + left.FileName]), StringComparison.Ordinal);
                Assert.DoesNotContain(lua.Keys, x => x.Contains("11111111", StringComparison.Ordinal));
                var docs = ModuleDocumentationWorkflow.Generate(snapshot);
                Assert.Contains("Explicit empty default baseline", docs["CONTROL-MAPPINGS.md"], StringComparison.Ordinal);
                Assert.Contains("Pitch custom", docs["CONTROL-MAPPINGS.md"], StringComparison.Ordinal);
                using var zip = new ZipArchive(new MemoryStream(package.Bytes));
                Assert.Equal(rendered.Count + 1, zip.Entries.Count(x => x.FullName.EndsWith(".png", StringComparison.Ordinal)));
                Assert.Throws<InvalidOperationException>(() => InventoryHardwareAuthoring.Add(after, left, AssignmentLayer.Module));
            }
            finally { Directory.Delete(root, true); }
        }

        /// <summary>High-count devices offer only verified inputs and paginate every label at a readable fixed size.</summary>
        [Fact]
        public void High_counts_and_long_chords_render_deterministic_image_free_pages()
        {
            var definition = GenericHardwareFixture.Definition(true);
            var hardware = GenericHardwareLayout.Create(definition);
            Assert.Equal(162, hardware.Diagram.Controls.Count);
            Assert.DoesNotContain(hardware.Diagram.Controls, x => x.InputKey == "JOY_Z");
            Assert.Contains(hardware.Diagram.Controls, x => x.InputKey == "JOY_BTN128");
            Assert.Contains(hardware.Diagram.Controls, x => x.InputKey == "JOY_BTN_POV4_UL");
            Assert.DoesNotContain("<image", hardware.Diagram.Svg, StringComparison.Ordinal);
            var labels = new Dictionary<string, IReadOnlyList<KneeboardLabelVariant>>
            {
                ["JOY_BTN128"] = [new(new string('A', 600), "#ffffff", AssignmentLayer.Module, "SHIFT+LONG"), new("Pause", "#0891b2", AssignmentLayer.UiLayer, "UI-SHIFT")]
            };
            var page = new KneeboardComposite("Generic", "CONTROLS", labels, new Dictionary<string, string>(), [new("SHIFT — JOY_BTN1 [Hold]", "#ffffff")]);
            var svgs = GenericKneeboardRenderer.BuildPages(hardware.Diagram, page);
            Assert.True(svgs.Count > 10);
            Assert.Contains("JOY_BTN128", string.Join('\n', svgs), StringComparison.Ordinal);
            Assert.Contains("UI Layer", string.Join('\n', svgs), StringComparison.Ordinal);
            Assert.All(svgs, svg => Assert.All(XDocument.Parse(svg).Descendants().Where(x => x.Name.LocalName == "text" && x.Attribute("y")!.Value != "1570"),
                x => Assert.True(int.Parse(x.Attribute("font-size")!.Value, System.Globalization.CultureInfo.InvariantCulture) >= 22)));
            var reordered = hardware.Diagram with { Controls = hardware.Diagram.Controls.Reverse().ToArray() };
            Assert.Equal(svgs, GenericKneeboardRenderer.BuildPages(reordered, page));
            var rendered = GenericKneeboardRenderer.RenderPages(hardware.Diagram, page);
            Assert.Equal(svgs.Count, rendered.Count);
            Assert.Equal(rendered[0], GenericKneeboardRenderer.RenderPages(reordered, page)[0]);
        }

        /// <summary>Empty defaults remain explicit while deliberately selected saved revisions retain their modifiers.</summary>
        [Fact]
        public void Neutral_modules_do_not_inherit_personal_controller_defaults()
        {
            var defaults = GenericHardwareFixture.Defaults();
            var hardware = new Dictionary<string, string> { ["config/ipia-hardware.json"] = "[]" };
            var selected = NewModuleWorkspaceFactory.Create("SELECTED", "Selected", defaults, hardware);
            Assert.NotNull(selected.SharedDefault);
            Assert.Contains(WorkspaceStructureEditor.Describe(selected).Modifiers, x => x.Name == "PERSONAL");
            var empty = GenericHardwareFixture.Workspace();
            Assert.Empty(empty.Configuration.Profiles);
            Assert.Empty(empty.Assignments);
            Assert.Empty(WorkspaceStructureEditor.Describe(empty).Modifiers);
            Assert.DoesNotContain(empty.Sources!.Values, x => x.Contains("11111111", StringComparison.Ordinal));
            Assert.Null(empty.SharedDefault);
            Assert.Single(empty.UiCommands);
            Assert.Equal(defaults.ContentSha256, selected.SharedDefault!.ContentSha256);
        }

        /// <summary>Forged controls, foreign artwork, mismatched layouts and unadopted physical units fail before persistence.</summary>
        /// <returns>Completion of rejected save checks.</returns>
        [Fact]
        public async Task Invalid_definition_or_layout_cannot_write_a_module()
        {
            var definition = GenericHardwareFixture.Definition();
            var choice = GenericHardwareFixture.Unit(definition, "Left");
            var workspace = GenericHardwareFixture.Workspace();
            Assert.Throws<InvalidOperationException>(() => InventoryHardwareAuthoring.Add(workspace, choice with { Instance = choice.Instance with { Selected = false } }, AssignmentLayer.Module));
            var hardware = GenericHardwareLayout.Create(definition);
            Assert.Throws<InvalidOperationException>(() => InventoryHardwareAuthoring.Add(workspace, choice, AssignmentLayer.Module,
                hardware with { Diagram = hardware.Diagram with { Controls = [] } }));
            Assert.Throws<InvalidDataException>(() => GenericHardwareLayout.Create(definition with { Id = "invalid" }));
            Assert.Throws<InvalidDataException>(() => GenericHardwareLayout.Create(new(ControllerCapabilityMapper.Fingerprint(ControllerCapabilityMapper.Map([])), ControllerCapabilityMapper.Map([]))));
            workspace = InventoryHardwareAuthoring.Add(workspace, choice, AssignmentLayer.Module);
            var diagram = workspace.Diagrams.Single();
            Assert.Throws<InvalidDataException>(() => GenericHardwareLayout.Validate(diagram with { Background = [1] }));
            Assert.Throws<InvalidDataException>(() => GenericHardwareLayout.Validate(diagram with { GenericCapabilities = null }));
            Assert.Throws<InvalidDataException>(() => InventoryHardwareAuthoring.RenameControl(workspace, diagram.ProfileId, "JOY_BTN1", ""));
            Assert.Throws<InvalidOperationException>(() => InventoryHardwareAuthoring.RenameControl(workspace, diagram.ProfileId, "JOY_BTN999", "Invalid"));
            var corrupt = workspace with { Diagrams = [diagram with { Controls = [.. diagram.Controls, new("fake", "Fake", "JOY_BTN999", 0, 0, 1, 1)] }] };
            Assert.Throws<InvalidDataException>(() => EditorSnapshotSerializer.Serialize(corrupt));
            var path = Path.Combine(Path.GetTempPath(), "ipia-invalid-generic-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync(corrupt));
                Assert.Empty(await repository.ListAsync());
                var rebound = WorkspaceStructureEditor.RebindDevice(workspace, WorkspaceStructureEditor.Describe(workspace).Devices.Single(),
                    GenericHardwareFixture.Unit(definition, "Replacement").FileName, "Replacement", null);
                Assert.Equal(diagram.GenericCapabilities, rebound.Diagrams.Single().GenericCapabilities);
                Assert.NotEqual(workspace.Configuration.Profiles.Single().SourceName, rebound.Configuration.Profiles.Single().SourceName);
            }
            finally { File.Delete(path); }
        }
        #endregion END Public
    }
}
