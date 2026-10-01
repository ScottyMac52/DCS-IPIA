using System.Text.Json;
using System.Text.Json.Nodes;
using Dcs.Ipia.Application;
using Dcs.Ipia.Domain;

namespace Dcs.Ipia.Dcs
{
    /// <summary>Stages shared command catalog changes while preserving bindings, source metadata, and module isolation.</summary>
    public static class SharedUiLayerCommandEditor
    {
        private const string CatalogPath = "assets/shared/ui-layer/functions.json";
        #region BEGIN Public
        /// <summary>Reads and validates catalog identities, command codes, labels, and categories.</summary>
        /// <param name="workspace">Current shared defaults.</param>
        /// <returns>Catalog entries in source order.</returns>
        public static IReadOnlyList<SharedUiLayerCommand> Commands(SharedUiLayerWorkspace workspace)
        {
            var commands = Functions(Document(workspace)).Select(x => Read(x as JsonObject ??
                throw new InvalidOperationException("Every catalog entry must be an object."))).ToArray();
            if (commands.DistinctBy(x => x.Id).Count() != commands.Length || commands.DistinctBy(x => x.Command).Count() != commands.Length)
                throw new InvalidOperationException("Catalog identities and DCS command codes must be unique.");
            return commands;
        }
        /// <summary>Adds or updates a catalog entry, protecting active command references and rejecting stale selections.</summary>
        /// <param name="workspace">Current draft defaults.</param>
        /// <param name="current">Original selected entry, or null to add.</param>
        /// <param name="entry">Replacement fields.</param>
        /// <returns>Immutable draft retaining its baseline revision.</returns>
        public static SharedUiLayerWorkspace Set(SharedUiLayerWorkspace workspace, SharedUiLayerCommand? current, SharedUiLayerCommand entry)
        {
            Validate(entry);
            var commands = Commands(workspace);
            var index = CurrentIndex(commands, current);
            if (current is not null && current.Id != entry.Id)
                throw new InvalidOperationException("Catalog identities are stable. Add a new entry to use a different identity.");
            if (commands.Where((_, i) => i != index).Any(x => x.Id == entry.Id || x.Command == entry.Command))
                throw new InvalidOperationException("That catalog identity or DCS command code already exists.");
            if (current is not null && current.Command != entry.Command && IsBound(workspace, current.Command))
                throw new InvalidOperationException("Clear this command's assignments before changing its DCS command code.");
            var document = Document(workspace);
            var functions = Functions(document);
            var item = index >= 0 ? (JsonObject)functions[index]! : new JsonObject();
            item["id"] = entry.Id;
            item["command"] = entry.Command;
            item["label"] = entry.Label;
            item["category"] = entry.Category;
            if (index < 0) functions.Add(item);
            return Store(workspace, document);
        }
        /// <summary>Removes an unused catalog entry without deleting bindings or removal history.</summary>
        /// <param name="workspace">Current draft defaults.</param>
        /// <param name="current">Original selected entry.</param>
        /// <returns>Immutable draft retaining its baseline revision.</returns>
        public static SharedUiLayerWorkspace Remove(SharedUiLayerWorkspace workspace, SharedUiLayerCommand current)
        {
            var index = CurrentIndex(Commands(workspace), current);
            if (IsBound(workspace, current.Command))
                throw new InvalidOperationException("Clear this command's assignments before removing its catalog entry.");
            var document = Document(workspace);
            Functions(document).RemoveAt(index);
            return Store(workspace, document);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static JsonObject Document(SharedUiLayerWorkspace workspace) => JsonNode.Parse(workspace.Sources[CatalogPath]) as JsonObject ??
            throw new InvalidOperationException("The shared command catalog must be an object.");
        private static JsonArray Functions(JsonObject document) => document["functions"] as JsonArray ??
            throw new InvalidOperationException("The shared command catalog must contain a functions array.");
        private static SharedUiLayerCommand Read(JsonObject item)
        {
            var command = new SharedUiLayerCommand(item["id"]?.GetValue<string>() ?? "", item["command"]?.GetValue<string>() ?? "",
                item["label"]?.GetValue<string>() ?? "", item["category"]?.GetValue<string>() ?? "");
            Validate(command);
            return command;
        }
        private static void Validate(SharedUiLayerCommand entry)
        {
            if (new[] { entry.Id, entry.Command, entry.Label, entry.Category }.Any(x =>
                string.IsNullOrWhiteSpace(x) || x != x.Trim() || x.Any(char.IsControl)) ||
                entry.Id.Any(char.IsWhiteSpace) || entry.Command.Any(char.IsWhiteSpace))
                throw new InvalidOperationException("Enter an identity, DCS command code, label, and category without surrounding whitespace or control characters. Identity and code cannot contain spaces.");
        }
        private static int CurrentIndex(IReadOnlyList<SharedUiLayerCommand> commands, SharedUiLayerCommand? current)
        {
            if (current is null) return -1;
            var index = Array.FindIndex(commands.ToArray(), x => x.Id == current.Id);
            if (index < 0 || commands[index] != current)
                throw new InvalidOperationException("The selected command changed. Select it again.");
            return index;
        }
        private static bool IsBound(SharedUiLayerWorkspace workspace, string command) => SharedUiLayerBindingEditor.Profiles(workspace)
            .SelectMany(path => SharedUiLayerBindingEditor.Bindings(workspace, path))
            .Any(x => x.CommandId == command && x.Disposition != BindingDisposition.Removed);
        private static SharedUiLayerWorkspace Store(SharedUiLayerWorkspace workspace, JsonObject document)
        {
            var sources = new Dictionary<string, string>(workspace.Sources, StringComparer.Ordinal) {
                [CatalogPath] = document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n"
            };
            return workspace with { Sources = sources, ContentSha256 = null };
        }
        #endregion END private helpers
    }
}
