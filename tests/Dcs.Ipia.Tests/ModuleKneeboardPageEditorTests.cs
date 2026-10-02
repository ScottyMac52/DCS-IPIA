using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Dcs.Ipia.Rendering;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks supported page authoring, manifest preservation, rendering and SQLite persistence.</summary>
    public sealed class ModuleKneeboardPageEditorTests
    {
        #region BEGIN Public
        /// <summary>Renames device output and stages summary cards without losing metadata, labels or assignments.</summary>
        /// <returns>Completion of real SQLite save/reopen.</returns>
        [Fact]
        public async Task Pages_render_from_authored_state_and_reopen_losslessly()
        {
            var original = Workspace();
            var page = original.Pages[0];
            var settings = ModuleKneeboardPageEditor.Describe(original, page);
            var updated = ModuleKneeboardPageEditor.Set(original, page, settings with { File = "stick", Title = "FLIGHT CONTROLS", Subtitle = "PILOT", Order = 2 });
            Assert.Equal(page.Id, Assert.Single(updated.Pages).Id);
            Assert.Equal(original.Assignments, updated.Assignments);
            Assert.Equal(original.Labels, updated.Labels);
            Assert.Empty(updated.Images);
            using (var document = JsonDocument.Parse(updated.Sources!["config/kneeboard.json"]))
            {
                Assert.Equal("keep", document.RootElement.GetProperty("custom").GetString());
                Assert.Equal("preserved", document.RootElement.GetProperty("pages")[0].GetProperty("custom").GetString());
                Assert.Equal("Left", document.RootElement.GetProperty("pages")[0].GetProperty("categoryLabels").GetProperty("left").GetString());
            }
            Assert.Equal("stick", KneeboardPageLocator.PageKeyForProfile(updated, original.Configuration.Profiles[0].Id));
            Assert.Equal("KNEEBOARD/TEST/stick.png", Assert.Single(SnapshotDeviceKneeboardRenderer.Generate(updated)).Key);
            var summary = new ModuleKneeboardPageSettings(KneeboardPageKind.Summary, null, "overview", "OVERVIEW", "CONTROLS", 0,
                [new("STICK", "Flight controls", "red"), new("AXIS", "Pitch and roll", "blue")]);
            updated = ModuleKneeboardPageEditor.Set(updated, null, summary);
            var summaryPage = updated.Pages.Single(x => x.Kind == KneeboardPageKind.Summary);
            var firstPng = Assert.Single(SnapshotSummaryKneeboardRenderer.Generate(updated)).Value;
            var root = System.Text.Json.Nodes.JsonNode.Parse(updated.Sources!["config/kneeboard.json"])!;
            root["summaryPages"]![0]!["custom"] = "page metadata";
            root["summaryPages"]![0]!["items"]![0]!["custom"] = "stick metadata";
            root["summaryPages"]![0]!["items"]![1]!["custom"] = "axis metadata";
            updated = updated with { Sources = new Dictionary<string, string>(updated.Sources!) { ["config/kneeboard.json"] = root.ToJsonString() } };
            var reordered = ModuleKneeboardPageEditor.Set(updated, summaryPage, summary with { Cards = [summary.Cards[1], summary.Cards[0]] });
            using (var preserved = JsonDocument.Parse(reordered.Sources!["config/kneeboard.json"]))
            {
                Assert.Equal("page metadata", preserved.RootElement.GetProperty("summaryPages")[0].GetProperty("custom").GetString());
                Assert.Equal("axis metadata", preserved.RootElement.GetProperty("summaryPages")[0].GetProperty("items")[0].GetProperty("custom").GetString());
            }
            updated = ModuleKneeboardPageEditor.Set(updated, summaryPage, summary with { Cards = [new("STICK", "Weapon controls", "green")] });
            Assert.NotEqual(firstPng, Assert.Single(SnapshotSummaryKneeboardRenderer.Generate(updated)).Value);
            var path = Path.Combine(Path.GetTempPath(), $"ipia-pages-{Guid.NewGuid():N}.db");
            var connection = "Data Source=" + path + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                updated = ModuleCommandCatalogEditor.Set(updated, null, new("AXIS", "Pitch", "Axes", true));
                await repository.SaveAsync(updated);
                var reopened = (await repository.GetAsync(updated.Configuration.Module.Id))!;
                Assert.Equal("green", Assert.Single(ModuleKneeboardPageEditor.Describe(reopened,
                    reopened.Pages.Single(x => x.Kind == KneeboardPageKind.Summary)).Cards).Accent);
                Assert.Equal(updated.Sources!["config/kneeboard.json"], reopened.Sources!["config/kneeboard.json"]);
                Assert.Equal(2, reopened.ModuleCommands.Count);
                Assert.Equal(Assert.Single(SnapshotSummaryKneeboardRenderer.Generate(updated)).Value,
                    Assert.Single(SnapshotSummaryKneeboardRenderer.Generate(reopened)).Value);
                Assert.Equal(Assert.Single(SnapshotDeviceKneeboardRenderer.Generate(updated)).Value,
                    Assert.Single(SnapshotDeviceKneeboardRenderer.Generate(reopened)).Value);
                var removed = ModuleKneeboardPageEditor.Remove(reopened, reopened.Pages.Single(x => x.Kind == KneeboardPageKind.Summary));
                Assert.Single(removed.Pages);
                Assert.Empty(SnapshotSummaryKneeboardRenderer.Generate(removed));
                Assert.Throws<InvalidOperationException>(() => ModuleKneeboardPageEditor.Remove(removed, removed.Pages[0]));
            }
            finally { File.Delete(path); }
        }

        /// <summary>Rejects invalid layouts, stale identities, duplicate outputs and unsupported cards.</summary>
        [Fact]
        public void Rejects_invalid_or_ambiguous_pages_and_creates_missing_device_pages()
        {
            var workspace = Workspace();
            var page = workspace.Pages[0];
            var settings = ModuleKneeboardPageEditor.Describe(workspace, page);
            Assert.Throws<InvalidOperationException>(() => ModuleKneeboardPageEditor.Set(workspace, page, settings with { File = "../bad" }));
            Assert.Throws<InvalidOperationException>(() => ModuleKneeboardPageEditor.Set(workspace, page, settings with { Title = "" }));
            Assert.Throws<InvalidOperationException>(() => ModuleKneeboardPageEditor.Set(workspace, page, settings with { Order = -1 }));
            Assert.Throws<InvalidOperationException>(() => ModuleKneeboardPageEditor.Set(workspace, page, settings with { ProfileId = Guid.NewGuid() }));
            Assert.Throws<InvalidOperationException>(() => ModuleKneeboardPageEditor.Set(workspace, page with { Title = "Stale" }, settings));
            Assert.Throws<InvalidOperationException>(() => ModuleKneeboardPageEditor.Set(workspace, null, settings with { File = "DEVICE" }));
            Assert.Throws<InvalidOperationException>(() => ModuleKneeboardPageEditor.Set(workspace, null, settings with { File = "duplicate" }));
            var summary = new ModuleKneeboardPageSettings(KneeboardPageKind.Summary, null, "summary", "Summary", "Cards", 1, [new("KEY", "Description", "cyan")]);
            Assert.Throws<InvalidOperationException>(() => ModuleKneeboardPageEditor.Set(workspace, null, summary with { Cards = [] }));
            Assert.Throws<InvalidOperationException>(() => ModuleKneeboardPageEditor.Set(workspace, null, summary with { Cards = [new("KEY", "Description", "bad")] }));
            Assert.Throws<InvalidOperationException>(() => ModuleKneeboardPageEditor.Set(workspace, null, summary with { Cards = Enumerable.Repeat(summary.Cards[0], 15).ToArray() }));
            Assert.Throws<InvalidOperationException>(() => ModuleKneeboardPageEditor.Set(workspace, page, settings with { Cards = summary.Cards }));
            var empty = workspace with { Pages = [], Images = [], Sources = new Dictionary<string, string> { ["config/kneeboard.json"] = """{"includeUiLayer":true,"profiles":{},"pages":[]}""" } };
            // This is the manifest shape produced by New Module, rather than an absent manifest.
            Assert.Null(KneeboardPageLocator.PageKeyForProfile(empty, empty.Configuration.Profiles[0].Id));
            Assert.Equal(empty.Diagrams[0].Title, KneeboardDraftProjection.CreateCombined(empty, empty.Diagrams[0], empty.Assignments, empty.Labels).Title);
            using (var document = JsonDocument.Parse("{}"))
                Assert.Throws<InvalidOperationException>(() => document.RootElement.EnumerateObject().FirstOrDefault().Name);
            var completed = ModuleKneeboardPageEditor.AddMissingDevicePages(empty);
            Assert.Single(completed.Pages);
            Assert.Equal(completed.Pages[0].Id, ModuleKneeboardPageEditor.AddMissingDevicePages(completed).Pages[0].Id);
            Assert.NotEmpty(SnapshotDeviceKneeboardRenderer.Generate(completed));
            Assert.Throws<InvalidOperationException>(() => ModuleKneeboardPageEditor.Set(empty with { Diagrams = [] }, null, settings));
            Assert.Throws<InvalidOperationException>(() => ModuleKneeboardPageEditor.Set(empty, null, settings with { ProfileId = Guid.NewGuid() }));
            var withSummary = ModuleKneeboardPageEditor.Set(workspace, null, summary);
            Assert.Equal(summary.Cards, ModuleKneeboardPageEditor.Describe(withSummary, withSummary.Pages.Single(x => x.Kind == KneeboardPageKind.Summary)).Cards);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static EditorWorkspaceSnapshot Workspace()
        {
            var module = new DcsModule(Guid.NewGuid(), "TEST", "Test");
            var profile = new DeviceProfile(Guid.NewGuid(), "test", null, "Test", "Test.diff.lua");
            var configuration = new ModuleConfiguration(module, [profile], [], [], "fixture");
            var page = new KneeboardPage(Guid.NewGuid(), module.Id, null, KneeboardPageKind.Device, "device", "Test device", [], 0);
            var diagram = new UiLayerDiagram("Test", null, "test.svg", 100, 100, [], [])
            {
                ProfileId = profile.Id, Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><rect width='100' height='100' fill='#07111d'/></svg>",
                Controls = [new HardwareGraphicControl("button", "Button", "JOY_BTN1", 5, 5, 20, 20)]
            };
            var command = new CommandPaletteEntry("FIRE", "Fire", "Keys", false);
            var slot = new DraftAssignmentSlot(module.Id, profile.Id, AssignmentLayer.Module, BindingSection.Key, "JOY_BTN1");
            const string manifest = """{"custom":"keep","profiles":{"p":"src/Config/Input/TEST/joystick/Test.diff.lua"},"pages":[{"profile":"p","deviceId":"test","file":"device","title":"Test device","kicker":"CONTROLS","custom":"preserved","categoryLabels":{"left":"Left"}}]}""";
            return new(configuration, [page], [new("device", "old.png", [1], "old")], [diagram], [command], [],
                [new(slot, new("FIRE", "Fire", false, AssignmentLayer.Module))], new Dictionary<string, string> { [slot.Key] = "Custom" },
                new Dictionary<string, string> { ["config/kneeboard.json"] = manifest });
        }
        #endregion END private helpers
    }
}
