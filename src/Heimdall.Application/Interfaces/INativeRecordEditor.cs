using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Edits one native indexed binding through its owning adapter without imposing ED slot semantics.</summary>
    public interface INativeRecordEditor
    {
        #region BEGIN Public
        /// <summary>Maps an adapter's input key to the shared physical key used by native capability enumeration.</summary>
        string PhysicalInputKey(string key);
        /// <summary>Returns an independent native-backed profile after validating the entire edit.</summary>
        ProfileDocument RebindNative(ProfileDocument profile, string recordId, string logicalDeviceId, string key, CommandType type, int direction);
        #endregion END Public
    }
}
