using System.Security.Cryptography;
using System.Text;
using Dcs.Ipia.Application;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Reviews JSON or legacy Markdown instruction imports and preserves frozen source evidence.</summary>
    public static class ModuleIntegrationImport
    {
        #region BEGIN Public
        /// <summary>Reads one selected file as strict UTF-8 without changing the workspace.</summary>
        /// <param name="workspace">Destination module draft.</param>
        /// <param name="path">User-selected JSON or Markdown file.</param>
        /// <returns>Unapplied review.</returns>
        public static ModuleIntegrationImportReview ReadFile(EditorWorkspaceSnapshot workspace, string path)
        {
            if (new FileInfo(path).Length > 4194304) throw new InvalidDataException("Integration source exceeds 4 MiB.");
            var content = new UTF8Encoding(false, true).GetString(File.ReadAllBytes(path));
            if (content.StartsWith('\ufeff')) content = content[1..];
            return Review(workspace, Path.GetFileName(path), content);
        }

        /// <summary>Validates explicit JSON instructions or captures a selected legacy Markdown guide verbatim.</summary>
        /// <param name="workspace">Destination module draft.</param>
        /// <param name="fileName">Original source basename.</param>
        /// <param name="content">Original UTF-8 source text.</param>
        /// <returns>Instructions to review before staging.</returns>
        public static ModuleIntegrationImportReview Review(EditorWorkspaceSnapshot workspace, string fileName, string content)
        {
            if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 ||
                fileName.Contains("..", StringComparison.Ordinal) || fileName.Any(char.IsControl) || fileName.EndsWith(' ') ||
                (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && !fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Choose a JSON or Markdown instruction source with a safe basename.");
            IReadOnlyList<ModuleIntegrationInstruction> entries = fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? ModuleIntegrationMetadata.Parse(content, workspace.Configuration.Module.DcsId)
                : new[] { new ModuleIntegrationInstruction(Path.GetFileNameWithoutExtension(fileName), content) };
            ModuleIntegrationMetadata.Serialize(entries, workspace.Configuration.Module.DcsId);
            if (entries.Count == 0) throw new InvalidDataException("The source has no integration instructions to import.");
            return new(EditorSnapshotSerializer.Serialize(workspace).Sha256, fileName, content, entries);
        }

        /// <summary>Stages the reviewed merge, retaining absent instruction groups and hashed original source text.</summary>
        /// <param name="workspace">Current module draft.</param>
        /// <param name="review">Previously reviewed source.</param>
        /// <returns>Module draft with effective metadata and original source evidence.</returns>
        public static EditorWorkspaceSnapshot Apply(EditorWorkspaceSnapshot workspace, ModuleIntegrationImportReview review)
        {
            if (EditorSnapshotSerializer.Serialize(workspace).Sha256 != review.BaselineSha256)
                throw new InvalidOperationException("The module changed after this integration review. Load and review again.");
            var verified = Review(workspace, review.FileName, review.Content);
            if (!verified.Instructions.SequenceEqual(review.Instructions))
                throw new InvalidDataException("Integration review differs from the captured source.");
            var entries = Instructions(workspace).ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var entry in verified.Instructions) entries[entry.Name] = entry;
            var sources = Sources(workspace);
            sources["config/integrations.json"] = ModuleIntegrationMetadata.Serialize(entries.Values, workspace.Configuration.Module.DcsId);
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(review.Content)));
            sources["config/ipia-integration-imports/" + hash + "/" + review.FileName] = review.Content;
            return workspace with { Sources = sources };
        }

        /// <summary>Lists effective module-owned instructions without inferring any enabled integration.</summary>
        /// <param name="workspace">Current editor state.</param>
        /// <returns>Validated instructions or an empty collection.</returns>
        public static IReadOnlyList<ModuleIntegrationInstruction> Instructions(EditorWorkspaceSnapshot workspace) =>
            workspace.Sources?.TryGetValue("config/integrations.json", out var source) == true
                ? ModuleIntegrationMetadata.Parse(source, workspace.Configuration.Module.DcsId) : [];

        /// <summary>Removes one effective instruction group while preserving imported source history.</summary>
        /// <param name="workspace">Current module draft.</param>
        /// <param name="name">Exact existing instruction name.</param>
        /// <returns>Staged module metadata.</returns>
        public static EditorWorkspaceSnapshot Remove(EditorWorkspaceSnapshot workspace, string name)
        {
            var entries = Instructions(workspace);
            if (!entries.Any(x => x.Name == name)) throw new InvalidOperationException("Select an existing integration instruction.");
            var sources = Sources(workspace);
            sources["config/integrations.json"] = ModuleIntegrationMetadata.Serialize(entries.Where(x => x.Name != name), workspace.Configuration.Module.DcsId);
            return workspace with { Sources = sources };
        }
        #endregion END Public
        #region BEGIN private helpers
        private static Dictionary<string, string> Sources(EditorWorkspaceSnapshot workspace) =>
            new(workspace.Sources ?? new Dictionary<string, string>(), StringComparer.Ordinal);
        #endregion END private helpers
    }
}
