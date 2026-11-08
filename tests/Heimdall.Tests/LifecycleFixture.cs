using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;

namespace Heimdall.Tests
{
    /// <summary>One game's real backend, durable file store, counted store view, context and shared operations.</summary>
    /// <param name="Context">The profile context holding the real native session.</param>
    /// <param name="Store">The counted store used by the operations.</param>
    /// <param name="Files">The durable file store.</param>
    /// <param name="Profiles">The shared save/discard operations.</param>
    /// <param name="Backend">The real native backend.</param>
    public sealed record LifecycleFixture(ProfileWorkspaceContextProbe Context, CountingWorkspaceStore Store, FileGameWorkspaceStore Files,
        LifecycleProfileOperations Profiles, ISimProfileBackend Backend) : IDisposable
    {
        #region BEGIN Public
        /// <inheritdoc/>
        public void Dispose() { Profiles.Dispose(); Context.Dispose(); }
        #endregion END Public
    }
}
