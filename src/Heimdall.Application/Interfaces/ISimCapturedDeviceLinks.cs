using Heimdall.Domain.MultiSim;

namespace Heimdall.Application.Interfaces
{
    /// <summary>Opts an adapter into explicit physical links where native slots cannot prove product or instance identity.</summary>
    public interface ISimCapturedDeviceLinks
    {
        #region BEGIN Public
        /// <summary>Rejects foreign, missing or unsupported captured references before a workspace-only link is reviewed.</summary>
        void ValidateCapturedDeviceLink(ProfileDocument profile, string logicalDeviceId);
        /// <summary>Remove only unreferenced captured slots.</summary>
        ProfileDocument RemoveUnusedCapturedDevice(ProfileDocument profile, string logicalDeviceId);
        /// <summary>Clears all captured joystick binding records on one slot.</summary>
        int CountCapturedAssignments(ProfileDocument profile, string logicalDeviceId);
        ProfileDocument ClearCapturedAssignments(ProfileDocument profile, string logicalDeviceId);
        /// <summary>Renumbers all declared native slots and matching input references.</summary>
        ProfileDocument ReorderCapturedSlots(ProfileDocument profile, IReadOnlyList<string> order);
        #endregion END Public
    }
}
