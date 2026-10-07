namespace Dcs.Ipia.Application.Interfaces
{
    /// <summary>Activates one saved module and removes its recorded deployment.</summary>
    public interface IFlyNowService
    {
        #region BEGIN Public
        /// <summary>Recovers interrupted copies and returns the persisted active deployment.</summary>
        Task<FlyNowDeployment?> GetActiveAsync(CancellationToken ct = default);
        /// <summary>Deploys the selected module while enforcing the single active module rule.</summary>
        Task<FlyNowDeployment> DeployAsync(Guid moduleId, string destination, CancellationToken ct = default);
        /// <summary>Deletes only the recorded copied files and unlocks other modules after complete cleanup.</summary>
        Task ClearAsync(CancellationToken ct = default);
        #endregion END Public
    }
}
