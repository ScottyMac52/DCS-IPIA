using Heimdall.Application.Interfaces;

namespace Heimdall.App.Games.Profiles
{
    /// <summary>Adapts a native input presenter's synchronous stop to the shared input lifetime role.</summary>
    internal sealed class DelegateInputLifetime(Action stop) : IWorkspaceInputLifetime
    {
        #region BEGIN Public
        /// <inheritdoc/>
        public void Stop() => stop();
        #endregion END Public
    }
}
