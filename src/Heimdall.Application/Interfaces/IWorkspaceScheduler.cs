using Heimdall.Application.Games;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Common workspace WorkspaceScheduler boundary.</summary>
    public interface IWorkspaceScheduler
    {
        #region BEGIN Public
        /// <summary>Determines whether the current caller owns this workspace.</summary>
        bool CheckAccess();
        /// <summary>Rejects a caller on another workspace thread.</summary>
        void VerifyAccess();
        /// <summary>Schedules a nonrecursive notification on the owning thread.</summary>
        void Post(Action action);
        #endregion END Public
    }
}
