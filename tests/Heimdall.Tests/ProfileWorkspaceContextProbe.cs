using Heimdall.Application.Games;

namespace Heimdall.Tests
{
    /// <summary>Exercises the inherited multi-sim session adapter using real native sessions.</summary>
    public sealed class ProfileWorkspaceContextProbe() : GameProfileWorkspaceContextBase(new WorkspaceTestScheduler())
    {
        /// <inheritdoc/>
        protected override NativeWorkspaceFacts ReadNativeFacts() => ProfileFacts(ProfileCapabilities(true, true));
    }
}
