using System.IO.Compression;
using System.Text.Json;
using Heimdall.Application;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Heimdall.Rendering;
using SkiaSharp;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Verifies independent shared Lua, pinned kneeboards, deterministic archives and empty-database restore.</summary>
    public sealed class SharedUiLayerOutputTests
    {
        private const string Root = "assets/shared/ui-layer/";
        private const string Profile = Root + "input/UiLayer/joystick/Stick.diff.lua";
        #region BEGIN Public
        /// <summary>The portable archive regenerates identical bytes without source assets or module storage.</summary>
        [Fact]
        public async Task Rebuilds_exact_output_and_restores_into_empty_database()
        {
            var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
            var restoredFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
            try
            {
                var connection = "Data Source=" + file + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var defaults = new SqliteSharedUiLayerWorkspaceRepository(connection);
                var saved = await defaults.SaveAsync(Defaults());
                var hardware = Hardware();
                var frozen = SharedUiLayerOutputSerializer.Freeze(saved, new string('b', 40), SharedUiLayerHardwareMapping.Resolve(saved, [hardware]));
                var immutable = SharedUiLayerOutputSerializer.Serialize(frozen);
                var portable = SharedUiLayerOutputSerializer.Deserialize(immutable);
                Assert.Equal(immutable.Content, SharedUiLayerOutputSerializer.Serialize(portable).Content);
                Assert.Equal(saved.Sources[Profile], SharedUiLayerOutputSerializer.GenerateLua(portable)["Config/Input/UiLayer/joystick/Stick.diff.lua"]);
                Assert.Equal(saved.Sources[Root + "input/UiLayer/modifiers.lua"], SharedUiLayerOutputSerializer.GenerateLua(portable)["Config/Input/UiLayer/modifiers.lua"]);
                var projection = SharedUiLayerOutputSerializer.CreatePreview(portable);
                var page = KneeboardDraftProjection.CreateCombined(projection, projection.Diagrams[0], projection.Assignments, projection.Labels);
                Assert.Contains("New", CompositeKneeboardRenderer.BuildDeviceSvg(projection.Diagrams[0], page));
                var pngs = SharedUiLayerKneeboardRenderer.Generate(projection);
                using var png = SKBitmap.Decode(Assert.Single(pngs).Value);
                Assert.Equal(1200, png.Width);
                Assert.Equal(1600, png.Height);
                var archive = SharedUiLayerOutputArchive.Build(portable, pngs);
                var reopened = SharedUiLayerOutputArchive.Read(archive);
                Assert.Equal(archive, SharedUiLayerOutputArchive.Build(reopened,
                    SharedUiLayerKneeboardRenderer.Generate(SharedUiLayerOutputSerializer.CreatePreview(reopened))));
                using (var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read))
                {
                    using var reference = new StreamReader(zip.GetEntry("bindings.txt")!.Open());
                    var text = reference.ReadToEnd();
                    Assert.Contains("JOY_BTN99", text);
                    Assert.Contains("Keyboard.diff.lua", text);
                }
                var restoredConnection = "Data Source=" + restoredFile + ";Pooling=False";
                await new SqliteMigrationRunner(restoredConnection).MigrateAsync();
                var restored = new SqliteSharedUiLayerWorkspaceRepository(restoredConnection);
                var revision = await restored.SaveAsync(reopened.Defaults with { Revision = 0, ContentSha256 = null });
                Assert.Equal(saved.ContentSha256, revision.ContentSha256);
                Assert.Empty(await new SqliteEditorWorkspaceRepository(restoredConnection).ListAsync());
                var later = await restored.SaveAsync(reopened.Defaults with { Revision = revision.Revision, ContentSha256 = null });
                Assert.Equal(2, later.Revision);
                Assert.Equal(saved.ContentSha256, later.ContentSha256);
                Assert.Throws<InvalidDataException>(() => SharedUiLayerOutputSerializer.Deserialize(immutable with { Sha256 = new string('0', 64) }));
                Assert.Throws<InvalidDataException>(() => SharedUiLayerOutputSerializer.Deserialize(immutable with {
                    Content = immutable.Content.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":2", StringComparison.Ordinal) }));
                Assert.Throws<InvalidDataException>(() => SharedUiLayerOutputSerializer.Freeze(saved, "bad", frozen.Diagrams));
                Assert.Throws<InvalidDataException>(() => SharedUiLayerOutputSerializer.Freeze(saved, new string('b', 40), new Dictionary<string, UiLayerDiagram>()));
                Assert.Throws<InvalidDataException>(() => SharedUiLayerOutputArchive.Build(portable, new Dictionary<string, byte[]>()));
            }
            finally { File.Delete(file); File.Delete(restoredFile); }
        }
        /// <summary>Custom profiles require explicit saved mappings, which survive snapshot export and revision saves.</summary>
        [Fact]
        public async Task Requires_mapping_for_custom_or_ambiguous_hardware()
        {
            var draft = Defaults();
            var sources = new Dictionary<string, string>(draft.Sources);
            sources[Profile.Replace("Stick", "Custom", StringComparison.Ordinal)] = sources[Profile];
            sources.Remove(Profile);
            draft = draft with { Sources = sources };
            var hardware = Hardware();
            var unresolved = SharedUiLayerHardwareMapping.Resolve(draft, [hardware]);
            Assert.Empty(unresolved);
            var path = Profile.Replace("Stick", "Custom", StringComparison.Ordinal);
            var mapped = SharedUiLayerHardwareMapping.Set(draft, path, hardware);
            Assert.Equal("stick.svg", SharedUiLayerHardwareMapping.Resolve(mapped, [hardware])[path].Path);
            Assert.Equal(draft.Sources[path], mapped.Sources[path]);
            var qualified = SharedUiLayerProfileEditor.Add(Defaults(), "Custom {11111111-1111-1111-1111-111111111111}.diff.lua");
            var custom = SharedUiLayerProfileEditor.Profiles(qualified).Single(x => x.FileName.StartsWith("Custom", StringComparison.Ordinal));
            qualified = SharedUiLayerHardwareMapping.Set(qualified, custom.Path, hardware);
            var renamed = SharedUiLayerProfileEditor.Rename(qualified, custom, "Renamed {11111111-1111-1111-1111-111111111111}.diff.lua");
            Assert.Equal(2, SharedUiLayerHardwareMapping.Resolve(renamed, [hardware]).Count);
            var other = hardware with { DeviceId = "other", DefaultFileName = "Other.diff.lua", Diagram = hardware.Diagram with { Path = "other.svg" } };
            var renamedProfile = SharedUiLayerProfileEditor.Profiles(renamed).Single(x => x.FileName.StartsWith("Renamed", StringComparison.Ordinal));
            var remapped = SharedUiLayerHardwareMapping.Set(renamed, renamedProfile.Path, other);
            Assert.Equal("other.svg", SharedUiLayerHardwareMapping.Resolve(remapped, [hardware, other])[renamedProfile.Path].Path);


            Assert.Throws<ArgumentException>(() => SharedUiLayerHardwareMapping.Set(draft, "missing", hardware));
            Assert.Throws<InvalidDataException>(() => SharedUiLayerHardwareMapping.Resolve(Defaults(), [hardware,
                hardware with { DeviceId = "other", Diagram = hardware.Diagram with { Path = "other.svg" } }]));
            var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
            try
            {
                var connection = "Data Source=" + file + ";Pooling=False";
                await new SqliteMigrationRunner(connection).MigrateAsync();
                var repository = new SqliteSharedUiLayerWorkspaceRepository(connection);
                var saved = await repository.SaveAsync(mapped);
                Assert.Equal("stick.svg", SharedUiLayerHardwareMapping.Resolve((await repository.GetAsync())!, [hardware])[path].Path);
                var frozen = SharedUiLayerOutputSerializer.Freeze(saved, new string('b', 40), SharedUiLayerHardwareMapping.Resolve(saved, [hardware]));
                hardware.Diagram.Background[0] = 9;
                Assert.Equal(1, frozen.Diagrams[path].Background[0]);
            }
            finally { File.Delete(file); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static SharedUiLayerWorkspace Defaults() => new(0, new string('a', 40), new Dictionary<string, string> {
            [Root + "functions.json"] = "{\"schema\":1,\"functions\":[{\"id\":\"new\",\"command\":\"NEW\",\"label\":\"New\",\"category\":\"UI\"}]}",
            [Root + "input/UiLayer/modifiers.lua"] = """
                local modifiers = { ["SHIFT"] = { ["device"] = "Keyboard", ["key"] = "LShift", ["switch"] = true } }
                return modifiers
                """,
            [Root + "input/UiLayer/keyboard/Keyboard.diff.lua"] = """
                local diff = { ["keyDiffs"] = { ["NEW"] = { ["name"] = "New", ["added"] = { [1] = { ["key"] = "K" } } } } }
                return diff
                """,
            [Profile] = """
                local diff = {
                  ["keyDiffs"] = {
                    ["NEW"] = { ["name"] = "New", ["added"] = {
                      [1] = { ["key"] = "JOY_BTN1", ["reformers"] = { [1] = "SHIFT" } },
                      [2] = { ["key"] = "JOY_BTN99" }
                    } },
                    ["HISTORY"] = { ["name"] = "History", ["removed"] = { [1] = { ["key"] = "JOY_BTN90" } } }
                  },
                  ["axisDiffs"] = { ["AXIS"] = { ["name"] = "Axis", ["added"] = {
                    [1] = { ["key"] = "JOY_X", ["filter"] = { ["deadzone"] = 3, ["invert"] = true } }
                  } } }
                }
                return diff
                """
        });
        private static WorkspaceHardware Hardware() => new("stick", "Stick", "Stick.diff.lua",
            new("Stick", null, "stick.svg", 100, 100, [1, 2, 3], []) {
                Svg = "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100' viewBox='0 0 100 100'><rect width='100' height='100' fill='black'/><text id='lbl-one' x='50' y='50'>Old</text><text id='lbl-axis' x='50' y='75'>Old</text></svg>",
                Controls = [new("one", "Button 1", "JOY_BTN1", 40, 40, 20, 10), new("axis", "Axis", "JOY_X", 40, 65, 20, 10, true)]
            });
        #endregion END private helpers
    }
}
