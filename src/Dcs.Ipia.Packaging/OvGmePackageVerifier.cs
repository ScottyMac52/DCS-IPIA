using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Dcs.Ipia.Packaging
{
    /// <summary>Reads a completed ZIP independently of the payload assembler before it is published.</summary>
    public static class OvGmePackageVerifier
    {
        #region BEGIN Public
        /// <summary>Checks the OvGME directory, Saved Games payload and checksum from archive bytes.</summary>
        /// <returns>The verified ZIP SHA-256.</returns>
        public static string Verify(OvGmePackage package)
        {
            ArgumentNullException.ThrowIfNull(package);
            if (package.FileName != Path.GetFileName(package.FileName) ||
                !package.FileName.EndsWith("-OVGME.zip", StringComparison.Ordinal) ||
                package.Bytes is null)
                throw new InvalidDataException("Invalid OvGME archive name or bytes.");
            var hash = Convert.ToHexStringLower(SHA256.HashData(package.Bytes));
            if (package.Sha256 != hash || package.ChecksumLine != $"{hash}  {package.FileName}\n")
                throw new InvalidDataException("Archive checksum does not match its bytes.");
            var root = package.FileName[..^".zip".Length] + "/";
            try
            {
                using var stream = new MemoryStream(package.Bytes, false);
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read, false, Encoding.UTF8);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var directories = new HashSet<string>(StringComparer.Ordinal);
                var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (var entry in zip.Entries)
                {
                    var path = entry.FullName;
                    if (!seen.Add(path) || path.Contains('\\') || path.Contains(':') ||
                        path.StartsWith('/') || path.Split('/').Any(x => x is "." or ".."))
                        throw new InvalidDataException("Duplicate or unsafe OvGME entry: " + path);
                    if (path.EndsWith('/'))
                    {
                        if (entry.Length != 0)
                            throw new InvalidDataException("Nonempty OvGME directory: " + path);
                        directories.Add(path);
                        continue;
                    }
                    if (path is not ("README.TXT" or "VERSION.TXT") &&
                        !path.StartsWith(root, StringComparison.Ordinal))
                        throw new InvalidDataException("File outside the OvGME mod directory: " + path);
                    using var input = entry.Open();
                    using var output = new MemoryStream();
                    input.CopyTo(output);
                    files.Add(path, output.ToArray());
                }
                if (!directories.Contains(root) || !files.TryGetValue("README.TXT", out var readme) ||
                    !files.TryGetValue("VERSION.TXT", out var versionBytes))
                    throw new InvalidDataException("OvGME root directory, README or VERSION is missing.");
                var version = new UTF8Encoding(false, true).GetString(versionBytes);
                if (version.Length == 0 || !version.All(x => char.IsAsciiLetterOrDigit(x) || x is '-' or '_' or '.') ||
                    !root.EndsWith("-" + version + "-OVGME/", StringComparison.Ordinal))
                    throw new InvalidDataException("OvGME version does not match archive name.");
                var suffix = "-" + version + "-OVGME/";
                var packageId = root[..^suffix.Length];
                if (!packageId.StartsWith("DCS-", StringComparison.Ordinal) ||
                    !packageId.EndsWith("-Components", StringComparison.Ordinal))
                    throw new InvalidDataException("OvGME package identity is invalid.");
                var module = packageId["DCS-".Length..^"-Components".Length];
                if (module.Length == 0 ||
                    !module.All(x => char.IsAsciiLetterOrDigit(x) || x is '-' or '_' or '.') ||
                    !new UTF8Encoding(false, true).GetString(readme)
                        .Contains("Install relative to your DCS Saved Games root.", StringComparison.Ordinal))
                    throw new InvalidDataException("OvGME module identity or README is invalid.");
                var joystick = root + "Config/Input/" + module + "/joystick/";
                var keyboard = root + "Config/Input/" + module + "/keyboard/";
                var ui = root + "Config/Input/UiLayer/";
                var knee = root + "KNEEBOARD/" + module + "/";
                var moduleProfile = false;
                var png = false;
                var uiProfile = false;
                var uiModifiers = false;
                foreach (var path in files.Keys.Where(x => x.StartsWith(root, StringComparison.Ordinal)))
                {
                    var relative = path[root.Length..];
                    var valid = false;
                    if (path.StartsWith(joystick, StringComparison.Ordinal) &&
                        path.EndsWith(".diff.lua", StringComparison.Ordinal) &&
                        !path[joystick.Length..].Contains('/'))
                        valid = moduleProfile = true;
                    else if (path.StartsWith(keyboard, StringComparison.Ordinal) &&
                             path.EndsWith(".diff.lua", StringComparison.Ordinal) &&
                             !path[keyboard.Length..].Contains('/'))
                        valid = true;
                    else if (path == root + "Config/Input/" + module + "/modifiers.lua")
                        valid = true;
                    else if (path.StartsWith(ui + "joystick/", StringComparison.Ordinal) ||
                             path.StartsWith(ui + "keyboard/", StringComparison.Ordinal))
                    {
                        var prefix = path.StartsWith(ui + "joystick/", StringComparison.Ordinal)
                                         ? ui + "joystick/" : ui + "keyboard/";
                        if (path.EndsWith(".diff.lua", StringComparison.Ordinal) &&
                            !path[prefix.Length..].Contains('/'))
                            valid = uiProfile = true;
                    }
                    else if (path == ui + "modifiers.lua")
                        valid = uiModifiers = true;
                    else if (path.StartsWith(knee, StringComparison.Ordinal) &&
                             (path.EndsWith(".png", StringComparison.Ordinal) ||
                              path.EndsWith(".svg", StringComparison.Ordinal)) &&
                             !path[knee.Length..].Contains('/'))
                        valid = true;
                    if (path.StartsWith(knee, StringComparison.Ordinal) &&
                        path.EndsWith(".png", StringComparison.Ordinal))
                        png = true;
                    if (!valid || files[path].Length == 0)
                        throw new InvalidDataException("Unexpected or empty OvGME payload: " + path);
                    for (var slash = path.IndexOf('/'); slash >= 0; slash = path.IndexOf('/', slash + 1))
                        if (!directories.Contains(path[..(slash + 1)]))
                            throw new InvalidDataException("OvGME parent directory is missing: " + path);
                }
                if (!moduleProfile || !png || uiProfile && !uiModifiers)
                    throw new InvalidDataException("OvGME module profile, kneeboard or UI modifiers are missing.");
                if (directories.Any(x => !x.StartsWith(root, StringComparison.Ordinal)))
                    throw new InvalidDataException("Unexpected OvGME directory outside mod root.");
                return hash;
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException or
                                       NotSupportedException or DecoderFallbackException)
            {
                throw new InvalidDataException("Invalid OvGME archive: " + ex.Message, ex);
            }
        }
        #endregion END Public
    }
}
