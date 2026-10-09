using System.IO;
using Dcs.Ipia.Application;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.App.Smoke
{
    /// <summary>Fails at the deployment boundary to exercise the real Fly Now exception handler.</summary>
    internal sealed class FailingModuleOutputFlyNow : IFlyNowService
    {
        internal const string Detail = "conflict detail: Stick JOY_BTN1 modifiers [] commands One and Two";
        #region BEGIN Public
        /// <inheritdoc/>
        public Task<FlyNowDeployment?> GetActiveAsync(CancellationToken ct = default) => Task.FromResult<FlyNowDeployment?>(null);
        /// <inheritdoc/>
        public Task<FlyNowDeployment> DeployAsync(Guid moduleId, string destination, CancellationToken ct = default) =>
            Task.FromException<FlyNowDeployment>(new InvalidDataException(Detail));
        /// <inheritdoc/>
        public Task ClearAsync(CancellationToken ct = default) => Task.CompletedTask;
        #endregion END Public
    }
}
