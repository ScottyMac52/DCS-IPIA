using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>Counts foreground input releases.</summary>
    public sealed class ProfileTestInput : IWorkspaceInputLifetime
    {
        #region BEGIN Public
        /// <summary>Gets the number of stops.</summary>
        public int Stops { get; private set; }
        /// <inheritdoc/>
        public void Stop() => Stops++;
        #endregion END Public
    }
}
