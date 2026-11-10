using System.IO;
using Heimdall.App.Games.Profiles;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;

namespace Heimdall.App.Games.Elite
{
    /// <summary>A persisted new/duplicate configuration ready to install.</summary>
    internal sealed record EliteConfigurationResult(GameSessionInstall Installed, bool Clear);
}
