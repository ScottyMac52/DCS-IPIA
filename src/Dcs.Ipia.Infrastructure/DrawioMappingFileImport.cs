using Dcs.Ipia.Application;

namespace Dcs.Ipia.Infrastructure
{
    /// <summary>Reads only bounded source and explicitly referenced local raster files, without external fetches.</summary>
    public static class DrawioMappingFileImport
    {
        #region BEGIN Public
        /// <summary>Loads a local source and safely captures its referenced images for portable preview.</summary>
        /// <param name="path">User-selected editable source.</param>
        /// <param name="definition">Explicit native target definition.</param>
        /// <param name="version">Application provenance.</param>
        /// <param name="catalogInputs">Optional trusted bundled marker-to-input mappings.</param>
        /// <param name="catalogCandidates">Optional installed maps for renamed drawing copies with preserved stable markers.</param>
        /// <returns>Verified self-contained import.</returns>
        public static UserMappingRevision Load(string path, HardwareDefinition definition, string version, IReadOnlyDictionary<string, string>? catalogInputs = null,
            IEnumerable<IReadOnlyDictionary<string, string>>? catalogCandidates = null)
        {
            path = Path.GetFullPath(path);
            var source = new System.Text.UTF8Encoding(false, true).GetString(Read(path, 2_000_000));
            if (catalogInputs is null && catalogCandidates is not null)
                catalogInputs = DrawioMappingImporter.ResolveCatalogInputs(source, definition, catalogCandidates);
            if (catalogInputs is not null) source = DrawioMappingImporter.WithCatalogInputs(source, catalogInputs);
            var assets = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var relative in DrawioMappingImporter.LocalAssets(source))
            {
                if (assets.Count >= 32) throw new InvalidDataException("At most 32 local images are supported.");
                assets.Add(relative, Read(Path.Combine(Path.GetDirectoryName(path)!, relative), 4_000_000));
                if (assets.Values.Sum(x => (long)x.Length) > 8_000_000) throw new InvalidDataException("Local images exceed 8 MB.");
            }
            return DrawioMappingImporter.Import(source, definition, Path.GetFileName(path), version, assets);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static byte[] Read(string path, int maximum)
        {
            for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
                if (new FileInfo(current).LinkTarget is not null ||
                    (File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Draw.io source and image paths cannot contain links or junctions.");
            using var input = File.OpenRead(path);
            if (input.Length > maximum) throw new InvalidDataException("Selected file exceeds its size limit: " + Path.GetFileName(path));
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = input.Read(buffer)) > 0)
            {
                if (output.Length + read > maximum) throw new InvalidDataException("Selected file grew beyond its size limit.");
                output.Write(buffer, 0, read);
            }
            return output.ToArray();
        }
        #endregion END private helpers
    }
}
