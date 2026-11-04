using System.IO;
using Heimdall.Application;
using Heimdall.Application.Interfaces;

namespace Heimdall.App.Smoke
{
    /// <summary>Fails at the deployment boundary to exercise the real Fly Now exception handler.</summary>
    internal sealed class FailingModuleOutputFlyNow : IDcsFlyNowService
    {
        internal const string Detail = "conflict detail: Stick JOY_BTN1 modifiers [] commands One and Two";
        #region BEGIN Public
        /// <inheritdoc/>
        public Task<DcsFlyNowDeployment?> GetActiveAsync(CancellationToken ct = default) => Task.FromResult<DcsFlyNowDeployment?>(null);
        /// <inheritdoc/>
        public Task<DcsFlyNowDeployment> DeployAsync(Guid moduleId, string destination, CancellationToken ct = default) =>
            Task.FromException<DcsFlyNowDeployment>(new InvalidDataException(Detail));
        /// <inheritdoc/>
        public Task ClearAsync(CancellationToken ct = default) => Task.CompletedTask;
        #endregion END Public
    }
}
