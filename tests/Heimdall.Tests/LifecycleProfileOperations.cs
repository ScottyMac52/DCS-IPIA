using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>Store-backed lifecycle hooks with controllable post-commit accept and refresh failures.</summary>
    public sealed class LifecycleProfileOperations(ProfileWorkspaceContextProbe context, IGameWorkspaceStore store)
        : GameStoreProfileOperationsBase(context, () => store, new NoWorkspaceInput())
    {
        #region BEGIN Public
        /// <summary>Gets or sets a failure thrown while installing the durable baseline.</summary>
        public Exception? AcceptFailure { get; set; }
        /// <summary>Gets or sets whether <see cref="AcceptFailure"/> is thrown after the native baseline was installed (partial install).</summary>
        public bool FailAfterInstall { get; set; }
        /// <summary>Gets or sets a failure thrown from the post-persistence refresh.</summary>
        public Exception? RefreshFailure { get; set; }
        /// <summary>Gets the number of post-persistence refreshes.</summary>
        public int Refreshes { get; private set; }
        #endregion END Public
        /// <inheritdoc/>
        protected override string UnsaveableMessage => "Open a profile.";
        /// <inheritdoc/>
        protected override string SavedMessage => "Profile saved.";
        /// <inheritdoc/>
        protected override string DiscardedMessage => "Draft changes discarded.";
        /// <inheritdoc/>
        protected override void AcceptCommit(WorkspaceCommit<GameWorkspace, string> commit, IWorkspaceOperationLease lease)
        {
            if (AcceptFailure is { } failure && !FailAfterInstall) throw failure;
            base.AcceptCommit(commit, lease);
            if (AcceptFailure is { } late) throw late;
        }
        /// <inheritdoc/>
        protected override void RefreshAfterPersistence(WorkspaceOperationKind completed)
        {
            Refreshes++;
            if (RefreshFailure is { } failure) throw failure;
        }
    }
}
