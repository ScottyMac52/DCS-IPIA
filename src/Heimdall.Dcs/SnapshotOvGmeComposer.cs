using Heimdall.Application;
using Heimdall.Packaging;
using Heimdall.Rendering;

namespace Heimdall.Dcs
{
    /// <summary>Composes a saved module workspace into a complete OvGME archive.</summary>
    public static class SnapshotOvGmeComposer
    {
        #region BEGIN Public
        /// <summary>Builds Lua and PNG payloads from one frozen workspace without source checkout reads.</summary>
        /// <param name="workspace">Saved module editor snapshot with pinned assets.</param>
        /// <param name="version">Consumer package version.</param>
        /// <returns>Deterministic ZIP bytes, filename and separate checksum line.</returns>
        public static OvGmePackage Build(EditorWorkspaceSnapshot workspace, string version)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            var payload = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            Add(SnapshotLuaPayloadRenderer.Generate(workspace));
            Add(SnapshotDeviceKneeboardRenderer.Generate(workspace));
            var summaries = SnapshotSummaryKneeboardRenderer.Generate(workspace);
            if (summaries.Count == 0)
                throw new InvalidDataException("At least one authored summary kneeboard is required.");
            Add(summaries);
            var moduleId = workspace.Configuration.Module.DcsId;
            return OvGmePackageBuilder.Build(new(
                $"DCS-{moduleId}-Components", moduleId, version, payload));

            void Add(IReadOnlyDictionary<string, byte[]> files)
            {
                foreach (var (path, bytes) in files)
                    if (!payload.TryAdd(path, bytes))
                        throw new InvalidDataException("Two generated files share a package path: " + path);
            }
        }
        #endregion END Public
    }
}
