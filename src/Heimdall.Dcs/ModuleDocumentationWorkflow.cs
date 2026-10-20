using System.Text;
using Heimdall.Application;
using Heimdall.Rendering;

namespace Heimdall.Dcs
{
    /// <summary>Generates documentation from verified frozen package inputs without a database or checkout.</summary>
    public static class ModuleDocumentationWorkflow
    {
        #region BEGIN Public
        /// <summary>Produces deterministic guides describing the exact deployable Lua and pinned inputs.</summary>
        /// <param name="snapshot">Frozen package inputs.</param>
        /// <returns>Fixed Markdown filenames and UTF-8 text.</returns>
        public static IReadOnlyDictionary<string, string> Generate(ModuleBuildSnapshot snapshot)
        {
            ModuleSnapshotWorkflow.Build(snapshot);
            var workspace = EditorSnapshotSerializer.Deserialize(snapshot.Workspace);
            var devices = WorkspaceStructureEditor.Describe(workspace).Devices;
            var profiles = SnapshotLuaPayloadRenderer.Generate(workspace).OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => Parse(x.Key, Encoding.UTF8.GetString(x.Value), workspace, devices)).ToArray();
            var defaults = (workspace.SharedDefault?.Sources ?? new Dictionary<string, string>())
                .Where(x => x.Key.EndsWith(".diff.lua", StringComparison.Ordinal))
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new ModuleDocumentationProfile(x.Key, null,
                    DcsLuaParser.ParseProfile(x.Value, workspace.Configuration.Module.Id), [])).ToArray();
            return ModuleDocumentationRenderer.Render(workspace, snapshot.Version, snapshot.Workspace.Sha256, profiles, defaults);
        }

        /// <summary>Publishes all guides atomically as a new directory, preserving every existing artifact.</summary>
        /// <param name="snapshot">Frozen package inputs.</param>
        /// <param name="path">New destination directory; its parent must exist.</param>
        public static void Write(ModuleBuildSnapshot snapshot, string path)
        {
            var guides = Generate(snapshot);
            var destination = Path.GetFullPath(path);
            if (Directory.Exists(destination) || File.Exists(destination))
                throw new IOException("Documentation destination already exists. Choose a new folder.");
            var parent = Path.GetDirectoryName(destination)!;
            if (!Directory.Exists(parent)) throw new DirectoryNotFoundException("Documentation parent folder does not exist.");
            var temporary = Path.Combine(parent, ".ipia-docs-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(temporary);
                foreach (var guide in guides)
                    File.WriteAllText(Path.Combine(temporary, guide.Key), guide.Value, new UTF8Encoding(false));
                Directory.Move(temporary, destination);
            }
            finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
        }
        #endregion END Public
        #region BEGIN private helpers
        private static ModuleDocumentationProfile Parse(string path, string lua, EditorWorkspaceSnapshot workspace,
            IReadOnlyList<WorkspaceDevice> devices)
        {
            if (path.EndsWith("/modifiers.lua", StringComparison.Ordinal))
                return new(path, null, [], DcsLuaParser.ParseModifiers(lua));
            var layer = path.StartsWith("Config/Input/UiLayer/", StringComparison.Ordinal)
                ? AssignmentLayer.UiLayer : AssignmentLayer.Module;
            var device = devices.SingleOrDefault(x => x.Layer == layer && x.FileName == Path.GetFileName(path));
            return new(path, device?.ProfileId, DcsLuaParser.ParseProfile(lua, device?.ProfileId ?? workspace.Configuration.Module.Id), []);
        }
        #endregion END private helpers
    }
}
