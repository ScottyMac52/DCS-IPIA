using System.Security.Cryptography;
using System.Text;

namespace Heimdall.Infrastructure
{
    /// <summary>Publishes only a named preset, backing up existing bytes atomically and retaining unrelated selectors.</summary>
    public static class GameBindingsExport
    {
        private const int MaximumBytes = 4 * 1024 * 1024;
        #region BEGIN Public
        /// <summary>Captures replacement evidence without creating directories or modifying files.</summary>
        public static GameBindingsExportPlan Prepare(string directory, IReadOnlyDictionary<string, string> files)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);
            ArgumentNullException.ThrowIfNull(files);
            var presets = files.Where(x => x.Key.EndsWith(".binds", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (presets.Length != 1) throw new InvalidDataException("Export requires exactly one named preset.");
            var preset = presets[0]; Validate(preset.Key, preset.Value);
            directory = Path.GetFullPath(directory); RejectLinks(directory);
            var target = Path.Combine(directory, preset.Key); RejectLinks(target);
            return new(directory, preset.Key, preset.Value, Revision(target));
        }
        /// <summary>Writes a new preset or atomically replaces reviewed bytes with a uniquely named backup; returns its backup path.</summary>
        public static string? Write(GameBindingsExportPlan plan)
        {
            ArgumentNullException.ThrowIfNull(plan);
            Validate(plan.FileName, plan.Content);
            if (!Path.IsPathFullyQualified(plan.Directory)) throw new InvalidDataException("Choose an absolute bindings directory.");
            var target = Path.Combine(plan.Directory, plan.FileName);
            RejectLinks(target); RequireRevision(target, plan.ExpectedHash);
            Directory.CreateDirectory(plan.Directory); RejectLinks(target);
            var stage = Path.Combine(plan.Directory, ".heimdall-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(stage, plan.Content, new UTF8Encoding(false));
                RejectLinks(target); RequireRevision(target, plan.ExpectedHash);
                if (plan.ExpectedHash is null) { File.Move(stage, target); return null; }
                var backup = target + ".backup-" + Guid.NewGuid().ToString("N");
                File.Replace(stage, target, backup);
                return backup;
            }
            finally { File.Delete(stage); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static void Validate(string name, string content)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(['/', '\\', ':', '<', '>', '"', '|', '*', '?']) >= 0
                || name.Contains("..", StringComparison.Ordinal) || name.EndsWith(' ') || !name.EndsWith(".binds", StringComparison.OrdinalIgnoreCase)
                || content is null || Encoding.UTF8.GetByteCount(content) > MaximumBytes)
                throw new InvalidDataException("Export requires one bounded .binds basename.");
        }
        private static string? Revision(string path)
        {
            if (!File.Exists(path)) return null;
            if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("Existing preset exceeds the supported size.");
            return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        }
        private static void RequireRevision(string target, string? expected)
        {
            if (Revision(target) != expected) throw new InvalidOperationException("The destination preset changed. Export again and review its replacement.");
        }
        private static void RejectLinks(string path)
        {
            for (var current = path; current is not null; current = Path.GetDirectoryName(current))
                if (new FileInfo(current).LinkTarget is not null || Directory.Exists(current) && new DirectoryInfo(current).LinkTarget is not null)
                    throw new InvalidDataException("The bindings destination cannot contain symbolic links.");
        }
        #endregion END private helpers
    }
}
