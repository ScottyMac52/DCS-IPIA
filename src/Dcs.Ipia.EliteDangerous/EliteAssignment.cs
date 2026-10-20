using Dcs.Ipia.Domain.MultiSim;

namespace Dcs.Ipia.EliteDangerous
{
    /// <summary>A native slot assignment independent of mutable device binding collections.</summary>
    internal sealed record EliteAssignment(string DeviceId, string Key, string CommandId, string Slot,
        List<Modifier> Modifiers, BindingMode Mode, AxisBinding? Axis)
    {
        #region BEGIN Public
        /// <summary>Gets the command and slot identity.</summary>
        public string Id => CommandId + "/" + Slot;
        #endregion END Public
    }
}
