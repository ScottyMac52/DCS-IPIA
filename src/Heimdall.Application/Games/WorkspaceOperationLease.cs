using Heimdall.Application.Interfaces;

namespace Heimdall.Application.Games
{
    /// <summary>A context-issued operation token; consumers cannot construct an admitted lease.</summary>
    internal sealed class WorkspaceOperationLease(WorkspaceIdentity identity, WorkspaceOperationKind kind, IWorkspaceScheduler owner, CancellationToken cancellation) : IWorkspaceOperationLease
    {
        #region BEGIN Public
        /// <inheritdoc/>
        public WorkspaceIdentity Identity { get; } = identity;
        /// <inheritdoc/>
        public WorkspaceOperationKind Kind { get; } = kind;
        /// <inheritdoc/>
        public CancellationToken Cancellation { get; } = cancellation;
        /// <inheritdoc/>
        public WorkspaceOwnerResume ResumeOnOwnerAsync() => new(owner);
        #endregion END Public
    }
}
