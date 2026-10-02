using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dcs.Ipia.Application;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Reviews and stages IPI JSON or DCS HTML command catalogs without importing device assignments.</summary>
    public static class ModuleCommandCatalogImport
    {
        #region BEGIN Public
        /// <summary>Reads user-selected files and creates a module-scoped merge review.</summary>
        /// <param name="workspace">Current aircraft draft.</param>
        /// <param name="paths">Selected JSON or HTML files.</param>
        /// <param name="html">Whether to read DCS HTML exports.</param>
        /// <returns>A review that has not changed the editor.</returns>
        public static ModuleCommandImportReview ReadFiles(EditorWorkspaceSnapshot workspace, IEnumerable<string> paths, bool html) =>
            Review(workspace, paths.Select(x => new CommandCatalogImportSource(Path.GetFileName(x), File.ReadAllText(x))), html);

        /// <summary>Parses source texts and validates a complete batch before it can be staged.</summary>
        /// <param name="workspace">Reviewed module draft.</param>
        /// <param name="files">IPI JSON or DCS HTML texts.</param>
        /// <param name="html">Whether the sources are HTML.</param>
        /// <returns>Preview rows and any blocking merge error.</returns>
        public static ModuleCommandImportReview Review(EditorWorkspaceSnapshot workspace, IEnumerable<CommandCatalogImportSource> files, bool html)
        {
            var sources = files.OrderBy(x => x.FileName, StringComparer.Ordinal).ThenBy(x => x.Content, StringComparer.Ordinal).ToArray();
            if (sources.Length == 0 || (!html && sources.Length != 1))
                throw new InvalidDataException("Select one IPI command catalog JSON file or one or more DCS HTML files.");
            foreach (var source in sources)
                if (string.IsNullOrWhiteSpace(source.FileName) || source.FileName.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 ||
                    source.FileName.Contains("..", StringComparison.Ordinal) || source.FileName.Any(char.IsControl) ||
                    (!html && !source.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Select a command source with a safe JSON or HTML basename.");
            var commands = html
                ? DcsHtmlCommandCatalogImporter.Parse(sources.Select(x => (x.FileName, x.Content)))
                : DcsCommandCatalogImporter.Parse(sources[0].Content, workspace.Configuration.Module.DcsId);
            if (commands.Count == 0) throw new InvalidDataException("The selected source has no assignable commands.");
            var current = workspace.ModuleCommands.ToDictionary(x => x.Id, StringComparer.Ordinal);
            var changes = commands.Select(x => new CommandCatalogImportChange(current.GetValueOrDefault(x.Id), x)).ToArray();
            string? error = null;
            try { ModuleCommandCatalogEditor.Merge(workspace, commands); }
            catch (InvalidOperationException ex) { error = ex.Message; }
            return new(Fingerprint(workspace), sources, commands, changes, error);
        }

        /// <summary>Applies a reviewed batch atomically and retains hashed source evidence in the module snapshot.</summary>
        /// <param name="workspace">Current module draft.</param>
        /// <param name="review">Previously reviewed input.</param>
        /// <returns>Staged module catalog with original source text.</returns>
        public static EditorWorkspaceSnapshot Apply(EditorWorkspaceSnapshot workspace, ModuleCommandImportReview review)
        {
            if (review.Error is not null) throw new InvalidOperationException(review.Error);
            if (review.BaselineSha256 != Fingerprint(workspace))
                throw new InvalidOperationException("The module changed after this command review. Load and review the files again.");
            var updated = ModuleCommandCatalogEditor.Merge(workspace, review.Commands);
            var sources = new Dictionary<string, string>(updated.Sources ?? new Dictionary<string, string>(), StringComparer.Ordinal);
            foreach (var source in review.Sources)
                sources["config/ipia-command-imports/" + Hash(source.Content) + "/" + source.FileName] = source.Content;
            return updated with { Sources = sources };
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Fingerprint(EditorWorkspaceSnapshot workspace) => Hash(JsonSerializer.Serialize(new {
            Module = workspace.Configuration.Module.Id,
            DcsId = workspace.Configuration.Module.DcsId,
            Commands = workspace.ModuleCommands,
            Bindings = workspace.Configuration.Bindings,
            Assignments = workspace.Assignments
        }));
        private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        #endregion END private helpers
    }
}
