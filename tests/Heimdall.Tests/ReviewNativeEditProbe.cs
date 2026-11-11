using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;

namespace Heimdall.Tests
{
    /// <summary>Counts live native draft evaluations without changing the isolated document supplied by the shared session.</summary>
    internal sealed class ReviewNativeEditProbe(string backendId) : ISimNativeEdit
    {
        #region BEGIN Public
        /// <inheritdoc/>
        public string BackendId => backendId;
        /// <summary>Gets how many candidate evaluations were requested.</summary>
        internal int Calls { get; private set; }
        /// <summary>Sets a transient candidate failure independent of document content.</summary>
        internal bool Fail { get; set; }
        /// <inheritdoc/>
        public ProfileDocument Apply(ProfileDocument profile)
        {
            Calls++;
            if (Fail) throw new InvalidOperationException("Transient candidate failure.");
            return profile;
        }
        #endregion END Public
    }
}
