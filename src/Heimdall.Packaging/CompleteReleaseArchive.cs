using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Heimdall.Packaging
{
    /// <summary>Assembles deterministic, checksummed distribution archives outside the deployable OvGME payload.</summary>
    public static class CompleteReleaseArchive
    {
        #region BEGIN Public
        /// <summary>Builds a distribution ZIP with a complete internal file checksum manifest.</summary>
        /// <param name="root">Safe archive basename and root directory.</param>
        /// <param name="files">Generated distribution files, without the internal checksum manifest.</param>
        /// <returns>ZIP bytes and external SHA-256 checksum.</returns>
        public static OvGmePackage Build(string root, IReadOnlyDictionary<string, byte[]> files)
        {
            ArgumentNullException.ThrowIfNull(files);
            if (string.IsNullOrWhiteSpace(root) || root is "." or ".." || root.EndsWith('.') ||
                !root.All(x => char.IsAsciiLetterOrDigit(x) || x is '-' or '_' or '.'))
                throw new ArgumentException("Invalid Complete release root.", nameof(root));
            var payload = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (path, bytes) in files)
            {
                CompleteReleaseVerifier.ValidatePath(path);
                if (path.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase) || !seen.Add(path) || bytes is null)
                    throw new ArgumentException("Duplicate, reserved or empty release file.", nameof(files));
                payload.Add(path, bytes);
            }
            payload.Add("SHA256SUMS.txt", Encoding.UTF8.GetBytes(string.Concat(payload.Select(x => $"{Hash(x.Value)}  {x.Key}\n"))));
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true, Encoding.UTF8))
                foreach (var (path, bytes) in payload)
                {
                    var entry = archive.CreateEntry(root + "/" + path, CompressionLevel.NoCompression);
                    entry.LastWriteTime = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    using var output = entry.Open();
                    output.Write(bytes);
                }
            var result = stream.ToArray();
            var name = root + ".zip";
            var hash = Hash(result);
            return new(name, result, hash, $"{hash}  {name}\n");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Hash(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));
        #endregion END private helpers
    }
}
