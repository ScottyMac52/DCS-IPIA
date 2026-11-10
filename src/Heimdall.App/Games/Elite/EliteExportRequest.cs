using System.IO;
using Heimdall.App.Games.Profiles;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Infrastructure;

namespace Heimdall.App.Games.Elite
{
    /// <summary>Install into the ED Bindings directory (reviewed replacement with exact backup) or a separate review copy.</summary>
    internal sealed record EliteExportRequest(bool ReviewCopy);
}
