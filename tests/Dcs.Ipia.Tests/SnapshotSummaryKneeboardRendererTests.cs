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

        /// <summary>Renders the complete Hornet overview and VAICOM text from the authored manifest.</summary>
        [Fact]
        public void GeneratesHornetOverviewAndVaicomSummary()
        {
            var workspace = Workspace();
            var overview = workspace.Pages[0] with { StableKey = "00-FA18C-CONTROL-OVERVIEW" };
            var voice = overview with { Id = Guid.NewGuid(), StableKey = "01-VAICOM-OVERVIEW", Order = 1 };
            const string manifest = """{"summaryPages":[{"type":"summary","file":"00-FA18C-CONTROL-OVERVIEW","title":"F/A-18C USED DEVICES","kicker":"PHYSICAL CONTROLLERS USED BY THIS CONFIGURATION","items":[{"key":"Left DDI","text":"Left DDI • OSBs • navigation • radar • FLIR • EW/RWR • stores","accent":"blue"},{"key":"Right MFD","text":"Right DDI • OSBs • SA/TSD • HARM • FLIR • weapons • engines","accent":"blue"},{"key":"MPCD","text":"MPCD • OSBs • checklist • FCS • fuel • stores • menu","accent":"blue"},{"key":"STICK","text":"MOZA Hornet • flight • trim • weapons • sensor control • AP/NWS","accent":"red"},{"key":"PDCP","text":"ALR-67 RWR • ALQ-165 ASPJ • threat display • ECM • VDI","accent":"purple"},{"key":"PEDALS","text":"TPR • rudder • left/right wheel brakes","accent":"blue"},{"key":"THROTTLE","text":"Warthog • comms • radar elevation • countermeasures • speedbrake • lights","accent":"blue"},{"key":"PTO2","text":"CarrierAce • gear • flaps • hook • launch bar • wing fold • refuel • lights","accent":"cyan"},{"key":"ICP","text":"ViperAce • keypad • COMM/IFF • A-A/A-G data entry","accent":"cyan"}]},{"type":"summary","file":"01-VAICOM-OVERVIEW","title":"VAICOM PRO + WARTHOG PTT","kicker":"AUTOHOTKEY V2 • VOICEATTACK / VAICOM PRO","items":[{"key":"TX1","text":"Warthog MIC forward / Joy6 -> VHF AM -> Left Ctrl + Left Alt + Left Shift + 1","accent":"blue"},{"key":"TX2","text":"Warthog MIC right / Joy3 -> UHF -> Left Ctrl + Left Alt + Left Shift + 2","accent":"blue"},{"key":"TX3","text":"Warthog MIC down / Joy4 -> VHF FM -> Left Ctrl + Left Alt + Left Shift + 3","accent":"blue"},{"key":"TX4","text":"Warthog MIC left / Joy5 -> AUTO -> Left Ctrl + Left Alt + Left Shift + 4","accent":"blue"},{"key":"TX5","text":"Warthog MIC push / Joy2 -> Interphone -> Left Ctrl + Left Alt + Left Shift + 5","accent":"blue"}]}]}""";
            var updated = workspace with {
                Pages = [overview, voice],
                Sources = new Dictionary<string, string> { ["config/kneeboard.json"] = manifest }
            };
            var images = SnapshotSummaryKneeboardRenderer.Generate(updated);
            Assert.Equal(2, images.Count);
            foreach (var file in new[] { "00-FA18C-CONTROL-OVERVIEW", "01-VAICOM-OVERVIEW" })
            {
                using var bitmap = SKBitmap.Decode(images["KNEEBOARD/TEST/" + file + ".png"]);
                Assert.Equal(1200, bitmap.Width);
                Assert.Equal(1600, bitmap.Height);
            }
        }

        /// <summary>The complete shared summary palette occupies the authored card positions.</summary>
        [Fact]
        public void UsesSharedCardPaletteForOverviewAndVoicePages()
        {
            var workspace = Workspace();
            var accents = new[] { "cyan", "gold", "red", "blue", "orange", "purple", "green", "unknown" };
            var items = accents.Select((accent, index) =>
                                         new { key = "D" + index, text = "Voice command " + index, accent });
            var manifest = System.Text.Json.JsonSerializer.Serialize(new {
                summaryPages = new[] { new { type = "summary", file = "00-OVERVIEW",
                                              title = "VAICOM OVERVIEW", kicker = "VOICE CONTROLS", items } }
            });
            var updated = workspace with {
                Sources = new Dictionary<string, string> { ["config/kneeboard.json"] = manifest }
            };
            using var bitmap = SKBitmap.Decode(Assert.Single(SnapshotSummaryKneeboardRenderer.Generate(updated)).Value);
            Assert.Equal(SKColor.Parse("#15314e"), bitmap.GetPixel(400, 300));
            Assert.Equal(SKColor.Parse("#3e2d12"), bitmap.GetPixel(880, 300));
            Assert.Equal(SKColor.Parse("#38131c"), bitmap.GetPixel(400, 470));
            Assert.Equal(SKColor.Parse("#412511"), bitmap.GetPixel(400, 640));
            Assert.Equal(SKColor.Parse("#153526"), bitmap.GetPixel(400, 810));
            Assert.Equal(SKColor.Parse("#15314e"), bitmap.GetPixel(880, 810));
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
            var tooMany = System.Text.Json.JsonSerializer.Serialize(new {
                summaryPages = new[] { new { type = "summary", file = "00-OVERVIEW",
                                              title = "CONTROL OVERVIEW", kicker = "PHYSICAL CONTROLLERS",
                                              items = Enumerable.Range(0, 15).Select(i => new {
                                                  key = "D" + i, text = "Controls", accent = "blue"
                                              }) } }
            });
            Assert.Throws<InvalidDataException>(() => SnapshotSummaryKneeboardRenderer.Generate(
                With(workspace, tooMany)));
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
