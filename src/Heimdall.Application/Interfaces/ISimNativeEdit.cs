using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Interfaces
{
    /// <summary>An adapter-owned operation staged through the common identity and export validation boundary.</summary>
    public interface ISimNativeEdit
    {
        #region BEGIN Public
        /// <summary>Gets the adapter that owns this operation.</summary>
        string BackendId { get; }
        /// <summary>Returns an independent edited profile; the shared session verifies identity and exportability.</summary>
        ProfileDocument Apply(ProfileDocument profile);
        #endregion END Public
    }
}
