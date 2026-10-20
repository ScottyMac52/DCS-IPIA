using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dcs.Ipia.Application.Games;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.Infrastructure
{
    /// <summary>Atomically stores game documents inside the leased user-data profile; full backups include this folder.</summary>
    public sealed class FileGameWorkspaceStore : IGameWorkspaceStore
    {
        private readonly string _root;
        private readonly object _gate = new();
        private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        private const int MaximumBytes = 64 * 1024 * 1024;
        /// <summary>Uses only the selected production/test data root, never a game installation.</summary>
        public FileGameWorkspaceStore(string dataDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
            _root = Path.Combine(Path.GetFullPath(dataDirectory), "GameWorkspaces");
        }
        #region BEGIN Public
        /// <summary>Lists validated saved game profiles in stable name order.</summary>
        public IReadOnlyList<GameWorkspaceEntry> List()
        {
            lock (_gate)
            {
                if (!Directory.Exists(_root)) return [];
                RejectLink(_root);
                return Directory.GetFiles(_root, "*.ipia-game.json").Select(path => ReadWorkspace(path).Workspace)
                    .Select(x => new GameWorkspaceEntry(x.Id, x.Name, x.Profile.BackendId))
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            }
        }
        /// <summary>Loads a bounded independent document and exact persisted revision.</summary>
        public GameWorkspaceRead Load(Guid id)
        {
            lock (_gate)
            {
                var result = ReadWorkspace(WorkspacePath(id));
                if (result.Workspace.Id != id) throw new InvalidDataException("Game workspace identity does not match its file.");
                return result;
            }
        }
        /// <summary>Saves all state atomically, rejecting stale or accidental duplicate writes.</summary>
        public string Save(GameWorkspace workspace, string? expectedRevision)
        {
            GameWorkspaceContract.Validate(workspace);
            lock (_gate)
            {
                var path = WorkspacePath(workspace.Id);
                RejectLink(path);
                var actual = File.Exists(path) ? Hash(File.ReadAllBytes(path)) : null;
                if (!string.Equals(actual, expectedRevision, StringComparison.Ordinal))
                    throw new InvalidOperationException("Saved game profile changed. Reopen it before saving; your pending edits are retained.");
                var bytes = JsonSerializer.SerializeToUtf8Bytes(workspace, Options);
                Write(path, bytes);
                return Hash(bytes);
            }
        }
        /// <summary>Reads a supported last selection, defaulting only when no selection has been saved.</summary>
        public GameSelectionState ReadSelection()
        {
            lock (_gate)
            {
                var path = Path.Combine(_root, "selection.json");
                if (!File.Exists(path)) return new();
                var state = Deserialize<GameSelectionState>(Read(path));
                GameWorkspaceContract.ValidateSelection(state);
                return state;
            }
        }
        /// <summary>Saves explicit supported choices using an atomic replacement.</summary>
        public void SaveSelection(GameSelectionState selection)
        {
            GameWorkspaceContract.ValidateSelection(selection);
            lock (_gate) Write(Path.Combine(_root, "selection.json"), JsonSerializer.SerializeToUtf8Bytes(selection, Options));
        }
        #endregion END Public
        #region BEGIN private helpers
        private string WorkspacePath(Guid id) => id == Guid.Empty ? throw new ArgumentException("A workspace identity is required.", nameof(id)) :
            Path.Combine(_root, id.ToString("N") + ".ipia-game.json");
        private static GameWorkspaceRead ReadWorkspace(string path)
        {
            var bytes = Read(path);
            var workspace = Deserialize<GameWorkspace>(bytes);
            GameWorkspaceContract.Validate(workspace);
            if (Path.GetFileName(path) != workspace.Id.ToString("N") + ".ipia-game.json")
                throw new InvalidDataException("Saved game profile filename and identity differ.");
            return new(workspace, Hash(bytes));
        }
        private static T Deserialize<T>(byte[] bytes) where T : class
        {
            try { return JsonSerializer.Deserialize<T>(bytes, Options) ?? throw new InvalidDataException("Empty game document."); }
            catch (JsonException error) { throw new InvalidDataException("Invalid saved game JSON: " + error.Message, error); }
        }
        private static byte[] Read(string path)
        {
            RejectLink(path);
            if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("Game document exceeds 64 MiB.");
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length > MaximumBytes) throw new InvalidDataException("Game document exceeds 64 MiB.");
            return bytes;
        }
        private static void Write(string path, byte[] bytes)
        {
            if (bytes.Length > MaximumBytes) throw new InvalidDataException("Game document exceeds 64 MiB.");
            RejectLink(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllBytes(temporary, bytes);
                RejectLink(path);
                File.Move(temporary, path, true);
            }
            finally { File.Delete(temporary); }
        }
        private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
        private static void RejectLink(string path)
        {
            for (var current = path; current is not null; current = Path.GetDirectoryName(current))
                if (new FileInfo(current).LinkTarget is not null || Directory.Exists(current) && new DirectoryInfo(current).LinkTarget is not null)
                    throw new InvalidDataException("Game workspace paths cannot contain symbolic links.");
        }
        #endregion END private helpers
    }
}
