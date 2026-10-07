using System.Text.Json;
using System.Text.Json.Nodes;
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
        private static readonly string[] ReorderedMetadata = ["metadata-2", "metadata-0", "metadata-1"];
        private static readonly string[] NumberedKeys = ["01", "02", "03"];
        private static readonly string[] ReorderedTexts = ["Third edited", "First", "Second"];
        #region BEGIN Public
        /// <summary>Renumbered cards retain their original metadata through edits, rendering and a database reopen.</summary>
        [Fact]
        public async Task Reordered_numbered_cards_keep_metadata_and_reopen()
        {
            var workspace = ModuleKneeboardPageEditor.Set(Workspace(), null,
                new(KneeboardPageKind.Summary, null, "summary", "Summary", "CONTROLS", 1,
                    [new("01", "First", "red"), new("01", "Second", "green"), new("03", "Third", "blue")]));
            var page = workspace.Pages.Single(x => x.Kind == KneeboardPageKind.Summary);
            var root = JsonNode.Parse(workspace.Sources!["config/kneeboard.json"])!;
            for (var i = 0; i < 3; i++) root["summaryPages"]![0]!["items"]![i]!["custom"] = "metadata-" + i;
            workspace = workspace with { Sources = new Dictionary<string, string>(workspace.Sources!) { ["config/kneeboard.json"] = root.ToJsonString() } };
            var before = SnapshotSummaryKneeboardRenderer.Generate(workspace).Single().Value;
            var settings = ModuleKneeboardPageEditor.Describe(workspace, page);
            var cards = settings.Cards.ToArray();
            cards[2] = cards[2] with { Text = "Third edited" };
            var reordered = ModuleKneeboardPageEditor.Set(workspace, page, settings with { Cards = SummaryCardOrder.Move(cards, 2, 0) });
            var items = JsonNode.Parse(reordered.Sources!["config/kneeboard.json"])!["summaryPages"]![0]!["items"]!.AsArray();
            Assert.Equal(ReorderedMetadata, items.Select(x => x!["custom"]!.GetValue<string>()));
            Assert.Equal(NumberedKeys, items.Select(x => x!["key"]!.GetValue<string>()));
            Assert.Equal(workspace.Assignments, reordered.Assignments);
            Assert.Equal(workspace.Configuration, reordered.Configuration);
            var after = SnapshotSummaryKneeboardRenderer.Generate(reordered).Single().Value;
            Assert.NotEqual(before, after);
            var path = Path.Combine(Path.GetTempPath(), $"ipia-card-order-{Guid.NewGuid():N}.db");
            var connection = "Data Source=" + path + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(reordered);
                var reopened = (await repository.GetAsync(reordered.Configuration.Module.Id))!;
                Assert.Equal(reordered.Sources!["config/kneeboard.json"], reopened.Sources!["config/kneeboard.json"]);
                Assert.Equal(ReorderedTexts, ModuleKneeboardPageEditor.Describe(reopened,
                    reopened.Pages.Single(x => x.Id == page.Id)).Cards.Select(x => x.Text));
                Assert.Equal(after, SnapshotSummaryKneeboardRenderer.Generate(reopened).Single().Value);
            }
            finally { File.Delete(path); }
        }

        /// <summary>Summary generation includes every device page in display order, preserving existing pages and saved state.</summary>
        /// <returns>Completion of real SQLite save and reopen.</returns>
        [Fact]
        public async Task GeneratesSummaryFromAllDevicePagesAndReopens()
        {
            var workspace = Workspace();
            var template = workspace.Pages[0];
            workspace = workspace with { Pages = Enumerable.Range(0, 15).Select(i => template with
                { Id = Guid.NewGuid(), StableKey = "device-" + i, Title = "Device " + i, Order = i * 2 }).Reverse().ToArray() };
            workspace = ModuleKneeboardPageEditor.Set(workspace, null,
                new(KneeboardPageKind.Summary, null, "summary", "Existing", "CONTROLS", 1, [new("OLD", "Keep", "cyan")]));
            var generated = ModuleKneeboardPageEditor.GenerateSummary(workspace);
            Assert.Equal(workspace.Configuration, generated.Configuration);
            Assert.Equal(workspace.Assignments, generated.Assignments);
            Assert.All(workspace.Pages, page => Assert.Contains(page, generated.Pages));
            var summaries = generated.Pages.Where(x => x.Kind == KneeboardPageKind.Summary && x.StableKey != "summary").ToArray();
            Assert.Equal(2, summaries.Length);
            var cards = summaries.SelectMany(x => ModuleKneeboardPageEditor.Describe(generated, x).Cards).ToArray();
            Assert.Equal(Enumerable.Range(0, 15).Select(i => "Device " + i), cards.Select(x => x.Text));
            Assert.Equal("01", cards[0].Key);
            Assert.Equal("03", cards[1].Key);
            Assert.Equal(14, ModuleKneeboardPageEditor.Describe(generated, summaries[0]).Cards.Count);
            Assert.Equal(3, SnapshotSummaryKneeboardRenderer.Generate(generated).Count);
            var again = ModuleKneeboardPageEditor.GenerateSummary(generated);
            Assert.Equal(5, again.Pages.Count(x => x.Kind == KneeboardPageKind.Summary));
            var path = Path.Combine(Path.GetTempPath(), $"ipia-summary-{Guid.NewGuid():N}.db");
            var connection = "Data Source=" + path + ";Pooling=False";
            try
            {
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteEditorWorkspaceRepository(connection);
                await repository.SaveAsync(generated);
                var reopened = (await repository.GetAsync(generated.Configuration.Module.Id))!;
                Assert.Equal(generated.Pages.Select(x => (x.Id, x.StableKey, x.Title, x.Order)),
                    reopened.Pages.Select(x => (x.Id, x.StableKey, x.Title, x.Order)));
                Assert.Equal(generated.Sources!["config/kneeboard.json"], reopened.Sources!["config/kneeboard.json"]);
                Assert.Equal(3, SnapshotSummaryKneeboardRenderer.Generate(reopened).Count);
            }
            finally { File.Delete(path); }
        }

        /// <summary>No device pages means there is no valid summary to generate.</summary>
        [Fact]
        public void SummaryGenerationRequiresDevicePages()
        {
            var workspace = Workspace() with { Pages = [] };
            Assert.Throws<InvalidOperationException>(() => ModuleKneeboardPageEditor.GenerateSummary(workspace));
        }

        /// <summary>A live generated device page can be removed even if its manifest row is missing.</summary>
        [Fact]
        public void RemovesLiveGeneratedDevicePageWithoutManifestRow()
        {
            var workspace = Workspace();
            var live = workspace.Pages[0] with { StableKey = "device-" + workspace.Configuration.Profiles[0].Id.ToString("N") };
            workspace = workspace with { Pages = [live], Sources = new Dictionary<string, string> { ["config/kneeboard.json"] = "{}" } };
            Assert.True(ModuleKneeboardPageEditor.CanRemove(workspace, live));
            var removed = ModuleKneeboardPageEditor.Remove(workspace, live);
            Assert.Empty(removed.Pages);
            Assert.Equal(workspace.Configuration, removed.Configuration);
            Assert.Equal(workspace.Assignments, removed.Assignments);
        }

        /// <summary>Already orphaned device pages can be removed without deleting a live device's page.</summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RemovesOrphanDevicePagesWithoutADevice(bool keepManifestRow)
        {
            var workspace = Workspace();
            var orphan = workspace.Pages[0];
            var root = JsonNode.Parse(workspace.Sources!["config/kneeboard.json"])!;
            root["profiles"]!.AsObject().Remove("p");
            if (!keepManifestRow) root["pages"]!.AsArray().Clear();
            workspace = workspace with { Sources = new Dictionary<string, string>(workspace.Sources!) { ["config/kneeboard.json"] = root.ToJsonString() } };
            var removed = ModuleKneeboardPageEditor.Remove(workspace, orphan);
            Assert.Empty(removed.Pages);
            Assert.Empty(removed.Images);
            Assert.Single(removed.Configuration.Profiles);
            Assert.Single(removed.Assignments);
            Assert.Throws<InvalidOperationException>(() => ModuleKneeboardPageEditor.Remove(Workspace(), orphan));
        }

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
                removed = ModuleKneeboardPageEditor.Remove(removed, removed.Pages[0]);
                await repository.SaveAsync(removed);
                var withoutPages = (await repository.GetAsync(removed.Configuration.Module.Id))!;
                Assert.Empty(withoutPages.Pages);
                Assert.Empty(withoutPages.Images);
                Assert.Equal(reopened.Configuration.Profiles, withoutPages.Configuration.Profiles);
                Assert.Equal(reopened.Assignments, withoutPages.Assignments);
                Assert.Empty(JsonNode.Parse(withoutPages.Sources!["config/kneeboard.json"])!["pages"]!.AsArray());
                Assert.Empty(JsonNode.Parse(withoutPages.Sources!["config/kneeboard.json"])!["profiles"]!.AsObject());
                var restored = ModuleKneeboardPageEditor.AddMissingDevicePages(withoutPages);
                Assert.Single(restored.Pages);
                Assert.Equal(reopened.Assignments, restored.Assignments);
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
            Assert.Equal(summary.Cards.Select(x => (x.Key, x.Text, x.Accent)),
                ModuleKneeboardPageEditor.Describe(withSummary, withSummary.Pages.Single(x => x.Kind == KneeboardPageKind.Summary)).Cards.Select(x => (x.Key, x.Text, x.Accent)));
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
                ProfileId = profile.Id,
                Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><rect width='100' height='100' fill='#07111d'/></svg>",
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
