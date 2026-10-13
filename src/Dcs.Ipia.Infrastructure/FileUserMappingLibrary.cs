using System.Text.Json;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.Infrastructure
{
    /// <summary>Atomically publishes immutable mapping revisions beneath the active profile for full backup.</summary>
    /// <param name="dataDirectory">Exclusively owned active user profile.</param>
    public sealed class FileUserMappingLibrary(string dataDirectory) : IUserMappingLibrary
    {
        #region BEGIN Public
        /// <inheritdoc/>
        public async Task<IReadOnlyList<UserMappingRevision>> ListAsync()
        {
            var directory = DirectoryPath();
            if (!Directory.Exists(directory)) return [];
            var revisions = new List<UserMappingRevision>();
            foreach (var file in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
            {
                RejectLink(file);
                if (new FileInfo(file).Length > 20_000_000) throw new InvalidDataException("Mapping revision exceeds 20 MB.");
                var revision = JsonSerializer.Deserialize<UserMappingRevision>(await File.ReadAllTextAsync(file))
                    ?? throw new InvalidDataException("Empty user mapping revision.");
                DrawioMappingImporter.Validate(revision);
                if (Path.GetFileNameWithoutExtension(file) != revision.Id)
                    throw new InvalidDataException("Mapping revision filename differs from its identity.");
                revisions.Add(revision);
            }
            return revisions;
        }
        /// <inheritdoc/>
        public async Task SaveAsync(UserMappingRevision revision)
        {
            DrawioMappingImporter.Validate(revision);
            var directory = DirectoryPath();
            Directory.CreateDirectory(directory);
            var destination = Path.Combine(directory, revision.Id + ".json");
            var content = JsonSerializer.Serialize(revision);
            RejectLink(destination);
            if (File.Exists(destination))
            {
                if (await File.ReadAllTextAsync(destination) != content) throw new InvalidDataException("An immutable revision was modified.");
                return;
            }
            var temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                await File.WriteAllTextAsync(temporary, content);
                File.Move(temporary, destination);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        /// <inheritdoc/>
        public async Task DeleteAsync(string revisionId)
        {
            if (string.IsNullOrWhiteSpace(revisionId) || revisionId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                revisionId is "." or ".." || revisionId.Contains('/') || revisionId.Contains('\\'))
                throw new ArgumentException("A valid mapping revision ID is required.", nameof(revisionId));
            var directory = DirectoryPath();
            var path = Path.Combine(directory, revisionId + ".json");
            RejectLink(path);
            if (!File.Exists(path)) throw new FileNotFoundException("The saved mapping revision was not found.", path);
            if (new FileInfo(path).Length > 20_000_000) throw new InvalidDataException("Mapping revision exceeds 20 MB.");
            var revision = JsonSerializer.Deserialize<UserMappingRevision>(await File.ReadAllTextAsync(path))
                ?? throw new InvalidDataException("Empty user mapping revision.");
            DrawioMappingImporter.Validate(revision);
            if (!string.Equals(Path.GetFileNameWithoutExtension(path), revision.Id, StringComparison.Ordinal) ||
                !string.Equals(revision.Id, revisionId, StringComparison.Ordinal))
                throw new InvalidDataException("Mapping revision filename differs from its identity.");
            File.Delete(path);
        }
        #endregion END Public
        #region BEGIN private helpers
        private string DirectoryPath()
        {
            var path = Path.Combine(Path.GetFullPath(dataDirectory), "UserMappings");
            RejectLink(path);
            return path;
        }
        private static void RejectLink(string path)
        {
            for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
                if (new FileInfo(current).LinkTarget is not null ||
                    (File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Mapping paths cannot contain links or junctions.");
        }
        #endregion END private helpers
    }
}
