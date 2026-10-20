using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Heimdall.Application;
using Heimdall.Domain;

namespace Heimdall.Dcs
{
    /// <summary>Builds a deterministic independent shared-default archive with verified round-trip inputs.</summary>
    public static class SharedUiLayerOutputArchive
    {
        #region BEGIN Public
        /// <summary>Builds Lua, PNGs, complete binding reference and immutable inputs into one ZIP.</summary>
        /// <param name="snapshot">Verified saved defaults and pinned graphics.</param>
        /// <param name="kneeboards">Rendered output for every pinned joystick graphic.</param>
        /// <returns>Deterministic archive bytes.</returns>
        public static byte[] Build(SharedUiLayerOutputSnapshot snapshot, IReadOnlyDictionary<string, byte[]> kneeboards)
        {
            var immutable = SharedUiLayerOutputSerializer.Serialize(snapshot);
            snapshot = SharedUiLayerOutputSerializer.Deserialize(immutable);
            var expected = snapshot.Diagrams.Values.Select(x => "KNEEBOARD/UiLayer/" + x.ProfileId.ToString("N") + ".png").Order(StringComparer.Ordinal);
            if (!expected.SequenceEqual(kneeboards.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal))
                throw new InvalidDataException("Expected one rendered PNG per pinned joystick graphic.");
            var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var pair in SharedUiLayerOutputSerializer.GenerateLua(snapshot)) files.Add(pair.Key, Encoding.UTF8.GetBytes(pair.Value));
            foreach (var pair in kneeboards) files.Add(pair.Key, pair.Value);
            files.Add("ipia-shared-default-snapshot.json", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(immutable) + "\n"));
            var reference = new StringBuilder("Shared default UI Layer — revision ").Append(snapshot.Defaults.Revision).Append('\n')
                .Append("Source commit: ").Append(snapshot.Defaults.SourceCommit).Append('\n')
                .Append("Hardware commit: ").Append(snapshot.HardwareSourceCommit).Append('\n')
                .Append("Snapshot SHA-256: ").Append(immutable.Sha256).Append("\n\nAll active bindings (including keyboard and unmapped controls):\n");
            foreach (var path in SharedUiLayerBindingEditor.Profiles(snapshot.Defaults))
                foreach (var binding in SharedUiLayerBindingEditor.Bindings(snapshot.Defaults, path).Where(x => x.Disposition != BindingDisposition.Removed))
                    reference.Append(Path.GetFileName(path)).Append(" | ").Append(binding.InputKey).Append(" | ")
                        .Append(string.Join(" + ", binding.Reformers)).Append(" | ").Append(binding.CommandName).Append(" [").Append(binding.CommandId).Append("]\n");
            files.Add("bindings.txt", Encoding.UTF8.GetBytes(reference.ToString()));
            files.Add("README.txt", Encoding.UTF8.GetBytes("Independent shared defaults. Config/Input/UiLayer is relative to Saved Games/DCS. Back up existing UI Layer files before installing. KNEEBOARD/UiLayer contains module-neutral reference pages; bindings.txt includes keyboard and unmapped controls. The immutable snapshot includes exact default sources and pinned graphics. Restore appends a new default revision; rebuild uses the snapshot without a database, module or source checkout.\n"));
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
                foreach (var pair in files)
                {
                    var entry = archive.CreateEntry(pair.Key, CompressionLevel.NoCompression);
                    entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    using var output = entry.Open();
                    output.Write(pair.Value);
                }
            return stream.ToArray();
        }
        /// <summary>Reads and verifies portable inputs from an exported ZIP without extracting files.</summary>
        /// <param name="bytes">Exported archive bytes.</param>
        /// <returns>Verified portable output inputs.</returns>
        public static SharedUiLayerOutputSnapshot Read(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes, false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var entries = archive.Entries.Where(x => x.FullName == "ipia-shared-default-snapshot.json").ToArray();
            if (entries.Length != 1) throw new InvalidDataException("Expected one shared default snapshot.");
            using var reader = new StreamReader(entries[0].Open(), Encoding.UTF8);
            var immutable = JsonSerializer.Deserialize<ImmutableSnapshot>(reader.ReadToEnd()) ?? throw new InvalidDataException("Missing shared snapshot.");
            return SharedUiLayerOutputSerializer.Deserialize(immutable);
        }
        #endregion END Public
    }
}
