using Heimdall.Application.Games;
using System.Diagnostics.CodeAnalysis;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Common workspace GameWorkspaceContext boundary.</summary>
    public interface IGameWorkspaceContext : IWorkspaceAssignmentHost, IWorkspaceOperationLeaseOwner, IDisposable
    {
        #region BEGIN Public
        /// <summary>Gets authoritative immutable state.</summary>
        WorkspaceState State { get; }
        /// <summary>Gets the current status without changing dirty state.</summary>
        string Status { get; }
        /// <summary>Gets the single existing assignment engine owned by this workspace.</summary>
        WorkspaceAssignmentWorkflow AssignmentWorkflow { get; }
        /// <summary>Notifies a distinct state or native draft transition.</summary>
        event EventHandler? StateChanged;
        /// <summary>Notifies changed status wording.</summary>
        event EventHandler<string>? StatusChanged;
        /// <summary>Changes native selection while preventing stale pending input.</summary>
        [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Select is the reviewed workspace contract verb; native consumers are C# adapters.")]
        void Select(WorkspaceSelection selection);
        /// <summary>Stages an editable name without changing the native saved baseline.</summary>
        void SetEditedName(string name);
        /// <summary>Recomputes shared state from typed native facts.</summary>
        void RefreshNativeState();
        /// <summary>Coalesces nested refresh scopes without owning an operation lock.</summary>
        IDisposable DeferRefresh();
        /// <summary>Publishes changed status text independently of state.</summary>
        void ReportStatus(string message);
        /// <summary>Admits one operation and releases it on every outcome.</summary>
        /// <summary>Admits a synchronous native edit or restoration using the same operation authority.</summary>
        WorkspaceOperationResult RunOperation(WorkspaceOperationKind kind, Func<IWorkspaceOperationLease, WorkspaceOperationResult> action);
        /// <summary>Admits an asynchronous native operation using the same authority.</summary>
        Task<WorkspaceOperationResult> RunOperationAsync(WorkspaceOperationKind kind, Func<IWorkspaceOperationLease, Task<WorkspaceOperationResult>> action, CancellationToken cancellationToken);
        /// <summary>Admits one cancellable activity or explains why it is unavailable.</summary>
        WorkspaceAvailability TryStartActivity(WorkspaceActivityKind kind, out IWorkspaceActivityLease? lease);
        /// <summary>Cancels active input and releases its native resources.</summary>
        void StopActivity();
        #endregion END Public
    }
}
