using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Dcs.Ipia.Packaging
{
    /// <summary>Assembles the consumer OvGME layout from generated, frozen payload bytes.</summary>
    public static class OvGmePackageBuilder
    {
        private static readonly DateTimeOffset FixedTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        #region BEGIN Public
        /// <summary>Builds a byte-identical ZIP for identical input bytes.</summary>
        /// <param name="input">Validated generated profiles, UI Layer and kneeboard files.</param>
        /// <returns>The archive, name and checksum.</returns>
        public static OvGmePackage Build(OvGmePackageInput input)
        {
            ArgumentNullException.ThrowIfNull(input);
            ValidateSegment(input.PackageId, nameof(input.PackageId));
            ValidateSegment(input.ModuleId, nameof(input.ModuleId));
            ValidateSegment(input.Version, nameof(input.Version));
            ArgumentNullException.ThrowIfNull(input.Payload);
            var modulePrefix = $"Config/Input/{input.ModuleId}/";
            var joystickPrefix = $"{modulePrefix}joystick/";
            var keyboardPrefix = $"{modulePrefix}keyboard/";
            var uiPrefix = "Config/Input/UiLayer/";
            var kneePrefix = $"KNEEBOARD/{input.ModuleId}/";
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = new List<KeyValuePair<string, byte[]>>();
            foreach (var (path, bytes) in input.Payload)
            {
                ValidatePath(path);
                if (bytes is null)
                    throw new ArgumentException($"Null package file: {path}", nameof(input));
                if (!seen.Add(path))
                    throw new ArgumentException($"Duplicate package file: {path}", nameof(input));
                ValidatePayloadPath(input, path, modulePrefix, joystickPrefix, keyboardPrefix, uiPrefix, kneePrefix);
                files.Add(new(path, bytes));
            }
            if (!files.Any(x => x.Key.StartsWith(joystickPrefix, StringComparison.Ordinal) &&
                                x.Key.EndsWith(".diff.lua", StringComparison.Ordinal)))
                throw new ArgumentException("At least one module joystick profile is required.", nameof(input));
            if (!files.Any(x => x.Key.StartsWith(kneePrefix, StringComparison.Ordinal) &&
                                x.Key.EndsWith(".png", StringComparison.Ordinal)))
                throw new ArgumentException("At least one generated kneeboard PNG is required.", nameof(input));
            if (files.Any(x => x.Key.StartsWith(uiPrefix, StringComparison.Ordinal) &&
                               x.Key.EndsWith(".diff.lua", StringComparison.Ordinal)) &&
                !files.Any(x => x.Key == $"{uiPrefix}modifiers.lua"))
                throw new ArgumentException("UI Layer profiles require modifiers.lua.", nameof(input));

            var root = $"{input.PackageId}-{input.Version}-OVGME";
            var fileName = root + ".zip";
            var readme =
                $"{input.PackageId} OVGME package version {input.Version}\n\n" +
                $"Install relative to your DCS Saved Games root.\nInput module: {input.ModuleId}\nKneeboard: {input.ModuleId}\n";
            using var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true, Encoding.UTF8))
            {
                Add(zip, "README.TXT", Encoding.UTF8.GetBytes(readme));
                Add(zip, "VERSION.TXT", Encoding.UTF8.GetBytes(input.Version));
                // OvGME scans actual ZIP directory entries for the folder matching
                // the archive basename. File paths alone do not make a Mod-Archive.
                var directories = new SortedSet<string>(StringComparer.Ordinal) { root + "/" };
                foreach (var (path, _) in files)
                {
                    var fullPath = root + "/" + path;
                    for (var slash = fullPath.IndexOf('/'); slash >= 0; slash = fullPath.IndexOf('/', slash + 1))
                        directories.Add(fullPath[..(slash + 1)]);
                }
                foreach (var directory in directories)
                    AddDirectory(zip, directory);
                foreach (var (path, bytes) in files.OrderBy(x => x.Key, StringComparer.Ordinal))
                    Add(zip, $"{root}/{path}", bytes);
            }
            var archive = stream.ToArray();
            var hash = Convert.ToHexStringLower(SHA256.HashData(archive));
            return new(fileName, archive, hash, $"{hash}  {fileName}\n");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void ValidatePayloadPath(OvGmePackageInput input, string path, string modulePrefix, string joystickPrefix, string keyboardPrefix, string uiPrefix, string kneePrefix)
        {
            var moduleProfile = path.StartsWith(joystickPrefix, StringComparison.Ordinal) &&
                                path.EndsWith(".diff.lua", StringComparison.Ordinal);
            var moduleModifier = path == $"{modulePrefix}modifiers.lua";
            var keyboardProfile = path.StartsWith(keyboardPrefix, StringComparison.Ordinal) &&
                                  path.EndsWith(".diff.lua", StringComparison.Ordinal) &&
                                  !path[keyboardPrefix.Length..].Contains('/');
            var uiProfile = path.StartsWith(uiPrefix, StringComparison.Ordinal) &&
                            path.EndsWith(".diff.lua", StringComparison.Ordinal);
            var uiModifier = path == $"{uiPrefix}modifiers.lua";
            var kneeboard = path.StartsWith(kneePrefix, StringComparison.Ordinal) &&
                            (path.EndsWith(".png", StringComparison.Ordinal) ||
                             path.EndsWith(".svg", StringComparison.Ordinal));
            if (!moduleProfile && !keyboardProfile && !moduleModifier && !uiProfile && !uiModifier && !kneeboard)
                throw new ArgumentException($"Unexpected OvGME payload path: {path}", nameof(input));
        }
        private static void ValidateSegment(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value is "." or ".." ||
                !value.All(x => char.IsAsciiLetterOrDigit(x) || x is '-' or '_' or '.'))
                throw new ArgumentException(
                    "Use a nonempty identifier with letters, digits, hyphen, underscore or dot.", name);
        }
        private static void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.Contains(':') || path.StartsWith('/') ||
                path.Split('/').Any(x => x is "" or "." or ".."))
                throw new ArgumentException($"Unsafe package path: {path}", nameof(path));
        }
        private static void AddDirectory(ZipArchive zip, string path)
        {
            var entry = zip.CreateEntry(path, CompressionLevel.NoCompression);
            entry.LastWriteTime = FixedTimestamp;
            entry.ExternalAttributes = (int)FileAttributes.Directory << 16;
        }
        private static void Add(ZipArchive zip, string path, byte[] bytes)
        {
            var entry = zip.CreateEntry(path, CompressionLevel.NoCompression);
            entry.LastWriteTime = FixedTimestamp;
            using var output = entry.Open();
            output.Write(bytes);
        }
        #endregion END private helpers
    }
}
