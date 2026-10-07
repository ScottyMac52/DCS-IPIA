using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.Infrastructure
{
    /// <summary>Prevents test profiles from reading, recovering, deploying or clearing real DCS files.</summary>
    public sealed class IsolatedFlyNowService : IFlyNowService
    {
        #region BEGIN Public
        /// <inheritdoc/>
        public Task<FlyNowDeployment?> GetActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<FlyNowDeployment?>(null);
        /// <inheritdoc/>
        public Task<FlyNowDeployment> DeployAsync(Guid moduleId, string destination, CancellationToken ct = default) =>
            Task.FromException<FlyNowDeployment>(new InvalidOperationException("Fly Now is disabled in isolated test profiles. Return to Production to deploy."));
        /// <inheritdoc/>
        public Task ClearAsync(CancellationToken ct = default) =>
            Task.FromException(new InvalidOperationException("Clear is disabled in isolated test profiles; no DCS files were changed."));
        #endregion END Public
    }
}
