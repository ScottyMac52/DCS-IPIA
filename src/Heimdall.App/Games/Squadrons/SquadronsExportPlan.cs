using System.IO;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;
using Heimdall.Squadrons;

namespace Heimdall.App.Games.Squadrons
{
    /// <summary>The reviewed native files and the folders a review copy must never overwrite.</summary>
    internal sealed record SquadronsExportPlan(string Parent, IReadOnlyDictionary<string, string> Files, IReadOnlyList<string> ProtectedRoots);
}
