using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Rendering;
using SkiaSharp;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks overview PNGs come from the frozen authored manifest.</summary>
    public sealed class SnapshotSummaryKneeboardRendererTests
    {
        #region BEGIN Public
        /// <summary>Renders a stable overview without using the imported PNG bytes.</summary>
        [Fact]
        public void GeneratesPinnedOverview()
        {
            var workspace = Workspace();
            var first = Assert.Single(SnapshotSummaryKneeboardRenderer.Generate(workspace));
            Assert.Equal("KNEEBOARD/TEST/00-OVERVIEW.png", first.Key);
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, first.Value.Take(8));
            Assert.Equal(
                first.Value,
                Assert.Single(SnapshotSummaryKneeboardRenderer.Generate(workspace with { Images = [] })).Value);
            var changed = workspace with {
                Sources = new Dictionary<string, string> { ["config/kneeboard.json"] =
                                                               workspace.Sources!["config/kneeboard.json"].Replace(
                                                                   "Flight controls", "Other controls",
                                                                   StringComparison.Ordinal) }
            };
            Assert.NotEqual(first.Value, Assert.Single(SnapshotSummaryKneeboardRenderer.Generate(changed)).Value);
        }
        /// <summary>A nine-device overview supports the authored Hornet palette.</summary>
        [Fact]
        public void GeneratesNineRowOverviewWithPurpleAndCyanAccents()
        {
            var workspace = Workspace();
            var items = Enumerable.Range(1, 9)
                                  .Select(i => new { key = "DEVICE " + i, text = "Controls " + i,
                                                     accent = i == 5 ? "purple" : i == 9 ? "cyan" : "blue" });
            var manifest = System.Text.Json.JsonSerializer.Serialize(new {
                summaryPages = new[] { new { type = "summary", file = "00-OVERVIEW",
                                              title = "CONTROL OVERVIEW", kicker = "PHYSICAL CONTROLLERS",
                                              items } }
            });
            var updated = workspace with {
                Sources = new Dictionary<string, string> { ["config/kneeboard.json"] = manifest }
            };
            var image = Assert.Single(SnapshotSummaryKneeboardRenderer.Generate(updated)).Value;
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, image.Take(8));
            using var bitmap = SKBitmap.Decode(image);
            Assert.Equal(SKColor.Parse("#142b52"), bitmap.GetPixel(400, 300));
            Assert.Equal(SKColor.Parse("#15314e"), bitmap.GetPixel(400, 1000));
        }

        /// <summary>Missing or ambiguous authored pages and unsupported text fail closed.</summary>
        [Fact]
        public void RejectsIncompleteOverview()
        {
            var workspace = Workspace();
            Assert.Throws<InvalidDataException>(
                () => SnapshotSummaryKneeboardRenderer.Generate(workspace with { Sources = null }));
            Assert.Throws<InvalidDataException>(
                () => SnapshotSummaryKneeboardRenderer.Generate(workspace with { Pages = [] }));
            Assert.Throws<InvalidDataException>(
                () => SnapshotSummaryKneeboardRenderer.Generate(
                    workspace with { Pages = [..workspace.Pages, workspace.Pages[0]] }));
            Assert.Throws<InvalidDataException>(() => SnapshotSummaryKneeboardRenderer.Generate(workspace with {
                Sources =
                    new Dictionary<string, string> { ["config/kneeboard.json"] =
                                                         workspace.Sources!["config/kneeboard.json"].Replace(
                                                             "00-OVERVIEW", "../OVERVIEW", StringComparison.Ordinal) }
            }));
            Assert.Throws<InvalidDataException>(() => SnapshotSummaryKneeboardRenderer.Generate(workspace with {
                Sources = new Dictionary<string, string> { ["config/kneeboard.json"] =
                                                               workspace.Sources!["config/kneeboard.json"].Replace(
                                                                   "Flight controls", "Unsupported 🚀",
                                                                   StringComparison.Ordinal) }
            }));
        }
        /// <summary>Rejects malformed rows and manifest drift instead of silently emitting an incomplete PNG.</summary>
        [Fact]
        public void RejectsMalformedAuthoredSummaryRows()
        {
            var workspace = Workspace();
            static EditorWorkspaceSnapshot With(EditorWorkspaceSnapshot saved, string manifest) =>
                saved with { Sources = new Dictionary<string, string> { ["config/kneeboard.json"] = manifest } };
            const string valid = """{"summaryPages":[{"type":"summary","file":"00-OVERVIEW","title":"CONTROL OVERVIEW","kicker":"PHYSICAL CONTROLLERS","items":[{"key":"STICK","text":"Flight controls","accent":"red"}]}]}""";
            var blue = valid.Replace("\"accent\":\"red\"", "\"accent\":\"blue\"", StringComparison.Ordinal);
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
                         Assert.Single(SnapshotSummaryKneeboardRenderer.Generate(With(workspace, blue))).Value.Take(8));
            Assert.Throws<InvalidDataException>(() => SnapshotSummaryKneeboardRenderer.Generate(With(workspace, "{}")));
            Assert.Throws<InvalidDataException>(
                () => SnapshotSummaryKneeboardRenderer.Generate(With(workspace, """{"summaryPages":[]}""")));
            Assert.Throws<InvalidDataException>(
                () => SnapshotSummaryKneeboardRenderer.Generate(With(
                    workspace,
                    valid.Replace("\"items\":[{\"key\":\"STICK\",\"text\":\"Flight controls\",\"accent\":\"red\"}]",
                                  "\"items\":[]", StringComparison.Ordinal))));
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
                         Assert.Single(SnapshotSummaryKneeboardRenderer.Generate(With(
                             workspace, valid.Replace("\"accent\":\"red\"", "\"accent\":\"orange\"",
                                                      StringComparison.Ordinal)))).Value.Take(8));
            Assert.Throws<InvalidDataException>(
                () => SnapshotSummaryKneeboardRenderer.Generate(
                    With(workspace,
                         valid.Replace("\"title\":\"CONTROL OVERVIEW\"", "\"title\":\"\"", StringComparison.Ordinal))));
            Assert.Throws<InvalidDataException>(
                () => SnapshotSummaryKneeboardRenderer.Generate(
                    With(workspace, valid.Replace("Flight controls", new string('A', 200), StringComparison.Ordinal))));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static EditorWorkspaceSnapshot Workspace()
        {
            var id = Guid.Parse("323efbe0-6d8b-44d3-a2ab-840d16593de5");
            var module = new DcsModule(id, "TEST", "Test");
            var configuration = new ModuleConfiguration(module, [], [], [], "fixture");
            var page = new KneeboardPage(Guid.NewGuid(), id, null, KneeboardPageKind.Summary, "00-OVERVIEW", "Overview",
                                         [], 0);
            const string manifest = """{"summaryPages":[{"type":"summary","file":"00-OVERVIEW","title":"CONTROL OVERVIEW","kicker":"PHYSICAL CONTROLLERS","items":[{"key":"STICK","text":"Flight controls","accent":"red"}]}]}""";
            return new(configuration, [page], [new KneeboardImageAsset("00-OVERVIEW", "old.png", [1], "old")], [], [],
                       [], [], new Dictionary<string, string>(),
                       new Dictionary<string, string> { ["config/kneeboard.json"] = manifest });
        }
        #endregion END private helpers
    }
}
