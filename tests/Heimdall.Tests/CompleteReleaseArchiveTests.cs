using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Heimdall.Packaging;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Checks distribution determinism, independent integrity and malicious/incomplete archive rejection.</summary>
    public sealed class CompleteReleaseArchiveTests
    {
        #region BEGIN Public
        /// <summary>Every file is checksummed and the unchanged nested OvGME archive verifies independently.</summary>
        [Fact]
        public void Complete_archive_is_deterministic_and_preserves_nested_payload()
        {
            var files = Files();
            var first = DcsCompleteReleaseArchive.Build(Root, files);
            var second = DcsCompleteReleaseArchive.Build(Root, files.Reverse().ToDictionary(x => x.Key, x => x.Value));
            Assert.Equal(first.Bytes, second.Bytes);
            var verified = DcsCompleteReleaseVerifier.Verify(first);
            Assert.Equal(files["OVGME/DCS-TEST-Components-1-OVGME.zip"], verified["OVGME/DCS-TEST-Components-1-OVGME.zip"]);
            using var zip = new ZipArchive(new MemoryStream(first.Bytes), ZipArchiveMode.Read);
            Assert.All(zip.Entries, x => Assert.Equal(1980, x.LastWriteTime.Year));
            Assert.Throws<InvalidDataException>(() => DcsCompleteReleaseVerifier.Verify(first with { Sha256 = "bad" }));
            Assert.Throws<InvalidDataException>(() => DcsCompleteReleaseVerifier.Verify(first with { ChecksumLine = "bad" }));
            Assert.Throws<ArgumentNullException>(() => DcsCompleteReleaseArchive.Build(Root, null!));
            Assert.Throws<ArgumentException>(() => DcsCompleteReleaseArchive.Build("../bad", files));
            Assert.Throws<ArgumentException>(() => DcsCompleteReleaseArchive.Build(Root, new Dictionary<string, byte[]> { ["a"] = null! }));
            Assert.Throws<ArgumentException>(() => DcsCompleteReleaseArchive.Build(Root, new Dictionary<string, byte[]> { ["SHA256SUMS.txt"] = [] }));
            Assert.Throws<ArgumentException>(() => DcsCompleteReleaseArchive.Build(Root, new Dictionary<string, byte[]> { ["A"] = [1], ["a"] = [2] }));
            Assert.Throws<ArgumentNullException>(() => DcsCompleteReleaseVerifier.Verify(null!));
        }

        /// <summary>Traversal, Windows aliases and control characters cannot enter distribution archives.</summary>
        /// <param name="path">Unsafe cross-platform path.</param>
        [Theory]
        [InlineData("../x")]
        [InlineData("/x")]
        [InlineData("x\\y")]
        [InlineData("x:y")]
        [InlineData("x?y")]
        [InlineData("x|y")]
        [InlineData("x//y")]
        [InlineData("x/./y")]
        [InlineData("x.")]
        [InlineData("x ")]
        [InlineData("x\ny")]
        [InlineData("")]
        public void Unsafe_paths_are_rejected(string path) =>
            Assert.Throws<InvalidDataException>(() => DcsCompleteReleaseArchive.Build(Root, new Dictionary<string, byte[]> { [path] = [1] }));

        /// <summary>Complete checksums cannot mask absent metadata, a wrong root, duplicate entries or broken deployable output.</summary>
        [Fact]
        public void Independent_verifier_rejects_incomplete_and_tampered_archives()
        {
            var valid = Files();
            var missing = valid.Where(x => x.Key != "VERSION.TXT").ToDictionary(x => x.Key, x => x.Value);
            Assert.Throws<InvalidDataException>(() => DcsCompleteReleaseVerifier.Verify(DcsCompleteReleaseArchive.Build(Root, missing)));
            var noOvgme = valid.Where(x => !x.Key.StartsWith("OVGME/", StringComparison.Ordinal)).ToDictionary(x => x.Key, x => x.Value);
            Assert.Throws<InvalidDataException>(() => DcsCompleteReleaseVerifier.Verify(DcsCompleteReleaseArchive.Build(Root, noOvgme)));
            valid["OVGME/DCS-TEST-Components-1-OVGME.zip"] = [1, 2];
            Assert.Throws<InvalidDataException>(() => DcsCompleteReleaseVerifier.Verify(DcsCompleteReleaseArchive.Build(Root, valid)));
            Assert.Throws<InvalidDataException>(() => DcsCompleteReleaseVerifier.Verify(Raw([(Root + "/VERSION.TXT", "1")])));
            Assert.Throws<InvalidDataException>(() => DcsCompleteReleaseVerifier.Verify(Raw([(Root + "/SHA256SUMS.txt", "bad")] )));
            Assert.Throws<InvalidDataException>(() => DcsCompleteReleaseVerifier.Verify(Raw([("outside/file", "bad")])));
            Assert.Throws<InvalidDataException>(() => DcsCompleteReleaseVerifier.Verify(Raw([(Root + "/a", "1"), (Root + "/A", "2")])));
        }
        #endregion END Public
        #region BEGIN private helpers
        private const string Root = "DCS-TEST-Components-1-Complete";
        private static Dictionary<string, byte[]> Files()
        {
            var ovgme = DcsOvGmePackageBuilder.Build(new("DCS-TEST-Components", "TEST", "1", new Dictionary<string, byte[]> {
                ["Config/Input/TEST/joystick/Stick.diff.lua"] = Encoding.UTF8.GetBytes("local diff = {} return diff"),
                ["KNEEBOARD/TEST/overview.png"] = [137, 80, 78, 71] }));
            return new() { ["VERSION.TXT"] = Encoding.UTF8.GetBytes("1\n"), ["OVGME/" + ovgme.FileName] = ovgme.Bytes,
                ["Documentation/README.md"] = [1], ["Documentation/INSTALLATION.md"] = [1],
                ["Documentation/CONTROL-MAPPINGS.md"] = [1], ["Documentation/THIRD-PARTY-ASSETS.md"] = [1],
                ["Documentation/RELEASE-NOTES.md"] = [1] };
        }
        private static DcsOvGmePackage Raw(IReadOnlyList<(string Path, string Content)> files)
        {
            using var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
                foreach (var file in files)
                {
                    using var writer = new StreamWriter(zip.CreateEntry(file.Path).Open());
                    writer.Write(file.Content);
                }
            var bytes = stream.ToArray();
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            return new(Root + ".zip", bytes, hash, $"{hash}  {Root}.zip\n");
        }
        #endregion END private helpers
    }
}
