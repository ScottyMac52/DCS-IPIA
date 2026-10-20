namespace Heimdall.Application
{
    /// <summary>Compares exact pinned source texts without changing module assignments or adopting defaults.</summary>
    public static class SharedUiLayerImpactReviewer
    {
        #region BEGIN Public
        /// <summary>Reviews changed default sources against saved module evidence; missing pins require review.</summary>
        /// <param name="baseline">Opened saved shared defaults.</param>
        /// <param name="proposal">Draft based on that revision.</param>
        /// <param name="modules">Saved module snapshots.</param>
        /// <returns>Deterministically ordered source and module comparisons.</returns>
        public static SharedUiLayerImpactReport Review(SharedUiLayerWorkspace baseline, SharedUiLayerWorkspace proposal,
            IEnumerable<EditorWorkspaceSnapshot> modules)
        {
            ArgumentNullException.ThrowIfNull(baseline);
            ArgumentNullException.ThrowIfNull(proposal);
            ArgumentNullException.ThrowIfNull(modules);
            if (baseline.Revision != proposal.Revision || baseline.SourceCommit != proposal.SourceCommit)
                throw new ArgumentException("The proposed defaults must use the opened baseline revision.", nameof(proposal));
            var paths = baseline.Sources.Keys.Union(proposal.Sources.Keys, StringComparer.Ordinal)
                .Where(path => !EqualSource(baseline.Sources, proposal.Sources, path)).Order(StringComparer.Ordinal).ToArray();
            var changes = paths.Select(path => new SharedUiLayerSourceChange(path,
                !baseline.Sources.ContainsKey(path) ? "Added" : !proposal.Sources.ContainsKey(path) ? "Removed" : "Updated")).ToArray();
            var impacts = modules.OrderBy(x => x.Configuration.Module.DcsId, StringComparer.Ordinal)
                .ThenBy(x => x.Configuration.Module.Id).Select(module => new SharedUiLayerModuleImpact(
                    module.Configuration.Module.Id, module.Configuration.Module.DisplayName + " (" + module.Configuration.Module.DcsId + ")",
                    paths.Select(path => new SharedUiLayerModuleSourceImpact(path, Compare(module.Sources, baseline.Sources,
                        proposal.Sources, path))).ToArray())).ToArray();
            return new(baseline.Revision, changes, impacts);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static string Compare(IReadOnlyDictionary<string, string>? module, IReadOnlyDictionary<string, string> baseline,
            IReadOnlyDictionary<string, string> proposal, string path)
        {
            if (module is null) return "Not pinned";
            if (EqualSource(module, proposal, path)) return "Matches proposal";
            if (!module.ContainsKey(path)) return "Not pinned";
            return EqualSource(module, baseline, path) ? "Matches baseline" : "Different source";
        }
        private static bool EqualSource(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right, string path) =>
            left.TryGetValue(path, out var first) == right.TryGetValue(path, out var second) && first == second;
        #endregion END private helpers
    }
}

