using System.IO;
using Heimdall.App.Games.Profiles;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;

namespace Heimdall.App.Games.Dcs
{
    /// <summary>The reviewed output plan: saved snapshot, version and chosen destination (null when the picker was cancelled).</summary>
    internal sealed record DcsOutputPlan(DcsEditorWorkspaceSnapshot Saved, string Version, DcsOutputKind Kind, string? Destination);
}
