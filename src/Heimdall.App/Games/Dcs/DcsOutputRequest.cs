using System.IO;
using Heimdall.App.Games.Profiles;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;

namespace Heimdall.App.Games.Dcs
{
    /// <summary>A typed request for one DCS output at an explicit package version.</summary>
    internal sealed record DcsOutputRequest(string Version, DcsOutputKind Kind);
}
