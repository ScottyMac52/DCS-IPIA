using System.Text;
using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Verifies reviewed instruction imports, source preservation and malformed/stale input safeguards.</summary>
    public sealed class ModuleIntegrationImportTests
    {
        #region BEGIN Public
        /// <summary>JSON merges preserve other groups and all module/default state; removal preserves source history.</summary>
        [Fact]
        public void Reviewed_import_merges_without_changing_assignments_or_other_metadata()
        {
            var workspace = Workspace();
            var source = Json("OpenKneeboard", "First instructions");
            var review = ModuleIntegrationImport.Review(workspace, "instructions.json", source);
            Assert.Empty(ModuleIntegrationImport.Instructions(workspace));
            var first = ModuleIntegrationImport.Apply(workspace, review);
            var second = ModuleIntegrationImport.Apply(first, ModuleIntegrationImport.Review(first, "other.json", Json("VAICOM", "Explicit PTT guidance")));
            var updated = ModuleIntegrationImport.Apply(second, ModuleIntegrationImport.Review(second, "instructions.json", Json("openkneeboard", "Replacement")));
            Assert.Equal(2, ModuleIntegrationImport.Instructions(updated).Count);
            Assert.Contains(ModuleIntegrationImport.Instructions(updated), x => x.Name == "openkneeboard" && x.Instructions == "Replacement");
            Assert.Contains(ModuleIntegrationImport.Instructions(updated), x => x.Name == "VAICOM");
            Assert.Equal(workspace.Configuration, updated.Configuration);
            Assert.Equal(workspace.SharedDefault, updated.SharedDefault);
            Assert.Equal(workspace.Assignments, updated.Assignments);
            Assert.Equal(workspace.Labels, updated.Labels);
            Assert.Equal("keep", updated.Sources!["custom.json"]);
            Assert.Equal(3, updated.Sources.Count(x => x.Key.StartsWith("config/ipia-integration-imports/", StringComparison.Ordinal)));
            var removed = ModuleIntegrationImport.Remove(updated, "openkneeboard");
            Assert.Single(ModuleIntegrationImport.Instructions(removed));
            Assert.Equal(updated.Sources.Count, removed.Sources!.Count);
            var empty = ModuleIntegrationImport.Remove(removed, "VAICOM");
            Assert.Empty(ModuleIntegrationImport.Instructions(empty));
            Assert.Throws<InvalidOperationException>(() => ModuleIntegrationImport.Remove(empty, "missing"));
            Assert.Throws<InvalidOperationException>(() => ModuleIntegrationImport.Apply(first, review));
            Assert.Throws<InvalidDataException>(() => ModuleIntegrationImport.Apply(workspace, review with { Instructions = [new("Changed", "Forged")] }));
            var canonical = ModuleIntegrationMetadata.Serialize([new("Z", "last"), new("A", "first")], "TEST");
            Assert.Equal(canonical, ModuleIntegrationMetadata.Serialize([new("A", "first"), new("Z", "last")], "TEST"));
            Assert.Equal(OrderedNames, ModuleIntegrationMetadata.Parse(canonical, "TEST").Select(x => x.Name));
            Assert.Throws<InvalidDataException>(() => ModuleIntegrationMetadata.Parse(canonical, "OTHER"));
            Assert.Throws<ArgumentNullException>(() => ModuleIntegrationMetadata.Parse(null!, "TEST"));
        }

        /// <summary>Real selected Markdown text survives as evidence, without persisting machine paths or loading scripts.</summary>
        [Fact]
        public void File_import_preserves_utf8_text_and_uses_only_safe_basename()
        {
            var path = Path.Combine(Path.GetTempPath(), "ipia-integration-" + Guid.NewGuid().ToString("N") + ".md");
            try
            {
                const string guide = "# Custom guide\r\n\r\nKeep the author’s physical inputs and paths.\r\n";
                File.WriteAllText(path, guide, new UTF8Encoding(false));
                var workspace = Workspace() with { Sources = null };
                var review = ModuleIntegrationImport.ReadFile(workspace, path);
                Assert.Equal(Path.GetFileName(path), review.FileName);
                Assert.Equal(guide, Assert.Single(review.Instructions).Instructions);
                var applied = ModuleIntegrationImport.Apply(workspace, review);
                Assert.Equal(guide, Assert.Single(ModuleIntegrationImport.Instructions(applied)).Instructions);
                Assert.Contains(applied.Sources!, x => x.Key.EndsWith("/" + review.FileName, StringComparison.Ordinal) && x.Value == guide);
                Assert.DoesNotContain(path, EditorSnapshotSerializer.Serialize(applied).Content, StringComparison.Ordinal);
                File.WriteAllText(path, guide, new UTF8Encoding(true));
                Assert.Equal(guide, ModuleIntegrationImport.ReadFile(workspace, path).Content);
                File.WriteAllBytes(path, new byte[4194305]);
                Assert.Throws<InvalidDataException>(() => ModuleIntegrationImport.ReadFile(workspace, path));
                File.WriteAllBytes(path, [255, 254, 255]);
                Assert.Throws<DecoderFallbackException>(() => ModuleIntegrationImport.ReadFile(workspace, path));
            }
            finally { File.Delete(path); }
        }

        /// <summary>Malformed shapes, wrong identity, empty groups and unsafe filenames are rejected before staging.</summary>
        /// <param name="fileName">Selected source name.</param>
        /// <param name="source">Untrusted source text.</param>
        [Theory]
        [InlineData("bad.lua", "script")]
        [InlineData("../guide.md", "guide")]
        [InlineData("guide.md ", "guide")]
        [InlineData("", "guide")]
        [InlineData("guide.md", " ")]
        [InlineData("guide.json", """{""")]
        [InlineData("guide.json", "null")]
        [InlineData("guide.json", """{}""")]
        [InlineData("guide.json", """{"schemaVersion":2,"integrations":[]}""")]
        [InlineData("guide.json", """{"schemaVersion":"1","integrations":[]}""")]
        [InlineData("guide.json", """{"schemaVersion":1,"integrations":{}}""")]
        [InlineData("guide.json", """{"schemaVersion":1,"integrations":[]}""")]
        [InlineData("guide.json", """{"schemaVersion":1,"integrations":[{}]}""")]
        [InlineData("guide.json", """{"schemaVersion":1,"integrations":[{"name":null,"instructions":"guide"}]}""")]
        [InlineData("guide.json", """{"schemaVersion":1,"integrations":[{"name":"X","instructions":null}]}""")]
        [InlineData("guide.json", """{"schemaVersion":1,"moduleDcsId":"OTHER","integrations":[]}""")]
        [InlineData("guide.json", """{"schemaVersion":1,"schemaVersion":1,"integrations":[]}""")]
        [InlineData("guide.json", """{"schemaVersion":1,"scripts":[],"integrations":[]}""")]
        [InlineData("guide.json", """{"schemaVersion":1,"integrations":[{"name":"X","name":"Y","instructions":"Guide"}]}""")]
        [InlineData("guide.json", """{"schemaVersion":1,"integrations":[{"name":"X","instructions":"Guide","asset":"ptt.ahk"}]}""")]
        public void Invalid_input_does_not_change_draft(string fileName, string source)
        {
            var workspace = Workspace();
            var before = EditorSnapshotSerializer.Serialize(workspace);
            Assert.Throws<InvalidDataException>(() => ModuleIntegrationImport.Review(workspace, fileName, source));
            Assert.Equal(before, EditorSnapshotSerializer.Serialize(workspace));
        }

        /// <summary>Ambiguous names and resource-limit violations fail canonical serialization and parsing.</summary>
        [Fact]
        public void Duplicate_names_control_characters_and_oversized_metadata_fail()
        {
            Assert.Throws<InvalidDataException>(() => ModuleIntegrationMetadata.Serialize([new("X", "guide"), new("x", "other")], "TEST"));
            Assert.Throws<InvalidDataException>(() => ModuleIntegrationMetadata.Serialize([new("X\n", "guide")], "TEST"));
            Assert.Throws<InvalidDataException>(() => ModuleIntegrationMetadata.Serialize([new("X", "\0")], "TEST"));
            Assert.Throws<InvalidDataException>(() => ModuleIntegrationMetadata.Serialize([new(new string('x', 201), "guide")], "TEST"));
            Assert.Throws<InvalidDataException>(() => ModuleIntegrationMetadata.Serialize([new("X", new string('x', 65537))], "TEST"));
            Assert.Throws<InvalidDataException>(() => ModuleIntegrationMetadata.Serialize(Enumerable.Range(0, 65).Select(x => new ModuleIntegrationInstruction("X" + x, "guide")), "TEST"));
            Assert.Throws<InvalidDataException>(() => ModuleIntegrationMetadata.Parse(new string('x', 1048577), "TEST"));
            Assert.Throws<InvalidDataException>(() => ModuleIntegrationMetadata.Serialize(Enumerable.Range(0, 25).Select(x => new ModuleIntegrationInstruction("X" + x, new string('x', 50000))), "TEST"));
            var workspace = Workspace();
            var review = ModuleIntegrationImport.Review(workspace, "guide.json", Json("X", "guide"));
            var changed = workspace with { Sources = new Dictionary<string, string> { ["custom.json"] = "changed" } };
            Assert.Throws<InvalidOperationException>(() => ModuleIntegrationImport.Apply(changed, review));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static readonly string[] OrderedNames = ["A", "Z"];
        private static string Json(string name, string instructions) => JsonSerializer.Serialize(new {
            schemaVersion = 1, integrations = new[] { new { name, instructions } }
        });
        private static EditorWorkspaceSnapshot Workspace() => new(new(new(Guid.NewGuid(), "TEST", "Test aircraft"), [], [], [], "local"),
            [], [], [], [], [], [], new Dictionary<string, string>(), new Dictionary<string, string> { ["custom.json"] = "keep" });
        #endregion END private helpers
    }
}
