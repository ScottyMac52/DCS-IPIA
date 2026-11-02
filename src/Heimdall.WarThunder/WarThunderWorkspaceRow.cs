using Heimdall.Domain.MultiSim;

namespace Heimdall.WarThunder
{
    /// <summary>A native reference row without invented physical input aliases.</summary>
    public sealed record WarThunderWorkspaceRow(WarThunderBinding Binding, SimCommand Command,
        IReadOnlyList<string> DeviceIds, string Inputs, string Details)
    {
        #region BEGIN Public
        /// <summary>Gets the stable native occurrence address.</summary>
        public string RecordId => Binding.Id;
        /// <summary>Gets the captured command display name.</summary>
        public string Name => Command.DisplayName;
        /// <summary>Gets the native section containing this record.</summary>
        public string Section => Binding.Section;
        #endregion END Public
    }
}
