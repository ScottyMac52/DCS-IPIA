namespace Heimdall.Application.Interfaces
{
    /// <summary>Activates one saved module and removes its recorded deployment.</summary>
    public interface IDcsFlyNowService
    {
        #region BEGIN Public
        /// <summary>Recovers interrupted copies and returns the persisted active deployment.</summary>
        Task<DcsFlyNowDeployment?> GetActiveAsync(CancellationToken ct = default);
        /// <summary>Deploys the selected module while enforcing the single active module rule.</summary>
        Task<DcsFlyNowDeployment> DeployAsync(Guid moduleId, string destination, CancellationToken ct = default);
        /// <summary>Deletes only the recorded copied files and unlocks other modules after complete cleanup.</summary>
        Task ClearAsync(CancellationToken ct = default);
        #endregion END Public
    }
}
