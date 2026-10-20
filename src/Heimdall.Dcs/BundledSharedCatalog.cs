using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Heimdall.Dcs
{
    /// <summary>Reads the versioned shared assets distributed with HEIMDALL, preserving original source identities.</summary>
    public static class BundledSharedCatalog
    {
        #region BEGIN Public
        /// <summary>Gets the shared asset directory beside the application binaries.</summary>
        public static string Root => Path.Combine(AppContext.BaseDirectory, "SharedAssets");
        /// <summary>Determines whether a source is the application-owned shared bundle.</summary>
        /// <param name="root">Selected source directory.</param>
        /// <returns>True for the packaged shared asset root.</returns>
        public static bool IsRoot(string root) => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)),
            Path.TrimEndingDirectorySeparator(Root), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        /// <summary>Checks every bundled source against its original Git blob identity before import.</summary>
        /// <param name="root">Directory containing the bundle and provenance manifest.</param>
        /// <returns>Pinned DCS-Common source commit.</returns>
        [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "SHA-1 is required to verify original Git blob IDs; this is source interoperability, not authentication.")]
        public static string Validate(string root)
        {
            using var manifest = ReadManifest(root);
            var commit = manifest.RootElement.GetProperty("sourceCommit").GetString()!;
            if (commit.Length != 40 || !commit.All(Uri.IsHexDigit))
                throw new InvalidDataException("Shared asset source commit is invalid.");
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in manifest.RootElement.GetProperty("files").EnumerateArray())
            {
                var path = item.GetProperty("path").GetString()!;
                if (!paths.Add(path)) throw new InvalidDataException("Duplicate shared asset: " + path);
                var target = ResolvePath(root, path);
                if (!File.Exists(target)) throw new InvalidDataException("Shared asset is missing: " + path);
                var bytes = File.ReadAllBytes(target);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
                hash.AppendData(Encoding.UTF8.GetBytes($"blob {bytes.Length}\0"));
                hash.AppendData(bytes);
                if (bytes.Length != item.GetProperty("size").GetInt64() ||
                    Convert.ToHexStringLower(hash.GetHashAndReset()) != item.GetProperty("gitBlob").GetString())
                    throw new InvalidDataException("Shared asset differs from its pinned source: " + path);
            }
            if (paths.Count == 0) throw new InvalidDataException("Shared asset bundle is empty.");
            return commit;
        }
        /// <summary>Gets the source commit carried by the bundle manifest.</summary>
        /// <returns>Pinned source commit, independent of a local Git checkout.</returns>
        public static string SourceCommit()
        {
            using var manifest = ReadManifest(Root);
            return manifest.RootElement.GetProperty("sourceCommit").GetString()!;
        }
        /// <summary>Lists pinned asset paths beneath requested source directories.</summary>
        /// <param name="prefixes">Original source directory prefixes.</param>
        /// <returns>Ordered source paths represented by the bundle.</returns>
        public static string[] Paths(params string[] prefixes)
        {
            using var manifest = ReadManifest(Root);
            return manifest.RootElement.GetProperty("files").EnumerateArray()
                .Select(x => x.GetProperty("path").GetString()!)
                .Where(path => prefixes.Any(prefix => path == prefix || path.StartsWith(prefix + "/", StringComparison.Ordinal)))
                .Order(StringComparer.Ordinal).ToArray();
        }
        /// <summary>Reads preserved source text using its original DCS-Common path.</summary>
        /// <param name="path">Original asset path.</param>
        /// <param name="cancellationToken">Cancellation for file reads.</param>
        /// <returns>Bundled source text.</returns>
        public static Task<string> ReadTextAsync(string path, CancellationToken cancellationToken = default) =>
            File.ReadAllTextAsync(ResolvePath(Root, path), cancellationToken);
        #endregion END Public
        #region BEGIN private helpers
        private static JsonDocument ReadManifest(string root) => JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "provenance.json")));
        private static string ResolvePath(string root, string path)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or "..") ||
                !(path.StartsWith("assets/shared/", StringComparison.Ordinal) || path == "DCS-Common-LICENSE.txt"))
                throw new InvalidDataException("Invalid shared asset path: " + path);
            return Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
        }
        #endregion END private helpers
    }
}
