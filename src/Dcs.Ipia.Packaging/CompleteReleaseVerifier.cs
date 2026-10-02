using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Dcs.Ipia.Packaging
{
    /// <summary>Independently verifies distribution paths, exact checksums and the nested deployable archive.</summary>
    public static class CompleteReleaseVerifier
    {
        #region BEGIN Public
        /// <summary>Reads and verifies every entry before returning distribution payload bytes.</summary>
        /// <param name="package">Complete distribution archive.</param>
        /// <returns>Verified root-relative files, including the internal checksum manifest.</returns>
        public static IReadOnlyDictionary<string, byte[]> Verify(OvGmePackage package)
        {
            ArgumentNullException.ThrowIfNull(package);
            var hash = Hash(package.Bytes);
            if (hash != package.Sha256 || package.ChecksumLine != $"{hash}  {package.FileName}\n" ||
                !package.FileName.EndsWith("-Complete.zip", StringComparison.Ordinal))
                throw new InvalidDataException("Complete release identity/checksum mismatch.");
            ValidatePath(package.FileName);
            if (package.FileName.Contains('/')) throw new InvalidDataException("Archive basename cannot contain directories.");
            var root = package.FileName[..^4] + "/";
            var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var stream = new MemoryStream(package.Bytes);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, false, Encoding.UTF8);
            foreach (var entry in zip.Entries)
            {
                ValidatePath(entry.FullName);
                if (!entry.FullName.StartsWith(root, StringComparison.Ordinal) || !seen.Add(entry.FullName) || entry.Length > 256L * 1024 * 1024)
                    throw new InvalidDataException("Unexpected or duplicate Complete release entry.");
                using var input = entry.Open();
                using var content = new MemoryStream();
                input.CopyTo(content);
                files.Add(entry.FullName[root.Length..], content.ToArray());
            }
            if (!files.TryGetValue("SHA256SUMS.txt", out var checksums)) throw new InvalidDataException("Missing internal checksums.");
            var expected = string.Concat(files.Where(x => x.Key != "SHA256SUMS.txt").Select(x => $"{Hash(x.Value)}  {x.Key}\n"));
            if (Encoding.UTF8.GetString(checksums) != expected) throw new InvalidDataException("Internal checksum mismatch.");
            foreach (var required in Required)
                if (!files.ContainsKey(required)) throw new InvalidDataException("Missing Complete release file: " + required);
            var ovgme = files.Where(x => x.Key.StartsWith("OVGME/", StringComparison.Ordinal)).ToArray();
            if (ovgme.Length != 1 || !ovgme[0].Key.EndsWith("-OVGME.zip", StringComparison.Ordinal))
                throw new InvalidDataException("Exactly one deployable OvGME ZIP is required.");
            var innerName = ovgme[0].Key[6..];
            var innerHash = Hash(ovgme[0].Value);
            OvGmePackageVerifier.Verify(new(innerName, ovgme[0].Value, innerHash, $"{innerHash}  {innerName}\n"));
            return files;
        }

        /// <summary>Rejects unsafe or ambiguous archive paths on every host operating system.</summary>
        /// <param name="path">ZIP-relative file path.</param>
        public static void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Any(char.IsControl) || path.IndexOfAny(['\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 ||
                path.Split('/').Any(x => x.Length == 0 || x is "." or ".." || x.EndsWith('.') || x.EndsWith(' ')))
                throw new InvalidDataException("Unsafe Complete release path.");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Hash(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));
        private static readonly string[] Required = ["VERSION.TXT", "Documentation/README.md", "Documentation/INSTALLATION.md",
            "Documentation/CONTROL-MAPPINGS.md", "Documentation/THIRD-PARTY-ASSETS.md", "Documentation/RELEASE-NOTES.md"];
        #endregion END private helpers
    }
}
