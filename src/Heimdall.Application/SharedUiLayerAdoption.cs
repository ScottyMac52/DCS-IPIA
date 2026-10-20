namespace Heimdall.Application
{
    /// <summary>Stages explicit source adoption while preserving module-owned assignments, labels and overlays.</summary>
    public static class SharedUiLayerAdoption
    {
        #region BEGIN Public
        /// <summary>Lists source changes and identifies module differences that need explicit review.</summary>
        /// <param name="module">Module carrying its recorded baseline.</param>
        /// <param name="target">Verified saved target revision.</param>
        /// <returns>Ordinal source change decisions.</returns>
        public static IReadOnlyList<SharedUiLayerAdoptionChange> Review(EditorWorkspaceSnapshot module, SharedUiLayerWorkspace target)
        {
            ArgumentNullException.ThrowIfNull(module);
            target = SharedUiLayerRevisionReference.Freeze(target);
            var baseline = SharedUiLayerRevisionReference.Freeze(module.SharedDefault ??
                throw new InvalidOperationException("Record a shared default baseline before adopting a later revision."));
            if (target.Revision < baseline.Revision)
                throw new InvalidOperationException("Use adoption rollback to restore an earlier baseline.");
            if (target.Revision == baseline.Revision && target.ContentSha256 != baseline.ContentSha256)
                throw new InvalidOperationException("The target revision has a different content identity.");
            var draft = target with { Revision = baseline.Revision, SourceCommit = baseline.SourceCommit };
            var report = SharedUiLayerImpactReviewer.Review(baseline, draft, [module]);
            return report.Changes.Select(x => {
                var evidence = report.Modules[0].Sources.Single(y => y.Path == x.Path).Status;
                return new SharedUiLayerAdoptionChange(x.Path, x.Change,
                    evidence == "Different source" || evidence == "Not pinned" && baseline.Sources.ContainsKey(x.Path));
            }).ToArray();
        }
        /// <summary>Applies only explicitly selected source paths and records the new baseline.</summary>
        /// <param name="module">Reviewed module snapshot.</param>
        /// <param name="target">Verified saved revision.</param>
        /// <param name="selectedPaths">Exact changed paths to take from defaults; other paths retain module sources.</param>
        /// <returns>Draft with isolated module edits and target baseline evidence.</returns>
        public static EditorWorkspaceSnapshot Apply(EditorWorkspaceSnapshot module, SharedUiLayerWorkspace target,
            IEnumerable<string> selectedPaths)
        {
            ArgumentNullException.ThrowIfNull(selectedPaths);
            var changes = Review(module, target);
            var selected = selectedPaths.ToHashSet(StringComparer.Ordinal);
            if (selected.Any(path => changes.All(x => x.Path != path)))
                throw new ArgumentException("Only reviewed changed source paths may be adopted.", nameof(selectedPaths));
            var sources = new Dictionary<string, string>(module.Sources ?? new Dictionary<string, string>(), StringComparer.Ordinal);
            foreach (var path in selected)
                if (target.Sources.TryGetValue(path, out var text)) sources[path] = text;
                else sources.Remove(path);
            return SharedUiLayerRevisionReference.Attach(module with { Sources = sources }, target);
        }
        #endregion END Public
    }
}
