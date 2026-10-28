using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Optional adapter boundary for adding independently verified native records.</summary>
    public interface INativeRecordCreator : INativeRecordEditor
    {
        /// <summary>Creates a validated new record without guessing native record metadata.</summary>
        ProfileDocument AddNativeBinding(ProfileDocument profile, string concept,
            string logicalDeviceId, string key, CommandType type, int direction);
    }
}
