using System.Runtime.CompilerServices;
using Heimdall.Application.Interfaces;

namespace Heimdall.Application.Games
{
    /// <summary>
    /// Awaits a return to the workspace owner thread: completes inline on the owner, otherwise posts the awaiting continuation
    /// itself to the owner scheduler, so no race can resume it on the thread that finished the native work.
    /// </summary>
    public readonly struct WorkspaceOwnerResume(IWorkspaceScheduler owner) : INotifyCompletion
    {
        #region BEGIN Public
        /// <summary>Gets whether the caller is already on the owner thread.</summary>
        public bool IsCompleted => owner.CheckAccess();
        /// <summary>Returns this awaiter.</summary>
        /// <returns>The awaiter.</returns>
        public WorkspaceOwnerResume GetAwaiter() => this;
        /// <inheritdoc/>
        public void OnCompleted(Action continuation) => owner.Post(continuation);
        /// <summary>Completes the await on the owner thread.</summary>
        public void GetResult() { }
        #endregion END Public
    }
}
