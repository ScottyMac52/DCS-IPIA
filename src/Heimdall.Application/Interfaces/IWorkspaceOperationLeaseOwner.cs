namespace Heimdall.Application.Interfaces
{
    /// <summary>Validates a context-issued lease without exposing mutable operation ownership.</summary>
    public interface IWorkspaceOperationLeaseOwner
    {
        #region BEGIN Public
        /// <summary>Checks reference identity against the still-admitted operation, including a committing write during disposal.</summary>
        bool OwnsOperation(IWorkspaceOperationLease lease);
        #endregion END Public
    }
}
