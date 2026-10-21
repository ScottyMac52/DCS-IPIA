using System.Text.Json;
using Heimdall.Application.Games;
using Heimdall.Infrastructure;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects named preset installation, reviewed replacement, exact backups and preference compatibility.</summary>
    public sealed class GameBindingsExportTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "heimdall-export-" + Guid.NewGuid().ToString("N"));
        #region BEGIN Public
        /// <summary>New output installs only its named preset; replacement preserves exact original bytes without changing selectors.</summary>
        [Fact]
        public void New_and_replaced_presets_preserve_selectors_and_back_up_exact_bytes()
        {
            var files = new Dictionary<string, string> { ["Flight.4.2.binds"] = "<Root/>", ["StartPreset.4.start"] = "Flight\n" };
            var plan = GameBindingsExport.Prepare(_root, files);
            Assert.False(Directory.Exists(_root)); Assert.Null(plan.ExpectedHash);
            Assert.Null(GameBindingsExport.Write(plan));
            var target = Path.Combine(_root, "Flight.4.2.binds"); Assert.Equal("<Root/>", File.ReadAllText(target));
            var selector = Path.Combine(_root, "StartPreset.4.start"); Assert.False(File.Exists(selector)); File.WriteAllText(selector, "Other\r\n");
            var original = new byte[] { 0xef, 0xbb, 0xbf, 65, 13, 10 }; File.WriteAllBytes(target, original);
            var reviewed = GameBindingsExport.Prepare(_root, files); Assert.NotNull(reviewed.ExpectedHash);
            var backup = GameBindingsExport.Write(reviewed); Assert.NotNull(backup);
            Assert.Equal(original, File.ReadAllBytes(backup!)); Assert.Equal("<Root/>", File.ReadAllText(target));
            Assert.Equal("Other\r\n", File.ReadAllText(selector)); Assert.DoesNotContain(Directory.GetFiles(_root), p => p.EndsWith(".tmp", StringComparison.Ordinal));
            Assert.Single(Directory.GetFiles(_root, "*.binds"));
        }
        /// <summary>A destination created, changed or deleted after review cannot be overwritten silently.</summary>
        [Theory]
        [InlineData("created")][InlineData("changed")][InlineData("deleted")]
        public void Changed_destination_revision_prevents_write(string change)
        {
            Directory.CreateDirectory(_root); var target = Path.Combine(_root, "Flight.4.2.binds");
            if (change != "created") File.WriteAllText(target, "original");
            var plan = GameBindingsExport.Prepare(_root, new Dictionary<string, string> { ["Flight.4.2.binds"] = "replacement" });
            if (change == "deleted") File.Delete(target); else File.WriteAllText(target, "external");
            Assert.Throws<InvalidOperationException>(() => GameBindingsExport.Write(plan));
            if (change != "deleted") Assert.Equal("external", File.ReadAllText(target)); else Assert.False(File.Exists(target));
            Assert.Empty(Directory.GetFiles(_root, "*.backup-*")); Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
        }
        /// <summary>Invalid payloads fail before creating the selected directory or a temporary output.</summary>
        [Theory]
        [InlineData("empty")][InlineData("two")][InlineData("path")][InlineData("traversal")]
        [InlineData("null")][InlineData("large")][InlineData("unicode")][InlineData("case")]
        public void Invalid_payloads_create_nothing(string failure)
        {
            var files = new Dictionary<string, string> { ["Flight.4.2.binds"] = "<Root/>" };
            switch (failure)
            {
                case "empty": files.Clear(); break;
                case "two": files["Second.binds"] = "<Root/>"; break;
                case "path": files.Clear(); files["../Flight.binds"] = "text"; break;
                case "traversal": files.Clear(); files["Flight..binds"] = "text"; break;
                case "null": files["Flight.4.2.binds"] = null!; break;
                case "large": files["Flight.4.2.binds"] = new string('x', 4 * 1024 * 1024 + 1); break;
                case "unicode": files["Flight.4.2.binds"] = new string('界', 2 * 1024 * 1024); break;
                case "case": files["FLIGHT.4.2.BINDS"] = "text"; break;
            }
            Assert.Throws<InvalidDataException>(() => GameBindingsExport.Prepare(_root, files)); Assert.False(Directory.Exists(_root));
        }
        /// <summary>Forged plans and unusable paths fail without changing existing files.</summary>
        [Fact]
        public void Invalid_plans_and_file_destinations_fail_safely()
        {
            Assert.Throws<ArgumentNullException>(() => GameBindingsExport.Write(null!));
            Assert.Throws<ArgumentException>(() => GameBindingsExport.Prepare(" ", new Dictionary<string, string>()));
            Assert.Throws<ArgumentNullException>(() => GameBindingsExport.Prepare(_root, null!));
            Assert.Throws<InvalidDataException>(() => GameBindingsExport.Write(new("relative", "Flight.binds", "text", null)));
            Assert.Throws<InvalidDataException>(() => GameBindingsExport.Write(new(_root, "../Flight.binds", "text", null)));
            Directory.CreateDirectory(_root); var blocker = Path.Combine(_root, "directory-is-file"); File.WriteAllText(blocker, "preserve");
            var plan = GameBindingsExport.Prepare(blocker, new Dictionary<string, string> { ["Flight.binds"] = "text" });
            Assert.Throws<IOException>(() => GameBindingsExport.Write(plan)); Assert.Equal("preserve", File.ReadAllText(blocker));
            var huge = Path.Combine(_root, "Huge.binds"); using (var stream = File.Create(huge)) stream.SetLength(4 * 1024 * 1024 + 1);
            Assert.Throws<InvalidDataException>(() => GameBindingsExport.Prepare(_root, new Dictionary<string, string> { ["Huge.binds"] = "text" }));
        }
        /// <summary>Preferences retain an optional absolute override across reopen and older selections use automatic detection.</summary>
        [Fact]
        public void Export_directory_preferences_are_optional_persisted_and_validated()
        {
            var store = new FileGameWorkspaceStore(_root); var selection = new GameSelectionState("elite-dangerous") { EliteBindingsDirectory = _root };
            store.SaveSelection(selection); Assert.Equal(selection, new FileGameWorkspaceStore(_root).ReadSelection());
            store.SaveSelection(selection with { EliteBindingsDirectory = null }); Assert.Null(store.ReadSelection().EliteBindingsDirectory);
            Assert.Null(JsonSerializer.Deserialize<GameSelectionState>("{\"BackendId\":\"dcs\"}")!.EliteBindingsDirectory);
            Assert.Throws<InvalidDataException>(() => store.SaveSelection(selection with { EliteBindingsDirectory = " " }));
            Assert.Throws<InvalidDataException>(() => store.SaveSelection(selection with { EliteBindingsDirectory = "relative" }));
            Assert.Throws<InvalidDataException>(() => store.SaveSelection(selection with { EliteBindingsDirectory = new string('x', 32768) }));
        }
        /// <summary>Symlinked destinations cannot redirect a preset write outside the reviewed directory.</summary>
        [Fact]
        public void Symlink_destinations_are_rejected_where_supported()
        {
            if (OperatingSystem.IsWindows()) return;
            Directory.CreateDirectory(_root); var link = Path.Combine(_root, "link"); Directory.CreateSymbolicLink(link, _root);
            Assert.Throws<InvalidDataException>(() => GameBindingsExport.Prepare(link, new Dictionary<string, string> { ["Flight.binds"] = "text" }));
            Directory.Delete(link);
        }
        /// <summary>Removes ephemeral exports and saved preferences.</summary>
        public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); GC.SuppressFinalize(this); }
        #endregion END Public
    }
}
