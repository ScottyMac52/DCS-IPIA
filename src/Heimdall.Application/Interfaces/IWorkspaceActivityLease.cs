using Heimdall.Application.Games;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Common workspace WorkspaceActivityLease boundary.</summary>
    public interface IWorkspaceActivityLease : IDisposable
    {
        #region BEGIN Public
        /// <summary>Gets the activity generation.</summary>
        WorkspaceIdentity Identity { get; }
        /// <summary>Gets the admitted activity kind.</summary>
        WorkspaceActivityKind Kind { get; }
        /// <summary>Gets cancellation for stale native results.</summary>
        CancellationToken Cancellation { get; }
        /// <summary>Registers release of native input resources on stop or disposal.</summary>
        void RegisterCleanup(Action cleanup);
        #endregion END Public
    }
}
