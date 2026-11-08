using System.IO;
using Heimdall.App.Games.Profiles;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;

namespace Heimdall.App.Games.Elite
{
    /// <summary>The reviewed export: a review-copy parent, or a prepared preset replacement; null parent and plan mean cancelled.</summary>
    internal sealed record EliteExportPlan(SimProfileExportPlan Plan, string? ReviewParent, GameBindingsExportPlan? Install, string CancelMessage);
}
