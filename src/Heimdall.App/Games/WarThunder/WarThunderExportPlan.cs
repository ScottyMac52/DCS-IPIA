using System.IO;
using Heimdall.App.Games.Profiles;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;
using Heimdall.Infrastructure;
using Heimdall.WarThunder;

namespace Heimdall.App.Games.WarThunder
{
    /// <summary>The validated native export plan and its protected source directory.</summary>
    internal sealed record WarThunderExportPlan(string Parent, SimProfileExportPlan Plan, string? SourceDirectory);
}
