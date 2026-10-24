using System.Text;

namespace Heimdall.Infrastructure
{
    /// <summary>Writes a new isolated review folder atomically; never overwrites existing exports or active bindings.</summary>
    public static class GameReviewExport
    {
        #region BEGIN Public
        /// <summary>Retains the existing ED review-export contract.</summary>
        public static string Write(string parent, IReadOnlyDictionary<string, string> files,
            IReadOnlyList<string> protectedRoots, CancellationToken cancellationToken = default) =>
            Write(parent, files, protectedRoots, "elite-dangerous", cancellationToken);
        /// <summary>Publishes a complete game payload under a new child folder, outside protected game roots.</summary>
        public static string Write(string parent, IReadOnlyDictionary<string, string> files,
            IReadOnlyList<string> protectedRoots, string backendId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(parent);
            ArgumentNullException.ThrowIfNull(files);
            cancellationToken.ThrowIfCancellationRequested();
            parent = Path.GetFullPath(parent);
            if (protectedRoots.Any(root => Within(parent, Path.GetFullPath(root))))
                throw new InvalidOperationException("Choose a review folder outside the active game Bindings folder.");
            if (backendId is not "elite-dangerous" and not "star-wars-squadrons") throw new InvalidDataException("Unsupported review export game.");
            ValidateFiles(files, backendId);
            Directory.CreateDirectory(parent);
            RejectLinks(parent);
            var name = (backendId == "star-wars-squadrons" ? "Squadrons-Review-" : "Elite-Dangerous-Review-") + Guid.NewGuid().ToString("N");
            var stage = Path.Combine(parent, "." + name + ".tmp");
            var output = Path.Combine(parent, name);
            try
            {
                Directory.CreateDirectory(stage);
                foreach (var (path, text) in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    File.WriteAllText(Path.Combine(stage, path), text, new UTF8Encoding(false));
                }
                cancellationToken.ThrowIfCancellationRequested();
                Directory.Move(stage, output);
                return output;
            }
            finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void ValidateFiles(IReadOnlyDictionary<string, string> files, string backendId)
        {
            if (files.Count == 0 || files.Count > 8 || files.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Count)
                throw new InvalidDataException("Expected one complete nonempty review payload.");
            foreach (var (path, text) in files)
                if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(['/', '\\', ':', '<', '>', '"', '|', '*', '?']) >= 0
                    || path.Contains("..", StringComparison.Ordinal) || text is null || text.Length > 4 * 1024 * 1024
                    || (backendId == "star-wars-squadrons" ? path != "ProfileOptions_profile_synced"
                        : !path.EndsWith(".binds", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".start", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Review output must contain bounded native game basenames only.");
        }
        private static void RejectLinks(string path)
        {
            for (var current = path; current is not null; current = Path.GetDirectoryName(current))
                if (new FileInfo(current).LinkTarget is not null || Directory.Exists(current) && new DirectoryInfo(current).LinkTarget is not null)
                    throw new InvalidDataException("Review folder cannot contain symbolic links.");
        }
        private static bool Within(string path, string root) => string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        #endregion END private helpers
    }
}
