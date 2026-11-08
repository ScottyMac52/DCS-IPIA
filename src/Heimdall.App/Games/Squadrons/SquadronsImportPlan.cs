using System.IO;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;

namespace Heimdall.App.Games.Squadrons
{
    /// <summary>A parsed Squadrons import ready to install, with its unique profile name and protected source directory.</summary>
    internal sealed record SquadronsImportPlan(GameWorkspace Workspace, string SourceDirectory);
}
