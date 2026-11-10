using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using Heimdall.Dcs;
using Heimdall.Packaging;

namespace Heimdall.TestSupport
{
    /// <summary>Requires reproducible authoring inputs alongside generated OVGME output.</summary>
    internal static class DcsPerformancePackageInput
    {
        #region BEGIN Public
        /// <summary>Reads a verified Complete release or an OVGME archive with its matching module snapshot.</summary>
        internal static DcsModuleBuildSnapshot Read(string archive, string? snapshot)
        {
            var package = Package(archive);
            if (package.FileName.EndsWith("-Complete.zip", StringComparison.Ordinal))
            {
                if (snapshot is not null) throw new InvalidDataException("A Complete release already includes its snapshot; omit DcsSnapshotFile.");
                return DcsModuleCompleteReleaseWorkflow.Verify(package);
            }
            if (snapshot is null) throw new InvalidDataException("A plain OVGME ZIP contains generated output, not editable module data. Supply -DcsSnapshotFile or use a HEIMDALL Complete release ZIP.");
            DcsOvGmePackageVerifier.Verify(package);
            var inputs = DcsModuleSnapshotWorkflow.Read(File.ReadAllText(snapshot));
            Compare(package, DcsModuleSnapshotWorkflow.Build(inputs));
            return inputs;
        }
        /// <summary>Checks file paths and bytes while allowing ZIP container metadata to differ.</summary>
        internal static void Compare(DcsOvGmePackage expected, DcsOvGmePackage actual)
        {
            DcsOvGmePackageVerifier.Verify(expected); DcsOvGmePackageVerifier.Verify(actual);
            var left = Files(expected); var right = Files(actual);
            if (left.Count != right.Count || left.Any(pair => !right.TryGetValue(pair.Key, out var value) || value != pair.Value))
                throw new InvalidDataException("OVGME payload does not match the editable module snapshot: paths or generated Lua/kneeboard contents differ.");
        }
        /// <summary>Wraps supplied bytes with a calculated digest; format verification is still mandatory.</summary>
        internal static DcsOvGmePackage Package(string path)
        {
            var bytes = File.ReadAllBytes(path); var name = Path.GetFileName(path);
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            return new(name, bytes, hash, hash + "  " + name + "\n");
        }
        #endregion END Public
        #region BEGIN private helpers
        private static Dictionary<string, string> Files(DcsOvGmePackage package)
        {
            using var stream = new MemoryStream(package.Bytes, false);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            return zip.Entries.Where(entry => !entry.FullName.EndsWith('/')).ToDictionary(entry => entry.FullName, entry =>
            {
                using var content = entry.Open(); return Convert.ToHexStringLower(SHA256.HashData(content));
            }, StringComparer.Ordinal);
        }
        #endregion END private helpers
    }
}
