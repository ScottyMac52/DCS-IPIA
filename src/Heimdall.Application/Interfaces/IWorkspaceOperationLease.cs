using Heimdall.Application.Games;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Common workspace WorkspaceOperationLease boundary.</summary>
    public interface IWorkspaceOperationLease
    {
        #region BEGIN Public
        /// <summary>Gets the admitted native identity and generation.</summary>
        WorkspaceIdentity Identity { get; }
        /// <summary>Gets the admitted operation kind.</summary>
        WorkspaceOperationKind Kind { get; }
        /// <summary>Gets cancellation including workspace disposal.</summary>
        CancellationToken Cancellation { get; }
        /// <summary>Returns to the owner thread after native asynchronous work, before any native acceptance or publication; completes immediately on the owner.</summary>
        Heimdall.Application.Games.WorkspaceOwnerResume ResumeOnOwnerAsync();
        #endregion END Public
    }
}
