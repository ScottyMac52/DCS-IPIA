using Heimdall.Application.Interfaces;

namespace Heimdall.Application.Games
{
    /// <summary>Shared native hooks for multi-sim profiles persisted through <see cref="IGameWorkspaceStore"/> with optimistic revisions.</summary>
    /// <remarks>Store-backed native adapters inherit this; repository-backed adapters supply their own hooks to the same sequence.</remarks>
    public abstract class GameStoreProfileOperationsBase : GameProfileOperationsBase<GameWorkspace, string>
    {
        private readonly GameProfileWorkspaceContextBase _context;
        private readonly Func<IGameWorkspaceStore> _store;
        /// <summary>Binds the store-backed hooks to a profile context and the native store.</summary>
        protected GameStoreProfileOperationsBase(GameProfileWorkspaceContextBase context, Func<IGameWorkspaceStore> store, IWorkspaceInputLifetime input) : base(context, input)
        {
            ArgumentNullException.ThrowIfNull(store);
            _context = context; _store = store;
        }
        /// <summary>Gets the native name for the candidate.</summary>
        protected virtual string CandidateName => _context.State.EditedName;
        /// <summary>Gets the message used when no writable session is open.</summary>
        protected abstract string UnsaveableMessage { get; }
        /// <summary>Validates the native candidate; the default validates the shared configuration name.</summary>
        protected virtual void ValidateCandidate(GameWorkspace candidate) { ArgumentNullException.ThrowIfNull(candidate); GameConfigurationName.Validate(candidate.Name); }
        /// <inheritdoc/>
        protected sealed override GameWorkspace PrepareSave()
        {
            if (_context.Session is not { } session || session.AccessMessage.Length > 0) throw new InvalidOperationException(UnsaveableMessage);
            var candidate = session.Snapshot with { Name = CandidateName };
            ValidateCandidate(candidate);
            return candidate;
        }
        /// <inheritdoc/>
        protected sealed override Task<WorkspaceCommit<GameWorkspace, string>> PersistAsync(GameWorkspace candidate, IWorkspaceOperationLease lease)
            => Task.FromResult(new WorkspaceCommit<GameWorkspace, string>(candidate, _store().Save(candidate, _context.Revision)));
        /// <inheritdoc/>
        protected override void AcceptCommit(WorkspaceCommit<GameWorkspace, string> commit, IWorkspaceOperationLease lease)
        {
            ArgumentNullException.ThrowIfNull(commit);
            _context.Session!.AcceptSaved(commit.Snapshot, lease); _context.Revision = commit.Receipt;
        }
        /// <inheritdoc/>
        protected override void RestoreSaved(IWorkspaceOperationLease lease) => _context.Session?.Discard(lease);
    }
}
