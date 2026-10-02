using System.Text;
using Dcs.Ipia.Packaging;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Builds, independently verifies and atomically exports a source-free Complete module release.</summary>
    public static class ModuleCompleteReleaseWorkflow
    {
        #region BEGIN Public
        /// <summary>Packages OvGME, generated documentation and portable build inputs with exact checksums.</summary>
        /// <param name="snapshot">Validated frozen module inputs.</param>
        /// <returns>Deterministic Complete distribution archive.</returns>
        public static OvGmePackage Build(ModuleBuildSnapshot snapshot)
        {
            var ovgme = ModuleSnapshotWorkflow.Build(snapshot);
            var workspace = EditorSnapshotSerializer.Deserialize(snapshot.Workspace);
            var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal) {
                ["OVGME/" + ovgme.FileName] = ovgme.Bytes,
                ["VERSION.TXT"] = Encoding.UTF8.GetBytes(snapshot.Version + "\n"),
                ["Snapshots/" + workspace.Configuration.Module.DcsId + ".ipia-module.json"] = Encoding.UTF8.GetBytes(ModuleSnapshotWorkflow.Export(snapshot)),
                ["Documentation/RELEASE-NOTES.md"] = Encoding.UTF8.GetBytes(Notes(snapshot))
            };
            foreach (var guide in ModuleDocumentationWorkflow.Generate(snapshot))
                files.Add("Documentation/" + guide.Key, Encoding.UTF8.GetBytes(guide.Value));
            var root = ovgme.FileName[..^10] + "-Complete";
            var result = CompleteReleaseArchive.Build(root, files);
            Verify(result);
            return result;
        }

        /// <summary>Verifies archive hashes/layout, restores captured inputs and compares nested output and guides.</summary>
        /// <param name="package">Complete archive to verify.</param>
        /// <returns>Verified portable build inputs.</returns>
        public static ModuleBuildSnapshot Verify(OvGmePackage package)
        {
            var files = CompleteReleaseVerifier.Verify(package);
            var snapshots = files.Where(x => x.Key.StartsWith("Snapshots/", StringComparison.Ordinal)).ToArray();
            if (snapshots.Length != 1) throw new InvalidDataException("Exactly one module snapshot is required.");
            var snapshot = ModuleSnapshotWorkflow.Read(Encoding.UTF8.GetString(snapshots[0].Value));
            var workspace = EditorSnapshotSerializer.Deserialize(snapshot.Workspace);
            var ovgme = ModuleSnapshotWorkflow.Build(snapshot);
            if (package.FileName != ovgme.FileName[..^10] + "-Complete.zip" ||
                snapshots[0].Key != "Snapshots/" + workspace.Configuration.Module.DcsId + ".ipia-module.json" ||
                Encoding.UTF8.GetString(files["VERSION.TXT"]) != snapshot.Version + "\n" ||
                !files.TryGetValue("OVGME/" + ovgme.FileName, out var nested) || !nested.SequenceEqual(ovgme.Bytes) ||
                Encoding.UTF8.GetString(files["Documentation/RELEASE-NOTES.md"]) != Notes(snapshot))
                throw new InvalidDataException("Complete release differs from its frozen build inputs.");
            var expected = ModuleDocumentationWorkflow.Generate(snapshot);
            foreach (var guide in expected)
                if (!files.TryGetValue("Documentation/" + guide.Key, out var bytes) || Encoding.UTF8.GetString(bytes) != guide.Value)
                    throw new InvalidDataException("Complete release guide differs from its frozen inputs.");
            var allowed = expected.Keys.Select(x => "Documentation/" + x).Concat(["Documentation/RELEASE-NOTES.md", "VERSION.TXT", "SHA256SUMS.txt",
                snapshots[0].Key, "OVGME/" + ovgme.FileName]).ToHashSet(StringComparer.Ordinal);
            if (files.Keys.Any(x => !allowed.Contains(x))) throw new InvalidDataException("Unexpected Complete release payload.");
            return snapshot;
        }

        /// <summary>Exports a verified archive/checksum pair into a new folder without replacing previous releases.</summary>
        /// <param name="snapshot">Frozen module inputs.</param>
        /// <param name="destination">New artifact folder with an existing parent.</param>
        public static void Write(ModuleBuildSnapshot snapshot, string destination)
        {
            var package = Build(snapshot);
            var path = Path.GetFullPath(destination);
            if (File.Exists(path) || Directory.Exists(path)) throw new IOException("Complete release destination already exists.");
            var parent = Path.GetDirectoryName(path)!;
            if (!Directory.Exists(parent)) throw new DirectoryNotFoundException("Complete release parent does not exist.");
            var staging = Path.Combine(parent, ".ipia-complete-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(staging);
                var archive = Path.Combine(staging, package.FileName);
                File.WriteAllBytes(archive, package.Bytes);
                File.WriteAllText(Path.Combine(staging, "SHA256SUMS.txt"), package.ChecksumLine, new UTF8Encoding(false));
                Verify(package with { Bytes = File.ReadAllBytes(archive) });
                Directory.Move(staging, path);
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Notes(ModuleBuildSnapshot snapshot) => "# Frozen module release\n\nVersion: " + snapshot.Version +
            "\n\nWorkspace SHA-256: " + snapshot.Workspace.Sha256 + "\n\nContains the generated OvGME package, snapshot-derived guides and portable build inputs. " +
            "Keep earlier Complete releases for reproducible rollback. Optional integration instructions do not install integration assets.\n";
        #endregion END private helpers
    }
}
