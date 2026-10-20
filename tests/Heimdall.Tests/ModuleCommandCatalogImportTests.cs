using System.Text.Json;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks reviewed command loading, preserved authoring state and real SQLite provenance.</summary>
    public sealed class ModuleCommandCatalogImportTests
    {
        #region BEGIN Public
        /// <summary>IPI JSON merges by stable code and survives persistence without altering UI ownership or custom labels.</summary>
        /// <returns>Completion of SQLite integration checks.</returns>
        [Fact]
        public async Task Json_merge_preserves_assignments_history_labels_and_original_sources()
        {
            var original = Workspace();
            var review = ModuleCommandCatalogImport.Review(original, [new("commands.json", Json)], false);
            Assert.Null(review.Error);
            Assert.Equal(2, review.Changes.Count);
            Assert.Equal("Update", review.Changes.Single(x => x.Code == Button).Action);
            Assert.Equal("Old name", review.Changes.Single(x => x.Code == Button).OldName);
            Assert.Equal("Old category", review.Changes.Single(x => x.Code == Button).OldCategory);
            Assert.Equal("Key", review.Changes.Single(x => x.Code == Button).OldType);
            Assert.Equal("Add", review.Changes.Single(x => x.Code == Axis).Action);
            Assert.Equal("Axis", review.Changes.Single(x => x.Code == Axis).Type);
            var merged = ModuleCommandCatalogImport.Apply(original, review);
            Assert.Equal(3, merged.ModuleCommands.Count);
            Assert.Contains(merged.ModuleCommands, x => x.Id == "KEEP");
            Assert.Equal("Flight / Modes", merged.ModuleCommands.Single(x => x.Id == Button).Category);
            Assert.Equal("New name", merged.Assignments.Single(x => x.Slot.Layer == AssignmentLayer.Module).Command.Name);
            Assert.Equal("UI name", merged.Assignments.Single(x => x.Slot.Layer == AssignmentLayer.UiLayer).Command.Name);
            Assert.Equal(original.UiCommands, merged.UiCommands);
            Assert.Equal(original.Labels, merged.Labels);
            Assert.Equal(original.Configuration.Profiles, merged.Configuration.Profiles);
            Assert.Equal(original.Configuration.Modifiers, merged.Configuration.Modifiers);
            Assert.Contains(merged.Configuration.Bindings, x => x.Disposition == BindingDisposition.Removed && x.CommandName == "History");
            Assert.Equal("keep", merged.Sources!["unrelated"]);
            Assert.Contains(merged.Sources, x => x.Key.StartsWith("config/ipia-command-imports/", StringComparison.Ordinal) && x.Key.EndsWith("/commands.json", StringComparison.Ordinal) && x.Value == Json);
            var repeated = ModuleCommandCatalogImport.Review(merged, [new("commands.json", Json)], false);
            Assert.All(repeated.Changes, x => Assert.Equal("Unchanged", x.Action));
            Assert.Equal(JsonSerializer.Serialize(merged), JsonSerializer.Serialize(ModuleCommandCatalogImport.Apply(merged, repeated)));
            var path = Path.Combine(Path.GetTempPath(), "ipia-catalog-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var connection = "Data Source=" + path + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                var other = Workspace("OTHER");
                await repository.SaveAsync(other);
                await repository.SaveAsync(merged);
                var reopened = (await repository.GetAsync(merged.Configuration.Module.Id))!;
                Assert.Equal(3, reopened.ModuleCommands.Count);
                Assert.Equal("Custom label", reopened.Labels.Values.Single());
                Assert.Equal(Json, reopened.Sources!.Single(x => x.Key.EndsWith("/commands.json", StringComparison.Ordinal)).Value);
                Assert.Equal("Old name", (await repository.GetAsync(other.Configuration.Module.Id))!.ModuleCommands.Single(x => x.Id == Button).Name);
            }
            finally { File.Delete(path); }
        }

        /// <summary>Multiple HTML exports deduplicate identical definitions and do not import physical assignments.</summary>
        [Fact]
        public void Html_review_merges_device_exports_and_matches_json_commands()
        {
            var workspace = Workspace();
            var first = Html("Stick", Button, "New name", "Flight; Modes");
            var second = "<h1>Throttle</h1><table><tr><td>JOY_X</td><td>Pitch</td><td>Flight</td><td>" + Axis + "</td></tr></table>";
            var third = Html("Keyboard", Button, "New name", "Flight; Modes");
            var review = ModuleCommandCatalogImport.Review(workspace,
                [new("Stick.html", first), new("Throttle.htm", second), new("Keyboard.html", third)], true);
            Assert.Null(review.Error);
            Assert.Equal(2, review.Commands.Count);
            Assert.Equal(DcsCommandCatalogImporter.Parse(Json, "TEST"), review.Commands);
            var merged = ModuleCommandCatalogImport.Apply(workspace, review);
            Assert.Equal(3, merged.ModuleCommands.Count);
            Assert.Equal(workspace.Assignments.Count, merged.Assignments.Count);
            Assert.Equal(workspace.Configuration.Bindings.Count, merged.Configuration.Bindings.Count);
            Assert.Equal(3, merged.Sources!.Count(x => x.Key.StartsWith("config/ipia-command-imports/", StringComparison.Ordinal)));
            Assert.Throws<InvalidDataException>(() => ModuleCommandCatalogImport.Review(workspace,
                [new("Stick.html", first), new("Other.html", Html("Other", Button, "Conflict", "Flight"))], true));
            Assert.Throws<InvalidDataException>(() => ModuleCommandCatalogImport.Review(workspace, [new("Stick.html", "bad")], true));
        }

        /// <summary>Any assigned type conflict blocks the whole batch; stale review and malformed input cannot alter the draft.</summary>
        [Fact]
        public void Invalid_and_stale_imports_leave_the_original_unchanged()
        {
            var original = Workspace();
            var json = Json.Replace("\"type\":\"button\"", "\"type\":\"axis\"", StringComparison.Ordinal);
            var blocked = ModuleCommandCatalogImport.Review(original, [new("commands.json", json)], false);
            Assert.Contains(Button, blocked.Error!);
            Assert.Throws<InvalidOperationException>(() => ModuleCommandCatalogImport.Apply(original, blocked));
            Assert.Equal(2, original.ModuleCommands.Count);
            var withoutCatalog = original with { ModuleCommands = [] };
            Assert.NotNull(ModuleCommandCatalogImport.Review(withoutCatalog, [new("commands.json", json)], false).Error);
            var unmapped = original with { Assignments = [], Diagrams = [] };
            Assert.NotNull(ModuleCommandCatalogImport.Review(unmapped, [new("commands.json", json)], false).Error);
            var valid = ModuleCommandCatalogImport.Review(original, [new("commands.json", Json)], false);
            var changed = ModuleCommandCatalogEditor.Set(original, null, new("NEW", "Extra", "General", false));
            Assert.Throws<InvalidOperationException>(() => ModuleCommandCatalogImport.Apply(changed, valid));
            Assert.Throws<InvalidOperationException>(() => ModuleCommandCatalogImport.Apply(original with { Assignments = [] }, valid));
            Assert.Throws<FormatException>(() => ModuleCommandCatalogImport.Review(original, [new("commands.json", Json.Replace("TEST", "OTHER", StringComparison.Ordinal))], false));
            Assert.Throws<FormatException>(() => ModuleCommandCatalogImport.Review(original, [new("commands.json", "{")], false));
            Assert.Throws<InvalidDataException>(() => ModuleCommandCatalogImport.Review(original, [], false));
            Assert.Throws<InvalidDataException>(() => ModuleCommandCatalogImport.Review(original, [new("a.json", Json), new("b.json", Json)], false));
            Assert.Throws<InvalidDataException>(() => ModuleCommandCatalogImport.Review(original, [new("../commands.json", Json)], false));
            Assert.Throws<InvalidDataException>(() => ModuleCommandCatalogImport.Review(original, [new("commands.txt", Json)], false));
            Assert.Throws<InvalidDataException>(() => ModuleCommandCatalogImport.Review(original, [new("commands.json", "{\"schemaVersion\":1,\"moduleId\":\"TEST\",\"commands\":[]}")], false));
            Assert.Throws<InvalidOperationException>(() => ModuleCommandCatalogEditor.Merge(original, [original.ModuleCommands[0], original.ModuleCommands[0]]));
            Assert.Throws<InvalidOperationException>(() => ModuleCommandCatalogEditor.Merge(original, [new("bad code", "Name", "Category", false)]));
        }
        /// <summary>File reads retain only safe basenames and surface missing files without staging anything.</summary>
        [Fact]
        public void Reads_json_and_html_from_selected_files()
        {
            var root = Path.Combine(Path.GetTempPath(), "ipia-read-catalog-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                var jsonPath = Path.Combine(root, "commands.json");
                var htmlPath = Path.Combine(root, "Stick.html");
                File.WriteAllText(jsonPath, Json);
                File.WriteAllText(htmlPath, Html("Stick", Button, "New name", "Flight; Modes"));
                Assert.Equal("commands.json", ModuleCommandCatalogImport.ReadFiles(Workspace(), [jsonPath], false).Sources[0].FileName);
                Assert.Single(ModuleCommandCatalogImport.ReadFiles(Workspace(), [htmlPath], true).Commands);
                Assert.Throws<FileNotFoundException>(() => ModuleCommandCatalogImport.ReadFiles(Workspace(), [Path.Combine(root, "missing.json")], false));
            }
            finally { Directory.Delete(root, true); }
        }
        /// <summary>Imported axis definitions retain authored tuning and modifier chords on assigned controls.</summary>
        [Fact]
        public void Axis_import_retains_tuning_and_modifier_chords()
        {
            var original = Workspace();
            var profile = original.Configuration.Profiles[0];
            var filter = new AxisFilter(Deadzone: 3, Curvature: [12], Invert: true, Slider: true);
            var slot = new DraftAssignmentSlot(original.Configuration.Module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Axis, "JOY_X", "SHIFT");
            original = original with {
                Configuration = new(original.Configuration.Module, original.Configuration.Profiles,
                    [new DcsModifier("SHIFT", "Test", "JOY_BTN2", ModifierMode.Hold)],
                    original.Configuration.Bindings.Append(new DcsBinding(profile.Id, BindingSection.Axis, Axis, "Old pitch", BindingDisposition.Added, "JOY_X", ["SHIFT"], filter)),
                    original.Configuration.Provenance),
                Assignments = [.. original.Assignments, new DraftAssignment(slot, new(Axis, "Old pitch", true, AssignmentLayer.Module)) { Filter = filter }],
                Diagrams = [original.Diagrams[0] with { Controls = [.. original.Diagrams[0].Controls, new HardwareGraphicControl("axis", "Pitch", "JOY_X", 20, 20, 10, 10, true)] }]
            };
            var review = ModuleCommandCatalogImport.Review(original, [new("commands.json", Json)], false);
            Assert.Null(review.Error);
            var updated = ModuleCommandCatalogImport.Apply(original, review);
            Assert.Equal(filter, updated.Configuration.Bindings.Single(x => x.CommandId == Axis).Filter);
            Assert.Equal(filter, updated.Assignments.Single(x => x.Command.Id == Axis).Filter);
            Assert.Equal("SHIFT", updated.Assignments.Single(x => x.Command.Id == Axis).Slot.ModifierChord);
            Assert.Equal("Pitch", updated.Assignments.Single(x => x.Command.Id == Axis).Command.Name);
            Assert.Equal(original.Configuration.Modifiers, updated.Configuration.Modifiers);
        }
        #endregion END Public
        #region BEGIN private helpers
        private const string Button = "d3011pnilu3011cd35vd1vpnilvu0";
        private const string Axis = "a2001cdnil";
        private const string Json = """{"schemaVersion":1,"moduleId":"TEST","locale":"en","sourceFingerprint":"reference","commands":[{"bindingKey":"d3011pnilu3011cd35vd1vpnilvu0","name":"New name","rawName":"Original text","aliases":["Alternate"],"categoryPath":["Flight","Modes"],"type":"button","actions":{"down":3011},"source":{"provider":"dcs-controls-html-export"},"currentAssignments":[{"device":"Stick","combo":"JOY_BTN99"}]},{"bindingKey":"a2001cdnil","name":"Pitch","categoryPath":["Flight"],"type":"axis"}]}""";
        private static string Html(string device, string code, string name, string category) =>
            "<h1>" + device + "</h1><table><tr><td>JOY_BTN99</td><td>" + name + "</td><td>" + category + "</td><td>" + code + "</td></tr></table>";
        private static EditorWorkspaceSnapshot Workspace(string dcsId = "TEST")
        {
            var module = new DcsModule(Guid.NewGuid(), dcsId, "Test");
            var profile = new DeviceProfile(Guid.NewGuid(), "test", null, "Test", "Test.diff.lua");
            var slot = new DraftAssignmentSlot(module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1");
            var configuration = new ModuleConfiguration(module, [profile], [], [
                new(profile.Id, BindingSection.Key, Button, "Old name", BindingDisposition.Added, "JOY_BTN1"),
                new(profile.Id, BindingSection.Key, "HISTORY", "History", BindingDisposition.Removed, "JOY_BTN2")], "fixture");
            var diagram = new UiLayerDiagram("Test", null, "test.svg", 100, 100, [], []) {
                ProfileId = profile.Id, Controls = [new HardwareGraphicControl("button", "Button", "JOY_BTN1", 1, 1, 10, 10)]
            };
            return new(configuration, [], [], [diagram], [new(Button, "Old name", "Old category", false), new("KEEP", "Keep", "Other", false)],
                [new(Guid.NewGuid(), Guid.NewGuid(), Button, "UI name", "UI", false)],
                [new(slot, new(Button, "Old name", false, AssignmentLayer.Module)),
                    new(slot with { Layer = AssignmentLayer.UiLayer }, new(Button, "UI name", false, AssignmentLayer.UiLayer))],
                new Dictionary<string, string> { [slot.Key] = "Custom label" }, new Dictionary<string, string> { ["unrelated"] = "keep" });
        }
        #endregion END private helpers
    }
}
