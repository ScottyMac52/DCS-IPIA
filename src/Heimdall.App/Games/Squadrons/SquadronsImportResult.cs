using System.IO;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;

namespace Heimdall.App.Games.Squadrons
{
    /// <summary>A validated imported session and the directory it came from.</summary>
    internal sealed record SquadronsImportResult(GameProfileSession Session, string SourceDirectory);
}
