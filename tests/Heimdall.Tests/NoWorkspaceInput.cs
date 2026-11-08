using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>An input lifetime with no live input to stop.</summary>
    public sealed class NoWorkspaceInput : IWorkspaceInputLifetime
    {
        #region BEGIN Public
        /// <inheritdoc/>
        public void Stop() { }
        #endregion END Public
    }
}
