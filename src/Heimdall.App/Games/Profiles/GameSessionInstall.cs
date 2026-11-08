using Heimdall.Application.Games;

namespace Heimdall.App.Games.Profiles
{
    /// <summary>A validated native session ready to install, with its durable store revision (null when never persisted).</summary>
    internal sealed record GameSessionInstall(GameProfileSession Session, string? Revision);
}
