using System.IO;
using Heimdall.App.Games.Profiles;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Application.Interfaces;
using Heimdall.Dcs;

namespace Heimdall.App.Games.Dcs
{
    /// <summary>The native DCS outputs, each built from the saved snapshot.</summary>
    internal enum DcsOutputKind { Validate, Package, ModuleSnapshot, Documentation, CompleteRelease }
}
