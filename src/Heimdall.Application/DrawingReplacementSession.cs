using Heimdall.Application.Interfaces;

namespace Heimdall.Application
{
    /// <summary>Reviews and stages explicit drawing targets independently of the presentation framework.</summary>
    /// <param name="revision">Verified drawing to apply.</param>
    /// <param name="repository">Saved modules and optional stale-checked write boundary.</param>
    public sealed class DrawingReplacementSession(UserMappingRevision revision, IEditorWorkspaceRepository repository)
    {
        private readonly Dictionary<Guid, (EditorWorkspaceSnapshot Expected, EditorWorkspaceSnapshot Updated)> _pending = [];
        #region BEGIN Public
        /// <summary>Gets reviewed devices, including built-in profiles; loading changes no saved data.</summary>
        public IReadOnlyList<DrawingReplacementTarget> Targets { get; private set; } = [];
        /// <summary>Gets all eligible devices for an explicit select-all action.</summary>
        public IReadOnlyList<DrawingReplacementTarget> CompatibleTargets => Targets.Where(x => x.CanReplace).ToArray();
        /// <summary>Gets the number of modules with staged changes.</summary>
        public int StagedModuleCount => _pending.Count;
        /// <summary>Gets the count committed during the last save, including a partially failed save.</summary>
        public int SavedModuleCount { get; private set; }
        /// <summary>Gets whether another review must be loaded after any save attempt.</summary>
        public bool RequiresReload { get; private set; }
        /// <summary>Gets whether the repository supports explicit reviewed writes.</summary>
        public bool CanSave => repository is IEditorWorkspaceUpdateRepository;

        /// <summary>Loads saved built-in, generic and custom profiles without selecting or modifying them.</summary>
        public async Task LoadAsync()
        {
            if (_pending.Count > 0) throw new InvalidOperationException("Discard staged changes before reloading review.");
            var targets = new List<DrawingReplacementTarget>();
            foreach (var id in await repository.ListAsync())
            {
                var workspace = await repository.GetAsync(id);
                if (workspace is null) continue;
                foreach (var diagram in workspace.Diagrams.Where(x => x.Controls.Count > 0))
                    targets.Add(new(workspace, diagram, MappingRevisionReview.Create(diagram, revision), diagram.CustomMapping?.Id == revision.Id));
            }
            Targets = targets;
            SavedModuleCount = 0;
            RequiresReload = false;
        }

        /// <summary>Stages all selected devices together; no repository writes occur until SaveAsync.</summary>
        /// <param name="selected">Explicit targets from the current review.</param>
        /// <param name="restoreOriginal">Whether to restore built-in artwork instead of applying the revision.</param>
        public void Stage(IEnumerable<DrawingReplacementTarget> selected, bool restoreOriginal = false)
        {
            if (RequiresReload || _pending.Count > 0) throw new InvalidOperationException("Discard or finish staged changes, then reload review before staging again.");
            var choices = selected.Distinct().ToArray();
            if (choices.Length == 0) throw new InvalidOperationException("Select at least one compatible target.");
            if (choices.Any(x => !Targets.Contains(x) || !(restoreOriginal ? x.CanRestoreOriginal : x.CanReplace)))
                throw new InvalidOperationException("Only eligible targets from this review can be staged.");
            var staged = new Dictionary<Guid, (EditorWorkspaceSnapshot Expected, EditorWorkspaceSnapshot Updated)>();
            foreach (var group in choices.GroupBy(x => x.Workspace.Configuration.Module.Id))
            {
                var expected = group.First().Workspace;
                var updated = expected;
                foreach (var target in group)
                    updated = restoreOriginal ? UserMappingAdoption.RestoreOriginal(updated, target.Diagram.ProfileId)
                        : UserMappingAdoption.Apply(updated, target.Diagram.ProfileId, revision);
                staged.Add(group.Key, (expected, updated));
            }
            foreach (var entry in staged) _pending.Add(entry.Key, entry.Value);
        }

        /// <summary>Saves each reviewed module with stale-state checking and reports partial progress on failure.</summary>
        public async Task SaveAsync()
        {
            if (RequiresReload) throw new InvalidOperationException("Discard and reload review before retrying a save.");
            if (repository is not IEditorWorkspaceUpdateRepository updates)
                throw new InvalidOperationException("This repository does not support reviewed drawing replacement.");
            if (_pending.Count == 0) throw new InvalidOperationException("No drawing changes are staged.");
            SavedModuleCount = 0;
            RequiresReload = true;
            foreach (var entry in _pending.ToArray())
            {
                await updates.SaveIfUnchangedAsync(entry.Value.Expected, entry.Value.Updated);
                _pending.Remove(entry.Key);
                SavedModuleCount++;
            }
        }

        /// <summary>Discards only unsaved drawing changes.</summary>
        public void Discard() => _pending.Clear();
        #endregion END Public
    }
}
