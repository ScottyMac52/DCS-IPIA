using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Optional game adapter support for writing both directional records
    /// of a single physical analog axis as one validated operation.</summary>
    public interface INativeAxisPairEditor : INativeRecordEditor
    {
        ProfileDocument RebindAxisPair(ProfileDocument profile, string positiveRecordId,
            string negativeRecordId, string logicalDeviceId, string physicalAxisKey);
    }
}
