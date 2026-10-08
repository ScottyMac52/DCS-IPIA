using System.IO.Compression;
using System.Text;
using Dcs.Ipia.Application;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Xunit;

namespace Dcs.Ipia.Tests
{
    /// <summary>Checks the saved workspace produces a complete reproducible OvGME ZIP.</summary>
    public sealed class SnapshotOvGmeComposerTests
    {
        private static readonly Guid ModuleId = Guid.Parse("43f7a1b3-0b96-414d-ae38-d9fd95a53fc0");
        private static readonly Guid ProfileId = Guid.Parse("270d1ec9-1b10-4fa4-93a1-c58b4ac2b9ea");
        private const string UiPath = "assets/shared/ui-layer/input/UiLayer/joystick/Stick.diff.lua";
        #region BEGIN Public
        /// <summary>Generated Lua, device PNG and summary PNG coexist under the consumer layout.</summary>
        [Fact]
        public void BuildsCompleteDeterministicArchiveFromSavedState()
        {
            var workspace = Workspace();
            var first = SnapshotOvGmeComposer.Build(workspace, "v1");
            var second = SnapshotOvGmeComposer.Build(workspace with { Images = [] }, "v1");
            Assert.Equal(first.Bytes, second.Bytes);
            Assert.Equal("DCS-TEST-Components-v1-OVGME.zip", first.FileName);
            Assert.Contains(first.Sha256, first.ChecksumLine);
            using var stream = new MemoryStream(first.Bytes);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            var root = "DCS-TEST-Components-v1-OVGME/";
            Assert.Contains(zip.Entries, x => x.FullName == root && x.Name.Length == 0);
            Assert.Equal(new[] {
                "README.TXT", "VERSION.TXT",
                root + "Config/Input/TEST/joystick/Stick.diff.lua",
                root + "Config/Input/TEST/modifiers.lua",
                root + "Config/Input/UiLayer/joystick/Stick.diff.lua",
                root + "Config/Input/UiLayer/modifiers.lua",
                root + "KNEEBOARD/TEST/00-OVERVIEW.png",
                root + "KNEEBOARD/TEST/01-DEVICE.png"
            }, zip.Entries.Where(x => x.Name.Length > 0).Select(x => x.FullName));
            Assert.Contains(DcsLuaParser.ParseProfile(Text(zip, root + "Config/Input/TEST/joystick/Stick.diff.lua"),
                                                       ProfileId), x => x.CommandId == "M");
            Assert.Contains(DcsLuaParser.ParseProfile(Text(zip, root + "Config/Input/UiLayer/joystick/Stick.diff.lua"),
                                                       ProfileId), x => x.CommandId == "U");
            Assert.Equal("SHIFT", Assert.Single(DcsLuaParser.ParseModifiers(
                Text(zip, root + "Config/Input/UiLayer/modifiers.lua")), x => x.Name == "SHIFT").Name);
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
                         Bytes(zip, root + "KNEEBOARD/TEST/01-DEVICE.png").Take(8));
        }

        /// <summary>The selected output folder receives a matching ZIP and standalone checksum.</summary>
        [Fact]
        public void WritesArtifactsAndProtectsExistingBuild()
        {
            var folder = Path.Combine(Path.GetTempPath(), "ipia-ovgme-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var package = SnapshotOvGmeComposer.Build(Workspace(), "v1");
                var paths = OvGmeArtifactWriter.Write(package, folder);
                Assert.Equal(package.Bytes, File.ReadAllBytes(paths.ArchivePath));
                Assert.Equal(package.ChecksumLine, File.ReadAllText(paths.ChecksumPath));
                Assert.Equal(package.Sha256, paths.Sha256);
                Assert.Throws<IOException>(() => OvGmeArtifactWriter.Write(package, folder));
                Assert.Equal(package.Bytes, File.ReadAllBytes(paths.ArchivePath));
                Assert.Equal(2, Directory.GetFiles(folder).Length);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <summary>Invalid packages cannot leave misleading output in the selected folder.</summary>
        [Fact]
        public void RejectsMismatchedPackageBeforeWriting()
        {
            var folder = Path.Combine(Path.GetTempPath(), "ipia-ovgme-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var package = SnapshotOvGmeComposer.Build(Workspace(), "v1");
                Assert.Throws<InvalidDataException>(() =>
                    OvGmeArtifactWriter.Write(package with { Sha256 = new string('0', 64) }, folder));
                Assert.Empty(Directory.GetFiles(folder));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <summary>A saved assignment changes both generated files, while missing assets fail closed.</summary>
        [Fact]
        public void EditsChangeLuaAndPngAndMissingAssetsRejectBuild()
        {
            var workspace = Workspace();
            var first = SnapshotOvGmeComposer.Build(workspace, "v1");
            var changed = workspace with {
                Assignments = [workspace.Assignments[0] with {
                    Command = new("M2", "Changed action", false, AssignmentLayer.Module)
                }, workspace.Assignments[1]]
            };
            var second = SnapshotOvGmeComposer.Build(changed, "v1");
            Assert.NotEqual(first.Bytes, second.Bytes);
            using var oldStream = new MemoryStream(first.Bytes);
            using var newStream = new MemoryStream(second.Bytes);
            using var oldZip = new ZipArchive(oldStream, ZipArchiveMode.Read);
            using var newZip = new ZipArchive(newStream, ZipArchiveMode.Read);
            const string root = "DCS-TEST-Components-v1-OVGME/";
            Assert.NotEqual(Bytes(oldZip, root + "Config/Input/TEST/joystick/Stick.diff.lua"),
                            Bytes(newZip, root + "Config/Input/TEST/joystick/Stick.diff.lua"));
            Assert.NotEqual(Bytes(oldZip, root + "KNEEBOARD/TEST/01-DEVICE.png"),
                            Bytes(newZip, root + "KNEEBOARD/TEST/01-DEVICE.png"));
            Assert.Throws<InvalidDataException>(() => SnapshotOvGmeComposer.Build(
                workspace with { Sources = null }, "v1"));
            Assert.Throws<InvalidDataException>(() => SnapshotOvGmeComposer.Build(
                workspace with { Pages = [workspace.Pages[0]] }, "v1"));
        }
        #endregion END Public
        #region BEGIN private helpers
        private static byte[] Bytes(ZipArchive zip, string path)
        {
            using var input = zip.GetEntry(path)!.Open();
            using var output = new MemoryStream();
            input.CopyTo(output);
            return output.ToArray();
        }
        private static string Text(ZipArchive zip, string path) => Encoding.UTF8.GetString(Bytes(zip, path));
        private static EditorWorkspaceSnapshot Workspace()
        {
            var profile = new DeviceProfile(ProfileId, "stick", null, "Stick", "Stick.diff.lua");
            var config = new ModuleConfiguration(new DcsModule(ModuleId, "TEST", "Test"),
                                                 [profile], [], [], "fixture");
            var diagram = new UiLayerDiagram("Stick", null, "stick.svg", 100, 100, [], []) {
                ProfileId = ProfileId, CanonicalUiLayerSourcePath = UiPath,
                Controls = [new("one", "One", "JOY_BTN1", 0, 0, 10, 10)],
                Svg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><rect width='100' height='100' fill='black'/><text id='lbl-one' x='20' y='20' fill='white'>Old</text></svg>"
            };
            var moduleSlot = new DraftAssignmentSlot(ModuleId, ProfileId, AssignmentLayer.Module,
                                                     BindingSection.Key, "JOY_BTN1");
            var uiSlot = new DraftAssignmentSlot(ModuleId, ProfileId, AssignmentLayer.UiLayer,
                                                 BindingSection.Key, "JOY_BTN1", "SHIFT");
            var assignments = new[] {
                new DraftAssignment(moduleSlot, new("M", "Module action", false, AssignmentLayer.Module)),
                new DraftAssignment(uiSlot, new("U", "UI action", false, AssignmentLayer.UiLayer))
            };
            var pages = new[] {
                new KneeboardPage(Guid.NewGuid(), ModuleId, null, KneeboardPageKind.Device,
                                  "01-DEVICE", "Device", [], 1),
                new KneeboardPage(Guid.NewGuid(), ModuleId, null, KneeboardPageKind.Summary,
                                  "00-OVERVIEW", "Overview", [], 0)
            };
            var sources = new Dictionary<string, string> {
                ["config/kneeboard.json"] = """
                    {"profiles":{"stick":"Stick.diff.lua"},
                     "pages":[{"profile":"stick","file":"01-DEVICE","title":"Stick"}],
                     "summaryPages":[{"type":"summary","file":"00-OVERVIEW","title":"CONTROL OVERVIEW",
                                      "kicker":"PHYSICAL CONTROLLERS","items":[
                                        {"key":"STICK","text":"Flight controls","accent":"red"}]}]}
                    """,
                [UiPath] = """local diff = { ["keyDiffs"] = {}, ["axisDiffs"] = {} } return diff""",
                ["assets/shared/ui-layer/input/UiLayer/modifiers.lua"] =
                    """local modifiers = { ["SHIFT"] = { ["device"] = "Stick", ["key"] = "JOY_BTN3", ["switch"] = false } } return modifiers"""
            };
            return new(config, pages, [new("01-DEVICE", "old.png", [1], "ignored")], [diagram],
                       [], [], assignments, new Dictionary<string, string>(), sources);
        }
        #endregion END private helpers
    }
}

