using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Games
{
    /// <summary>A game-owned saved document; no aircraft or DCS database identity is synthesized.</summary>
    /// <param name="SchemaVersion">Workspace schema version, currently one.</param>
    /// <param name="Id">Stable user-owned workspace identity.</param>
    /// <param name="Name">Configuration display name used for the exported native preset filename.</param>
    /// <param name="Profile">Versioned backend document with retained native sources.</param>
    /// <param name="Hardware">Explicit native-reference to hardware associations.</param>
    public sealed record GameWorkspace(int SchemaVersion, Guid Id, string Name, ProfileDocument Profile,
        Dictionary<string, GameHardwarePin> Hardware)
    {
        #region BEGIN Public
        /// <summary>Gets presentation order only; native devices and ED indices retain their original order.</summary>
        public List<string> ControllerOrder { get; init; } = [];
        /// <summary>Retains independent drawing links while their physical controller is explicitly unlinked.</summary>
        public Dictionary<string, string> UnlinkedDrawings { get; init; } = new();
        #endregion END Public
    }
}
