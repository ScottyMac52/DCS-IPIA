using System.IO;
using System.Text;
using Heimdall.Packaging;

namespace Heimdall.Dcs
{
    /// <summary>Writes a validated package and its checksum without replacing another build.</summary>
    public static class OvGmeArtifactWriter
    {
        #region BEGIN Public
        /// <summary>Writes both artifacts to an existing output folder, cleaning up on failure.</summary>
        /// <param name="package">Generated archive and checksum.</param>
        /// <param name="folder">User-selected destination directory.</param>
        /// <returns>Full paths and SHA-256 of the archive.</returns>
        public static OvGmeArtifactPaths Write(OvGmePackage package, string folder)
        {
            ArgumentNullException.ThrowIfNull(package);
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                throw new DirectoryNotFoundException("Select an existing output folder.");
            if (package.FileName != Path.GetFileName(package.FileName) ||
                !package.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Invalid archive file name.");
            var hash = OvGmePackageVerifier.Verify(package);
            var archive = Path.Combine(folder, package.FileName);
            var checksum = Path.Combine(folder, "SHA256SUMS.txt");
            if (File.Exists(archive) || File.Exists(checksum))
                throw new IOException("An archive or SHA256SUMS.txt already exists in this folder. Choose another folder or move the earlier artifacts.");
            var temporaryArchive = Path.Combine(folder, "." + Guid.NewGuid().ToString("N") + ".zip.tmp");
            var temporaryChecksum = Path.Combine(folder, "." + Guid.NewGuid().ToString("N") + ".sha.tmp");
            var movedArchive = false;
            try
            {
                File.WriteAllBytes(temporaryArchive, package.Bytes);
                File.WriteAllText(temporaryChecksum, package.ChecksumLine, new UTF8Encoding(false));
                OvGmePackageVerifier.Verify(package with { Bytes = File.ReadAllBytes(temporaryArchive) });
                if (File.ReadAllText(temporaryChecksum, Encoding.UTF8) != package.ChecksumLine)
                    throw new InvalidDataException("Written checksum differs from the verified archive.");
                File.Move(temporaryArchive, archive);
                movedArchive = true;
                File.Move(temporaryChecksum, checksum);
                return new(archive, checksum, hash);
            }
            catch
            {
                if (movedArchive)
                    File.Delete(archive);
                throw;
            }
            finally
            {
                if (File.Exists(temporaryArchive))
                    File.Delete(temporaryArchive);
                if (File.Exists(temporaryChecksum))
                    File.Delete(temporaryChecksum);
            }
        }
        #endregion END Public
    }
}
