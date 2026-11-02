using Heimdall.Application.Interfaces;
using Heimdall.Domain.MultiSim;

namespace Heimdall.WarThunder
{
    /// <summary>Adapter-owned edit request shared by desktop and application tests.</summary>
    public sealed record WarThunderNativeEdit(WarThunderEditKind Kind, string RecordId = "", string FieldAddress = "",
        string DeviceId = "", string CommandId = "", CommandType Type = CommandType.Button, int NativeIndex = 0,
        bool Inverted = false, string Literal = "") : ISimNativeEdit
    {
        #region BEGIN Public
        /// <summary>Gets the owning native backend.</summary>
        public string BackendId => "war-thunder";
        /// <summary>Validates and applies this native operation to an independent profile.</summary>
        public ProfileDocument Apply(ProfileDocument profile) => Kind switch
        {
            WarThunderEditKind.Rebind => WarThunderProfileBackend.RebindInput(profile, RecordId, FieldAddress, DeviceId, Type, NativeIndex),
            WarThunderEditKind.Add => WarThunderProfileBackend.AddBinding(profile, CommandId, DeviceId, Type, NativeIndex),
            WarThunderEditKind.Clear => WarThunderProfileBackend.ClearBinding(profile, RecordId),
            WarThunderEditKind.Inverse => WarThunderProfileBackend.SetAxisInverse(profile, RecordId, Inverted),
            WarThunderEditKind.Parameter => WarThunderProfileBackend.EditAxisParameter(profile, RecordId, FieldAddress, Literal),
            _ => throw new InvalidOperationException("Unsupported native edit.")
        };
        #endregion END Public
    }
}
