using Heimdall.Application;
using Heimdall.Application.Interfaces;

namespace Heimdall.Infrastructure
{
    /// <summary>Prevents test profiles from reading, recovering, deploying or clearing real DCS files.</summary>
    public sealed class DcsIsolatedFlyNowService : IDcsFlyNowService
    {
        #region BEGIN Public
        /// <inheritdoc/>
        public Task<DcsFlyNowDeployment?> GetActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<DcsFlyNowDeployment?>(null);
        /// <inheritdoc/>
        public Task<DcsFlyNowDeployment> DeployAsync(Guid moduleId, string destination, CancellationToken ct = default) =>
            Task.FromException<DcsFlyNowDeployment>(new InvalidOperationException("Fly Now is disabled in isolated test profiles. Return to Production to deploy."));
        /// <inheritdoc/>
        public Task ClearAsync(CancellationToken ct = default) =>
            Task.FromException(new InvalidOperationException("Clear is disabled in isolated test profiles; no DCS files were changed."));
        #endregion END Public
    }
}
