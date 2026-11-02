using Heimdall.Domain.MultiSim;

namespace Heimdall.Application
{
    /// <summary>Independent native removal result; identity mapping keeps physical and drawing links on their original units.</summary>
    public sealed record SimControllerRemoval(ProfileDocument Profile, IReadOnlyDictionary<string, string> RemainingIds, string Summary)
    {
        #region BEGIN Public
        #endregion END Public
    }
}
