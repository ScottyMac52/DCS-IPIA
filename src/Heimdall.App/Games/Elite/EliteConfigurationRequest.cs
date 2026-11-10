using System.IO;
using Heimdall.App.Games.Profiles;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;

namespace Heimdall.App.Games.Elite
{
    /// <summary>A new (assignments cleared) or duplicate configuration from an explicit source and validated unique name.</summary>
    internal sealed record EliteConfigurationRequest(GameWorkspace Source, string Name, bool Clear);
}
