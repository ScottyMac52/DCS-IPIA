using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;
using Heimdall.WarThunder;

namespace Heimdall.App.Games
{
    /// <summary>Applies all reviewed native axis fields to one candidate profile.</summary>
    internal sealed class WarThunderAxisTuningEdit(IReadOnlyList<WarThunderNativeEdit> changes) : ISimNativeEdit
    {
        #region BEGIN Public
        /// <summary>Gets the adapter that owns the native field edits.</summary>
        public string BackendId => "war-thunder";
        /// <summary>Builds the candidate before the session stages any of the edits.</summary>
        public ProfileDocument Apply(ProfileDocument profile) =>
            changes.Aggregate(profile, (candidate, change) => change.Apply(candidate));
        #endregion END Public
    }
}
