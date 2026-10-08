using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Dcs.Ipia.Application;

namespace Dcs.Ipia.Infrastructure
{
    /// <summary>Exports editable draw.io source and captured local images without original filesystem dependencies.</summary>
    public static class UserMappingBundle
    {
        #region BEGIN Public
        /// <summary>Builds a portable editable bundle from one verified revision.</summary>
        /// <param name="revision">Immutable user mapping.</param>
        /// <returns>ZIP bytes containing mapping.drawio, referenced raster files and revision.json.</returns>
        public static byte[] Export(UserMappingRevision revision)
        {
            DrawioMappingImporter.Validate(revision);
            using var output = new MemoryStream();
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            {
                Write(zip, "mapping.drawio", Encoding.UTF8.GetBytes(revision.Source));
                Write(zip, "revision.json", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(revision)));
                foreach (var asset in revision.Assets.OrderBy(x => x.Key, StringComparer.Ordinal)) Write(zip, asset.Key, asset.Value);
            }
            return output.ToArray();
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Write(ZipArchive zip, string path, byte[] bytes)
        {
            var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
            entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
            using var stream = entry.Open();
            stream.Write(bytes);
        }
        #endregion END private helpers
    }
}
